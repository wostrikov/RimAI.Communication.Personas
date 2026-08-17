using Verse;
using RimWorld;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Core.Handshake;

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

            // 2. 执行数据迁移 (Chattiness 2.0 -> 1.0)
            PersonasMod.Settings.MigrateChattinessValuesIfNeeded();

            // 3. 应用规则到现有 Pawn (解决中途加 Mod 没人设的问题)
            ApplyRulesToExistingPawns();
        }

        private void ApplyRulesToExistingPawns()
        {
            // 确保 Def 已加载
            HediffDef personaDef = DefDatabase<HediffDef>.GetNamed("RimTalk_PersonaData", false);
            if (personaDef == null) return;

            int count = 0;

            // 遍历所有活着的 Pawn
            foreach (var p in PawnsFinder.AllMapsWorldAndTemporary_Alive)
            {
                if (p == null || !p.RaceProps.Humanlike || p.Dead) continue;

                // 检查是否已经有人格
                bool hasPersona = false;
                var hediff = p.health.hediffSet.GetFirstHediffOfDef(personaDef);

                if (hediff is Hediff_Persona persona && !string.IsNullOrWhiteSpace(persona.Personality))
                {
                    hasPersona = true;
                }

                // 如果没有人格，尝试应用我们的规则
                if (!hasPersona)
                {
                    // 调用 Patch_GetOrAddNew 里的查找逻辑
                    CustomPreset preset = Patch_GetOrAddNew.FindPresetFor(p);

                    if (preset != null)
                    {
                        DirectorUtils.ApplyPersonalityToPawn(p, new PersonalityData(preset.personaText, preset.chattiness));
                        count++;
                    }
                }
            }

            if (count > 0)
            {
                Log.Message($"[RimAI.Personas] Initialization: Applied rules to {count} existing pawns.");
            }
        }
    }
}