using System.Text;

namespace Ustas.RimAI.Communication.Personas.Policy
{
    /// <summary>
    /// The sentence that tells the model why a persona is being written or
    /// updated. Model-facing, so plain English like the rest of the Director's
    /// prompt data, and one line per event: the lines of several events are
    /// joined into one section, and the same text is what the history shows.
    ///
    /// "Role changed from Prisoner to Colonist" was all a role change used to
    /// say, and it leaves out the part a persona is made of: a raider taken
    /// from a hostile band and a guest who decided to stay are both a prisoner
    /// or a visitor becoming a colonist. So each side of the change names the
    /// faction the pawn belonged to and how that faction stands towards the
    /// colony, and whose prisoner or slave the pawn was.
    /// </summary>
    public static class PersonaTriggerContextPolicy
    {
        // A trait description is a paragraph in some mods; the trigger section
        // is one of several and the history keeps a copy of it.
        public const int MaxTraitDescriptionChars = 240;

        /// <summary>"Name (standing)", or just the name when the standing is unknown; null without a name.</summary>
        public static string Faction(string name, string standing)
        {
            string label = Clean(name);
            if (label.Length == 0) return null;
            string how = Clean(standing);
            return how.Length == 0 ? label : label + " (" + how + ")";
        }

        /// <summary>
        /// One side of a role change. <paramref name="heldBy"/> is whose prisoner the pawn is,
        /// <paramref name="takenFrom"/> the faction a slave was taken from; either is left out
        /// when it only repeats the pawn's own faction.
        /// </summary>
        public static string RoleState(string faction, string heldBy, string takenFrom)
        {
            string own = Clean(faction);
            var sb = new StringBuilder(own.Length == 0 ? "no faction" : "faction " + own);
            string host = Clean(heldBy);
            if (host.Length > 0 && host != own) sb.Append(", held by ").Append(host);
            string origin = Clean(takenFrom);
            if (origin.Length > 0 && origin != own) sb.Append(", taken from ").Append(origin);
            return sb.ToString();
        }

        /// <summary>
        /// The role change with both sides described. When the sides read the same - a colonist
        /// enslaved by his own colony and freed again - the description adds nothing and is left out.
        /// </summary>
        public static string RoleChange(string fromRole, string toRole, string before, string after)
        {
            string line = "Role changed from " + Clean(fromRole) + " to " + Clean(toRole) + ".";
            string was = Clean(before);
            string now = Clean(after);
            if (was.Length == 0 || now.Length == 0 || was == now) return line;
            return line + " Before: " + was + ". After: " + now + ".";
        }

        public static string TraitGained(string label, string description)
        {
            string line = "Gained a new trait: " + Clean(label) + ".";
            string what = Clean(description);
            if (what.Length == 0) return line;
            if (what.Length > MaxTraitDescriptionChars)
                what = what.Substring(0, MaxTraitDescriptionChars).TrimEnd() + "...";
            return line + " What it means: " + what;
        }

        /// <summary>The parent's own line about a birth; either name may be unknown.</summary>
        public static string Birth(string childName, string otherParentName)
        {
            string child = Clean(childName);
            string line = child.Length > 0
                ? "Became a parent: " + child + " was born."
                : "Became a parent: a child was born.";
            string other = Clean(otherParentName);
            return other.Length == 0 ? line : line + " The other parent is " + other + ".";
        }

        // One line whatever the game's text held: descriptions carry line breaks,
        // and a break here would start what reads as another event.
        static string Clean(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var sb = new StringBuilder(text.Length);
            bool space = false;
            foreach (char c in text.Trim())
            {
                if (char.IsWhiteSpace(c))
                {
                    space = true;
                    continue;
                }
                if (space) sb.Append(' ');
                space = false;
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
