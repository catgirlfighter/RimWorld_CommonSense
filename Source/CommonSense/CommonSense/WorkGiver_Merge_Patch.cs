using System.Collections.Generic;
using System.Reflection.Emit;
using Harmony;
using RimWorld;
using Verse;

namespace CommonSense
{
    class WorkGiver_Merge_Patch
    {
        //Patch based on recommendations of littlewhitemouse (and basically a copypase of his own patch)
        //With it my "CanMergeWith" patch should be wholesome
        [HarmonyPatch(typeof(WorkGiver_Merge), "JobOnThing")]
        static class Compatibility_RimWorld_CommonSense
        {
            // Replace WorkGiver_Merge's JobOnThing's test of "if (thing.def==t.def) ..." with
            //                                                "if (thing.CanStackWith(t)) ..."
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var code = new List<CodeInstruction>(instructions);
                var i = 0; // using two for loops
                while (i < code.Count)
                {
                    yield return code[i];
                    i++;
                    if (code[i].opcode == OpCodes.Ldfld &&
                        code[i].operand == typeof(Verse.Thing).GetField("def"))
                    {
                        if (code[i + 2]?.opcode == OpCodes.Ldfld &&
                            code[i + 2].operand == typeof(Verse.Thing).GetField("def"))
                        {
                            // Found it!
                            // Instead of loading the two defs and checking if they are equal,
                            i++;  // advance past the .def call
                            yield return code[i]; // put the second thing on the stack
                            i = i + 2; // advance past the 2nd thing(we just added it) and its .def call
                                       // Call thing.CanStackWith(t);
                            yield return new CodeInstruction(OpCodes.Callvirt, typeof(Thing).GetMethod("CanStackWith"));

                            // i now points to "branch if equal"
                            CodeInstruction c = new CodeInstruction(OpCodes.Brtrue);
                            c.operand = code[i].operand; // grab it's target
                            yield return c;
                            i++; // advance past bre
                                 // continue returning everything:
                            break;
                        }
                    }
                } // end first for loop
                for (; i < code.Count; i++)
                {
                    yield return code[i];
                }
            } // end Transpler

        } // end Compatibility Patch for Common Sense
    }
}
