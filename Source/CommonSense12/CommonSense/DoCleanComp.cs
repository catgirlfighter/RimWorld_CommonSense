using System.Collections.Generic;
using Verse;
using RimWorld;
using UnityEngine;

namespace CommonSense
{
    class CleanCommand_Toggle : Command_Toggle
    {
        public override bool Visible
        {
            get { return Settings.clean_gizmo; }
        }
    }

    public class DoCleanComp : ThingComp
    {
        public bool Active
        {
            get
            {
                return active;
            }
            set
            {
                if (value == active)
                {
                    return;
                }
                this.active = value;
            }
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref active, "active", true, false);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            Command_Toggle command_Toggle = new CleanCommand_Toggle();
            //command_Toggle.hotKey = KeyBindingDefOf.Command_TogglePower;
            command_Toggle.defaultLabel = "DoCleanCompToggleLabel".Translate();
            command_Toggle.icon = ContentFinder<Texture2D>.Get("Things/Mote/Clean");
            command_Toggle.isActive = (() => this.Active);
            command_Toggle.toggleAction = delegate ()
            {
                this.Active = !this.Active;
            };
            if (this.Active)
            {
                command_Toggle.defaultDesc = "DoCleanCompToggleDescActive".Translate();
            }
            else
            {
                command_Toggle.defaultDesc = "DoCleanCompToggleDescInactive".Translate();
            }
            yield return command_Toggle;
            yield break;
        }

        private bool active = true;
    }

}