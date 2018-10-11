//using System;
//using System.Reflection;
//using RimWorld;
//using Harmony;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using Verse.AI;
using Verse;
using UnityEngine;


namespace ClearTheStockpiles
{

    class CTS_Loader : Mod
    {
        public CTS_Loader(ModContentPack content) : base(content)
        {
            settings = GetSettings<CTS_Settings>();
        }

        public static CTS_Settings settings;

        public override string SettingsCategory() => Content.Name;

        public override void DoSettingsWindowContents(Rect inRect)
        {
            string numInputBuffer1 = settings.radiusToSearch.ToString();
            //string numInputBuffer2 = settings.numMaximumRoll.ToString();
            const string LabelRadiusSearchStockpiles = "CTS_LookRadiusLabel";


            Listing_Standard listing_Standard = new Listing_Standard()
            {
                ColumnWidth = inRect.width / 3
            };

            listing_Standard.Begin(inRect);


            listing_Standard.Label(LabelRadiusSearchStockpiles.Translate(), -1);

            listing_Standard.TextFieldNumeric(ref settings.radiusToSearch, ref numInputBuffer1, 1, 25);


            //listing_Standard.Gap(12f);

            listing_Standard.End();
        }

        public class CTS_Settings : ModSettings
        {
            public int radiusToSearch = 18;

            //public int numMinimumRoll = 1;

            //public int numMaximumRoll = 20;

            public override void ExposeData()
            {
                Scribe_Values.Look(ref radiusToSearch, "val_RadiusToSearch", 18, true);

                //Scribe_Values.Look(ref numMinimumRoll, "val_MinimumRoll", 1, true);
                //Scribe_Values.Look(ref numMaximumRoll, "val_MaximumRoll", 20, true);
            }



        }


    }//End of CTS_Loader class


}
