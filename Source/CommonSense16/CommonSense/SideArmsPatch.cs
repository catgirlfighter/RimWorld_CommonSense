using System;
using HarmonyLib;
using Verse;
using System.Reflection;


namespace CommonSense
{
    [StaticConstructorOnStartup]
    public static class Sidearms_Utility
    {
        //static Type sidearmsPatch = null;
        private static readonly Type compSidearmMemory = null;
        private static readonly MethodInfo LInformOfDroppedSidearm = null;
        private static readonly MethodInfo LGetMemoryCompForPawn = null;
        //
        static Sidearms_Utility()
        {
            compSidearmMemory = AccessTools.TypeByName("SimpleSidearms.rimworld.CompSidearmMemory");

            if (compSidearmMemory == null)
                return;

            LInformOfDroppedSidearm = AccessTools.Method(compSidearmMemory, "InformOfDroppedSidearm");
            LGetMemoryCompForPawn = AccessTools.Method(compSidearmMemory, "GetMemoryCompForPawn");
        }
        public static bool Active { get { return compSidearmMemory != null; } }

        public static void ForgetSidearm(Pawn pawn, Thing thing)
        {
            var instance = LGetMemoryCompForPawn.Invoke(null, new object[] { pawn, false });
            if (instance != null) LInformOfDroppedSidearm.Invoke(instance, new object[] { thing, true });
        }
    }
}
