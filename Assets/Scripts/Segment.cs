// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// describes the composition of a section of a path
/// </summary>
public class Segment {
	public readonly Sim g;
	public readonly Path path;
	public int id; // index in segment list
	public long timeStart;
	/// <summary>
	/// segments that branch off at the beginning of this segment;
	/// connected branches share the same List instance so updating this list in one segment updates it for all branches
	/// (NOTE: protobuf-net won't like that)
	/// </summary>
	public List<Segment> branches;
	public List<int> units; // indices of units on this path segment
	public bool unseen; // whether path segment is known to not be seen by another player
	
	public Segment(Path pathVal, int idVal, long timeStartVal, List<int> unitsVal, bool unseenVal) {
		path = pathVal;
		g = path.g;
		id = idVal;
		timeStart = timeStartVal;
		branches = new List<Segment>();
		branches.Add (this);
		units = unitsVal;
		unseen = unseenVal;
	}
	
	/// <summary>
	/// removes all units if doing so wouldn't affect anything that another player saw
	/// </summary>
	public void removeAllUnits() {
		while (units.Count > 0) {
			if (!removeUnit(g.units[units.Last ()])) throw new SystemException("failed to remove a unit from segment");
		}
	}

	/// <summary>
	/// removes specified unit if doing so wouldn't affect anything that another player saw, returns whether successful
	/// </summary>
	public bool removeUnit(Unit unit) {
		if (!units.Contains (unit.id)) return true; // if this segment already doesn't contain specified unit, return true
		List<Segment> ancestors = new List<Segment>();
		Dictionary<Segment, List<int>> removed = new Dictionary<Segment, List<int>>();
		long timeEarliestChild = long.MaxValue;
		int i;
		ancestors.Add (this);
		// find all ancestor segments to start removal from
		for (i = 0; i < ancestors.Count; i++) {
			if (ancestors[i].prev (unit).Any ()) {
				// if this ancestor has a sibling segment that we're not currently planning to remove unit from,
				// don't remove unit from previous segments shared by both
				bool hasSibling = false;
				foreach (Segment segment in ancestors[i].branches) {
					if (segment.units.Contains (unit.id) && !ancestors.Contains (segment)
						&& (segment.path.timeSimPast == long.MaxValue || ancestors[i].path.timeSimPast != long.MaxValue)) {
						hasSibling = true;
						break;
					}
				}
				if (!hasSibling) {
					// indicate to remove unit from previous segments
					ancestors.AddRange (ancestors[i].prev (unit));
					ancestors.RemoveAt(i);
					i--;
				}
			}
			else if (ancestors[i].prev ().Any ()) {
				// unit has a parent but we're deleting its first segment, so may need to check resources starting at this time
				timeEarliestChild = Math.Min (timeEarliestChild, ancestors[i].timeStart);
			}
			else {
				// reached a segment with no previous segment whatsoever, so return false (we assume other players know the scenario's starting state)
				return false;
			}
		}
		// remove unit recursively, starting at the ancestor segments we found
		for (i = 0; i < ancestors.Count; i++) {
			if (!ancestors[i].removeUnitAfter (unit, ref removed, ref timeEarliestChild)) break;
		}
		// if a removeUnitAfter() call failed or removing unit might have led to player having negative resources,
		// add units back to segments they were removed from
		if (i < ancestors.Count || (timeEarliestChild != long.MaxValue && path.player.checkNegRsc (timeEarliestChild, false) >= 0)) {
			foreach (KeyValuePair<Segment, List<int>> item in removed) {
				item.Key.units.AddRange (item.Value);
			}
			return false;
		}
		// remove paths that no longer contain units from visibility tiles
		foreach (Segment segment in removed.Keys) {
			if (segment.id == segment.path.segments.Count - 1 && segment.units.Count == 0 && segment.path.tileX != Sim.OffMap) {
				g.events.add(new TileMoveEvt(g.timeSim, segment.path.id, Sim.OffMap, 0));
			}
		}
		return true;
	}
	
	private bool removeUnitAfter(Unit unit, ref Dictionary<Segment, List<int>> removed, ref long timeEarliestChild) {
		if (units.Contains (unit.id)) {
			if (!unseen && timeStart < g.timeSim) return false;
			// only remove units from next segments if this is their only previous segment
			if (nextOnPath () == null || nextOnPath ().prev (unit).Count () == 1) {
				// remove unit from next segments
				foreach (Segment segment in next (unit)) {
					if (!segment.removeUnitAfter (unit, ref removed, ref timeEarliestChild)) return false;
				}
				// remove child units that only this unit could have made
				foreach (KeyValuePair<Segment, Unit> child in children (unit).ToArray ()) {
					// TODO: if has alternate non-live parent, do we need to recursively make children non-live?
					if (child.Key.parents (child.Value).Count () == 1) {
						if (!child.Key.removeUnitAfter (child.Value, ref removed, ref timeEarliestChild)) return false;
						timeEarliestChild = Math.Min (timeEarliestChild, child.Key.timeStart);
					}
				}
			}
			// remove unit from this segment
			units.Remove (unit.id);
			if (!removed.ContainsKey (this)) removed.Add (this, new List<int>());
			removed[this].Add (unit.id);
		}
		return true;
	}
	
