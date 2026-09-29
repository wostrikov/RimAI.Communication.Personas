using System.Collections.Generic;
using RimWorld;
using Ustas.RimAI.Communication.Personas.Config;
using Verse;

namespace Ustas.RimAI.Communication.Personas.Automation
{
    public enum PersonaGameEventKind
    {
        NewPawn,
        RoleChange,
        Marriage,
        Breakup,
        Birth,
        Death,
        TraitGained
    }

    /// <summary>One game event as a Harmony hook saw it, before anyone judged it.</summary>
    public sealed class PersonaGameEvent
    {
        public PersonaGameEventKind Kind;
        public Pawn Subject;
        public Pawn Other;
        public Pawn Third;
        public Trait Trait;
        public PawnRoleCategory From;
    }

    /// <summary>
    /// Where the Harmony hooks drop what they saw. Recording is all a hook does:
    /// it runs inside a vanilla method (a death, a faction change, a birth), and
    /// nothing the persona automation decides may be able to fail there. The
    /// events are judged on the next world tick by <see cref="PersonaTriggerRouter"/>,
    /// where a fault costs one persona update and the game's own component
    /// guard reports it.
    /// </summary>
    public sealed class PersonaGameEventInbox
    {
        // A raid or a mass recruitment can raise many events in one tick; the
        // queue behind this is bounded anyway, so the excess is dropped here.
        const int MaxPending = 128;

        readonly List<PersonaGameEvent> _pending = new List<PersonaGameEvent>();

        static PersonaAutomationSettings Settings => PersonasMod.Settings?.Automation;

        public bool WantsNewPawns => Settings?.autoGenEnabled == true;

        public bool WantsRoleChanges
        {
            get
            {
                PersonaAutomationSettings s = Settings;
                return s != null && (s.autoGenEnabled || (s.autoEvolveEnabled && s.evolveOnRoleChange));
            }
        }

        public bool WantsEvolveEvent(PersonaGameEventKind kind)
        {
            PersonaAutomationSettings s = Settings;
            if (s == null || !s.autoEvolveEnabled) return false;
            switch (kind)
            {
                case PersonaGameEventKind.Marriage: return s.evolveOnMarriage;
                case PersonaGameEventKind.Breakup: return s.evolveOnBreakup;
                case PersonaGameEventKind.Birth: return s.evolveOnBirth;
                case PersonaGameEventKind.Death: return s.evolveOnFamilyDeath;
                case PersonaGameEventKind.TraitGained: return s.evolveOnTraitGained;
                default: return false;
            }
        }

        public void Record(PersonaGameEvent gameEvent)
        {
            if (gameEvent?.Subject != null && _pending.Count < MaxPending) _pending.Add(gameEvent);
        }

        internal List<PersonaGameEvent> Drain()
        {
            if (_pending.Count == 0) return null;
            var drained = new List<PersonaGameEvent>(_pending);
            _pending.Clear();
            return drained;
        }

        public void Clear() => _pending.Clear();
    }
}
