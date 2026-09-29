using Ustas.RimAI.Communication.Personas.Automation;

namespace Ustas.RimAI.Communication.Personas.Patches
{
    /// <summary>
    /// What the automation hooks may touch. Each hook sits inside a vanilla
    /// method (a death, a faction change, a birth) and only records what it saw
    /// into the inbox; judging the event happens on the next world tick, so no
    /// persona decision can fail inside the game method itself.
    /// </summary>
    internal static class PersonaHookBoundary
    {
        /// <summary>The event inbox, or null while the module is not running.</summary>
        internal static PersonaGameEventInbox Inbox =>
            PersonasComposition.Current.IsStarted ? PersonasComposition.Current.Automation.Inbox : null;
    }
}
