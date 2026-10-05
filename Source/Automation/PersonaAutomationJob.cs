using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Personas.Config;
using Ustas.RimAI.Communication.Personas.Policy;
using Ustas.RimAI.Communication.Util;
using Verse;

namespace Ustas.RimAI.Communication.Personas.Automation
{
    public enum PersonaAutomationKind
    {
        Generate,
        Evolve
    }

    /// <summary>
    /// One pawn's pending automatic request. Triggers that arrive while it
    /// waits are merged into it, so a pawn is asked about once per queue pass.
    /// </summary>
    public sealed class PersonaAutomationJob
    {
        public PersonaAutomationJob(Pawn pawn, PersonaAutomationKind kind, bool roleChange)
        {
            Pawn = pawn;
            PawnId = pawn.thingIDNumber;
            Kind = kind;
            RoleChange = roleChange;
        }

        public Pawn Pawn { get; }
        public int PawnId { get; }
        public PersonaAutomationKind Kind { get; }
        public bool RoleChange { get; }

        public List<string> TriggerKeys { get; } = new List<string>();
        public List<string> TriggerContexts { get; } = new List<string>();
        public string TriggerText => string.Join("\n", TriggerContexts);

        // Set when the request starts; read back when its answer is applied.
        public TalkRequest Request { get; set; }
        public string OriginalPersona { get; set; } = "";
        public PawnRoleCategory Category { get; set; }
        public Task<PersonalityData> Task { get; set; }
        public DateTime StartedUtc { get; set; }
        public int Attempts { get; set; }

        public bool AddTrigger(string key, string context) =>
            PersonaAutomationPolicy.TryAddTrigger(TriggerKeys, TriggerContexts, key, context);
    }

    /// <summary>Which pawns Auto-Evolve looks after.</summary>
    public static class AutoEvolveEligibility
    {
        public static bool IsEligible(Pawn pawn, PersonaAutomationSettings settings)
        {
            if (!IsLivePersonaPawn(pawn) || settings == null) return false;
            // A pawn the player left out in the per-pawn roster takes no update, scheduled or not.
            if (Find.World?.GetComponent<DirectorWorldComponent>()?.AutoEvolvePawns.IsExcluded(pawn) == true) return false;
            PawnRoleCategory category = PawnRoleClassifier.Classify(pawn);
            if (category == PawnRoleCategory.Colonist) return true;
            return settings.autoEvolveIncludeCaptives
                && (category == PawnRoleCategory.Prisoner || category == PawnRoleCategory.Slave);
        }

        /// <summary>
        /// A spawned, living, humanlike pawn past infancy that is not the player's
        /// own avatar, whose persona lives in the player settings instead.
        /// </summary>
        public static bool IsLivePersonaPawn(Pawn pawn)
        {
            return pawn != null
                && !pawn.Destroyed
                && !pawn.Dead
                && pawn.Spawned
                && pawn.RaceProps != null
                && pawn.RaceProps.Humanlike
                && !pawn.DevelopmentalStage.Baby()
                && !pawn.IsPlayer();
        }
    }
}
