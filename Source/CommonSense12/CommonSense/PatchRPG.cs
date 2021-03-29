using System;
using HarmonyLib;
using Verse;

namespace CommonSense
{
    [StaticConstructorOnStartup]
    public static class PatchRPG
    {
        static PatchRPG()
        {
            var harmonyInstance = new Harmony("net.avilmask.rimworld.mod.CommonSense.RPGInventory");
            Type type;
            if ((type = AccessTools.TypeByName("Sandy_Detailed_RPG_GearTab")) != null)
            {
                var mi = AccessTools.Method(type, "DrawThingRow", null, null);
                HarmonyMethod hm = new HarmonyMethod(typeof(ITab_Pawn_Gear_DrawThingRow_CommonSensePatch), nameof(ITab_Pawn_Gear_DrawThingRow_CommonSensePatch.Prefix), null);
                harmonyInstance.Patch(mi, hm, null);
            }
        }
    }
}
