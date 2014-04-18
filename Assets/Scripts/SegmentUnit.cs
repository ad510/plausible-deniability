// Copyright (c) 2013-2014 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// identifies a unit on a path segment
/// </summary>
/// <remarks>this is a struct instead of a class because it compares by value</remarks>
public struct SegmentUnit {
	public readonly Sim g;
	public readonly Segment segment;
	public readonly Unit unit;
	
	public SegmentUnit(Segment segmentVal, Unit unitVal) {
		segment = segmentVal;
		unit = unitVal;
		g = segment.g;
	}
	
	public SegmentUnit(SegmentUnit segmentUnit) {
		this = segmentUnit;
	}

	/// <summary>
	/// removes unit from segment if doing so wouldn't affect anything that another player saw, returns whether successful
	/// </summary>
	public bool delete(bool addMoveLines = false) {
		HashSet<SegmentUnit> dependencies = new HashSet<SegmentUnit>();
		Dictionary<Segment, List<Unit>> removed = new Dictionary<Segment, List<Unit>>();
		long timeEarliestChild = long.MaxValue;
		// find all SegmentUnits that need to be removed along with this one
		if (!deleteDependencies(dependencies, ref timeEarliestChild)) return false;
		// remove unit and its dependencies
		foreach (SegmentUnit dependency in dependencies) {
			dependency.segment.units.Remove(dependency.unit);
			if (!removed.ContainsKey (dependency.segment)) removed.Add (dependency.segment, new List<Unit>());
			removed[dependency.segment].Add (dependency.unit);
		}
		// obsolesce player's list of unit combinations
		List<HashSet<SegmentUnit>> oldUnitCombinations = unit.player.unitCombinations;
		unit.player.unitCombinations = null;
		// if removing unit might have led to player having negative resources,
		// add units back to segments they were removed from
		if (timeEarliestChild != long.MaxValue && segment.path.player.checkNegRsc (timeEarliestChild, false) >= 0) {
			foreach (KeyValuePair<Segment, List<Unit>> item in removed) {
				item.Key.units.AddRange (item.Value);
			}
			unit.player.unitCombinations = oldUnitCombinations;
			return false;
		}
		foreach (KeyValuePair<Segment, List<Unit>> item in removed) {
			// remove paths that no longer contain units from visibility tiles
			if (item.Key.id == item.Key.path.segments.Count - 1 && item.Key.units.Count == 0 && item.Key.path.tileX != Sim.OffMap) {
				g.events.add(new TileMoveEvt(g.timeSim, item.Key.path.id, Sim.OffMap, 0));
			}
			// add deleted units to list
			if (item.Key.timeStart < g.timeSim) {
				if (item.Key.nextOnPath () == null) item.Key.path.insertSegment (g.timeSim);
				item.Key.deletedUnits.AddRange (item.Value);
			}
		}
		if (addMoveLines) {
			// add deleted unit lines
			// TODO: tweak time if deleted before timeSimPast
			MoveLine deleteLine = new MoveLine(Math.Min (segment.path.timeSimPast, g.timeSim), unit.player);
			foreach (Segment seg in removed.Keys) {
				deleteLine.vertices.AddRange (seg.path.moveLines (seg.timeStart,
					(seg.nextOnPath () == null || seg.nextOnPath ().timeStart > deleteLine.time) ? deleteLine.time : seg.nextOnPath ().timeStart));
			}
			g.deleteLines.Add (deleteLine);
		}
		return true;
	}

	/// <summary>
	/// sets dependencies to all SegmentUnits that would need to be deleted in order to safely delete this SegmentUnit,
	/// returns whether this is possible (does not check for possibility of negative resources)
	/// </summary>
	public bool deleteDependencies(HashSet<SegmentUnit> dependencies, ref long timeEarliestChild) {
		if (!segment.units.Contains (unit)) return true; // if this segment already doesn't contain this unit, return true
		List<SegmentUnit> ancestors = new List<SegmentUnit> { this };
		// find all ancestor segments to start removal from
		for (int i = 0; i < ancestors.Count; i++) {
			if (ancestors[i].prev ().Any ()) {
				// if this ancestor has a sibling segment that we're not currently planning to remove unit from,
				// don't remove unit from previous segments shared by both
				bool hasSibling = false;
				foreach (Segment seg in ancestors[i].segment.branches) {
					if (seg.units.Contains (unit) && !ancestors.Contains (new SegmentUnit(seg, unit))
						&& (seg.path.timeSimPast == long.MaxValue || ancestors[i].segment.path.timeSimPast != long.MaxValue)) {
						hasSibling = true;
						break;
					}
				}
				if (!hasSibling) {
					// indicate to remove unit from previous segments
					ancestors.AddRange (ancestors[i].prev ());
					ancestors.RemoveAt(i);
					i--;
				}
			}
			else if (ancestors[i].segment.prev ().Any ()) {
				// unit has a parent but we're deleting its first segment, so may need to check resources starting at this time
				if (ancestors[i].unit.attacks.Count > 0) return false;
				if (ancestors[i].segment.timeStart < timeEarliestChild && ancestors[i].unit.type.rscCollectRate.Where (r => r > 0).Any ()) {
					timeEarliestChild = ancestors[i].segment.timeStart;
				}
			}
			else {
				// reached a segment with no previous segment whatsoever, so return false (we assume other players know the scenario's starting state)
				return false;
			}
		}
		// find later dependencies recursively, starting at the ancestor segments we found
		for (int i = 0; i < ancestors.Count; i++) {
			if (!ancestors[i].deleteDependenciesAfter (dependencies, ref timeEarliestChild)) return false;
		}
		return true;
	}
	
