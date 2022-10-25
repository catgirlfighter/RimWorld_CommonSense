using System;
using HarmonyLib;
using Verse;
using Verse.Sound;
using RimWorld;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace CommonSense
{

    //[HarmonyPatch]
    static class RPGStyleInventory_PopupMenu_NonUnoPinataPatch
    {
        static readonly Color hColor = new Color(1f, 0.8f, 0.8f, 1f);

        internal static void Postfix(object __instance, List<FloatMenuOption> __result, Pawn pawn, Thing thing, bool inventory)
        {
            if (!Settings.gui_manual_unload)
                return;
            //Pawn SelPawn = (Pawn)ITab_Pawn_Gear_Utility.LSelPawnForGear.GetValue(__instance);
            bool CanControl = (bool)ITab_Pawn_Gear_Utility.LCanControl.GetValue(__instance);
            if ((thing is ThingWithComps)
                && CanControl
                && (inventory || pawn.IsColonistPlayerControlled || pawn.Spawned && !pawn.Map.IsPlayerHome)
                && !pawn.IsBiocodedOrLinked(thing, inventory)
                && !pawn.IsLocked(thing))
            {
                var c = CompUnloadChecker.GetChecker(thing, false, true);
                if (c.ShouldUnload)
                {
                    __result.Add(new FloatMenuOption("UnloadThingCancel".Translate(), delegate ()
                    {
                        SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
                        c.ShouldUnload = false;

                        if (MassUtility.Capacity(pawn, null) < MassUtility.GearAndInventoryMass(pawn)
                            && thing.stackCount * thing.GetStatValue(StatDefOf.Mass, true) > 0
                            && !thing.def.destroyOnDrop)
                        {
                            ITab_Pawn_Gear_Utility.LInterfaceDrop.Invoke(__instance, new object[] { thing });
                        }
                    }, ContentFinder<Texture2D>.Get("UI/Icons/Unload_Thing_Cancel"), hColor));
                }
                else
                {
                    __result.Add(new FloatMenuOption("UnloadThing".Translate(), delegate ()
                    {
                        SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
                        c.ShouldUnload = true;
                    }, ContentFinder<Texture2D>.Get("UI/Icons/Unload_Thing"), Color.white));
                }
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class RPGStyleInventory_CommonSensePatch
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

                mi = AccessTools.Method(type, "PopupMenu", null, null);
                if (mi != null)
                {
                    hm = new HarmonyMethod(typeof(RPGStyleInventory_PopupMenu_NonUnoPinataPatch), nameof(RPGStyleInventory_PopupMenu_NonUnoPinataPatch.Postfix), null);
                    harmonyInstance.Patch(mi, null, hm);
                }
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class AwesomeInventory_CommonSensePatch
    {
        //private static readonly PropertyInfo LCanControl = null;
        static AwesomeInventory_CommonSensePatch()
        {
            var harmonyInstance = new Harmony("net.avilmask.rimworld.mod.CommonSense.AwesomeInventory");
            Type type;
            if ((type = AccessTools.TypeByName("AwesomeInventory.UI.DrawGearTabWorker")) != null)
            {
                //Log.Message("patched DrawGearTabWorker");
                //LCanControl = AccessTools.Property(typeof(ITab_Pawn_Gear), "CanControl");
                var mi = AccessTools.Method(type, "DrawThingRow", null, null);
                HarmonyMethod hm = new HarmonyMethod(typeof(AwesomeInventory_CommonSensePatch), nameof(AwesomeInventory_CommonSensePatch.Prefix), null);
                harmonyInstance.Patch(mi, hm, null);
            }
        }

        public static void Prefix(object __instance, Pawn selPawn, ref float y, ref float width, Thing thing, ref bool inventory)
        {
            if (selPawn == null || thing == null) return;
            var val = Traverse.Create(__instance).Field("_gearTab").GetValue();
            //ITab_Pawn_Gear tab = val as ITab_Pawn_Gear;
            Utility.DrawThingRow(val, ref y, ref width, thing, inventory);
        }
    }
}
