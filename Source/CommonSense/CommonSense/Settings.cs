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

        public static void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing_Standard = new Listing_Standard();
            listing_Standard.Begin(inRect);
            listing_Standard.CheckboxLabeled("Pawns are encouraged to fulfill their need of outdoors by seeking outdoors (unroofed) activities", ref fulfill_outdoors, "Too bad, but it doesn't mean 'any', only those, that marked as 'unroofed'.");
            listing_Standard.CheckboxLabeled("Separate meals with negative thoughts about them", ref separate_meals);
            listing_Standard.CheckboxLabeled("Count odd meat (ex. insect) as normal, allowing to stack it with normal meals", ref odd_is_normal);

            listing_Standard.End();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref separate_meals, "separate_meals", true, false);
            Scribe_Values.Look<bool>(ref fulfill_outdoors, "fulfill_outdoors", true, false);
            Scribe_Values.Look<bool>(ref odd_is_normal, "odd_is_normal", false, false);
        }
    }
}
