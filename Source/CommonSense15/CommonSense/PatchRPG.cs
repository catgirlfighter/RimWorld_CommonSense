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

    static class RPGStyleInventory_PopupMenu_CommonSensePatch
    {
        private static readonly Color hColor = new Color(1f, 0.8f, 0.8f, 1f);

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
                    }, Utility.texUnloadThingCancel, hColor));
                }
                else
                {
                    __result.Add(new FloatMenuOption("UnloadThing".Translate(), delegate ()
                    {
                        SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
                        c.ShouldUnload = true;
                    }, Utility.texUnloadThing, Color.white));
                }
            }
        }
    }

    static class RPGStyleInventory_DrawSlotIcons_CommonSensePatch
    {
        internal static void Postfix(object __instance, Thing thing, Rect slotRect, ref float x, ref float y)
        {
            var c = CompUnloadChecker.GetChecker(thing);
            if (c?.ShouldUnload == true)
            {
                RPGStyleInventory_CommonSensePatch.LDrawSlotIcon.Invoke(__instance, new object[] { slotRect, x, y, Utility.texUnloadThing, (string)"UnloadThing".Translate() });
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class RPGStyleInventory_CommonSensePatch
    {
        //public void DrawSlotIcon(Rect slotRect, ref float x, ref float y, Texture2D tex, string tip)
        public static MethodInfo LDrawSlotIcon = null;
        static RPGStyleInventory_CommonSensePatch()
        {
            var harmonyInstance = new Harmony("net.avilmask.rimworld.mod.CommonSense.RPGInventory");
            Type type;
            if ((type = AccessTools.TypeByName("Sandy_Detailed_RPG_GearTab")) != null)
            {
                MethodInfo mi;
                HarmonyMethod hm;
                if (!Settings.optimal_patching_in_use || Settings.gui_manual_unload)
                {
                    LDrawSlotIcon = AccessTools.Method(type, "DrawSlotIcon");
                    mi = AccessTools.Method(type, "DrawThingRow");
                    hm = new HarmonyMethod(typeof(ITab_Pawn_Gear_DrawThingRow_CommonSensePatch), nameof(ITab_Pawn_Gear_DrawThingRow_CommonSensePatch.Prefix));
                    harmonyInstance.Patch(mi, hm, null);

                    mi = AccessTools.Method(type, "PopupMenu");
                    if (mi != null)
                    {
                        hm = new HarmonyMethod(typeof(RPGStyleInventory_PopupMenu_CommonSensePatch), nameof(RPGStyleInventory_PopupMenu_CommonSensePatch.Postfix));
                        harmonyInstance.Patch(mi, null, hm);
                    }
                }

                mi = AccessTools.Method(type, "DrawSlotIcons");
                if (mi != null)
                {
                    hm = new HarmonyMethod(typeof(RPGStyleInventory_DrawSlotIcons_CommonSensePatch), nameof(RPGStyleInventory_DrawSlotIcons_CommonSensePatch.Postfix));
                    harmonyInstance.Patch(mi, null, hm);
                }
            }
        }
    }

    [StaticConstructorOnStartup]
    static class AwesomeInventory_CommonSensePatch
    {
        //private static readonly PropertyInfo LCanControl = null;
        static AwesomeInventory_CommonSensePatch()
        {
            var harmonyInstance = new Harmony("net.avilmask.rimworld.mod.CommonSense.AwesomeInventory");
            Type type;
            if ((type = AccessTools.TypeByName("AwesomeInventory.UI.DrawGearTabWorker")) != null)
            {
                if (!Settings.optimal_patching_in_use || Settings.gui_manual_unload)
                {
                    var mi = AccessTools.Method(type, "DrawThingRow", null, null);
                    HarmonyMethod hm = new HarmonyMethod(typeof(AwesomeInventory_CommonSensePatch), nameof(AwesomeInventory_CommonSensePatch.Prefix), null);
                    harmonyInstance.Patch(mi, hm, null);
                }
            }
        }

        internal static void Prefix(object __instance, Pawn selPawn, ref float y, ref float width, Thing thing, ref bool inventory)
        {
            if (selPawn == null || thing == null) return;
            var val = Traverse.Create(__instance).Field("_gearTab").GetValue();
            //ITab_Pawn_Gear tab = val as ITab_Pawn_Gear;
            Utility.DrawThingRow(val, ref y, ref width, thing, inventory);
        }
    }
}
