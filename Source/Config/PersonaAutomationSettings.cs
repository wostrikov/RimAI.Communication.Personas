using System.Collections.Generic;
using Ustas.RimAI.Communication.Personas.Automation;
using Ustas.RimAI.Communication.Personas.Policy;
using Verse;

namespace Ustas.RimAI.Communication.Personas.Config
{
    public enum AutoEvolveMode
    {
        Append,
        Overwrite
    }

    /// <summary>
    /// The player's choices for the automatic persona work, saved with the mod
    /// settings. Every feature here is off until the player turns it on. The
    /// work queue itself is runtime state and is never saved.
    /// </summary>
    public sealed class PersonaAutomationSettings : IExposable
    {
        // Evolve in overwrite mode rewrites the persona instead of appending a
        // development line, so it needs its own instruction. Append mode keeps
        // to the Evolution slot of the prompt presets.
        public const string DefaultPrompt_Overwrite = @"# Роль: автор персонажів Rimworld
# Мова: {LANG}

# Завдання:
Перепишіть персону повністю. Вплетіть нові переживання та зміни стану в саму основу особистості.

# ІЄРАРХІЯ ДАНИХ:
1. ОСНОВА: [Previous Persona], [Time Context] (визначають масштаб і характер змін).
2. КОНТЕКСТ: [Trigger Events], [Director's Notes], [New Memories], [Status Changes] (дають конкретні причини змін).

# ПРАВИЛА:
1. Не дописуйте в кінець: органічно вплетіть нові риси в текст.
2. Збережіть формат і приблизний обсяг попередньої персони.
3. Не перелічуйте події: перетворюйте їх на характер, світогляд і манеру мовлення.
4. Поверніть лише новий текст персони.";

        public bool autoGenEnabled;
        public Dictionary<string, AutoGenCategorySettings> autoGenCategories = new Dictionary<string, AutoGenCategorySettings>();

        public bool autoEvolveEnabled;
        public int autoEvolveIntervalDays = 15;
        public bool autoEvolveIncludeCaptives;
        public AutoEvolveMode autoEvolveMode = AutoEvolveMode.Append;
        public bool evolveOnRoleChange;
        public bool evolveOnMarriage;
        public bool evolveOnBreakup;
        public bool evolveOnBirth;
        public bool evolveOnFamilyDeath;
        public bool evolveOnTraitGained;

        public bool notifyOnAutoChange;
        public int historyMaxRecords = PersonaHistoryPolicy.DefaultMaxRecords;

        // No new automatic request starts at this game speed or faster (TimeSpeed
        // as a number: 2 = fast, 3 = superfast, 4 = ultrafast); 0 never pauses.
        public int pauseAtSpeed = PersonaAutomationPolicy.DefaultPauseAtSpeed;

        // Set by PersonaAutomationFuse; cleared only by the player.
        public bool fuseTripped;
        public string fuseReason = string.Empty;

        public bool AnyEnabled => autoGenEnabled || autoEvolveEnabled;

        public void ExposeData()
        {
            Scribe_Values.Look(ref autoGenEnabled, "autoGenEnabled", false);
            Scribe_Collections.Look(ref autoGenCategories, "autoGenCategories", LookMode.Value, LookMode.Deep);
            Scribe_Values.Look(ref autoEvolveEnabled, "autoEvolveEnabled", false);
            Scribe_Values.Look(ref autoEvolveIntervalDays, "autoEvolveIntervalDays", 15);
            Scribe_Values.Look(ref autoEvolveIncludeCaptives, "autoEvolveIncludeCaptives", false);
            Scribe_Values.Look(ref autoEvolveMode, "autoEvolveMode", AutoEvolveMode.Append);
            Scribe_Values.Look(ref evolveOnRoleChange, "evolveOnRoleChange", false);
            Scribe_Values.Look(ref evolveOnMarriage, "evolveOnMarriage", false);
            Scribe_Values.Look(ref evolveOnBreakup, "evolveOnBreakup", false);
            Scribe_Values.Look(ref evolveOnBirth, "evolveOnBirth", false);
            Scribe_Values.Look(ref evolveOnFamilyDeath, "evolveOnFamilyDeath", false);
            Scribe_Values.Look(ref evolveOnTraitGained, "evolveOnTraitGained", false);
            Scribe_Values.Look(ref notifyOnAutoChange, "notifyOnAutoChange", false);
            Scribe_Values.Look(ref historyMaxRecords, "historyMaxRecords", PersonaHistoryPolicy.DefaultMaxRecords);
            Scribe_Values.Look(ref pauseAtSpeed, "pauseAtSpeed", PersonaAutomationPolicy.DefaultPauseAtSpeed);
            Scribe_Values.Look(ref fuseTripped, "fuseTripped", false);
            Scribe_Values.Look(ref fuseReason, "fuseReason", string.Empty);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                autoEvolveIntervalDays = PersonaAutomationPolicy.ClampIntervalDays(autoEvolveIntervalDays);
                historyMaxRecords = PersonaHistoryPolicy.ClampMaxRecords(historyMaxRecords);
                pauseAtSpeed = PersonaAutomationPolicy.ClampPauseAtSpeed(pauseAtSpeed);
                if (fuseReason == null) fuseReason = string.Empty;
                EnsureCategories();
            }
        }

