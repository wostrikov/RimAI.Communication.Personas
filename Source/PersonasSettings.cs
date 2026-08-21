using HarmonyLib;
using Ustas.RimAI.Communication.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Verse;
using Ustas.RimAI.Core.Diagnostics;
using Ustas.RimAI.Core.Personas;

namespace Ustas.RimAI.Communication.Personas
{
    public class PromptPreset : IExposable
    {
        public string label;
        public string text;

        public PromptPreset() { }
        public PromptPreset(string label, string text)
        {
            this.label = label;
            this.text = text;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref label, PersonaScribeLabels.PromptPreset.Label);
            Scribe_Values.Look(ref text, PersonaScribeLabels.PromptPreset.Text);
        }
    }

    public class CustomPreset : IExposable
    {
        public string id;
        public string label;
        public string personaText;
        public float chattiness = 1.0f;
        public string category = "Default";
        public bool enabled = true;
        public CustomPreset()
        {
            id = System.Guid.NewGuid().ToString();
        }

        public CustomPreset(string label, string text) : this()
        {
            this.label = label;
            this.personaText = text;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, PersonaScribeLabels.CustomPreset.Id);
            Scribe_Values.Look(ref label, PersonaScribeLabels.CustomPreset.Label);
            Scribe_Values.Look(ref personaText, PersonaScribeLabels.CustomPreset.PersonaText);
            Scribe_Values.Look(ref chattiness, PersonaScribeLabels.CustomPreset.Chattiness, PersonaScribeLabels.CustomPreset.DefaultChattiness);
            Scribe_Values.Look(ref category, PersonaScribeLabels.CustomPreset.Category, PersonaScribeLabels.CustomPreset.DefaultCategory);
            Scribe_Values.Look(ref enabled, PersonaScribeLabels.CustomPreset.Enabled, PersonaScribeLabels.CustomPreset.DefaultEnabled);
        }
    }

    public enum RuleType { FactionDef, RaceDef, XenotypeDef }

    public class AssignmentRule : IExposable
    {
        public bool enabled = true;
        public string targetDefName;
        public RuleType type;
        public int priority = 0;
        public List<string> allowedPresetIds = new List<string>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref enabled, PersonaScribeLabels.AssignmentRule.Enabled, PersonaScribeLabels.AssignmentRule.DefaultEnabled);
            Scribe_Values.Look(ref targetDefName, PersonaScribeLabels.AssignmentRule.TargetDefName);
            Scribe_Values.Look(ref type, PersonaScribeLabels.AssignmentRule.Type);
            Scribe_Values.Look(ref priority, PersonaScribeLabels.AssignmentRule.Priority, PersonaScribeLabels.AssignmentRule.DefaultPriority);
            Scribe_Collections.Look(ref allowedPresetIds, PersonaScribeLabels.AssignmentRule.AllowedPresetIds, LookMode.Value);
        }
    }

    public class PersonasSettings : ModSettings
    {
        // =============================================================
        // Prompt Templates (Default Constants)
        // =============================================================

        public const string DefaultPrompt_Standard = @"# Роль: режисер особистості Rimworld
# Мова: {LANG}

# Завдання:
Прочитай [Дані персонажа], щоб згенерувати 3 відмінні варіанти «Системної інструкції».

# ПРІОРИТЕТ: [Нотатки режисера]
[Нотатки режисера] є Абсолютною опорою. Вони мають перевагу над даними гри. Усі варіанти повинні їм відповідати.

# ЗАБОРОНИ:
1. НЕ ВИВАНТАЖУВАТИ ДАНІ: НІКОЛИ не згадуй конкретні числа (наприклад, «Стрільба 10»), рівні навичок або необроблені назви генів/рис.
2. ПЕРЕКЛАД: Перетворюй характеристики на оповідний опис.

# ТВОРЧІ ПРАВИЛА:
1. Екстраполюй: Якщо даних мало, вигадуй правдоподібні деталі на основі рис/передісторії.
2. Голос: Визнач стиль мовлення персонажа.
3. Контекст:
   - Навички (0–20): Високі = професійні звички/жаргон. Низькі = уникання/невпевненість.
   - [НЕЗДАТНИЙ]: Травма, інвалідність або зарозумілість.
   - Стосунки: Перетворюй статус (Мертвий/Ворожий) на емоційний багаж.

# СТРАТЕГІЯ ВИВЕДЕННЯ:
Згенеруй 3 відмінні інтерпретації особистості. Кожен варіант опиши одним абзацом.

# ШАБЛОН ВМІСТУ:
---
### Варіант 1: [Стиль із 2–4 слів]
[Розгорнутий опис: Опиши історію на основі даних і передісторії персонажа, пояснивши, чому і як він став тим, ким є зараз. Вигадай короткострокову психологічну мету. Явно опиши темп мовлення, словниковий запас і ставлення.]

---
### Варіант 2: [Стиль із 2–4 слів]
[Інший підхід...]

---
### Варіант 3: [Стиль із 2–4 слів]
[Інший підхід...]
";

        public const string DefaultPrompt_Simple = @"# Роль: письменник художніх історій Rimworld
# Мова: {LANG}

# ЗАВДАННЯ:
Ігноруй обмеження симуляції. Вигадай передісторію на основі [Character Data].
Твоя мета — створити персонажа з глибиною та багатогранністю.

# ПРІОРИТЕТ: [Director's Notes]
[Director's Notes] — абсолютна опора. Вони мають перевагу над даними гри. Усі варіанти мають їм відповідати.

# ЗАБОРОНИ:
1. НЕ ВИВАНТАЖУЙ ДАНІ: НІКОЛИ не згадуй конкретні числа (наприклад, «Стрільба 10»), рівні навичок або назви генів/рис без змін.
2. ПЕРЕКЛАДАЙ: Перетворюй характеристики на оповідь.

# ТВОРЧІ ПРАВИЛА:
1. Тональна нейтральність:
   - Не нав'язуй певного стилю. Нехай тон визначають дані.
   - Мета: відобразити весь спектр людяності — від трагічного до абсурдного, від злого до святого.
2. Прихований вимір:
   - Кожному персонажу потрібен шар, який не одразу помітний. Це може бути таємний злочин, прихований талант, дріб'язкова образа або слабке місце.
   - Він не обов'язково має бути драматичним; достатньо, щоб він був людським.
3. Приземлена реальність:
   - Якщо дані не натякають на високотехнологічне походження, уникай науково-фантастичних кліше (клонів/амнезії).
   - Зосередься на зрозумілих людських переживаннях: виживанні, амбіціях, родині, лінощах, відданості або жадібності.
4. Протокол чистого аркуша:
   - Проаналізуй соціологічну та психологічну еволюцію від середовища дитинства до ролі в дорослому віці.
   - Якщо [Backstory] або [Traits] відсутні: вигадуй особистість. Легко олюднюй. Наділи персонажа виразною особистістю.

# СТРАТЕГІЯ ВИВЕДЕННЯ:
Створи ОДИН суцільний оповідний профіль.
Не розбивай його на розділи. Поєднай історію, голос і особистість в одному абзаці.

# Шаблон вмісту:
[2–4 слова для стилю]
[Почни з розкриття унікальної історії або таємниці, яка пояснює його минуле. Пов'яжи цю історію з тим, чому він став виконувати свою нинішню роль. Вигадай конкретну короткострокову психологічну мету, зумовлену цією історією. Явно опиши, як ця історія впливає на темп його мовлення, словниковий запас і ставлення. Усе має бути одним суцільним абзацом.]";

        public const string DefaultPrompt_Strict = @"# Роль: Профілер поведінки Rimworld
# Мова: {LANG}

# ЗАВДАННЯ
Виконайте суворий логічний синтез [Даних персонажа].
КРИТИЧНО: Створіть реалістичний біографічний міст між [Дитинством] і [Дорослим життям]. Розглядайте їх не як окремі теги, а як точки на безперервній часовій шкалі.

# ЗАБОРОНИ:
1. НЕ ВИВАНТАЖУЙТЕ ДАНІ: НІКОЛИ не згадуйте конкретні числа (наприклад, «Стрільба 10»), рівні навичок або назви генів/рис без обробки.
2. ПЕРЕКЛАДАЙТЕ: Перетворюйте характеристики на наратив.

# ПРАВИЛА ЛОГІКИ (Зв’язувальний елемент)
1. Аналіз внутрішньої динаміки:
    -   Проаналізуйте соціологічну та психологічну еволюцію від середовища Дитинства до ролі в Дорослому житті.
    -   Визначте реалістичний переломний момент, який пояснює цю зміну, не покладаючись на зовнішні науково-фантастичні тропи, якщо їх прямо не наведено в даних.
2. Психологічний слід:
    -   Визначте, як дитячий досвід зберігається в теперішній особистості.
    -   Виявіть конкретні звички, страхи або цінності, сформовані в ранні роки, які або підтримують, або суперечать нинішній професії Дорослого життя.
3. Дані як докази:
    -   Розглядайте кожен рівень навички та кожну рису як фізичний доказ минулого досвіду.
    -   Обґрунтуйте високі навички як результат необхідності виживання або інтенсивної підготовки, а низькі — як наслідок відсутності відповідного середовища або уникання.
4. Стандартний протокол архетипу:
    -   Якщо [Передісторія] або [Риси] відсутні: застосуйте стандартні заводські налаштування для їхньої раси або віку.

# СТРАТЕГІЯ ВИВЕДЕННЯ
Створіть ОДИН цілісний психологічний профіль. Цілком зосередьтеся на причинності — пояснюйте результат, спираючись лише на причину.
1. Абсолютна впевненість: Використовуйте категоричні формулювання. Жодних «можливо» або «ймовірно».
2. Прихована логіка: НІКОЛИ не використовуйте слова «Переломний момент», «Перехід» або «Динаміка».

# Шаблон вмісту
[Стиль із 2–4 слів]
[Опишіть логічний життєвий шлях персонажа на основі даних. Поясніть переломний момент, що привів його до нинішньої ролі. Визначте короткострокову психологічну мету, що відповідає його рисам. Явно опишіть темп мовлення, словниковий запас і ставлення як результат його життєвого досвіду. Викладіть усе в одному суцільному абзаці.]";

        public const string DefaultPrompt_Evolve = @"# Роль: аналітик розвитку персонажа Rimworld
# Мова: {LANG}

# Завдання:
Проаналізуйте надані дані й напишіть короткий додаток про розвиток. Зосередьтеся на змінах у світогляді персонажа, його стилі мовлення та поведінкових схильностях.

# ІЄРАРХІЯ ДАНИХ:
1. ОСНОВА: [Previous Persona], [Time Context] (визначає масштаб і характер розвитку).
2. КОНТЕКСТ: [Director's Notes], [New Memories], [Status Changes] (надає конкретні тригери для змін).

# КРИТИЧНІ ПРАВИЛА:
1. Без повторів: ніколи не повторюйте фрази чи слова з [Previous Persona].
2. Причина й наслідок: почніть із стислого підсумку нещодавніх переживань/труднощів, а потім опишіть відповідну зміну світогляду та стилю діалогу.
3. Лише синтез: не перелічуйте події. Перетворіть спогади й зміни навичок на риси персонажа.
4. Фокус на діалозі: зосередьтеся на тому, як персонаж тепер говорить або мислить.
5. Логіка віку: якщо [Time Context] свідчить про значне старіння, надайте пріоритет змінам зрілості та світогляду; якщо період короткий, зосередьтеся на безпосередніх емоційних реакціях і зацикленнях.
6. Обмеження довжини: суворо 1–2 речення. Максимум 50 слів.";

        // =============================================================
        // Technical Protocols (Hidden)
        // =============================================================

        public const string HiddenTechnicalPrompt_Single = @"
# СИСТЕМНИЙ ПРОТОКОЛ (ЗАБЕЗПЕЧЕННЯ ФОРМАТУ JSON):
Ви повинні повернути дійсний об’єкт JSON. НЕ використовуйте блоки коду Markdown.
Поле 'persona' має бути РЯДКОМ В ОДИН РЯДОК із використанням \n для розривів.
Поля:
1. ""persona"": Повний текст результату (використовуйте \n для форматування).
2. ""chattiness"": Float (0.1 - 1.0).
";

        public const string HiddenTechnicalPrompt_Batch = @"
# СИСТЕМНИЙ ПРОТОКОЛ (ЗАБЕЗПЕЧЕННЯ ФОРМАТУ ПАКЕТНОГО JSON):
Ви обробляєте КІЛЬКА персонажів.
Кінцевий результат MUST бути дійсним об’єктом JSON з ЄДИНИМ полем 'persona'.
У рядку 'persona' перелічіть особистість кожного персонажа.

КРИТИЧНІ ПРАВИЛА ФОРМАТУ:
1. Блок кожного персонажа MUST починатися з його ID точно так, як його надано у вхідних даних, наприклад '[ID:Human123]'.
2. Розділяйте персонажів за допомогою '---'.
3. Використовуйте \n для перенесення рядків.

Приклад для поля 'persona':
""[ID:Human101]: Опис Джона...\n---\n[ID:Human102]: Опис Джейн...""

Поля:
1. ""persona"": Об’єднаний текст для ВСІХ персонажів.
2. ""chattiness"": Float (просто використовуйте 0.5 як значення за замовчуванням).
";

        // =============================================================
        // Settings Fields
        // =============================================================

        public string activePrompt = "";

        public List<PromptPreset> presets;
        public int selectedPresetIndex = 0;

        public bool EnableDebugLog = false;
        public string directorNotes = "";
        public bool ShowMainButton = true;
        public bool enableEvolveFeature = true;
        public ContextSettings Context = new ContextSettings();
        public Dictionary<string, bool> BatchFilters;

        public string rimTalkPreset_Single = "";
        public string rimTalkPreset_Evolve = "";

        public List<CustomPreset> userPresets = new List<CustomPreset>();
        public List<AssignmentRule> assignmentRules = new List<AssignmentRule>();
        public bool _libraryInitialized = false;
        public static List<PersonalityData> OriginalVanillaCache;
        private bool _chattinessMigratedV2 = false;
        public string rimTalkPresetName = "Director";
        public override void ExposeData()
        {
            Scribe_Values.Look(ref activePrompt, PersonaScribeLabels.Settings.ActivePrompt, "", true);

            Scribe_Values.Look(ref selectedPresetIndex, PersonaScribeLabels.Settings.SelectedPresetIndex, 0);
            Scribe_Collections.Look(ref presets, PersonaScribeLabels.Settings.Presets, LookMode.Deep);

            Scribe_Values.Look(ref EnableDebugLog, PersonaScribeLabels.Settings.EnableDebugLog, false);
            Scribe_Values.Look(ref directorNotes, PersonaScribeLabels.Settings.DirectorNotes, "");
            Scribe_Values.Look(ref ShowMainButton, PersonaScribeLabels.Settings.ShowMainButton, true);
            Scribe_Values.Look(ref enableEvolveFeature, PersonaScribeLabels.Settings.EnableEvolveFeature, true);

            Scribe_Deep.Look(ref Context, PersonaScribeLabels.Settings.Context);
            if (Context == null) Context = new ContextSettings();

            Scribe_Collections.Look(ref BatchFilters, PersonaScribeLabels.Settings.BatchFilters, LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref userPresets, PersonaScribeLabels.Settings.UserPresets, LookMode.Deep);
            Scribe_Collections.Look(ref assignmentRules, PersonaScribeLabels.Settings.AssignmentRules, LookMode.Deep);
            Scribe_Values.Look(ref _libraryInitialized, PersonaScribeLabels.Settings.LibraryInitialized, false);
            Scribe_Values.Look(ref _chattinessMigratedV2, PersonaScribeLabels.Settings.ChattinessMigratedV2, false);

            Scribe_Values.Look(ref rimTalkPreset_Single, PersonaScribeLabels.Settings.RimTalkPresetSingle, "");
            Scribe_Values.Look(ref rimTalkPreset_Evolve, PersonaScribeLabels.Settings.RimTalkPresetEvolve, "");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                InitPresets();
                InitFilters();

                if (userPresets == null) userPresets = new List<CustomPreset>();
                if (assignmentRules == null) assignmentRules = new List<AssignmentRule>();
                if (!_libraryInitialized && userPresets.Count > 0)
                {
                    _libraryInitialized = true;
                }
            }

            base.ExposeData();
        }

        public void MigrateChattinessValuesIfNeeded()
        {
            if (_chattinessMigratedV2) return;

            if (userPresets == null) return;

            int count = 0;
            foreach (var preset in userPresets)
            {
                if (preset.chattiness > 0)
                {
                    preset.chattiness = Mathf.Clamp(preset.chattiness / 2.0f, 0.1f, 1.0f);
                    count++;
                }
            }
            _chattinessMigratedV2 = true;
            RimAiLog.Info(RimAiLogCategory.Personas, $"[RimAI.Personas] Migrated {count} user presets to new chattiness scale (v2).");
        }

        public void InitLibrary()
        {
            RimAiLog.Info(RimAiLogCategory.Personas, "[RimAI.Personas] -> InitLibrary: Starting...");
            if (userPresets == null) userPresets = new List<CustomPreset>();
            else userPresets.Clear();
            if (assignmentRules == null) assignmentRules = new List<AssignmentRule>();
            else assignmentRules.Clear();
            RimAiLog.Info(RimAiLogCategory.Personas, "[RimAI.Personas] -> InitLibrary: Cleared existing lists. Loading built-in presets...");
            int builtInCount = 0;
            foreach (var def in PresetLibrary.Defaults)
            {
                string translatedText = def.personaText.Translate().Resolve();
                string smartLabel = ExtractLabelFromText(translatedText) ?? def.label;

                if (string.IsNullOrEmpty(smartLabel)) smartLabel = def.label;

                userPresets.Add(new CustomPreset
                {
                    label = smartLabel,
                    personaText = translatedText,
                    chattiness = def.chattiness,
                    category = "Built-in",
                });
                builtInCount++;
            }
            RimAiLog.Info(RimAiLogCategory.Personas, $"[RimAI.Personas] -> InitLibrary: Loaded {builtInCount} built-in presets. Loading vanilla presets...");
            int vanillaCount = 0;
            IEnumerable<Ustas.RimAI.Communication.Data.PersonalityData> sourceList = null;

            if (OriginalVanillaCache != null)
            {
                sourceList = OriginalVanillaCache;
            }
            else if (Ustas.RimAI.Communication.Data.Constant.Personalities != null)
            {
                var currentList = Ustas.RimAI.Communication.Data.Constant.Personalities as IEnumerable<Ustas.RimAI.Communication.Data.PersonalityData>;
                if (currentList != null)
                {
                    OriginalVanillaCache = currentList.ToList();
                    sourceList = OriginalVanillaCache;
                }
            }

            if (sourceList != null)
            {
                foreach (var p in sourceList)
                {
                    string translatedText = p.Persona;
                    try { translatedText = p.Persona.Translate().Resolve(); } catch { }
                    bool isBuiltIn = PresetLibrary.Defaults.Any(d => d.personaText == translatedText);
                    if (!isBuiltIn && !userPresets.Any(existing => existing.personaText == translatedText))
                    {
                        string smartLabel = $"Vanilla {vanillaCount + 1}";
                        try { smartLabel = ExtractLabelFromText(translatedText) ?? smartLabel; } catch { }

                        userPresets.Add(new CustomPreset
                        {
                            label = smartLabel,
                            personaText = translatedText,
                            chattiness = p.Chattiness,
                            category = "Vanilla",
                        });
                        vanillaCount++;
                    }
                }
            }
            RimAiLog.Info(RimAiLogCategory.Personas, $"[RimAI.Personas] -> InitLibrary: Loaded {vanillaCount} vanilla presets. Loading default rules...");
            _chattinessMigratedV2 = true;

            AddDefaultRules();
            RimAiLog.Info(RimAiLogCategory.Personas, "[RimAI.Personas] -> InitLibrary: Default rules loaded. Syncing to Ustas.RimAI.Communication...");
            PresetSynchronizer.SyncToRimTalk();
            RimAiLog.Info(RimAiLogCategory.Personas, "[RimAI.Personas] -> InitLibrary: Sync complete.");
            RimAiLog.Info(RimAiLogCategory.Personas, "[RimAI.Personas] Library reset/initialized to defaults.");

        }

        public string ExtractLabelFromText(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            string[] separators = new[] { " - ", " – ", " — ", "：", ": " };

            foreach (var sep in separators)
            {
                int index = text.IndexOf(sep);
                if (index > 0 && index < 30)
                {
                    return text.Substring(0, index).Trim();
                }
            }
            return null;
        }

        private void AddDefaultRules()
        {
            var pirateRule = new AssignmentRule
            {
                enabled = false,
                type = RuleType.FactionDef,
                targetDefName = "Pirate",
                priority = 10
            };
            AddIdsToRule(pirateRule, "Apocalypse", "Sociopath", "Machiavellian", "Narcissist", "Troll");
            assignmentRules.Add(pirateRule);

            var tribeRule = new AssignmentRule
            {
                enabled = false,
                type = RuleType.FactionDef,
                targetDefName = "TribeRough",
                priority = 10
            };
            AddIdsToRule(tribeRule, "Primal", "Monk", "Daoist", "Weary Survivor");
            assignmentRules.Add(tribeRule);

            if (ModsConfig.RoyaltyActive)
            {
                var empireRule = new AssignmentRule
                {
                    enabled = false,
                    type = RuleType.FactionDef,
                    targetDefName = "Empire",
                    priority = 20
                };
                AddIdsToRule(empireRule, "Young Master", "Bureaucrat", "Noble", "Paladin", "Butler");
                assignmentRules.Add(empireRule);
            }

            if (ModsConfig.BiotechActive)
            {
                var wasterRule = new AssignmentRule
                {
                    enabled = false,
                    type = RuleType.XenotypeDef,
                    targetDefName = "Waster",
                    priority = 50
                };
                AddIdsToRule(wasterRule, "Apocalypse", "Doomer", "Grindset");
                assignmentRules.Add(wasterRule);
            }
        }

        private void AddIdsToRule(AssignmentRule rule, params string[] searchLabels)
        {
            foreach (var label in searchLabels)
            {
                var preset = userPresets.FirstOrDefault(p => p.label.Contains(label));
                if (preset != null && !rule.allowedPresetIds.Contains(preset.id))
                {
                    rule.allowedPresetIds.Add(preset.id);
                }
            }
        }
        public void InitPresets()
        {
            bool migratedLegacyDefault = false;
            if (presets == null) presets = new List<PromptPreset>();

            while (presets.Count < 4)
            {
                presets.Add(new PromptPreset("", ""));
            }

            string[] localizedDefaults = { DefaultPrompt_Standard, DefaultPrompt_Simple, DefaultPrompt_Strict, DefaultPrompt_Evolve };
            string[] localizedLabels = { "Стандартний (3 варіанти)", "Сюжетний", "На основі даних", "Розвиток (лише оновлення)" };
            string[] englishLabels = { "Standard (3 Options)", "Story-Driven", "Data-Driven", "Evolution (Update Only)" };
            for (int i = 0; i < localizedDefaults.Length; i++)
            {
                if (IsLegacyEnglishDefault(presets[i].text, i))
                {
                    presets[i].text = localizedDefaults[i];
                    migratedLegacyDefault = true;
                }
                if (presets[i].label == englishLabels[i])
                {
                    presets[i].label = localizedLabels[i];
                    migratedLegacyDefault = true;
                }
            }
            if (IsLegacyEnglishDefault(activePrompt, 0)) activePrompt = "";

            if (!string.IsNullOrEmpty(activePrompt) && activePrompt != DefaultPrompt_Standard)
            {
                if (string.IsNullOrEmpty(presets[0].text) || presets[0].text == DefaultPrompt_Standard)
                {
                    presets[0].text = activePrompt;
                    presets[0].label = "Custom (Migrated)";
                }
                activePrompt = "";
            }

            if (string.IsNullOrEmpty(presets[0].text))
            {
                presets[0].label = localizedLabels[0];
                presets[0].text = DefaultPrompt_Standard;
            }

            if (string.IsNullOrEmpty(presets[1].text))
            {
                presets[1].label = localizedLabels[1];
                presets[1].text = DefaultPrompt_Simple;
            }

            if (string.IsNullOrEmpty(presets[2].text))
            {
                presets[2].label = localizedLabels[2];
                presets[2].text = DefaultPrompt_Strict;
            }
            if (string.IsNullOrEmpty(presets[3].text))
            {
                presets[3].label = localizedLabels[3];
                presets[3].text = DefaultPrompt_Evolve;
            }
            if (migratedLegacyDefault && Scribe.mode == LoadSaveMode.PostLoadInit)
                LongEventHandler.ExecuteWhenFinished(Write);
        }

        private static bool IsLegacyEnglishDefault(string value, int index)
        {
            if (string.IsNullOrEmpty(value) || index < 0 || index >= 4) return false;
            string[] hashes =
            {
                "A87080AAC9C5B7561F4E59D8AE8F91187096FC68322223C26DBE1C119FBEA69E",
                "F6F6B7291CD23F8E95B1B8884D470AE175D020E7F332EB51B707CB2846F04172",
                "6D828D85D6ABE1AF4E7BE2C005FF4547C723FF57E0FC3727FB435A351D774209",
                "4A441356B0473AFD9ED14DAC14C36202CEA3DA01C4C09BB13312194490EEC1A5"
            };
            using (SHA256 sha = SHA256.Create())
            {
                string normalized = value.Replace("\r\n", "\n").Trim();
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
                string actual = BitConverter.ToString(digest).Replace("-", "");
                return string.Equals(actual, hashes[index], StringComparison.Ordinal);
            }
        }

        public string GetActivePrompt(bool isEvolveMode = false)
        {
            if (presets == null || presets.Count == 0) InitPresets();

            int indexToUse = selectedPresetIndex;

            if (!isEvolveMode && selectedPresetIndex == 3)
            {
                indexToUse = 0;
            }

            int safeIndex = Mathf.Clamp(indexToUse, 0, presets.Count - 1);
            return presets[safeIndex].text;
        }

        public void InitFilters()
        {
            if (BatchFilters == null) BatchFilters = new Dictionary<string, bool>();
            EnsureKey("Colonists", true);
            EnsureKey("Prisoners", true);
            EnsureKey("Slaves", true);
            EnsureKey("Visitors", false);
            EnsureKey("Enemies", false);
            EnsureKey("Animals", false);
            EnsureKey("Mechs", false);
            EnsureKey("Anomalies", false);
        }

        private void EnsureKey(string key, bool defaultValue)
        {
            if (!BatchFilters.ContainsKey(key)) BatchFilters[key] = defaultValue;
        }
    }

    public class ContextSettings : IExposable
    {
        public bool Inc_Basic = true;
        public bool Inc_Race = true; public bool Inc_Race_Desc = false;
        public bool Inc_Genes = true; public bool Inc_Genes_Desc = false;
        public bool Inc_Backstory = true; public bool Inc_Backstory_Desc = true;
        public bool Inc_Relations = true;
        public bool Inc_DirectorNotes = true;

        public bool Inc_Traits = true; public bool Inc_Traits_Desc = true;
        public bool Inc_Ideology = false; public bool Inc_Ideology_Desc = false;
        public bool Inc_Skills = true; public bool Inc_Skills_Desc = true;
        public bool Inc_Health = false; public bool Inc_Health_Desc = false;
        public bool Inc_Equipment = false;
        public bool Inc_Inventory = false;
        public bool Inc_RimPsyche = false; public bool Inc_RimPsyche_All = false;
        public bool Inc_Memories = false;
        public bool Inc_CommonKnowledge = false;
        public bool Inc_DataComparison = false;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Inc_Basic, PersonaScribeLabels.Context.IncBasic, true);
            Scribe_Values.Look(ref Inc_Race, PersonaScribeLabels.Context.IncRace, true);
            Scribe_Values.Look(ref Inc_Race_Desc, PersonaScribeLabels.Context.IncRaceDesc, false);
            Scribe_Values.Look(ref Inc_Genes, PersonaScribeLabels.Context.IncGenes, true);
            Scribe_Values.Look(ref Inc_Genes_Desc, PersonaScribeLabels.Context.IncGenesDesc, false);
            Scribe_Values.Look(ref Inc_Backstory, PersonaScribeLabels.Context.IncBackstory, true);
            Scribe_Values.Look(ref Inc_Backstory_Desc, PersonaScribeLabels.Context.IncBackstoryDesc, true);
            Scribe_Values.Look(ref Inc_Relations, PersonaScribeLabels.Context.IncRelations, true);
            Scribe_Values.Look(ref Inc_DirectorNotes, PersonaScribeLabels.Context.IncDirectorNotes, true);
            Scribe_Values.Look(ref Inc_Traits, PersonaScribeLabels.Context.IncTraits, true);
            Scribe_Values.Look(ref Inc_Traits_Desc, PersonaScribeLabels.Context.IncTraitsDesc, true);
            Scribe_Values.Look(ref Inc_Ideology, PersonaScribeLabels.Context.IncIdeology, false);
            Scribe_Values.Look(ref Inc_Ideology_Desc, PersonaScribeLabels.Context.IncIdeologyDesc, false);
            Scribe_Values.Look(ref Inc_Skills, PersonaScribeLabels.Context.IncSkills, true);
            Scribe_Values.Look(ref Inc_Skills_Desc, PersonaScribeLabels.Context.IncSkillsDesc, true);
            Scribe_Values.Look(ref Inc_Health, PersonaScribeLabels.Context.IncHealth, false);
            Scribe_Values.Look(ref Inc_Health_Desc, PersonaScribeLabels.Context.IncHealthDesc, false);
            Scribe_Values.Look(ref Inc_Equipment, PersonaScribeLabels.Context.IncEquipment, false);
            Scribe_Values.Look(ref Inc_Inventory, PersonaScribeLabels.Context.IncInventory, false);
            Scribe_Values.Look(ref Inc_RimPsyche, PersonaScribeLabels.Context.IncRimPsyche, false);
            Scribe_Values.Look(ref Inc_RimPsyche_All, PersonaScribeLabels.Context.IncRimPsycheAll, false);
            Scribe_Values.Look(ref Inc_Memories, PersonaScribeLabels.Context.IncMemories, false);
            Scribe_Values.Look(ref Inc_CommonKnowledge, PersonaScribeLabels.Context.IncCommonKnowledge, false);
            Scribe_Values.Look(ref Inc_DataComparison, PersonaScribeLabels.Context.IncDataComparison, false);
        }
    }

}
