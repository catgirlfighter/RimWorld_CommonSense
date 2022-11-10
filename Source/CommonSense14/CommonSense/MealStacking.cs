using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CommonSense
{
    /*
    [HarmonyPatch(typeof(Thing), "CanStackWith", new Type[] { typeof(Thing) })]
    public static class CompIngredients_CanStackWith_CommonSensePatch
    {
        private static int getflags(CompIngredients compIngredients)
        {
            if (compIngredients?.ingredients == null)
                return 0;
            int b = 0;
            //0 - clean;
            //1 - positive
            //2 - negative
            //4 - humanlike

            foreach (var ing in compIngredients.ingredients)
                if (ing == null)
                    continue;
                else if (!ing.IsIngestible)
                    continue;
                else if (FoodUtility.GetMeatSourceCategory(ing) == MeatSourceCategory.Humanlike)
                    b |= 4;
                else if (ing.ingestible?.specialThoughtAsIngredient?.stages?.Count > 0)
                    if (!Settings.odd_is_normal && ing.ingestible.specialThoughtAsIngredient.stages[0].baseMoodEffect < 0)
                        b |= 2;

            return b;
        }

        public static void Postfix(ref bool __result, ref Thing __instance, ref Thing other)
        {
            if (!Settings.separate_meals || __instance == null || !__result || other == null || !other.def.IsIngestible)
                return;

            CompIngredients ings = __instance.TryGetComp<CompIngredients>();

            if (ings == null)
                return;

            CompIngredients otherings = other.TryGetComp<CompIngredients>();

            if (otherings == null)
                return;

            __result = getflags(ings) == getflags(otherings);
        }
    }
    */
}
