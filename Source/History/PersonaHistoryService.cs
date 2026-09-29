using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Personas.Automation;
using Ustas.RimAI.Communication.Personas.Policy;
using Ustas.RimAI.Communication.Util;
using Verse;

namespace Ustas.RimAI.Communication.Personas.History
{
    /// <summary>
    /// Applies persona text on behalf of the Director and keeps the before/after
    /// pair, so any change it makes can be undone from the history window.
    /// Main thread only: it reads and writes the pawn's persona hediff.
    /// </summary>
    public static class PersonaHistoryService
    {
        public static PersonaHistoryStore CurrentStore =>
            Find.World?.GetComponent<DirectorWorldComponent>()?.History;

        static int MaxRecords =>
            PersonasMod.Settings?.Automation?.historyMaxRecords ?? PersonaHistoryPolicy.DefaultMaxRecords;

        public static void Record(Pawn pawn, string before, string after, PersonaChangeSource source, string context)
        {
            if (pawn == null || pawn.IsPlayer() || !PersonaHistoryPolicy.ShouldRecord(before, after)) return;
            PersonaHistoryStore store = CurrentStore;
            if (store == null) return;

            store.Add(pawn, new PersonaHistoryRecord
            {
                tick = Find.TickManager?.TicksGame ?? 0,
                before = PersonaHistoryPolicy.Normalize(before),
                after = PersonaHistoryPolicy.Normalize(after),
                source = source,
                context = PersonaHistoryPolicy.ClampContext(context)
            }, MaxRecords);
        }

        /// <summary>
        /// Writes <paramref name="text"/> as the pawn's persona and records the
        /// change. Returns false when the pawn cannot take a persona.
        /// </summary>
        public static bool Apply(Pawn pawn, string text, PersonaChangeSource source, string context)
        {
            if (pawn == null || pawn.Destroyed || string.IsNullOrWhiteSpace(text)) return false;

            string before = PersonaOwnership.ReadWithoutCreating(pawn);
            PersonaService.SetPersonality(pawn, text.Trim());
            string after = PersonaOwnership.ReadWithoutCreating(pawn);
            if (after.Length == 0) return false;

            Record(pawn, before, after, source, context);
            return true;
        }

        public static bool Restore(Pawn pawn, string text)
        {
            return Apply(pawn, text, PersonaChangeSource.Restore, "RPD_History_SystemRestore".Translate());
        }

        public static string SourceLabel(PersonaChangeSource source)
        {
            switch (source)
            {
                case PersonaChangeSource.AutoGen: return "RPD_History_AutoGen".Translate();
                case PersonaChangeSource.AutoEvolve: return "RPD_History_AutoEvolve".Translate();
                case PersonaChangeSource.Restore: return "RPD_History_Restored".Translate();
                case PersonaChangeSource.ManualEdit: return "RPD_ManualEdit".Translate();
                default: return "RPD_History_Director".Translate();
            }
        }
    }
}
