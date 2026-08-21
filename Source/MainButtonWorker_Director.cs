using UnityEngine;
using Verse;
using RimWorld;
using System.Linq;

namespace Ustas.RimAI.Communication.Personas
{
    public class MainButtonWorker_Director : MainButtonWorker
    {
        public override bool Visible => PersonasMod.Settings.ShowMainButton;

        public override void Activate()
        {
            if (Event.current.shift)
            {
                var mod = LoadedModManager.GetMod<PersonasMod>();
                if (mod != null) Find.WindowStack.Add(new Dialog_ModSettings(mod));
                return;
            }

            if (Event.current.button == 1)
            {
                var win = Find.WindowStack.Windows.OfType<Window_BatchDirector>().FirstOrDefault();
                if (win != null) win.Close();
                else Find.WindowStack.Add(new Window_BatchDirector());

                Event.current.Use();
            }
            else
            {
                var win = Find.WindowStack.Windows.OfType<Window_DirectorNotesEditor>().FirstOrDefault();
                if (win != null) win.Close();
                else Find.WindowStack.Add(new Window_DirectorNotesEditor());
            }
        }
    }
}