	private bool deleteDependenciesAfter(HashSet<SegmentUnit> dependencies, ref long timeEarliestChild) {
		if (segment.units.Contains (unit) && !dependencies.Contains(this)) {
			if (!segment.unseen && segment.timeStart < g.timeSim) return false;
			// only remove units from next segments if this is their only previous segment
			if (segment.nextOnPath () == null || new SegmentUnit(segment.nextOnPath (), unit).prev ().Where(u => !dependencies.Contains(u)).Count () == 1) {
				// remove unit from next segments
				foreach (SegmentUnit segmentUnit in next ()) {
					if (!segmentUnit.deleteDependenciesAfter (dependencies, ref timeEarliestChild)) return false;
				}
				// remove child units that only this unit could have made
				foreach (SegmentUnit child in children ()) {
					// TODO: if has alternate non-live parent, do we need to recursively make children non-live?
					if (child.parents ().Where(u => !dependencies.Contains(u)).Count () == 1) {
						if (child.unit.attacks.Count > 0) return false;
						if (!child.deleteDependenciesAfter (dependencies, ref timeEarliestChild)) return false;
						if (child.segment.timeStart < timeEarliestChild && child.unit.type.rscCollectRate.Where (r => r > 0).Any ()) {
							timeEarliestChild = child.segment.timeStart;
						}
					}
				}
			}
			// add this SegmentUnit to dependencies
			dependencies.Add(this);
		}
		return true;
	}
	
	public bool unseenAfter(long time) {
		if (!segment.unseen || (unit.attacks.Count > 0 && time < unit.attacks.Last().time)) return false;
		foreach (SegmentUnit segmentUnit in next ()) {
			if (!segmentUnit.unseenAfter (time)) return false;
		}
		foreach (SegmentUnit child in children ()) {
			if (!child.unseenAfter (time)) return false;
		}
		return true;
	}

	/// <summary>
	/// returns every combination of units that this unit could have made
	/// </summary>
	public List<HashSet<SegmentUnit>> allChildren() {
		List<HashSet<SegmentUnit>> ret = new List<HashSet<SegmentUnit>>();
		// add children in each combination of next segments
		foreach (SegmentUnit segmentUnit in next ()) {
			foreach (HashSet<SegmentUnit> nextCombination in segmentUnit.allChildren ()) {
				if (ret.Find (x => x.SetEquals (nextCombination)) == null) ret.Add (nextCombination);
			}
		}
		// if this is last segment, add empty set of children
		if (ret.Count == 0) ret.Add (new HashSet<SegmentUnit>());
		// add children in this segment
		foreach (SegmentUnit child in children ()) {
			List<HashSet<SegmentUnit>> childChildren = child.allChildren ();
			// add child to each combination so far
			foreach (HashSet<SegmentUnit> combination in ret) {
				combination.Add (child);
			}
			if (childChildren.Count != 1 || childChildren[0].Count != 0) {
				// add each child unit combination to each combination so far
				List<HashSet<SegmentUnit>> newRet = new List<HashSet<SegmentUnit>>();
				foreach (HashSet<SegmentUnit> childCombination in childChildren) {
					foreach (HashSet<SegmentUnit> combination in ret) {
						HashSet<SegmentUnit> newCombination = new HashSet<SegmentUnit>(combination);
						newCombination.UnionWith (childCombination);
						if (newRet.Find (x => x.SetEquals (newCombination)) == null) newRet.Add (newCombination);
					}
				}
				ret = newRet;
			}
		}
		return ret;
	}
	
	/// <summary>
	/// returns whether this unit is in the same path before specified time,
	/// so if it makes a child unit, it's unambiguous who is the parent
	/// </summary>
	public bool canBeUnambiguousParent(long time) {
		return segment.timeStart < time || (segment.prevOnPath () != null && segment.prevOnPath ().units.Contains (unit));
	}
	
	/// <summary>
	/// iterates over all segment/unit pairs that could have made this unit in this segment
	/// </summary>
	public IEnumerable<SegmentUnit> parents() {
		if (!prev ().Any ()) {
			foreach (Segment seg in segment.prev ()) {
				foreach (SegmentUnit segmentUnit in seg.segmentUnits ()) {
					if (segmentUnit.unit.type.canMake[unit.type.id]) {
						yield return segmentUnit;
					}
				}
			}
		}
	}
	
	/// <summary>
	/// iterates over all segment/unit pairs that this unit in this segment could have made
	/// </summary>
	public IEnumerable<SegmentUnit> children() {
		foreach (Segment seg in segment.next ()) {
			foreach (SegmentUnit segmentUnit in seg.segmentUnits ()) {
				if (unit.type.canMake[segmentUnit.unit.type.id] && !segmentUnit.prev ().Any ()) {
					yield return segmentUnit;
				}
			}
		}
	}
	
	/// <summary>
	/// iterates over all segments containing this unit that merge onto the beginning of this segment
	/// </summary>
	public IEnumerable<SegmentUnit> prev() {
		foreach (Segment seg in segment.prev()) {
			if (seg.units.Contains (unit)) yield return new SegmentUnit(seg, unit);
		}
	}
	
	/// <summary>
	/// iterates over all segments containing this unit that branch off from the end of this segment
	/// </summary>
	public IEnumerable<SegmentUnit> next() {
		foreach (Segment seg in segment.next()) {
			if (seg.units.Contains (unit)) yield return new SegmentUnit(seg, unit);
		}
	}
}
