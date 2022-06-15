using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace CommonSense
{
    [HarmonyPatch(typeof(JoyGiver_Ingest), "CreateIngestJob")]
    public static class JoyGiver_Ingest_CreateIngestJob_CommonSensePatch
    {
        public static void Postfix(Job __result,Thing ingestible, Pawn pawn)
        {
            //used to be a prefix, but something prevented new job from being taken
            if (!Settings.pick_proper_amount)
                return;

            __result.count = Mathf.Min(__result.count, FoodUtility.WillIngestStackCountOf(pawn, ingestible.def, FoodUtility.GetNutrition(ingestible, ingestible.def)));
        }
    }
}
