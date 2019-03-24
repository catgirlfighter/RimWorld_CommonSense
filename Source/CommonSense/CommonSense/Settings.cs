using System;
using Harmony;
using UnityEngine;
using Verse;

namespace CommonSense
{
    public class Settings : ModSettings
    {
        public static bool separate_meals = true;
        public static bool fulfill_outdoors = true;
        public static bool odd_is_normal = false;
        public static bool clean_before_work = true;
        public static bool clean_after_tanding = true;
        public static bool calculate_full_path = true;
        public static bool add_meal_ingredients = false;
        public static bool add_to_que = false;
        public static bool hauling_over_bills = true;
        public static bool drugs_use_potential_mood = true;
        public static bool adv_cleaning = true;
        public static bool adv_haul_all_ings = true;
        public static bool gui_extended_recipe = true;
        public static bool prefer_spoiling_ingredients = true;
        public static bool gui_manual_unload = true;
        public static bool put_back_to_inv = true;
        public static bool pick_proper_amount = true;
        public static bool fun_police = true;

        public static void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing_Standard = new Listing_Standard();
            listing_Standard.Begin(inRect);
            listing_Standard.Label("fulfill_needs_head".Translate());
            listing_Standard.CheckboxLabeled("fulfill_outdoors_label".Translate(), ref fulfill_outdoors, "fulfill_outdoors_note".Translate());
            listing_Standard.CheckboxLabeled("drugs_use_potential_mood_label".Translate(), ref drugs_use_potential_mood, "drugs_use_potential_mood_note".Translate());
            listing_Standard.CheckboxLabeled("pick_proper_amount_label".Translate(), ref pick_proper_amount, "pick_proper_amount_note".Translate());
            listing_Standard.CheckboxLabeled("fun_police_label".Translate(), ref fun_police, "fun_police_note".Translate());

            listing_Standard.GapLine();
            listing_Standard.Label("clean_head".Translate());
            listing_Standard.CheckboxLabeled("clean_before_working_label".Translate(), ref clean_before_work, "clean_before_working_note".Translate());
            listing_Standard.CheckboxLabeled("add_to_que_label".Translate(), ref add_to_que, "add_to_que_note".Translate());
            listing_Standard.CheckboxLabeled("clean_after_tending_label".Translate(), ref clean_after_tanding, "clean_after_tending_note".Translate());
            listing_Standard.CheckboxLabeled("hauling_over_bills_label".Translate(), ref hauling_over_bills, "hauling_over_bills_note".Translate());
            listing_Standard.CheckboxLabeled("prefer_spoiling_ingredients_label".Translate(), ref prefer_spoiling_ingredients, "prefer_spoiling_ingredients_note".Translate());
            listing_Standard.CheckboxLabeled("put_back_to_inv_label".Translate(), ref put_back_to_inv, "put_back_to_inv_note".Translate());

            listing_Standard.GapLine();
            listing_Standard.Label("meal_stacking_head".Translate());
            listing_Standard.CheckboxLabeled("meal_stacking_label".Translate(), ref separate_meals, "meal_stacking_note".Translate());
            listing_Standard.CheckboxLabeled("dont_count_odd_label".Translate(), ref odd_is_normal, "dont_count_odd_note".Translate());


            listing_Standard.GapLine();
            listing_Standard.Label("advanced_head".Translate());
            listing_Standard.CheckboxLabeled("advanced_inbetween_cleaning_label".Translate(), ref adv_cleaning, "advanced_inbetween_cleaning_note".Translate());
            listing_Standard.CheckboxLabeled("advanced_haul_all_ings_label".Translate(), ref adv_haul_all_ings, "advanced_haul_all_ings_note".Translate());

            listing_Standard.GapLine();
            listing_Standard.Label("miscellaneous_head".Translate());
            listing_Standard.CheckboxLabeled("gen_ingredients_label".Translate(), ref add_meal_ingredients, "gen_ingredients_note".Translate());
            listing_Standard.CheckboxLabeled("extended_recipe_label".Translate(), ref gui_extended_recipe, "extended_recipe_note".Translate());
            listing_Standard.CheckboxLabeled("manual_unload_label".Translate(), ref gui_manual_unload, "manual_unload_note".Translate());
            listing_Standard.End();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref separate_meals, "separate_meals", true, false);
            Scribe_Values.Look(ref fulfill_outdoors, "fulfill_outdoors", true, false);
            Scribe_Values.Look(ref odd_is_normal, "odd_is_normal", false, false);
            Scribe_Values.Look(ref clean_before_work, "clean_before_work", true, false);
            Scribe_Values.Look(ref clean_after_tanding, "clean_after_tanding", true, false);
            Scribe_Values.Look(ref calculate_full_path, "calculate_full_path", true, false);
            Scribe_Values.Look(ref add_meal_ingredients, "add_meal_ingredients", false, false);
            Scribe_Values.Look(ref drugs_use_potential_mood, "drugs_use_potential_mood", true, false);
            Scribe_Values.Look(ref adv_cleaning, "adv_cleaning", true, false);
            Scribe_Values.Look(ref adv_haul_all_ings, "adv_haul_all_ings", true, false);
            Scribe_Values.Look(ref gui_extended_recipe, "extended_recipe", true, false);
            Scribe_Values.Look(ref prefer_spoiling_ingredients, "prefer_spoiling_ingredients", true, false);
            Scribe_Values.Look(ref gui_manual_unload, "gui_manual_unload", true, false);
            Scribe_Values.Look(ref put_back_to_inv, "put_back_to_inv", true, false);
            Scribe_Values.Look(ref pick_proper_amount, "pick_proper_amount", true, false);
            Scribe_Values.Look(ref fun_police, "fun_police", true, false);
        }
    }
}
