using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Ustas.RimAI.Communication.Personas.Policy
{
    public sealed class PersonaVariant
    {
        public string Title;
        public string Text;
        public float Chattiness = 0.5f;
    }

    public enum PersonaVariantOutcome
    {
        Ready,
        EmptySource,
        MalformedResponse,
        InsufficientVariants
    }

    public sealed class PersonaVariantParseResult
    {
        public PersonaVariantOutcome Outcome;
        public readonly List<PersonaVariant> Variants = new List<PersonaVariant>();
        public bool CanSelect => Outcome == PersonaVariantOutcome.Ready && Variants.Count >= 2;
    }

    public sealed class PersonaVariantSelection
    {
        public int Index = -1;
        public PersonaVariant Variant;
        public bool Applied;
    }

    public static class PersonaVariantGenerationPolicy
    {
        public const int DefaultCount = 3;
        public const int MinSelectable = 2;

        static readonly Regex MarkdownHeader = new Regex(
            @"^#{1,3}\s*(?:Варіант|Variant|Option)\s*(\d+)\s*[:：-]?\s*(.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);

        public static string ComposeInstruction(string customPrompt, string language, int count, string technicalProtocol)
        {
            string prompt = customPrompt ?? string.Empty;
            prompt = prompt.Replace("{LANG}", language ?? string.Empty);
            prompt = prompt.Replace("{COUNT}", Math.Max(1, count).ToString());
            if (string.IsNullOrWhiteSpace(technicalProtocol))
                return prompt;
            if (string.IsNullOrWhiteSpace(prompt))
                return technicalProtocol;
            return prompt + "\n" + technicalProtocol;
        }

        public static bool IncludesCustomPrompt(string envelope, string customPrompt)
        {
            if (string.IsNullOrWhiteSpace(customPrompt))
                return false;
            int placeholder = customPrompt.IndexOf('{');
            string needle = placeholder > 8 ? customPrompt.Substring(0, placeholder).Trim() : customPrompt.Trim();
            if (needle.Length > 48)
                needle = needle.Substring(0, 48);
            return !string.IsNullOrEmpty(needle) && (envelope ?? string.Empty).IndexOf(needle, StringComparison.Ordinal) >= 0;
        }

        public static PersonaVariantParseResult Parse(string source, float defaultChattiness = 0.5f)
        {
            var result = new PersonaVariantParseResult();
            if (string.IsNullOrWhiteSpace(source))
            {
                result.Outcome = PersonaVariantOutcome.EmptySource;
                return result;
            }

            if (TryParseJsonVariants(source, defaultChattiness, result.Variants))
                return Finish(result);

            ParseMarkdownVariants(source, defaultChattiness, result.Variants);
            if (result.Variants.Count == 0)
                ParseDashedVariants(source, defaultChattiness, result.Variants);

            return Finish(result);
        }

        public static PersonaVariantSelection Select(IList<PersonaVariant> variants, int index)
        {
            var selection = new PersonaVariantSelection { Index = index };
            if (variants == null || index < 0 || index >= variants.Count || variants[index] == null)
                return selection;
            if (string.IsNullOrWhiteSpace(variants[index].Text))
                return selection;
            selection.Variant = variants[index];
            return selection;
        }

        static PersonaVariantParseResult Finish(PersonaVariantParseResult result)
        {
            if (result.Variants.Count >= MinSelectable)
                result.Outcome = PersonaVariantOutcome.Ready;
            else if (result.Variants.Count == 1)
                result.Outcome = PersonaVariantOutcome.InsufficientVariants;
            else
                result.Outcome = PersonaVariantOutcome.MalformedResponse;
            return result;
        }

        static bool TryParseJsonVariants(string source, float defaultChattiness, List<PersonaVariant> into)
        {
            string trimmed = source.Trim();
            int arrayStart = trimmed.IndexOf('[');
            int variantsKey = IndexOfInsensitive(trimmed, "\"variants\"");
            if (variantsKey >= 0)
            {
                int after = trimmed.IndexOf('[', variantsKey);
                if (after >= 0)
                    arrayStart = after;
            }
            if (arrayStart < 0)
                return false;

            int depth = 0;
            int end = -1;
            for (int i = arrayStart; i < trimmed.Length; i++)
            {
                if (trimmed[i] == '[')
                    depth++;
                else if (trimmed[i] == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        end = i;
                        break;
                    }
                }
            }
            if (end < 0)
                return false;

            string array = trimmed.Substring(arrayStart, end - arrayStart + 1);
            int cursor = 0;
            while (cursor < array.Length)
            {
                int objStart = array.IndexOf('{', cursor);
                if (objStart < 0)
                    break;
                int objEnd = array.IndexOf('}', objStart);
                if (objEnd < 0)
                    break;
                string obj = array.Substring(objStart, objEnd - objStart + 1);
                string text = ExtractJsonString(obj, "persona") ?? ExtractJsonString(obj, "text");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    into.Add(new PersonaVariant
                    {
                        Title = ExtractJsonString(obj, "title") ?? ("Variant " + (into.Count + 1)),
                        Text = UnescapeJson(text),
                        Chattiness = ExtractJsonFloat(obj, "chattiness", defaultChattiness)
                    });
                }
                cursor = objEnd + 1;
            }
            return into.Count > 0;
        }

        static void ParseMarkdownVariants(string source, float defaultChattiness, List<PersonaVariant> into)
        {
            MatchCollection matches = MarkdownHeader.Matches(source);
            if (matches.Count == 0)
                return;

            for (int i = 0; i < matches.Count; i++)
            {
                int start = matches[i].Index + matches[i].Length;
                int end = i + 1 < matches.Count ? matches[i + 1].Index : source.Length;
                string body = source.Substring(start, end - start).Trim();
                body = body.Trim('-', '\r', '\n', ' ');
                if (string.IsNullOrWhiteSpace(body))
                    continue;
                string title = matches[i].Groups[2].Value.Trim();
                if (string.IsNullOrWhiteSpace(title))
                    title = "Variant " + matches[i].Groups[1].Value;
                into.Add(new PersonaVariant
                {
                    Title = title,
                    Text = body,
                    Chattiness = defaultChattiness
                });
            }
        }

        static void ParseDashedVariants(string source, float defaultChattiness, List<PersonaVariant> into)
        {
            string[] parts = source.Split(new[] { "\n---\n", "\r\n---\r\n", "\n---\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < MinSelectable)
                return;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim().Trim('-').Trim();
                if (string.IsNullOrWhiteSpace(part))
                    continue;
                into.Add(new PersonaVariant
                {
                    Title = "Variant " + (into.Count + 1),
                    Text = part,
                    Chattiness = defaultChattiness
                });
            }
        }

        static string ExtractJsonString(string obj, string name)
        {
            string key = "\"" + name + "\"";
            int keyIndex = IndexOfInsensitive(obj, key);
            if (keyIndex < 0)
                return null;
            int colon = obj.IndexOf(':', keyIndex + key.Length);
            if (colon < 0)
                return null;
            int quote = obj.IndexOf('"', colon + 1);
            if (quote < 0)
                return null;
            var sb = new StringBuilder();
            for (int i = quote + 1; i < obj.Length; i++)
            {
                char c = obj[i];
                if (c == '\\' && i + 1 < obj.Length)
                {
                    sb.Append(obj[i + 1] == 'n' ? '\n' : obj[i + 1]);
                    i++;
                    continue;
                }
                if (c == '"')
                    return sb.ToString();
                sb.Append(c);
            }
            return null;
        }

        static float ExtractJsonFloat(string obj, string name, float fallback)
        {
            string key = "\"" + name + "\"";
            int keyIndex = IndexOfInsensitive(obj, key);
            if (keyIndex < 0)
                return fallback;
            int colon = obj.IndexOf(':', keyIndex + key.Length);
            if (colon < 0)
                return fallback;
            int end = colon + 1;
            while (end < obj.Length && (char.IsDigit(obj[end]) || obj[end] == '.' || obj[end] == '-' || obj[end] == ' '))
                end++;
            if (float.TryParse(obj.Substring(colon + 1, end - colon - 1).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
                return value;
            return fallback;
        }

        static string UnescapeJson(string text)
        {
            return text.Replace("\\n", "\n");
        }

        static int IndexOfInsensitive(string haystack, string needle)
        {
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        }
    }
}
