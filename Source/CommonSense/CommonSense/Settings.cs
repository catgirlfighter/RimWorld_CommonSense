using UnityEngine;
using Verse;
using System;

namespace CommonSense
{
    class Settings : ModSettings
    {
        public static bool separate_meals = true;
        public static bool fulfill_outdoors = true;
        public static bool odd_is_normal = false;
        public static bool clean_before_work = true;
        public static bool clean_after_tanding = true;
        public static bool calculate_full_path = true;
        public static bool add_meal_ingredients = true;

        public static void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing_Standard = new Listing_Standard();
            listing_Standard.Begin(inRect);
            listing_Standard.Label("Need of Outdoors");
            listing_Standard.CheckboxLabeled("Pawns are encouraged to fulfill their need of outdoors by seeking recreation outdoors", ref fulfill_outdoors, "NOTE: Don't place the same joy giving sources both indoors and outdoors to avoid indecisiveness.");
            listing_Standard.GapLine();
            listing_Standard.Label("Cleaning");
            listing_Standard.CheckboxLabeled("Pawns are encouraged to clean the room before aperating a building in it", ref clean_before_work,"NOTE: don't exptect cleaning from those guys, that don't like to clean");
            listing_Standard.CheckboxLabeled("Calculate accurate path to the building for bill jobs", ref calculate_full_path, "It'll help to avoid making unreasonable hooks around when your pawns need to bring resources first, but it may be costly on CPU");
            listing_Standard.GapLine();
            listing_Standard.Label("Meal stacking");
            listing_Standard.CheckboxLabeled("Pawns avoid mixing negative thoughts when stacking meals", ref separate_meals, "That way pawns will never mix disgusting meals with normal ones");
            listing_Standard.CheckboxLabeled("Count odd meat (ex. insect) as normal, allowing to stack it with normal meals", ref odd_is_normal, "Insect meat isn't that bad...");
            listing_Standard.GapLine();
            listing_Standard.Label("Meal generation");
            listing_Standard.CheckboxLabeled("Add random ingredients to randomly generated meals", ref add_meal_ingredients, "ex. gifted by story teller or sold by a trader. Only mood neutral components are used");
            listing_Standard.End();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref separate_meals, "separate_meals", true, false);
            Scribe_Values.Look<bool>(ref fulfill_outdoors, "fulfill_outdoors", true, false);
            Scribe_Values.Look<bool>(ref odd_is_normal, "odd_is_normal", false, false);
            Scribe_Values.Look<bool>(ref clean_before_work, "clean_before_work", true, false);
            Scribe_Values.Look<bool>(ref clean_after_tanding, "clean_after_tanding", true, false);
            Scribe_Values.Look<bool>(ref calculate_full_path, "calculate_full_path", true, false);
            Scribe_Values.Look<bool>(ref add_meal_ingredients, "add_meal_ingredients", true, false);
        }
    }
}
