using Harmony;
using System;
using System.Linq;
using System.Reflection;
using Verse;
using UnityEngine;

namespace CommonSense
{
    [StaticConstructorOnStartup]
    class CommonSense : Mod
    {
#pragma warning disable 0649
        public static Settings Settings;
#pragma warning restore 0649

        public CommonSense(ModContentPack content) : base(content)
        {
            var harmony = HarmonyInstance.Create("net.avilmask.rimworld.mod.CommonSense");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            base.GetSettings<Settings>();
        }
        
        public void Save()
        {
            LoadedModManager.GetMod<CommonSense>().GetSettings<Settings>().Write();
        }

        public override string SettingsCategory()
        {
            return "CommonSense";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
        }
    }
}
