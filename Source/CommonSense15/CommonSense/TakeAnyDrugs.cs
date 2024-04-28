using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace CommonSense
{
    [HarmonyPatch(typeof(FoodUtility), "WillIngestFromInventoryNow")]
    static class FoodUtility_WillIngestFromInventoryNow_CommonSensePatch
    {
        internal static bool Prefix(ref bool __result, Pawn pawn, Thing inv)
        {
            if (!Settings.ingest_any_drugs)
                return true;

            __result = ((inv.def.IsNutritionGivingIngestible && pawn.WillEat(inv, null, true, false)) || (pawn.CanTakeDrug(inv.def))) && inv.IngestibleNow;
            return false;
        }
    }
}
