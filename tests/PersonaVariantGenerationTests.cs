using System;
using System.IO;
using Ustas.RimAI.Communication.Personas.Policy;

internal static class PersonaVariantGenerationTests
{
    public static int Run()
    {
        int n = 0;
        void T(bool x, string s)
        {
            if (!x)
                throw new Exception("FAILED " + s);
            n++;
        }

        const string custom = "Use a wry colonial voice. Language={LANG}. Emit {COUNT} variants.";
        string envelope = PersonaVariantGenerationPolicy.ComposeInstruction(
            custom, "Ukrainian", 3, "TECHNICAL_JSON");
        T(envelope.Contains("Ukrainian"), "envelope-language");
        T(envelope.Contains("3"), "envelope-count");
        T(envelope.Contains("TECHNICAL_JSON"), "envelope-protocol");
        T(PersonaVariantGenerationPolicy.IncludesCustomPrompt(envelope, custom), "envelope-custom-prompt");

        var empty = PersonaVariantGenerationPolicy.Parse("");
        T(empty.Outcome == PersonaVariantOutcome.EmptySource, "parse-empty");

        var markdown = PersonaVariantGenerationPolicy.Parse(
            "### Варіант 1: Стоїк\nСпокійний голос, короткі речення.\n---\n### Варіант 2: Іронік\nГоворить крізь усмішку.\n---\n### Variant 3: Прагматик\nЛише факти.");
        T(markdown.CanSelect, "markdown-selectable");
        T(markdown.Variants.Count == 3, "markdown-count");
        T(markdown.Variants[0].Title.Contains("Стоїк"), "markdown-title");
        T(markdown.Variants[1].Text.Contains("усмішку"), "markdown-body");

        var json = PersonaVariantGenerationPolicy.Parse(
            "{\"variants\":[{\"title\":\"A\",\"persona\":\"first voice\",\"chattiness\":0.2},{\"title\":\"B\",\"persona\":\"second voice\",\"chattiness\":0.8}]}");
        T(json.CanSelect && json.Variants.Count == 2, "json-two-variants");
        T(Math.Abs(json.Variants[1].Chattiness - 0.8f) < 0.0001f, "json-chattiness");

        var single = PersonaVariantGenerationPolicy.Parse("### Варіант 1: Один\nЛише один текст.");
        T(single.Outcome == PersonaVariantOutcome.InsufficientVariants, "single-is-not-selection");
        T(!single.CanSelect, "single-cannot-select");

        var malformed = PersonaVariantGenerationPolicy.Parse("no headings and no json array here");
        T(malformed.Outcome == PersonaVariantOutcome.MalformedResponse, "malformed");

        var picked = PersonaVariantGenerationPolicy.Select(markdown.Variants, 1);
        T(picked.Variant != null && picked.Variant.Title.Contains("Іронік"), "select-second");
        T(PersonaVariantGenerationPolicy.Select(markdown.Variants, -1).Variant == null, "select-negative-fail-closed");
        T(PersonaVariantGenerationPolicy.Select(markdown.Variants, 99).Variant == null, "select-oob-fail-closed");

        string generator = Read("DirectorPersonalityGenerator.cs.src");
        string flow = Read("DirectorPersonaVariantFlow.cs.src");
        string batch = Read("Window_BatchDirector.cs.src");
        string picker = Read("Window_PersonaVariantPicker.cs.src");
        string settings = Read("PersonasSettings.cs.src");
        T(generator.Contains("PersonaVariantGenerationPolicy.ComposeInstruction"), "generator-uses-policy");
        T(flow.Contains("PersonaVariantGenerationPolicy.Parse"), "flow-parses");
        T(flow.Contains("Window_PersonaVariantPicker"), "flow-opens-picker");
        T(flow.Contains("PersonaVariantGenerationPolicy.Select"), "flow-selects-via-policy");
        T(batch.Contains("DirectorPersonaVariantFlow.OfferSelectionOrApply"), "batch-offers-selection");
        T(picker.Contains("DirectorPersonaVariantFlow.ApplySelection"), "picker-applies-selection");
        T(settings.Contains("згенерувати 3 відмінні варіанти") || settings.Contains("3 відмінні інтерпретації"), "standard-prompt-asks-variants");
        return n;
    }

    static string Read(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, name);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
