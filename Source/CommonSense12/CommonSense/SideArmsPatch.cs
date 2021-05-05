using System;
using HarmonyLib;
using Verse;
using System.Reflection;


namespace CommonSense
{
    [StaticConstructorOnStartup]
    public static class Sidearms_Utility
    {
        static Type sidearmsPatch = null;
        static Type CompSidearmMemory = null;
        static MethodInfo LInformOfDroppedSidearm = null;
        static MethodInfo LGetMemoryCompForPawn = null;
        //
        static Sidearms_Utility()
        {
            sidearmsPatch = AccessTools.TypeByName("SimpleSidearms.intercepts.ITab_Pawn_Gear_InterfaceDrop_Prefix");
            if (sidearmsPatch == null)
                return;
            //
            CompSidearmMemory = AccessTools.TypeByName("SimpleSidearms.rimworld.CompSidearmMemory");
            LInformOfDroppedSidearm = AccessTools.Method(CompSidearmMemory, "InformOfDroppedSidearm");
            LGetMemoryCompForPawn = AccessTools.Method(CompSidearmMemory, "GetMemoryCompForPawn");
        }

        public static bool Active { get { return sidearmsPatch != null; } }

        public static void ForgetSidearm(Pawn pawn, Thing thing)
        {
            //Log.Message($"GoldfishModule={pawn}-{def}, {GoldfishModule}, {LGetGoldfishForPawn}, {LForgetSidearm}");
            object instance = LGetMemoryCompForPawn.Invoke(null, new object[] { pawn, false });
            //Log.Message($"GoldfishModule={instance}");
            if (instance != null) LInformOfDroppedSidearm.Invoke(instance, new object[] { thing, true });
        }
    }
}
