using Ustas.RimAI.Communication.Personas.Policy;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Personas;

public sealed class Window_PersonaVariantPicker : Window
{
    readonly Pawn _pawn;
    readonly PersonaVariantParseResult _parsed;
    int _selected;
    Vector2 _scroll;

    public Window_PersonaVariantPicker(Pawn pawn, PersonaVariantParseResult parsed)
    {
        _pawn = pawn;
        _parsed = parsed;
        _selected = 0;
        doCloseX = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = false;
    }

    public override Vector2 InitialSize => new Vector2(640f, 520f);

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "RPD_VariantPicker_Title".Translate(_pawn?.LabelShortCap ?? ""));
        Text.Font = GameFont.Small;

        Rect listRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 88f);
        float rowHeight = 92f;
        float viewHeight = Mathf.Max(listRect.height, (_parsed?.Variants.Count ?? 0) * rowHeight);
        Widgets.BeginScrollView(listRect, ref _scroll, new Rect(0f, 0f, listRect.width - 16f, viewHeight));
        if (_parsed != null)
        {
            for (int i = 0; i < _parsed.Variants.Count; i++)
            {
                Rect row = new Rect(0f, i * rowHeight, listRect.width - 20f, rowHeight - 8f);
                bool chosen = i == _selected;
                if (Widgets.RadioButtonLabeled(new Rect(row.x, row.y, row.width, 24f), _parsed.Variants[i].Title ?? ("Variant " + (i + 1)), chosen))
                    _selected = i;
                Widgets.Label(new Rect(row.x + 28f, row.y + 26f, row.width - 28f, row.height - 26f), _parsed.Variants[i].Text);
            }
        }
        Widgets.EndScrollView();

        Rect apply = new Rect(inRect.x, inRect.yMax - 36f, 160f, 32f);
        Rect cancel = new Rect(inRect.x + 172f, inRect.yMax - 36f, 160f, 32f);
        if (Widgets.ButtonText(apply, "RPD_VariantPicker_Apply".Translate()))
        {
            if (DirectorPersonaVariantFlow.ApplySelection(_pawn, _parsed, _selected))
            {
                Messages.Message("RPD_Message_GeneratedSuccess".Translate(_pawn.LabelShortCap), MessageTypeDefOf.PositiveEvent, false);
                Close();
            }
        }
        if (Widgets.ButtonText(cancel, "RPD_VariantPicker_Cancel".Translate()))
            Close();
    }
}
