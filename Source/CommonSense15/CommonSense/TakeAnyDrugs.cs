using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace CommonSense
{
    [HarmonyPatch(typeof(FoodUtility), "WillIngestFromInventoryNow")]
    public static class FoodUtility_WillIngestFromInventoryNow_CommonSensePatch
    {
        public static bool Prefix(ref bool __result, Pawn pawn, Thing inv)
        {
            if (!Settings.ingest_any_drugs)
                return true;

            //Log.Message($"{pawn}+{inv.def},nutr={inv.def.IsNutritionGivingIngestible},drug={inv.def.IsDrug},ing={inv.IngestibleNow},willeat={pawn.WillEat(inv, null, true)}");
            //__result = (inv.def.IsNutritionGivingIngestible || inv.def.IsDrug) && inv.IngestibleNow && pawn.WillEat_NewTemp(inv);
            __result = ((inv.def.IsNutritionGivingIngestible && pawn.WillEat(inv, null, true, false)) || (/*inv.def.IsNonMedicalDrug && */pawn.CanTakeDrug(inv.def))) && inv.IngestibleNow;
            return false;
        }
    }
}
