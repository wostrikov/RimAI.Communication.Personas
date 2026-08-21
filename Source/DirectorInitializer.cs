using Verse;
using RimWorld;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Core.Handshake;
using Ustas.RimAI.Core.Diagnostics;
using Ustas.RimAI.Core.Personas;

namespace Ustas.RimAI.Communication.Personas
{
    public class DirectorInitializer : GameComponent
    {
        public DirectorInitializer(Game game) { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (!RimAiHandshake.IsApproved(RimAiModuleIds.Personas))
            {
                return;
            }

            PersonasMod.Settings.MigrateChattinessValuesIfNeeded();

            ApplyRulesToExistingPawns();
        }

        private void ApplyRulesToExistingPawns()
        {
            HediffDef personaDef = DefDatabase<HediffDef>.GetNamed(PersonaScribeLabels.Hediff.DefName, false);
            if (personaDef == null) return;

            int count = 0;

            foreach (var p in PawnsFinder.AllMapsWorldAndTemporary_Alive)
            {
                if (p == null || !p.RaceProps.Humanlike || p.Dead) continue;

                bool hasPersona = false;
                var hediff = p.health.hediffSet.GetFirstHediffOfDef(personaDef);

                if (hediff is Hediff_Persona persona && !string.IsNullOrWhiteSpace(persona.Personality))
                {
                    hasPersona = true;
                }

                if (!hasPersona)
                {
                    CustomPreset preset = PersonaResolver.FindPresetFor(p);

                    if (preset != null)
                    {
                        DirectorUtils.ApplyPersonalityToPawn(p, new PersonalityData(preset.personaText, preset.chattiness));
                        count++;
                    }
                }
            }

            if (count > 0)
            {
                RimAiLog.Info(RimAiLogCategory.Personas, $"[RimAI.Personas] Initialization: Applied rules to {count} existing pawns.");
            }
        }
    }
}