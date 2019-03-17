using System;
using System.Collections.Generic;
using System.Linq;
using Harmony;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace CommonSense
{
    class FoodCountLimiter
    {
        [HarmonyPatch(typeof(JoyGiver_Ingest), "CreateIngestJob")]
        static class JobGiver_GetJoy_TryGiveJob_CommonSensePatch
        {
            static bool Prefix(Job __result,Thing ingestible, Pawn pawn)
            {
                if (!Settings.pick_proper_amount)
                    return true;

                __result = new Job(JobDefOf.Ingest, ingestible)
                {
                    count = Mathf.Min(ingestible.stackCount, FoodUtility.WillIngestStackCountOf(pawn, ingestible.def, ingestible.GetStatValue(StatDefOf.Nutrition, true)))
                };
                return false;
            }
        }
    }
}
