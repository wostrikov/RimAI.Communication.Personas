using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Core.Runtime;
using RimWorld;
using RimWorld.Planet;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Personas.Config;
using Ustas.RimAI.Communication.Personas.Diagnostics;
using Ustas.RimAI.Communication.Personas.Policy;
using Ustas.RimAI.Communication.Service;
using Ustas.RimAI.Core.AI;
using Ustas.RimAI.Core.Diagnostics;
using Verse;

namespace Ustas.RimAI.Communication.Personas.Automation
{
    /// <summary>
    /// Runs the automatic persona work: one request at a time, with a timeout,
    /// bound to one world. Owned by <see cref="PersonasComposition"/> and ticked
    /// by <see cref="DirectorWorldComponent"/>; nothing here is saved.
    ///
    /// Requests go through the Communication query path like the editor's own
    /// buttons, as ordinary (non-player) talk, so any talk the player starts can
    /// cancel one. A new request starts only while that path is idle, so the
    /// automatic work waits for dialogue rather than competing with it.
    /// </summary>
    public sealed class PersonaAutomationCoordinator
    {
        const int MaxQueuedJobs = 24;
        const int MaxAttempts = 2;
        const int StartCheckIntervalTicks = 60;

        // The shared request timeout is 90 s and one retry may follow it; past
        // this the answer is not coming, and the queue must not wait on it.
        static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(200);

        readonly List<PersonaAutomationJob> _queue = new List<PersonaAutomationJob>();
        readonly IPersonaJobHandler _generate = new AutoGenJobHandler();
        readonly IPersonaJobHandler _evolve = new AutoEvolveJobHandler();
        readonly AutoEvolveScheduler _scheduler = new AutoEvolveScheduler();
        PersonaAutomationJob _active;
        World _world;
        int _nextStartCheckTick;
        int _consecutiveTimeouts;

        public PersonaAutomationCoordinator()
        {
            Triggers = new PersonaTriggerRouter(this);
        }

        public PersonaTriggerRouter Triggers { get; }

        /// <summary>Where the Harmony hooks record game events; judged on the next tick.</summary>
        public PersonaGameEventInbox Inbox { get; } = new PersonaGameEventInbox();

        public int PendingCount => _queue.Count + (_active != null ? 1 : 0);

        public bool IsWorking => _active != null;

        /// <summary>Drops everything that belonged to another world.</summary>
        public void BindWorld(World world)
        {
            if (ReferenceEquals(world, _world)) return;
            Reset();
            _world = world;
        }

        /// <summary>
        /// Abandons the running request and forgets the queue. Called when the
        /// world is unloaded or replaced and when the module stops, so no answer
        /// is ever applied to a pawn from a game that is gone.
        /// </summary>
        public void Reset()
        {
            AbandonActive();
            _queue.Clear();
            Inbox.Clear();
            Triggers.Clear();
            _scheduler.Clear();
            _world = null;
            _nextStartCheckTick = 0;
            _consecutiveTimeouts = 0;
        }

        public bool Enqueue(Pawn pawn, PersonaAutomationKind kind, bool roleChange, string triggerKey, string triggerContext)
        {
            if (pawn == null || Find.World == null) return false;
            BindWorld(Find.World);

            if (_active != null && _active.PawnId == pawn.thingIDNumber) return false;
            PersonaAutomationJob queued = _queue.Find(job => job.PawnId == pawn.thingIDNumber);
            if (queued != null)
            {
                // A pawn already waiting for a fresh persona gets one that fits the
                // latest events anyway, and anything else merges into the waiting job.
                return queued.AddTrigger(triggerKey, triggerContext);
            }

            if (_queue.Count >= MaxQueuedJobs)
            {
                ModuleLog.Message("[RimAI.Personas] automation queue is full; skipped " + kind + " for " + pawn.LabelShortCap);
                return false;
            }

            var job = new PersonaAutomationJob(pawn, kind, roleChange);
            job.AddTrigger(triggerKey, triggerContext);
            _queue.Add(job);
            return true;
        }

        public void Tick(DirectorWorldComponent component)
        {
            BindWorld(component.world);
            PersonaAutomationSettings settings = PersonasMod.Settings?.Automation;
            if (settings == null || !settings.AnyEnabled || PersonaAutomationFuse.IsTripped(settings))
            {
                if (_active != null || _queue.Count > 0) Reset();
                Inbox.Clear();
                return;
            }

            PersonaAutomationKind? working = null;
            try
            {
                int now = Find.TickManager.TicksGame;
                Triggers.Flush(Inbox, component, now);
                _scheduler.Tick(this, component, now);

                if (_active != null)
                {
                    working = _active.Kind;
                    PollActive(component, now, settings);
                    return;
                }

                if (now < _nextStartCheckTick) return;
                _nextStartCheckTick = now + StartCheckIntervalTicks;
                if (_queue.Count == 0 || AIService.IsBusy() || RimAiBackground.IsShuttingDown) return;
                // Fast-forwarding: let the time pass rather than spend a request per pawn on it.
                if (PersonaAutomationPolicy.IsPausedAtSpeed((int)Find.TickManager.CurTimeSpeed, settings.pauseAtSpeed)) return;
                working = _queue[0].Kind;
                StartNext(component, now);
            }
            // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY — world-tick automation fault opens the fuse instead of repeating each tick
            catch (Exception ex)
            {
                PersonaAutomationFuse.Trip(settings, FailureReason(working), ex);
                Reset();
            }
        }

