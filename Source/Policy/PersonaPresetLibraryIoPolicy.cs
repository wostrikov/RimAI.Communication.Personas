using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

namespace Ustas.RimAI.Communication.Personas.Policy;

public sealed class PersonaLibraryPresetRecord
{
    public string Id;
    public string Label;
    public string PersonaText;
    public float Chattiness = 1f;
    public string Category = "Default";
    public bool Enabled = true;
}

public sealed class PersonaLibraryRuleRecord
{
    public bool Enabled = true;
    public string TargetDefName;
    public string Type;
    public int Priority;
    public List<string> AllowedPresetIds = new List<string>();
}

public sealed class PersonaLibrarySnapshot
{
    public List<PersonaLibraryPresetRecord> Presets = new List<PersonaLibraryPresetRecord>();
    public List<PersonaLibraryRuleRecord> Rules = new List<PersonaLibraryRuleRecord>();
}

public sealed class PersonaLibraryParseResult
{
    public bool Accepted;
    public string Reason;
    public PersonaLibrarySnapshot Snapshot;
}

/// <summary>
/// Verse-free import/export for the user preset library. Fail-closed:
/// garbage or empty payloads never mutate the current lists.
/// </summary>
public static class PersonaPresetLibraryIoPolicy
{
    public const string RootName = "rpdLibrary";
    public const string Version = "1";

    public static string Format(PersonaLibrarySnapshot snapshot)
    {
        snapshot ??= new PersonaLibrarySnapshot();
        var root = new XElement(RootName, new XAttribute("version", Version));
        foreach (var preset in snapshot.Presets ?? new List<PersonaLibraryPresetRecord>())
        {
            if (preset == null)
                continue;
            root.Add(new XElement(
                "preset",
                new XAttribute("id", preset.Id ?? string.Empty),
                new XAttribute("label", preset.Label ?? string.Empty),
                new XAttribute("chattiness", preset.Chattiness.ToString("0.###", CultureInfo.InvariantCulture)),
                new XAttribute("category", preset.Category ?? "Default"),
                new XAttribute("enabled", preset.Enabled ? "true" : "false"),
                preset.PersonaText ?? string.Empty));
        }

        foreach (var rule in snapshot.Rules ?? new List<PersonaLibraryRuleRecord>())
        {
            if (rule == null)
                continue;
            root.Add(new XElement(
                "rule",
                new XAttribute("enabled", rule.Enabled ? "true" : "false"),
                new XAttribute("type", rule.Type ?? string.Empty),
                new XAttribute("priority", rule.Priority.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("target", rule.TargetDefName ?? string.Empty),
                string.Join(",", rule.AllowedPresetIds ?? new List<string>())));
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root).ToString();
    }

    public static PersonaLibraryParseResult Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Reject("empty");

        XDocument document;
        try
        {
            document = XDocument.Parse(text);
        }
        catch (System.Xml.XmlException)
        {
            return Reject("xml");
        }

        var root = document.Root;
        if (root == null || !string.Equals(root.Name.LocalName, RootName, StringComparison.Ordinal))
            return Reject("root");
        if (!string.Equals((string)root.Attribute("version"), Version, StringComparison.Ordinal))
            return Reject("version");

        var snapshot = new PersonaLibrarySnapshot();
        foreach (var element in root.Elements("preset"))
        {
            snapshot.Presets.Add(new PersonaLibraryPresetRecord
            {
                Id = (string)element.Attribute("id") ?? string.Empty,
                Label = (string)element.Attribute("label") ?? string.Empty,
                Chattiness = ParseFloat((string)element.Attribute("chattiness"), 1f),
                Category = (string)element.Attribute("category") ?? "Default",
                Enabled = ParseBool((string)element.Attribute("enabled"), true),
                PersonaText = element.Value ?? string.Empty
            });
        }

        foreach (var element in root.Elements("rule"))
        {
            var ids = new List<string>();
            var raw = element.Value ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (var part in raw.Split(','))
                {
                    if (!string.IsNullOrWhiteSpace(part))
                        ids.Add(part.Trim());
                }
            }

            snapshot.Rules.Add(new PersonaLibraryRuleRecord
            {
                Enabled = ParseBool((string)element.Attribute("enabled"), true),
                Type = (string)element.Attribute("type") ?? string.Empty,
                Priority = ParseInt((string)element.Attribute("priority"), 0),
                TargetDefName = (string)element.Attribute("target") ?? string.Empty,
                AllowedPresetIds = ids
            });
        }

        return new PersonaLibraryParseResult { Accepted = true, Snapshot = snapshot };
    }

    public static PersonaLibrarySnapshot Apply(
        PersonaLibrarySnapshot current,
        PersonaLibrarySnapshot incoming,
        bool overwrite)
    {
        if (incoming == null)
            return Clone(current);

        if (overwrite)
            return Clone(incoming);

        var merged = Clone(current);
        merged.Presets.AddRange(Clone(incoming).Presets);
        merged.Rules.AddRange(Clone(incoming).Rules);
        return merged;
    }

    static PersonaLibrarySnapshot Clone(PersonaLibrarySnapshot source)
    {
        var copy = new PersonaLibrarySnapshot();
        if (source?.Presets != null)
        {
            foreach (var preset in source.Presets)
            {
                if (preset == null)
                    continue;
                copy.Presets.Add(new PersonaLibraryPresetRecord
                {
                    Id = preset.Id,
                    Label = preset.Label,
                    PersonaText = preset.PersonaText,
                    Chattiness = preset.Chattiness,
                    Category = preset.Category,
                    Enabled = preset.Enabled
                });
            }
        }

        if (source?.Rules != null)
        {
            foreach (var rule in source.Rules)
            {
                if (rule == null)
                    continue;
                copy.Rules.Add(new PersonaLibraryRuleRecord
                {
                    Enabled = rule.Enabled,
                    TargetDefName = rule.TargetDefName,
                    Type = rule.Type,
                    Priority = rule.Priority,
                    AllowedPresetIds = new List<string>(rule.AllowedPresetIds ?? new List<string>())
                });
            }
        }

        return copy;
    }

    static PersonaLibraryParseResult Reject(string reason) =>
        new PersonaLibraryParseResult { Accepted = false, Reason = reason };

    static float ParseFloat(string raw, float fallback) =>
        float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    static int ParseInt(string raw, int fallback) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    static bool ParseBool(string raw, bool fallback)
    {
        if (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase))
            return false;
        return fallback;
    }
}
