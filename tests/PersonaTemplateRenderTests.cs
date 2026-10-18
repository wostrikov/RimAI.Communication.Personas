using System;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>
/// A template that fails to render never reaches the model as its literal "{{ }}" text: every
/// call site uses ScribanParser.TryRender and drops the preset, the notes or the persona line
/// with a warning instead.
/// </summary>
internal static class PersonaTemplateRenderTests
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

        string generator = Read("DirectorPersonalityGenerator.cs.src");
        string renderer = Read("DirectorPresetRenderer.cs.src");
        string composer = Read("DirectorPromptComposer.cs.src");

        foreach (var (name, text) in new[] { ("generator", generator), ("renderer", renderer), ("composer", composer) })
        {
            T(!Regex.IsMatch(text, @"ScribanParser\.Render\s*\("), name + "-no-raw-text-render");
            T(text.Contains("ScribanParser.TryRender("), name + "-uses-try-render");
        }

        // The preset is abandoned for the built-in prompt; the notes are left out and the request goes ahead.
        T(Failure(generator, "TryRender(entry.Content", "return null;"), "generator-abandons-preset-with-warning");
        T(Failure(renderer, "TryRender(entry.Content", "return false;"), "renderer-abandons-preset-with-warning");
        T(Failure(renderer, "TryRender(notes", "return string.Empty;"), "notes-left-out-with-warning");
        // The persona line is left out, warned once per template, and never falls back to the raw text.
        T(composer.Contains("RimAiLog.WarningOnce(RimAiLogCategory.Personas"), "persona-warns-once");
        T(!Regex.IsMatch(composer, @"catch \(Exception\)\s*\{[^}]*return rawText;", RegexOptions.Singleline),
            "persona-no-raw-fallback");

        return n;
    }

    // The failure branch after the render call warns and then takes the given exit.
    private static bool Failure(string text, string call, string exit)
    {
        int at = text.IndexOf(call, StringComparison.Ordinal);
        if (at < 0) return false;
        int warn = text.IndexOf("RimAiLog.Warning", at, StringComparison.Ordinal);
        int leave = warn < 0 ? -1 : text.IndexOf(exit, warn, StringComparison.Ordinal);
        return warn > at && warn - at < 400 && leave > warn && leave - warn < 400;
    }

    private static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name)).Replace("\r\n", "\n");
}
