using System;
using HarmonyLib;
using Verse;
using RimWorld;
using System.Reflection;

namespace CommonSense
{
    [StaticConstructorOnStartup]
    static class RPGStyleInventory_CommonSensePatch
    {
        static RPGStyleInventory_CommonSensePatch()
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

    [StaticConstructorOnStartup]
    static class AwesomeInventory_CommonSensePatch
    {
        static PropertyInfo LCanControl = null;
        static AwesomeInventory_CommonSensePatch()
        {
            var harmonyInstance = new Harmony("net.avilmask.rimworld.mod.CommonSense.AwesomeInventory");
            Type type;
            if ((type = AccessTools.TypeByName("AwesomeInventory.UI.DrawGearTabWorker")) != null)
            {
                //Log.Message("patched DrawGearTabWorker");
                LCanControl = AccessTools.Property(typeof(ITab_Pawn_Gear), "CanControl");
                var mi = AccessTools.Method(type, "DrawThingRow", null, null);
                HarmonyMethod hm = new HarmonyMethod(typeof(AwesomeInventory_CommonSensePatch), nameof(AwesomeInventory_CommonSensePatch.Prefix), null);
                harmonyInstance.Patch(mi, hm, null);
            }
        }

        static void Prefix(object __instance, Pawn selPawn, ref float y, ref float width, Thing thing, ref bool inventory)
        {
            if (selPawn == null || thing == null) return;
            var val = Traverse.Create(__instance).Field("_gearTab").GetValue();
            //ITab_Pawn_Gear tab = val as ITab_Pawn_Gear;
            Utility.DrawThingRow(val, ref y, ref width, thing, inventory);
        }
    }
}
