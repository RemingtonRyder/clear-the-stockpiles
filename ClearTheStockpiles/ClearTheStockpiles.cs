using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;

namespace ClearTheStockpiles
{
	public class WorkGiver_ClearStockpile : WorkGiver_Haul
	{
		public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn Pawn)
		{
			var stuffToHaul = Pawn.Map.listerHaulables.ThingsPotentiallyNeedingHauling();

			// For this particular WorkGiver, we want to eliminate anything which
			// is not currently in storage or is in a storage which allows it.

			var stuffToHaulAside = new List<Thing>();

			foreach (Thing h in stuffToHaul)
			{
				if (h.IsInAnyStorage() && !h.IsInValidStorage())
				{
					stuffToHaulAside.Add(h);
				}
			}

			return stuffToHaulAside;
		}

		public override bool ShouldSkip(Pawn pawn, bool forced=false)
		{
			return pawn.Map.listerHaulables.ThingsPotentiallyNeedingHauling().Count == 0;
		}

		public override Job JobOnThing(Pawn pawn, Thing t, bool forced)
		{
			if (!HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, t, forced))
			{
				return null;
			}

			return HaulOuttaHere.HaulOuttaHereJobFor(pawn, t);
		}


	}//End Class

}//End Namespace
