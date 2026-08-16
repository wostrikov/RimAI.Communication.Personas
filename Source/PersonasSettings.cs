using HarmonyLib;
using Ustas.RimAI.Communication.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Personas
{
    // 新增：预设数据结构
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
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref text, "text");
        }
    }

    // 单个预设
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
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref personaText, "personaText");
            Scribe_Values.Look(ref chattiness, "chattiness", 1.0f);
            Scribe_Values.Look(ref category, "category", "Default");
            Scribe_Values.Look(ref enabled, "enabled", true);
        }
    }

    public enum RuleType { FactionDef, RaceDef, XenotypeDef }

    // 分配规则
    public class AssignmentRule : IExposable
    {
        public bool enabled = true;
        public string targetDefName; // 只存字符串，防崩坏
        public RuleType type;
        public int priority = 0;
        public List<string> allowedPresetIds = new List<string>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref targetDefName, "targetDefName");
            Scribe_Values.Look(ref type, "type");
            Scribe_Values.Look(ref priority, "priority", 0);
            Scribe_Collections.Look(ref allowedPresetIds, "allowedPresetIds", LookMode.Value);
        }
    }

    public class PersonasSettings : ModSettings
    {
        // =============================================================
        // Prompt Templates (Default Constants)
        // =============================================================

        // 1. 标准模式 (原版三选一)
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

        // 2. 故事模式 （3选1变单选）
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

        // 3. 背景模式
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

        // 4. 演变/更新模式 (专用)
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

        // 旧数据 (保留用于迁移)
        public string activePrompt = "";

        // 新数据：预设列表
        public List<PromptPreset> presets;
        public int selectedPresetIndex = 0;

        public bool EnableDebugLog = false;
        public string directorNotes = "";
        public bool ShowMainButton = true;
        public bool enableEvolveFeature = true;
        public ContextSettings Context = new ContextSettings();
        public Dictionary<string, bool> BatchFilters;

        public string rimTalkPreset_Single = ""; // 单体生成用的预设名
        public string rimTalkPreset_Evolve = ""; // 演变生成用的预设名

        //  新增字段：预设库和规则库
        public List<CustomPreset> userPresets = new List<CustomPreset>();
        public List<AssignmentRule> assignmentRules = new List<AssignmentRule>();
        // 初始化状态标记
        public bool _libraryInitialized = false;
        // 缓存 (不保存)
        public static List<PersonalityData> OriginalVanillaCache;
        //  新增：迁移标记 (默认为 false)
        private bool _chattinessMigratedV2 = false;
        // ★★★ 新增：目标 RimTalk 预设名称 ★★★
        public string rimTalkPresetName = "Director";
        public override void ExposeData()
        {
            // 读取旧数据
            Scribe_Values.Look(ref activePrompt, "activePrompt", "", true);

            // 读取新数据
            Scribe_Values.Look(ref selectedPresetIndex, "selectedPresetIndex", 0);
            Scribe_Collections.Look(ref presets, "presets", LookMode.Deep);

            // 其他设置
            Scribe_Values.Look(ref EnableDebugLog, "EnableDebugLog", false);
            Scribe_Values.Look(ref directorNotes, "directorNotes", "");
            Scribe_Values.Look(ref ShowMainButton, "ShowMainButton", true);
            Scribe_Values.Look(ref enableEvolveFeature, "enableEvolveFeature", true);

            Scribe_Deep.Look(ref Context, "Context");
            if (Context == null) Context = new ContextSettings();

            Scribe_Collections.Look(ref BatchFilters, "BatchFilters", LookMode.Value, LookMode.Value);
            // 库数据
            Scribe_Collections.Look(ref userPresets, "userPresets", LookMode.Deep);
            Scribe_Collections.Look(ref assignmentRules, "assignmentRules", LookMode.Deep);
            // 保存初始化标记
            Scribe_Values.Look(ref _libraryInitialized, "libraryInitialized", false);
            Scribe_Values.Look(ref _chattinessMigratedV2, "chattinessMigratedV2", false);

            Scribe_Values.Look(ref rimTalkPreset_Single, "rimTalkPreset_Single", "");
            Scribe_Values.Look(ref rimTalkPreset_Evolve, "rimTalkPreset_Evolve", "");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                InitPresets(); // Prompt 1-4 初始化 (这个是安全的，因为只涉及我们自己的类)
                InitFilters(); // 过滤器初始化 (安全)

                // ★★★ 核心修复：不要在这里调用 InitLibrary() ★★★
                // 我们只确保 List 对象不为 null，防止 UI 报错
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
                // 旧版逻辑是 0-2.0，新版是 0-1.0
                // 直接除以 2，进行无损压缩
                if (preset.chattiness > 0)
                {
                    preset.chattiness = Mathf.Clamp(preset.chattiness / 2.0f, 0.1f, 1.0f);
                    count++;
                }
            }
            _chattinessMigratedV2 = true;
            Log.Message($"[RimAI.Personas] Migrated {count} user presets to new chattiness scale (v2).");
        }

        public void InitLibrary()
        {
            Log.Message("[RimAI.Personas] -> InitLibrary: Starting...");
            if (userPresets == null) userPresets = new List<CustomPreset>();
            else userPresets.Clear();
            if (assignmentRules == null) assignmentRules = new List<AssignmentRule>();
            else assignmentRules.Clear();
            Log.Message("[RimAI.Personas] -> InitLibrary: Cleared existing lists. Loading built-in presets...");
            // 填充预设库
            // 1. 内置库
            int builtInCount = 0;
            foreach (var def in PresetLibrary.Defaults)
            {
                string translatedText = def.personaText.Translate().Resolve();
                string smartLabel = ExtractLabelFromText(translatedText) ?? def.label;

                // 如果提取失败(比如没有横杠)，就用原来的英文 Label 做保底
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
            Log.Message($"[RimAI.Personas] -> InitLibrary: Loaded {builtInCount} built-in presets. Loading vanilla presets...");
            // 2. 填充原版
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
                    // 保护翻译和提取过程
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
            Log.Message($"[RimAI.Personas] -> InitLibrary: Loaded {vanillaCount} vanilla presets. Loading default rules...");
            _chattinessMigratedV2 = true;

            // 3. 填充规则库
            AddDefaultRules();
            Log.Message("[RimAI.Personas] -> InitLibrary: Default rules loaded. Syncing to Ustas.RimAI.Communication...");
            PresetSynchronizer.SyncToRimTalk();
            Log.Message("[RimAI.Personas] -> InitLibrary: Sync complete.");
            Log.Message("[RimAI.Personas] Library reset/initialized to defaults.");

        }

        // ★★★ 辅助方法：智能提取标题 ★★★
        public string ExtractLabelFromText(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            // ★★★ 支持更多分隔符: –, —, :, ： ★★★
            string[] separators = new[] { " - ", " – ", " — ", "：", ": " };

            foreach (var sep in separators)
            {
                int index = text.IndexOf(sep);
                if (index > 0 && index < 30) // 限制标题长度
                {
                    return text.Substring(0, index).Trim();
                }
            }
            return null;
        }

        private void AddDefaultRules()
        {
            // --- 规则 1: 海盗 (Pirate) ---
            // 对应预设：废土狂徒, 反社会, 强盗, 混乱邪恶类
            var pirateRule = new AssignmentRule
            {
                enabled = false,
                type = RuleType.FactionDef,
                targetDefName = "Pirate", // 原版海盗
                priority = 10
            };
            // 查找合适的预设并加入池子
            AddIdsToRule(pirateRule, "Apocalypse", "Sociopath", "Machiavellian", "Narcissist", "Troll");
            assignmentRules.Add(pirateRule);

            // --- 规则 2: 部落 (Tribe) ---
            // 对应预设：原始本能, 萨满(Monk/Daoist代替), 猎人
            var tribeRule = new AssignmentRule
            {
                enabled = false,
                type = RuleType.FactionDef,
                targetDefName = "TribeRough", // 狂暴部落
                priority = 10
            };
            AddIdsToRule(tribeRule, "Primal", "Monk", "Daoist", "Weary Survivor");
            assignmentRules.Add(tribeRule);

            // --- 规则 3: 帝国 (Empire - DLC) ---
            // 对应预设：贵族, 骑士, 官僚
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

            // --- 规则 4: 污秽人 (Waster - DLC) ---
            // 对应预设：废土风
            if (ModsConfig.BiotechActive)
            {
                var wasterRule = new AssignmentRule
                {
                    enabled = false,
                    type = RuleType.XenotypeDef,
                    targetDefName = "Waster",
                    priority = 50 // 种族优先级高于派系
                };
                AddIdsToRule(wasterRule, "Apocalypse", "Doomer", "Grindset");
                assignmentRules.Add(wasterRule);
            }
        }

        private void AddIdsToRule(AssignmentRule rule, params string[] searchLabels)
        {
            foreach (var label in searchLabels)
            {
                // 模糊匹配预设名称
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

            // 1. 确保至少有3个槽位
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

            // 2. 数据迁移：如果旧 activePrompt 存在且不是默认值，迁移到 Slot 1
            if (!string.IsNullOrEmpty(activePrompt) && activePrompt != DefaultPrompt_Standard)
            {
                // 只有当 Slot 1 还没被初始化或被修改时才覆盖
                if (string.IsNullOrEmpty(presets[0].text) || presets[0].text == DefaultPrompt_Standard)
                {
                    presets[0].text = activePrompt;
                    presets[0].label = "Custom (Migrated)";
                }
                activePrompt = ""; // 清除旧数据标记完成
            }

            // 3. 填充默认值 (如果槽位为空)
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

        // 获取当前激活的 Prompt 内容
        public string GetActivePrompt(bool isEvolveMode = false)
        {
            if (presets == null || presets.Count == 0) InitPresets();

            int indexToUse = selectedPresetIndex;

            // 如果不是 Evolve 调用，但用户不小心选中了 Evolve 专用槽位 (索引3)，
            // 那么强制使用标准槽位 (索引0) 来防止错误。
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
            Scribe_Values.Look(ref Inc_Basic, "Inc_Basic", true);
            Scribe_Values.Look(ref Inc_Race, "Inc_Race", true);
            Scribe_Values.Look(ref Inc_Race_Desc, "Inc_Race_Desc", false);
            Scribe_Values.Look(ref Inc_Genes, "Inc_Genes", true);
            Scribe_Values.Look(ref Inc_Genes_Desc, "Inc_Genes_Desc", false);
            Scribe_Values.Look(ref Inc_Backstory, "Inc_Backstory", true);
            Scribe_Values.Look(ref Inc_Backstory_Desc, "Inc_Backstory_Desc", true);
            Scribe_Values.Look(ref Inc_Relations, "Inc_Relations", true);
            Scribe_Values.Look(ref Inc_DirectorNotes, "Inc_DirectorNotes", true);
            Scribe_Values.Look(ref Inc_Traits, "Inc_Traits", true);
            Scribe_Values.Look(ref Inc_Traits_Desc, "Inc_Traits_Desc", true);
            Scribe_Values.Look(ref Inc_Ideology, "Inc_Ideology", false);
            Scribe_Values.Look(ref Inc_Ideology_Desc, "Inc_Ideology_Desc", false);
            Scribe_Values.Look(ref Inc_Skills, "Inc_Skills", true);
            Scribe_Values.Look(ref Inc_Skills_Desc, "Inc_Skills_Desc", true);
            Scribe_Values.Look(ref Inc_Health, "Inc_Health", false);
            Scribe_Values.Look(ref Inc_Health_Desc, "Inc_Health_Desc", false);
            Scribe_Values.Look(ref Inc_Equipment, "Inc_Equipment", false);
            Scribe_Values.Look(ref Inc_Inventory, "Inc_Inventory", false);
            Scribe_Values.Look(ref Inc_RimPsyche, "Inc_RimPsyche", false);
            Scribe_Values.Look(ref Inc_RimPsyche_All, "Inc_RimPsyche_All", false);
            Scribe_Values.Look(ref Inc_Memories, "Inc_Memories", false);
            Scribe_Values.Look(ref Inc_CommonKnowledge, "Inc_CommonKnowledge", false);
            Scribe_Values.Look(ref Inc_DataComparison, "Inc_DataComparison", false);
        }
    }

}
