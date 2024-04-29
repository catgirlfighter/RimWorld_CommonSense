using HarmonyLib;
using System.Reflection;
using Verse;
using UnityEngine;
using RimWorld;

namespace CommonSense
{
    [StaticConstructorOnStartup]
    public class CommonSense : Mod
    {
        public static Settings Settings;
        public CommonSense(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("net.avilmask.rimworld.mod.CommonSense");
            GetSettings<Settings>();
            //
            Settings.optimal_patching_in_use = Settings.optimal_patching;
            if (Settings.optimal_patching_in_use && !Settings.fun_police)
            {
                var compJoyToppedOff = typeof(CompJoyToppedOff);
                var list = DefDatabase<ThingDef>.AllDefsListForReading;
                for (int i = list.Count; i-- > 0;)
                {
                    var def = list[i];
                    if (def.HasComp(compJoyToppedOff)) def.comps.RemoveAll(x => x.compClass == compJoyToppedOff);
                }
            }
            //
            harmony.PatchAll(Assembly.GetExecutingAssembly());
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
