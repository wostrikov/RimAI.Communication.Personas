using System.Collections.Generic;
using Ustas.RimAI.Communication.Personas.Automation;
using Ustas.RimAI.Communication.Personas.Policy;
using Verse;

namespace Ustas.RimAI.Communication.Personas.History
{
    /// <summary>
    /// Records what the player saves in the persona editor. The editor belongs
    /// to Communication; its footer hook runs every frame, including the frame
    /// in which Save writes the persona, so a change of the stored text between
    /// two frames of the same editor is the player's own edit.
    /// </summary>
    internal static class PersonaEditorHistoryWatcher
    {
        static Window _window;
        static Pawn _pawn;
        static string _lastSeen;

        internal static void Observe(Window editor, Pawn pawn)
        {
            if (editor == null || pawn == null) return;
            string current = PersonaOwnership.ReadWithoutCreating(pawn);
            if (!ReferenceEquals(editor, _window) || pawn != _pawn)
            {
                _window = editor;
                _pawn = pawn;
                _lastSeen = current;
                return;
            }

            if (current == _lastSeen) return;
            string before = _lastSeen;
            _lastSeen = current;

            // A restore from the history window also lands here; it already has
            // its record, and a second one would only repeat it.
            IReadOnlyList<PersonaHistoryRecord> records = PersonaHistoryService.CurrentStore?.GetRecords(pawn);
            if (records != null && records.Count > 0
                && PersonaHistoryPolicy.Normalize(records[0].after) == PersonaHistoryPolicy.Normalize(current))
            {
                return;
            }

            PersonaHistoryService.Record(pawn, before, current, PersonaChangeSource.ManualEdit, null);
        }
    }
}
