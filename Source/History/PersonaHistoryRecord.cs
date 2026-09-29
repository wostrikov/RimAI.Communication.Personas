using System.Collections.Generic;
using Verse;

namespace Ustas.RimAI.Communication.Personas.History
{
    /// <summary>What changed a persona, shown in the history window.</summary>
    public enum PersonaChangeSource
    {
        Director,
        AutoGen,
        AutoEvolve,
        Restore,
        ManualEdit
    }

    /// <summary>One persona change: the text before, the text after, and why.</summary>
    public sealed class PersonaHistoryRecord : IExposable
    {
        public int tick;
        public string before = "";
        public string after = "";
        public PersonaChangeSource source;
        public string context = "";

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick", 0);
            Scribe_Values.Look(ref before, "before", "");
            Scribe_Values.Look(ref after, "after", "");
            Scribe_Values.Look(ref source, "source", PersonaChangeSource.Director);
            Scribe_Values.Look(ref context, "context", "");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                before ??= "";
                after ??= "";
                context ??= "";
            }
        }
    }

    /// <summary>
    /// One pawn's records, newest first. A wrapper rather than a bare list
    /// because Scribe can deep-save an IExposable dictionary value but not a list.
    /// </summary>
    public sealed class PersonaPawnHistory : IExposable
    {
        public List<PersonaHistoryRecord> records = new List<PersonaHistoryRecord>();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref records, "records", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                records ??= new List<PersonaHistoryRecord>();
                records.RemoveAll(record => record == null);
            }
        }
    }
}
