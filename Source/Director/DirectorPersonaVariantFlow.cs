using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Personas.Policy;
using Verse;

namespace Ustas.RimAI.Communication.Personas;

public static class DirectorPersonaVariantFlow
{
    public static bool OfferSelectionOrApply(Pawn pawn, PersonalityData data)
    {
        if (pawn == null || pawn.Destroyed || data == null || string.IsNullOrWhiteSpace(data.Persona))
            return false;

        PersonaVariantParseResult parsed = PersonaVariantGenerationPolicy.Parse(data.Persona, data.Chattiness);
        if (parsed.CanSelect)
        {
            Find.WindowStack.Add(new Window_PersonaVariantPicker(pawn, parsed));
            return true;
        }

        DirectorPersonalityGenerator.ApplyPersonalityToPawn(pawn, data);
        return false;
    }

    public static bool ApplySelection(Pawn pawn, PersonaVariantParseResult parsed, int index)
    {
        PersonaVariantSelection selection = PersonaVariantGenerationPolicy.Select(parsed?.Variants, index);
        if (pawn == null || pawn.Destroyed || selection.Variant == null)
            return false;

        DirectorPersonalityGenerator.ApplyPersonalityToPawn(
            pawn,
            new PersonalityData(selection.Variant.Text, selection.Variant.Chattiness));
        selection.Applied = true;
        return true;
    }
}
