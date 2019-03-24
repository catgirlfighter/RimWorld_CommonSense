using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using System.Text;
using Harmony;
using RimWorld;
using Verse;

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

    class JoyPriority
    {

        static float JoyPolicePriority(Pawn pawn)
        {
            CompJoyToppedOff c = pawn.TryGetComp<CompJoyToppedOff>();
            if (c == null || !c.JoyToppedOff)
                return 0.95f;
            else
                return 0.8f;
        }

        [HarmonyPatch(typeof(ThinkNode_Priority_GetJoy), "GetPriority")]
        static class ThinkNode_Priority_GetJoy_GetPriority_CommonSensePatch
        {

            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il, MethodBase mb)
            {
                foreach (var i in (instructions))
                {
                    if (i.opcode == OpCodes.Ldc_R4 && (float)i.operand == 0.95f)
                    {
                        yield return new CodeInstruction(OpCodes.Ldarg_1);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JoyPriority), nameof(JoyPriority.JoyPolicePriority)));
                    }
                    else if (i.opcode == OpCodes.Ldc_R4 && (float)i.operand == 2f)
                    {
                        yield return new CodeInstruction(OpCodes.Ldc_R4, 4f);
                    }
                    else
                    {
                        yield return i;
                    }
                }
            }
        }
    }


}
