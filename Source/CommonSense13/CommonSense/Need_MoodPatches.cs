using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using RimWorld.Planet;

namespace CommonSense
{
    [HarmonyPatch(typeof(Need_Mood), "NeedInterval")]

    static class Need_NeedInterval_CommonSensePatch
    {
        static FieldInfo LPawn = AccessTools.Field(typeof(Need), "pawn");
        static PropertyInfo LIsFrozen = AccessTools.Property(typeof(Need), "IsFrozen");

        static void Postfix(Need_Mood __instance)
        {
            if (!Settings.mood_regen) return;
            Pawn pawn = (Pawn)LPawn.GetValue(__instance);
            if ((bool)LIsFrozen.GetValue(__instance) && !pawn.Suspended && pawn.health.capacities.CanBeAwake)
            {
                float curInstantLevel = __instance.CurInstantLevel;
                if (curInstantLevel > __instance.CurLevel && __instance.CurLevel < 0.5f)
                    __instance.CurLevel = Mathf.Min(__instance.CurLevel + __instance.def.seekerRisePerHour * 0.01f, curInstantLevel);
                else if (curInstantLevel < __instance.CurLevel && __instance.CurLevel > 0.5f)
                    __instance.CurLevel = Mathf.Max(__instance.CurLevel - __instance.def.seekerFallPerHour * 0.01f, curInstantLevel);
            }
        }
    }
}