        static string FailureReason(PersonaAutomationKind? kind)
        {
            if (kind == PersonaAutomationKind.Generate) return PersonaAutomationFuse.GenerateFailedReason;
            if (kind == PersonaAutomationKind.Evolve) return PersonaAutomationFuse.EvolveFailedReason;
            return PersonaAutomationFuse.AutomationFailedReason;
        }

        void StartNext(DirectorWorldComponent component, int now)
        {
            while (_queue.Count > 0)
            {
                PersonaAutomationJob job = _queue[0];
                _queue.RemoveAt(0);

                TalkRequest request = HandlerFor(job).Prepare(job, component);
                if (request == null)
                {
                    if (job.Kind == PersonaAutomationKind.Evolve) _scheduler.Backoff(job.Pawn, now);
                    continue;
                }

                // Nobody is waiting on an automatic persona: it queues behind every line a
                // colonist is about to say, as Memory's and Art's background work does.
                request.Priority = AiRequestPriority.Background;
                job.Request = request;
                job.StartedUtc = DateTime.UtcNow;
                job.Task = RimAiBackground.Run(() => AIService.Query<PersonalityData>(request));
                _active = job;
                ModuleLog.Message("[RimAI.Personas] " + job.Kind + " started for " + job.Pawn.LabelShortCap);
                return;
            }
        }

        void PollActive(DirectorWorldComponent component, int now, PersonaAutomationSettings settings)
        {
            PersonaAutomationJob job = _active;
            if (!job.Task.IsCompleted)
            {
                if (DateTime.UtcNow - job.StartedUtc < RequestTimeout) return;
                RimAiLog.Warning(RimAiLogCategory.Personas,
                    "[RimAI.Personas] automatic " + job.Kind + " for " + job.Pawn.LabelShortCap
                    + " timed out after " + (int)RequestTimeout.TotalSeconds + " s and was dropped.");
                AbandonActive();
                if (job.Kind == PersonaAutomationKind.Evolve) _scheduler.Backoff(job.Pawn, now);
                if (++_consecutiveTimeouts >= PersonaAutomationFuse.MaxConsecutiveTimeouts)
                {
                    PersonaAutomationFuse.Trip(settings, job.Kind == PersonaAutomationKind.Generate
                        ? PersonaAutomationFuse.GenerateTimeoutReason
                        : PersonaAutomationFuse.EvolveTimeoutReason, null);
                    Reset();
                }
                return;
            }

            _consecutiveTimeouts = 0;
            _active = null;
            bool applied = false;
            if (job.Task.IsFaulted)
            {
                RimAiLog.Warning(RimAiLogCategory.Personas,
                    "[RimAI.Personas] automatic " + job.Kind + " for " + job.Pawn.LabelShortCap
                    + " failed: " + job.Task.Exception?.GetBaseException().Message);
            }
            else if (job.Task.IsCanceled || job.Task.Result == null)
            {
                // Cancelled for a player's talk, or refused: the API log has the
                // reason. A talk cutting in is the common case and says nothing
                // about the request, so it goes to the back of the queue once.
                ModuleLog.Message("[RimAI.Personas] " + job.Kind + " for " + job.Pawn.LabelShortCap + " returned no persona");
                if (++job.Attempts < MaxAttempts && _queue.Count < MaxQueuedJobs)
                {
                    job.Task = null;
                    job.Request = null;
                    _queue.Add(job);
                    return;
                }
            }
            else
            {
                applied = HandlerFor(job).Apply(job, job.Task.Result, component);
                ModuleLog.Message("[RimAI.Personas] " + job.Kind + " for " + job.Pawn.LabelShortCap
                    + (applied ? " applied" : " discarded: the pawn or its persona changed meanwhile"));
            }

            if (applied)
                Notify(job);
            else if (job.Kind == PersonaAutomationKind.Evolve)
                _scheduler.Backoff(job.Pawn, now);
        }

        static void Notify(PersonaAutomationJob job)
        {
            if (PersonasMod.Settings?.Automation?.notifyOnAutoChange != true) return;
            Messages.Message(
                "RPD_Msg_AutoPersonaUpdated".Translate(job.Pawn.LabelShortCap),
                job.Pawn,
                MessageTypeDefOf.SilentInput,
                false);
        }

        void AbandonActive()
        {
            PersonaAutomationJob job = _active;
            _active = null;
            if (job == null) return;

            // Stop the request if it is still the one in flight, rather than let a
            // dead world's question hold the dialogue slot until it times out.
            if (job.Request != null && ReferenceEquals(AIService.CurrentRequest, job.Request))
                AIService.CancelCurrent();
            ObserveFault(job.Task);
        }

        IPersonaJobHandler HandlerFor(PersonaAutomationJob job) =>
            job.Kind == PersonaAutomationKind.Generate ? _generate : _evolve;

        // An abandoned task may still fault later; observing it keeps that from
        // surfacing as an unobserved task exception.
        static void ObserveFault(Task task)
        {
            task?.ContinueWith(
                completed => { _ = completed.Exception; },
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }
    }
}