        public AutoGenCategorySettings GetCategory(PawnRoleCategory category)
        {
            if (category == PawnRoleCategory.None) return null;
            EnsureCategories();
            return autoGenCategories.TryGetValue(category.ToString(), out AutoGenCategorySettings found) ? found : null;
        }

        public void EnsureCategories()
        {
            if (autoGenCategories == null) autoGenCategories = new Dictionary<string, AutoGenCategorySettings>();
            foreach (PawnRoleCategory category in PawnRoleClassifier.Ordered)
            {
                string key = category.ToString();
                if (!autoGenCategories.TryGetValue(key, out AutoGenCategorySettings existing) || existing == null)
                    autoGenCategories[key] = new AutoGenCategorySettings();
            }
        }
    }

    /// <summary>Auto-Gen choices for one role category.</summary>
    public sealed class AutoGenCategorySettings : IExposable
    {
        // Slot 0 asks the model for three variants to pick from, which nobody is
        // there to do for an automatic request; the single-persona slots are 1 and 2.
        public const int FirstSinglePersonaSlot = 1;
        public const int LastSinglePersonaSlot = 2;

        public bool enabled;
        public bool onRoleChange;
        public bool syncWithGlobalContext = true;
        public int presetSlot = FirstSinglePersonaSlot;
        public ContextSettings customContext = CreateDefaultContext();

        public void ExposeData()
        {
            Scribe_Values.Look(ref enabled, "enabled", false);
            Scribe_Values.Look(ref onRoleChange, "onRoleChange", false);
            Scribe_Values.Look(ref syncWithGlobalContext, "syncWithGlobalContext", true);
            Scribe_Values.Look(ref presetSlot, "presetSlot", FirstSinglePersonaSlot);
            Scribe_Deep.Look(ref customContext, "customContext");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (customContext == null) customContext = CreateDefaultContext();
                presetSlot = ClampSlot(presetSlot);
            }
        }

        public ContextSettings EffectiveContext(ContextSettings global)
        {
            return syncWithGlobalContext || customContext == null ? global : customContext;
        }

        public static int ClampSlot(int slot)
        {
            if (slot < FirstSinglePersonaSlot) return FirstSinglePersonaSlot;
            if (slot > LastSinglePersonaSlot) return LastSinglePersonaSlot;
            return slot;
        }

        // The global director notes describe the colony's own story, which rarely
        // fits a raider or a passing trader; a category's own context leaves them out.
        static ContextSettings CreateDefaultContext() => new ContextSettings { Inc_DirectorNotes = false };
    }
}