	public bool unseenAfter(Unit unit) {
		if (!units.Contains (unit.id)) throw new ArgumentException("segment does not contain specified unit");
		if (!unseen) return false;
		foreach (Segment segment in next (unit)) {
			if (!segment.unseenAfter (unit)) return false;
		}
		foreach (KeyValuePair<Segment, Unit> child in children (unit)) {
			if (!child.Key.unseenAfter (child.Value)) return false;
		}
		return true;
	}

	/// <summary>
	/// returns resource amount gained by specified unit and its children (subtracting cost to make children)
	/// from this segment's start time to specified time
	/// </summary>
	/// <param name="max">
	/// since different paths can have collected different resource amounts,
	/// determines whether to use paths that collected least or most resources in calculation
	/// </param>
	// TODO: this currently double-counts child paths/units if paths merge, fix this before enabling stacking
	public long rscCollected(long time, Unit unit, int rscType, bool max, bool includeNonLiveChildren) {
		// if this segment wasn't active yet, unit can't have collected anything
		if (time < timeStart) return 0;
		// if next segment wasn't active yet, return resources collected from timeStart to time
		if (nextOnPath () == null || time < nextOnPath ().timeStart) {
			return unit.type.rscCollectRate[rscType] * (time - timeStart);
		}
		long ret = 0;
		bool foundNextSeg = false;
		// add resources gained in next segment that collected either least or most resources (depending on max parameter)
		foreach (Segment segment in next (unit)) {
			if (includeNonLiveChildren || segment.path.timeSimPast == long.MaxValue) {
				long segCollected = segment.rscCollected (time, unit, rscType, max, includeNonLiveChildren);
				if (!foundNextSeg || (max ^ (segCollected < ret))) {
					ret = segCollected;
					foundNextSeg = true;
				}
			}
		}
		// add resources gained by children
		foreach (KeyValuePair<Segment, Unit> child in children (unit)) {
			ret += child.Key.rscCollected (time, child.Value, rscType, max, includeNonLiveChildren);
			// subtract cost to make child unit
			ret -= child.Value.type.rscCost[rscType];
		}
		// add resources collected on this segment
		ret += unit.type.rscCollectRate[rscType] * (nextOnPath ().timeStart - timeStart);
		return ret;
	}
	
	/// <summary>
	/// iterates over all segment/unit pairs that could have made specified unit in this segment
	/// </summary>
	public IEnumerable<KeyValuePair<Segment, Unit>> parents(Unit unit) {
		if (!prev (unit).Any ()) {
			foreach (Segment segment in prev ()) {
				foreach (int unit2 in segment.units) {
					if (g.units[unit2].type.canMake[unit.type.id]) {
						yield return new KeyValuePair<Segment, Unit>(segment, g.units[unit2]);
					}
				}
			}
		}
	}
	
	/// <summary>
	/// iterates over all segment/unit pairs that specified unit in this segment could have made
	/// </summary>
	public IEnumerable<KeyValuePair<Segment, Unit>> children(Unit unit) {
		foreach (Segment segment in next ()) {
			foreach (int unit2 in segment.units) {
				if (unit.type.canMake[g.units[unit2].type.id] && !segment.prev (g.units[unit2]).Any ()) {
					yield return new KeyValuePair<Segment, Unit>(segment, g.units[unit2]);
				}
			}
		}
	}
	
	/// <summary>
	/// iterates over all segments containing the specified unit that merge onto the beginning of this segment
	/// </summary>
	public IEnumerable<Segment> prev(Unit unit) {
		foreach (Segment segment in prev()) {
			if (segment.units.Contains (unit.id)) yield return segment;
		}
	}
	
	/// <summary>
	/// iterates over all segments containing the specified unit that branch off from the end of this segment
	/// </summary>
	public IEnumerable<Segment> next(Unit unit) {
		foreach (Segment segment in next()) {
			if (segment.units.Contains (unit.id)) yield return segment;
		}
	}
	
	/// <summary>
	/// iterates over all segments that merge onto the beginning of this segment
	/// </summary>
	public IEnumerable<Segment> prev() {
		foreach (Segment segment in branches) {
			if (segment.prevOnPath() != null) yield return segment.prevOnPath();
		}
	}
	
	/// <summary>
	/// iterates over all segments that branch off from the end of this segment
	/// </summary>
	public IEnumerable<Segment> next() {
		if (nextOnPath() != null) {
			foreach (Segment segment in nextOnPath().branches) {
				yield return segment;
			}
		}
	}
	
	/// <summary>
	/// returns previous segment on this path, or null if this is the first segment
	/// </summary>
	public Segment prevOnPath() {
		if (id == 0) return null;
		return path.segments[id - 1];
	}
	
	/// <summary>
	/// returns next segment on this path, or null if this is the last segment
	/// </summary>
	public Segment nextOnPath() {
		if (id == path.segments.Count - 1) return null;
		return path.segments[id + 1];
	}
}
