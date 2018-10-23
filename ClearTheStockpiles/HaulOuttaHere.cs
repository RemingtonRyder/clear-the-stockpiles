using RimWorld;
//using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;


namespace ClearTheStockpiles
{
	public static class HaulOuttaHere
	{
		private static List<IntVec3> candidates = new List<IntVec3>();
		private const int cellsToSearch = 100;
		//private const int radiusToSearchStockpiles = 18;

		public static bool CanHaulOuttaHere(Pawn p, Thing t, out IntVec3 storeCell)
		{
			storeCell = IntVec3.Invalid;
			bool couldHaul = t.def.EverHaulable
							  && !t.IsBurning()
							  && p.CanReserveAndReach(t, PathEndMode.ClosestTouch, p.NormalMaxDanger());

			if (!couldHaul)
			{
				return false;
			}

			bool canStoreNearby = TryFindBetterStoreCellInRange(t, p, p.Map,
			                                                    CTS_Loader.settings.radiusToSearch,
			                                                    StoragePriority.Unstored,
			                                                    p.Faction, out storeCell, true);
			
			if (!canStoreNearby)
			{

				return TryFindSpotToPlaceHaulableCloseTo(t, p, t.PositionHeld, out storeCell);
			}
			return true;
		}

		public static Job HaulOuttaHereJobFor(Pawn p, Thing t)
		{
            if (!CanHaulOuttaHere(p, t, out IntVec3 c))
            {
                if (CTS_Loader.settings.debug) JobFailReason.Is("Can't clear: No place to clear to.");
                return null;
            }

            var requiredHaulMode = HaulMode.ToCellNonStorage;

            if (c.GetSlotGroup(p.Map) != null) requiredHaulMode = HaulMode.ToCellStorage;

            return new Job(JobDefOf.HaulToCell, t, c)
			{
				count = 99999,
				haulOpportunisticDuplicates = false,
				haulMode = requiredHaulMode,
				ignoreDesignations = true
			};
		}

		private static bool TryFindSpotToPlaceHaulableCloseTo(Thing haulable, Pawn worker, IntVec3 center,
		                                                      out IntVec3 spot)
		{
            List<string> debugMessages = new List<string>();

			Region region = center.GetRegion(worker.Map);
			if (region == null)
			{
				spot = center;
				return false;
			}
			TraverseParms traverseParms = TraverseParms.For(worker, Danger.Deadly, TraverseMode.ByPawn, false);
			IntVec3 foundCell = IntVec3.Invalid;
			RegionTraverser.BreadthFirstTraverse(region, (Region from, Region r) => r.Allows(traverseParms, false),
			                                     delegate (Region r)
            {
                candidates.Clear();
                candidates.AddRange(r.Cells);

                // This function helps to identify cells which are part of the haulable's current stockpile
                // (which it is not supposed to be in.
                bool currentStockpile(IntVec3 slot) =>
                  worker.Map.haulDestinationManager.SlotGroupAt(haulable.Position).CellsList.Contains(slot);

                // We remove cells which we could never haul this thing to.
                candidates.RemoveAll(currentStockpile);

                candidates.Sort((IntVec3 a, IntVec3 b) => a.DistanceToSquared(center).CompareTo(b.DistanceToSquared(center)));
                IntVec3 intVec;
                for (int i = 0; i < candidates.Count; i++)
                {
                    intVec = candidates[i];
                    if (HaulablePlaceValidator(haulable, worker, intVec, out string debugMsg))
                    {

                        foundCell = intVec;

                        if (CTS_Loader.settings.debug)
                        {
                            debugMessages.Add(debugMsg);
                        }

                        //Debugging output.
                        if (debugMessages.Count != 0)
                        {
                            foreach (string s in debugMessages)
                            {
                                Log.Message(s);
                            }
                        }
                        return true;
                    }
                    else if (CTS_Loader.settings.debug)
                    {
                        debugMessages.Add(debugMsg);
                    }


                }

                //Debugging output.
                if (debugMessages.Count != 0)
                {
                    foreach (string s in debugMessages)
                    {
                        Log.Message(s);
                    }
                }
                return false;
			}, cellsToSearch);

			if (foundCell.IsValid)
			{
				spot = foundCell;
				return true;
			}
			spot = center;
			return false;
		}

