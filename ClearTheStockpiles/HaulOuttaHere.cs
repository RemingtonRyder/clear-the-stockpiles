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
				candidates.Sort((IntVec3 a, IntVec3 b) => a.DistanceToSquared(center).CompareTo(b.DistanceToSquared(center)));
				IntVec3 intVec;
				for (int i = 0; i < candidates.Count; i++)
				{
					intVec = candidates[i];
					if (HaulablePlaceValidator(haulable, worker, intVec))
					{
						foundCell = intVec;
						return true;
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

		private static bool HaulablePlaceValidator(Thing haulable, Pawn worker, IntVec3 c)
		{
			if (!worker.CanReserveAndReach(c, PathEndMode.OnCell, worker.NormalMaxDanger()))
			{
				return false;
			}
			if (GenPlace.HaulPlaceBlockerIn(haulable, c, worker.Map, true) != null)
			{
				return false;
			}
			var thisIsAPile = c.GetSlotGroup(worker.Map);

			if (thisIsAPile != null)
			{
				if (!thisIsAPile.Settings.AllowedToAccept(haulable))
				{
					return false;
				}
			}

			if (!c.Standable(worker.Map))
			{
				return false;
			}
			if (c == haulable.Position && haulable.Spawned)
			{
				return false;
			}
            if (c.ContainsStaticFire(worker.Map))
            {
                return false;
            }
            if (haulable != null && haulable.def.BlockPlanting)
			{
				Zone zone = worker.Map.zoneManager.ZoneAt(c);
				if (zone is Zone_Growing)
				{
					return false;
				}
			}
			if (haulable.def.passability != Traversability.Standable)
			{
				for (int i = 0; i < 8; i++)
				{
					IntVec3 c2 = c + GenAdj.AdjacentCells[i];
					if (worker.Map.designationManager.DesignationAt(c2, DesignationDefOf.Mine) != null)
					{
						return false;
					}
				}
			}

            bool validPositionExists = false;

            var crossGrid = GenAdj.CardinalDirectionsAndInside;
			for (int a = 0; a < crossGrid.CountAllowNull(); a++)
			{
                
				IntVec3 adjCell = c + crossGrid[a];
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

			}

            if (!validPositionExists) return false;

			Building edifice = c.GetEdifice(worker.Map);
			if (edifice != null)
			{
                if (edifice is Building_Trap)
                {
                    return false;
                }

                if (edifice is Building_WorkTable)
                {
                    return false;
                }

            }


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
