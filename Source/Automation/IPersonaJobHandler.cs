using Ustas.RimAI.Communication.Data;

namespace Ustas.RimAI.Communication.Personas.Automation
{
    /// <summary>
    /// The two kinds of automatic work share one runner; each kind decides how
    /// to build its request and what to do with the answer. Both methods run on
    /// the main thread, where the pawn may be read and written.
    /// </summary>
    internal interface IPersonaJobHandler
    {
        /// <summary>Checks the job still applies and builds its request, or returns null.</summary>
        TalkRequest Prepare(PersonaAutomationJob job, DirectorWorldComponent world);

        /// <summary>Applies the model's answer. Returns whether the persona changed.</summary>
        bool Apply(PersonaAutomationJob job, PersonalityData result, DirectorWorldComponent world);
    }
}