		private static bool HaulablePlaceValidator(Thing haulable, Pawn worker, IntVec3 c, out string debugText)
		{
			if (!worker.CanReserveAndReach(c, PathEndMode.OnCell, worker.NormalMaxDanger()))
			{
                debugText = "Could not reserve or reach";
				return false;
			}
			if (GenPlace.HaulPlaceBlockerIn(haulable, c, worker.Map, true) != null)
			{
                debugText = "Place was blocked";
				return false;
			}
			var thisIsAPile = c.GetSlotGroup(worker.Map);

			if (thisIsAPile != null)
			{
				if (!thisIsAPile.Settings.AllowedToAccept(haulable))
				{
                    debugText = "Stockpile does not accept";
					return false;
				}
			}

			if (!c.Standable(worker.Map))
			{
                debugText = "Cell not standable";
				return false;
			}
			if (c == haulable.Position && haulable.Spawned)
			{
                debugText = "Current position of thing to be hauled";
				return false;
			}
            if (c.ContainsStaticFire(worker.Map))
            {
                debugText = "Cell has fire";
                return false;
            }
            if (haulable != null && haulable.def.BlockPlanting)
			{
				Zone zone = worker.Map.zoneManager.ZoneAt(c);
				if (zone is Zone_Growing)
				{
                    debugText = "Growing zone here";
					return false;
				}
			}
			if (haulable.def.passability != Traversability.Standable)
			{
				for (int i = 0; i < 8; i++)
				{
					IntVec3 adjCell = c + GenAdj.AdjacentCells[i];

                    if (!adjCell.InBounds(worker.Map)) continue;

					if (worker.Map.designationManager.DesignationAt(adjCell, DesignationDefOf.Mine) != null)
					{
                        debugText = "Mining designated nearby";
						return false;
					}
				}
			}

            bool validPositionExists = false;

            var crossGrid = GenAdj.CardinalDirectionsAndInside;
			for (int a = 0; a < crossGrid.CountAllowNull(); a++)
			{
                
				IntVec3 adjCell = c + crossGrid[a];

                if (!adjCell.InBounds(worker.Map)) continue;

                Building restrictedBuildingAdj = adjCell.GetEdifice(worker.Map);
				if (restrictedBuildingAdj != null)
				{
                    if (restrictedBuildingAdj is Building_Door)
                    {
                        break;
                    }
                    if (restrictedBuildingAdj is Building_WorkTable)
                    {
                        thisIsAPile = adjCell.GetSlotGroup(worker.Map);

                        if (thisIsAPile != null)
                        {
                            if (thisIsAPile.Settings.AllowedToAccept(haulable))
                            {
                                validPositionExists = true;
                            }
                        }


                        
                    }

                }
                else
                {
                    validPositionExists = true;
                }

			}

            if (!validPositionExists)
            {
                debugText = "No valid position could be found.";
                return false;
            }

			Building edifice = c.GetEdifice(worker.Map);
			if (edifice != null)
			{
                if (edifice is Building_Trap)
                {
                    debugText = "It's a trap.";
                    return false;
                }

                if (edifice is Building_WorkTable)
                {
                    debugText = "Worktable here.";
                    return false;
                }

            }

            debugText = "OK";
			return true;
		}





		public static bool TryFindBetterStoreCellInRange(Thing t, Pawn carrier, Map map, int range, StoragePriority currentPriority, Faction faction, out IntVec3 foundCell, bool needAccurateResult = true)
		{
			List<SlotGroup> allGroupsListInPriorityOrder = map.haulDestinationManager.AllGroupsListInPriorityOrder;
			if (allGroupsListInPriorityOrder.Count == 0)
			{
				foundCell = IntVec3.Invalid;
				return false;
			}
			IntVec3 a = (t.MapHeld == null) ? carrier.PositionHeld : t.PositionHeld;
			StoragePriority storagePriority = currentPriority;
			float num = Mathf.Pow(range, 2);
			IntVec3 intVec = default(IntVec3);
			bool flag = false;
			int count = allGroupsListInPriorityOrder.Count;
			for (int i = 0; i < count; i++)
			{
				SlotGroup slotGroup = allGroupsListInPriorityOrder[i];
				StoragePriority priority = slotGroup.Settings.Priority;
				if (priority < storagePriority || priority <= currentPriority)
				{
					break;
				}
				if (slotGroup.Settings.AllowedToAccept(t))
				{
					List<IntVec3> cellsList = slotGroup.CellsList;
					int count2 = cellsList.Count;
					int num2;
					if (needAccurateResult)
					{
						num2 = Mathf.FloorToInt(count2 * Rand.Range(0.005f, 0.018f));
					}
					else
					{
						num2 = 0;
					}
					for (int j = 0; j < count2; j++)
					{
						IntVec3 intVec2 = cellsList[j];
						float lengthHorizontalSquared = (a - intVec2).LengthHorizontalSquared;
						if (lengthHorizontalSquared <= num)
						{
							if (StoreUtility.IsGoodStoreCell(intVec2, map, t, carrier, faction))
							{
								flag = true;
								intVec = intVec2;
								num = lengthHorizontalSquared;
								storagePriority = priority;
								if (j >= num2)
								{
									break;
								}
							}
						}
					}
				}
			}
			if (!flag)
			{
				foundCell = IntVec3.Invalid;
				return false;
			}
			foundCell = intVec;
			return true;
		}



	}
}
