using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace CommonSense
{
    public class CompJoyToppedOff : ThingComp
    {
        public bool JoyToppedOff = false;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref JoyToppedOff, "CommonSenseJoyToppedOff", defaultValue: false);
        }
    }

    [HarmonyPatch(typeof(ThinkNode_Priority_GetJoy), "GetPriority")]
    static class ThinkNode_Priority_GetJoy_GetPriority_CommonSensePatch
    {
        internal static bool Prepare()
        {
            return !Settings.optimal_patching_in_use || Settings.fun_police;
        }
        private static float JoyPolicePriority(Pawn pawn)
        {
            if (!Settings.fun_police)
                return 0.8f;

            CompJoyToppedOff c = pawn.TryGetComp<CompJoyToppedOff>();
            if (c == null || !c.JoyToppedOff)
                return 0.95f;
            else
                return 0.8f;
        }

        private static float JoyPolicePriority2(Pawn pawn)
        {
            if (Settings.fun_police)
            {
                CompJoyToppedOff c = pawn.TryGetComp<CompJoyToppedOff>();
                if(c?.JoyToppedOff == false)
                    return 4f;
            }
            return 2f;
        }

        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var i in (instructions))
            {
                if (i.opcode == OpCodes.Ldc_R4 && (float)i.operand == 0.95f)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ThinkNode_Priority_GetJoy_GetPriority_CommonSensePatch), nameof(JoyPolicePriority)));
                }
                else if (i.opcode == OpCodes.Ldc_R4 && (float)i.operand == 2f)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ThinkNode_Priority_GetJoy_GetPriority_CommonSensePatch), nameof(JoyPolicePriority2)));
                }
                else
                {
                    yield return i;
                }
            }
        }
    }
}
