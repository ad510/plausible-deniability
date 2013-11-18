// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

// TODO: places with NotImplementedException

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class Path {
	public class Segment {
		private readonly Sim g;
		private readonly Path path;
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
		
		public Segment(Path pathVal, int idVal, long timeVal, List<int> unitsVal, bool unseenVal) {
			path = pathVal;
			g = path.g;
			id = idVal;
			timeStart = timeVal;
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
				if (!removeUnit(units[units.Count - 1])) throw new SystemException("failed to remove a unit from segment");
			}
		}

		/// <summary>
		/// removes specified unit if doing so wouldn't affect anything that another player saw, returns whether successful
		/// </summary>
		public bool removeUnit(int unit) {
			if (!units.Contains (unit)) return true; // if this segment already doesn't contain specified unit, return true
			List<Segment> ancestors = new List<Segment>();
			Dictionary<Segment, List<int>> removed = new Dictionary<Segment, List<int>>();
			long startRemoveTime = long.MaxValue;
			int i;
			ancestors.Add (this);
			// find all ancestor segments to start removal from
			for (i = 0; i < ancestors.Count; i++) {
				List<Segment> ancestorPrev = ancestors[i].prev (unit);
				if (ancestorPrev.Count > 0) {
					// if this ancestor has a sibling segment that we're not currently planning to remove unit from,
					// don't remove unit from previous segments shared by both
					bool hasSibling = false;
					foreach (Segment seg in ancestors[i].branches) {
						if (seg.units.Contains (unit) && !ancestors.Contains (seg)
							&& (seg.path.timeSimPast == long.MaxValue || ancestors[i].path.timeSimPast != long.MaxValue)) {
							hasSibling = true;
							break;
						}
					}
					if (!hasSibling) {
						// indicate to remove unit from previous segments
						ancestors.RemoveAt(i);
						i--;
						ancestors.AddRange (ancestorPrev);
					}
				}
				else if (ancestors[i].prev ().Count == 0) {
					// reached a segment with no previous segment whatsoever, so return false (we assume other players know the scenario's starting state)
					return false;
				}
			}
			// remove unit recursively, starting at the ancestor segments we found
			for (i = 0; i < ancestors.Count; i++) {
				if (!ancestors[i].removeUnitAfter (unit, ref removed)) break;
				startRemoveTime = Math.Min (startRemoveTime, ancestors[i].timeStart);
			}
			// if a removeUnitAfter() call failed or removing unit might have led to player having negative resources,
			// add units back to segments they were removed from
			if (i < ancestors.Count || g.playerCheckNegRsc (path.player, startRemoveTime, false) >= 0) {
				foreach (KeyValuePair<Segment, List<int>> item in removed) {
					item.Key.units.AddRange (item.Value);
				}
				return false;
			}
			// remove paths that no longer contain units from visibility tiles
			foreach (Segment seg in removed.Keys) {
				if (seg.id == seg.path.segments.Count - 1 && seg.units.Count == 0 && seg.path.tileX != Sim.OffMap) {
					g.events.add(new TileMoveEvt(g.timeSim, seg.path.id, Sim.OffMap, 0));
				}
			}
			return true;
		}
		
		private bool removeUnitAfter(int unit, ref Dictionary<Segment, List<int>> removed) {
			if (units.Contains (unit)) {
				if (!unseen && timeStart < g.timeSim) return false;
				// only remove units from next segments if this is their only previous segment
				if (nextOnPath () == null || nextOnPath ().prev (unit).Count == 1) {
					// remove unit from next segments
					foreach (Segment seg in next (unit)) {
						seg.removeUnitAfter (unit, ref removed);
					}
					// remove child units that only this unit could have made
					foreach (KeyValuePair<Segment, int> child in children (unit)) {
						// TODO: if has alternate non-live parent, do we need to recursively make children non-live?
						if (child.Key.parents (child.Value).Count == 1) child.Key.removeUnitAfter (child.Value, ref removed);
					}
				}
				// remove unit from this segment
				units.Remove (unit);
				if (!removed.ContainsKey (this)) removed.Add (this, new List<int>());
				removed[this].Add (unit);
			}
			return true;
		}
		
		public bool unseenAfter(int unit) {
			if (!units.Contains (unit)) throw new ArgumentException("segment does not contain specified unit");
			if (!unseen) return false;
			foreach (Segment seg in next (unit)) {
				if (!seg.unseenAfter (unit)) return false;
			}
			foreach (KeyValuePair<Segment, int> child in children (unit)) {
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
		public long rscCollected(long time, int unit, int rscType, bool max, bool includeNonLiveChildren) {
			// if this segment wasn't active yet, unit can't have collected anything
			if (time < timeStart) return 0;
			// if next segment wasn't active yet, return resources collected from timeStart to time
			if (nextOnPath () == null || time < nextOnPath ().timeStart) {
				return g.unitT[g.units[unit].type].rscCollectRate[rscType] * (time - timeStart);
			}
			long ret = 0;
			bool foundNextSeg = false;
			// add resources gained in next segment that collected either least or most resources (depending on max parameter)
			foreach (Segment seg in next (unit)) {
				if (includeNonLiveChildren || seg.path.timeSimPast == long.MaxValue) {
					long segCollected = seg.rscCollected (time, unit, rscType, max, includeNonLiveChildren);
					if (!foundNextSeg || (max ^ (segCollected < ret))) {
						ret = segCollected;
						foundNextSeg = true;
					}
				}
			}
			// add resources gained by children
			foreach (KeyValuePair<Segment, int> child in children (unit)) {
				ret += child.Key.rscCollected (time, child.Value, rscType, max, includeNonLiveChildren);
				// subtract cost to make child unit
				ret -= g.unitT[g.units[child.Value].type].rscCost[rscType];
			}
			// add resources collected on this segment
			ret += g.unitT[g.units[unit].type].rscCollectRate[rscType] * (nextOnPath ().timeStart - timeStart);
			return ret;
		}
		
		/// <summary>
		/// returns all segment/unit pairs that could have made specified unit in this segment
		/// </summary>
		public List<KeyValuePair<Segment, int>> parents(int unit) {
			List<KeyValuePair<Segment, int>> ret = new List<KeyValuePair<Segment, int>>();
			if (prev (unit).Count == 0) {
				foreach (Segment seg in prev ()) {
					foreach (int unit2 in seg.units) {
						if (g.unitT[g.units[unit2].type].canMake[g.units[unit].type]) {
							ret.Add (new KeyValuePair<Segment, int>(seg, unit2));
						}
					}
				}
			}
			return ret;
		}
		
		/// <summary>
		/// returns all segment/unit pairs that specified unit in this segment could have made
		/// </summary>
		public List<KeyValuePair<Segment, int>> children(int unit) {
			List<KeyValuePair<Segment, int>> ret = new List<KeyValuePair<Segment, int>>();
			foreach (Segment seg in next ()) {
				foreach (int unit2 in seg.units) {
					if (g.unitT[g.units[unit].type].canMake[g.units[unit2].type] && seg.prev (unit2).Count == 0) {
						ret.Add (new KeyValuePair<Segment, int>(seg, unit2));
					}
				}
			}
			return ret;
		}
		
		/// <summary>
		/// returns all segments containing the specified unit that merge onto the beginning of this segment
		/// </summary>
		public List<Segment> prev(int unit) {
			List<Segment> ret = new List<Segment>();
			foreach (Segment seg in prev()) {
				if (seg.units.Contains (unit)) ret.Add (seg);
			}
			return ret;
		}
		
		/// <summary>
		/// returns all segments containing the specified unit that branch off from the end of this segment
		/// </summary>
		public List<Segment> next(int unit) {
			List<Segment> ret = new List<Segment>();
			foreach (Segment seg in next()) {
				if (seg.units.Contains (unit)) ret.Add (seg);
			}
			return ret;
		}
		
		/// <summary>
		/// returns all segments that merge onto the beginning of this segment
		/// </summary>
		public List<Segment> prev() {
			List<Segment> ret = new List<Segment>();
			foreach (Segment seg in branches) {
				if (seg.prevOnPath() != null) ret.Add (seg.prevOnPath());
			}
			return ret;
		}
		
		/// <summary>
		/// returns all segments that branch off from the end of this segment
		/// </summary>
		public List<Segment> next() {
			if (nextOnPath() == null) return new List<Segment>();
			return new List<Segment>(nextOnPath().branches);
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
	
	/// <summary>
	/// represents a single movement that starts at a specified location,
	/// moves at constant velocity to a specified end location, then stops
	/// </summary>
	public class Move {
		public long timeStart; // time when starts moving
		public long timeEnd; // time when finishes moving
		public FP.Vector vecStart; // location at timeStart, z indicates rotation (TODO: implement rotation)
		public FP.Vector vecEnd; // location at timeEnd, z indicates rotation

		/// <summary>
		/// constructor that directly sets all instance variables
		/// </summary>
		public Move(long timeStartVal, long timeEndVal, FP.Vector vecStartVal, FP.Vector vecEndVal) {
			timeStart = timeStartVal;
			timeEnd = timeEndVal;
			vecStart = vecStartVal;
			vecEnd = vecEndVal;
		}

		/// <summary>
		/// constructor for nonmoving trajectory
		/// </summary>
		public Move(long timeVal, FP.Vector vecVal)
			: this(timeVal, timeVal + 1, vecVal, vecVal) {
		}

		/// <summary>
		/// alternate method to create Path.Move object that asks for speed (in position units per millisecond) instead of end time
		/// </summary>
		public static Move fromSpeed(long timeStartVal, long speed, FP.Vector vecStartVal, FP.Vector vecEndVal) {
			return new Move(timeStartVal, timeStartVal + new FP.Vector(vecEndVal - vecStartVal).length() / speed, vecStartVal, vecEndVal);
		}

		/// <summary>
		/// returns location at specified time
		/// </summary>
		public FP.Vector calcPos(long time) {
			if (time >= timeEnd) return vecEnd;
			return vecStart + (vecEnd - vecStart) * FP.div(time - timeStart, timeEnd - timeStart);
		}

		/// <summary>
		/// returns time when position is at specified x value (inaccurate when x isn't between vecStart.x and vecEnd.x)
		/// </summary>
		public long timeAtX(long x) {
			return FP.lineCalcX(new FP.Vector(timeStart, vecStart.x), new FP.Vector(timeEnd, vecEnd.x), x);
		}

		/// <summary>
		/// returns time when position is at specified y value (inaccurate when y isn't between vecStart.y and vecEnd.y)
		/// </summary>
		public long timeAtY(long y) {
			return FP.lineCalcX(new FP.Vector(timeStart, vecStart.y), new FP.Vector(timeEnd, vecEnd.y), y);
		}
	}
	
	private readonly Sim g;
	private readonly int id; // index in path list
	public readonly long speed; // in position units per millisecond
	public readonly int player;
	public List<Segment> segments;
	public List<Move> moves; // later moves are later in list
	public int tileX, tileY; // current position on visibility tiles
	public long timeSimPast; // time traveling simulation time if made in the past, otherwise set to long.MaxValue

	public Path(Sim simVal, int idVal, long speedVal, int playerVal, List<int> units, long startTime, FP.Vector startPos, bool startUnseen) {
		g = simVal;
		id = idVal;
		speed = speedVal;
		player = playerVal;
		if (!g.stackAllowed (units, speed, player)) throw new ArgumentException("specified units may not be on the same path");
		segments = new List<Segment>();
		segments.Add (new Segment(this, 0, startTime, units, startUnseen));
		moves = new List<Move>();
		moves.Add (new Move(startTime, startPos));
		tileX = Sim.OffMap + 1;
		tileY = Sim.OffMap + 1;
		timeSimPast = (startTime > g.timeSim) ? long.MaxValue : startTime;
	}
	
	public Path(Sim simVal, int idVal, List<int> units, long startTime, FP.Vector startPos)
		: this(simVal, idVal, simVal.unitT[simVal.units[units[0]].type].speed, simVal.units[units[0]].player, units,
		startTime, startPos, simVal.tileAt(startPos).exclusiveWhen(simVal.units[units[0]].player, startTime)) {
	}

	/// <summary>
	/// ensure that if path is moving in the past, it does not move off exclusively seen areas
	/// </summary>
	public void updatePast(long curTime) {
		if (curTime <= timeSimPast || segments[segments.Count - 1].units.Count == 0) return;
		long timeSimPastNext = Math.Min(curTime, g.timeSim);
		SimEvtList pastEvents = new SimEvtList();
		TileMoveEvt evt;
		FP.Vector pos;
		int tX, tY, exclusiveIndex;
		// delete path if tile that path starts on stops being exclusive since timeSimPast
		pos = calcPos(timeSimPast);
		tX = (int)(pos.x >> FP.Precision);
		tY = (int)(pos.y >> FP.Precision);
		// TODO: without modifications, line below may cause syncing problems in multiplayer b/c addTileMoveEvts() sometimes adds events before timeSimPast
		addTileMoveEvts(ref pastEvents, timeSimPast, timeSimPastNext);
		evt = (TileMoveEvt)pastEvents.pop();
		exclusiveIndex = g.tiles[tX, tY].exclusiveIndexWhen(player, (evt != null) ? evt.time - 1 : curTime);
		if (!g.tiles[tX, tY].exclusiveWhen(player, (evt != null) ? evt.time - 1 : curTime)
			|| g.tiles[tX, tY].exclusive[player][exclusiveIndex] > timeSimPast) {
			segments[segments.Count - 1].removeAllUnits();
			return;
		}
		// delete path if path moves off exclusive area or tile that path moves to stops being exclusive
		if (evt != null) {
			do {
				if (evt.tileX != int.MinValue) tX = evt.tileX;
				if (evt.tileY != int.MinValue) tY = evt.tileY;
				exclusiveIndex = g.tiles[tX, tY].exclusiveIndexWhen(player, evt.time);
				if (!g.tiles[tX, tY].exclusiveWhen(player, evt.time)
					|| (exclusiveIndex + 1 < g.tiles[tX, tY].exclusive[player].Count() && g.tiles[tX, tY].exclusive[player][exclusiveIndex + 1] <= Math.Min(g.events.peekTime(), timeSimPastNext))) {
					segments[segments.Count - 1].removeAllUnits();
					return;
				}
			} while ((evt = (TileMoveEvt)pastEvents.pop()) != null);
		}
		// update past simulation time
		timeSimPast = timeSimPastNext;
	}
	
	/// <summary>
	/// returns index of node that is active at specified time
	/// </summary>
	public int getNode(long time) {
		int ret = segments.Count - 1;
		while (ret >= 0 && time < segments[ret].timeStart) ret--;
		return ret;
	}
	
	public int insertNode(long time) {
		int node = getNode (time);
		if (node >= 0 && segments[node].timeStart == time) return node;
		segments.Insert (node + 1, new Segment(this, node + 1, time, new List<int>(segments[node].units), segments[node].unseen));
		for (int i = node + 2; i < segments.Count; i++) {
			segments[i].id = i;
		}
		return node + 1;
	}
	
	/// <summary>
	/// move towards specified location starting at specified time,
	/// return index of moved path (in case moving a subset of units in path)
	/// </summary>
	public int moveTo(long time, List<int> units, FP.Vector pos) {
		int path2 = id; // move this path by default
		int node = getNode (time);
		if (time < g.timeSim) {
			// move non-live path if in past
			// replacement paths currently not implemented, so make a new path every time a path is moved in the past
			if (!makePath (time, units)) throw new SystemException("make non-live path failed when moving units");
			path2 = g.paths.Count - 1;
		}
		else {
			foreach (int unit in segments[node].units) {
				if (!units.Contains (unit)) {
					// some units in path aren't being moved, so make a new path
					if (!makePath (time, units)) throw new SystemException("make new path failed when moving units");
					//removeUnit (time, unit); // TODO: this fails because new path isn't live yet
					path2 = g.paths.Count - 1;
					break;
				}
			}
		}
		g.paths[path2].moveTo (time, pos);
		return path2;
	}

	/// <summary>
	/// move towards specified location starting at specified time
	/// </summary>
	public void moveTo(long time, FP.Vector pos) {
		FP.Vector curPos = calcPos(time);
		FP.Vector goalPos = pos;
		// don't move off map edge
		if (goalPos.x < 0) goalPos.x = 0;
		if (goalPos.x > g.mapSize) goalPos.x = g.mapSize;
		if (goalPos.y < 0) goalPos.y = 0;
		if (goalPos.y > g.mapSize) goalPos.y = g.mapSize;
		// add move
		moves.Add (Move.fromSpeed(time, speed, curPos, goalPos));
		if (!g.movedPaths.Contains(id)) g.movedPaths.Add(id); // indicate to delete and recalculate later TileMoveEvts for this path
	}

	/// <summary>
	/// returns whether allowed to move at specified time
	/// </summary>
	public bool canMove(long time) {
		// TODO: maybe make overloaded version that also checks units
		if (time < moves[0].timeStart || speed <= 0) return false;
		if (time < g.timeSim) {
			int node = getNode (time);
			if (node < 0) return false;
			foreach (int unit in segments[node].units) {
				if (!segments[node].unseenAfter (unit)) return false;
			}
		}
		return true;
	}

	/// <summary>
	/// returns location at specified time
	/// </summary>
	public FP.Vector calcPos(long time) {
		return moves[getMove(time)].calcPos(time);
	}

	/// <summary>
	/// returns index of move that is occurring at specified time
	/// </summary>
	public int getMove(long time) {
		int ret = moves.Count - 1;
		while (ret >= 0 && time < moves[ret].timeStart) ret--;
		return ret;
	}

	/// <summary>
	/// inserts TileMoveEvt events for this path into events for the time interval from timeMin to timeMax
	/// </summary>
	/// <remarks>due to fixed point imprecision in lineCalcX() and lineCalcY(), this sometimes adds events outside the requested time interval</remarks>
	public void addTileMoveEvts(ref SimEvtList events, long timeMin, long timeMax) {
		int move, moveLast;
		FP.Vector pos, posLast;
		int i, j, iNext, tX, tY, dir;
		if (timeMax < moves[0].timeStart) return;
		moveLast = getMove(timeMin);
		move = getMove(timeMax);
		if (moveLast < 0) {
			// put path on visibility tiles for the first time
			// TODO: do this manually in scnOpen and makePath? then paths made at timeSim can start out live
			events.add(new TileMoveEvt(moves[0].timeStart, id, (int)(moves[0].vecStart.x >> FP.Precision), (int)(moves[0].vecStart.y >> FP.Precision)));
			moveLast = 0;
		}
		for (i = moveLast; i <= move; i = iNext) {
			// next move may not be i + 1 if times are out of order
			iNext = i + 1;
			for (j = iNext + 1; j < moves.Count; j++) {
				if (moves[j].timeStart <= moves[iNext].timeStart) iNext = j;
			}
			posLast = (i == moveLast) ? moves[i].calcPos(Math.Max(timeMin, moves[0].timeStart)) : moves[i].vecStart;
			pos = (i == move) ? moves[i].calcPos(timeMax) : moves[iNext].vecStart;
			// moving between columns (x)
			dir = (pos.x >= posLast.x) ? 0 : -1;
			for (tX = (int)(Math.Min(pos.x, posLast.x) >> FP.Precision) + 1; tX <= (int)(Math.Max(pos.x, posLast.x) >> FP.Precision); tX++) {
				events.add(new TileMoveEvt(moves[i].timeAtX(tX << FP.Precision), id, tX + dir, int.MinValue));
			}
			// moving between rows (y)
			dir = (pos.y >= posLast.y) ? 0 : -1;
			for (tY = (int)(Math.Min(pos.y, posLast.y) >> FP.Precision) + 1; tY <= (int)(Math.Max(pos.y, posLast.y) >> FP.Precision); tY++) {
				events.add(new TileMoveEvt(moves[i].timeAtY(tY << FP.Precision), id, int.MinValue, tY + dir));
			}
		}
		if (segments[segments.Count - 1].units.Count == 0 && segments[getNode (timeMin)].units.Count > 0) {
			// path no longer contains any units
			// TODO: do this directly in takeHealth?
			g.events.add(new TileMoveEvt(segments[segments.Count - 1].timeStart, id, Sim.OffMap, 0));
		}
	}

	/// <summary>
	/// let path be updated in the present (i.e., stop time traveling) starting at timeSim
	/// </summary>
	public void goLive() {
		timeSimPast = long.MaxValue;
		FP.Vector pos = calcPos(g.timeSim);
		g.events.add(new TileMoveEvt(g.timeSim, id, (int)(pos.x >> FP.Precision), (int)(pos.y >> FP.Precision)));
	}

	public void beUnseen(long time) {
		segments[insertNode(time)].unseen = true;
	}

	public void beSeen(long time) {
		segments[insertNode(time)].unseen = false;
		// TODO: delete all child paths made before time unseen
	}

	/// <summary>
	/// makes a new path containing specified units, returns whether successful
	/// </summary>
	public bool makePath(long time, List<int> units) {
		if (canMakePath(time, units)) {
			int node = insertNode (time);
			g.paths.Add (new Path(g, g.paths.Count, g.unitT[g.units[units[0]].type].speed, player, units, time, calcPos (time), segments[node].unseen));
			connect (time, g.paths.Count - 1);
			// if this path isn't live, new path can't be either
			if (timeSimPast != long.MaxValue) g.paths[g.paths.Count - 1].timeSimPast = time;
			// indicate to calculate TileMoveEvts for new path starting at timeSim
			if (!g.movedPaths.Contains(g.paths.Count - 1)) g.movedPaths.Add(g.paths.Count - 1);
			// if new path isn't live, indicate that player now has a non-live path
			if (g.paths[g.paths.Count - 1].timeSimPast != long.MaxValue) g.players[player].hasNonLivePaths = true;
			return true;
		}
		return false;
	}

	/// <summary>
	/// returns whether this path can make a new path as specified
	/// </summary>
	public bool canMakePath(long time, List<int> units) {
		if (units.Count == 0) return false;
		int node = getNode(time);
		if (node < 0) return false;
		long[] rscCost = new long[g.nRsc];
		foreach (int unit in units) {
			if (segments[node].units.Contains (unit)) {
				// unit in path would be child path
				// check parent made before (not at same time as) child, so it's unambiguous who is the parent
				if (!canBeUnambiguousParent (time, node, unit)) return false;
				// check parent unit won't be seen later
				if (!segments[node].unseenAfter (unit)) return false;
			}
			else {
				if (!canMakeUnitType (time, g.units[unit].type)) return false;
				// unit in path would be non-path child unit
				for (int i = 0; i < g.nRsc; i++) {
					rscCost[i] += g.unitT[g.units[unit].type].rscCost[i];
				}
			}
		}
		bool newPathIsLive = (time >= g.timeSim && timeSimPast == long.MaxValue);
		for (int i = 0; i < g.nRsc; i++) {
			// TODO: may be more permissive by passing in max = true, but this really complicates removeUnit() algorithm (see planning notes)
			if (g.playerResource(player, time, i, false, !newPathIsLive) < rscCost[i]) return false;
		}
		return true;
	}
	
	public bool canMakeUnitType(long time, int type) {
		int node = getNode (time);
		if (node >= 0) {
			foreach (int unit in segments[node].units) {
				if (g.unitT[g.units[unit].type].canMake[type] && canBeUnambiguousParent (time, node, unit)
					&& (time >= g.timeSim || segments[node].unseenAfter (unit))) {
					return true;
				}
			}
		}
		return false;
	}
	
	/// <summary>
	/// returns whether specified unit is in path before specified time,
	/// so if it makes a child unit, it's unambiguous who is the parent
	/// </summary>
	private bool canBeUnambiguousParent(long time, int node, int unit) {
		return segments[node].timeStart < time || (node > 0 && segments[node - 1].units.Contains (unit));
	}

	/// <summary>
	/// returns index (in unit array) of path that isn't updated in the present and is therefore safe to move in the past
	/// </summary>
	private int prepareNonLivePath(long time) {
		throw new NotImplementedException();
	}
	
	/// <summary>
	/// connects this path to specified path at specified time,
	/// returns this path's node where the paths were connected
	/// </summary>
	public int connect(long time, int path) {
		int node = insertNode (time);
		int node2 = g.paths[path].insertNode (time);
		if (!segments[node].branches.Contains (g.paths[path].segments[node2])) {
			segments[node].branches.AddRange (g.paths[path].segments[node2].branches);
			g.paths[path].segments[node2].branches = segments[node].branches;
		}
		return node;
	}

	/// <summary>
	/// mark existing unit as a child of this unit
	/// </summary>
	private void addChild(int unit) {
		throw new NotImplementedException();
	}

	/// <summary>
	/// non-recursively delete specified child unit
	/// </summary>
	private void deleteChild(int unit) {
		throw new NotImplementedException();
	}

	/// <summary>
	/// recursively delete all child units
	/// </summary>
	/// <remarks>this does not check whether deleting the units may lead to player having negative resources</remarks>
	private void deleteAllChildren() {
		throw new NotImplementedException();
	}

	/// <summary>
	/// delete child units made after the specified time
	/// </summary>
	/// <remarks>this does not check whether deleting the units may lead to player having negative resources</remarks>
	private void deleteChildrenAfter(long time) {
		throw new NotImplementedException();
	}

	/// <summary>
	/// change unit movement to make it look like this unit never existed
	/// </summary>
	private void deleteAllMoves() {
		throw new NotImplementedException();
	}
	
	/// <summary>
	/// returns minimum absolute position where clicking would select the path
	/// </summary>
	public FP.Vector selMinPos(long time) {
		FP.Vector ret = new FP.Vector(int.MaxValue, int.MaxValue);
		foreach (int unit in segments[getNode(time)].units) {
			ret.x = Math.Min (ret.x, g.unitT[g.units[unit].type].selMinPos.x);
			ret.y = Math.Min (ret.y, g.unitT[g.units[unit].type].selMinPos.y);
		}
		return ret + calcPos(time);
	}
	
	/// <summary>
	/// returns maximum absolute position where clicking would select the path
	/// </summary>
	public FP.Vector selMaxPos(long time) {
		FP.Vector ret = new FP.Vector(int.MinValue, int.MinValue);
		foreach (int unit in segments[getNode(time)].units) {
			ret.x = Math.Max (ret.x, g.unitT[g.units[unit].type].selMaxPos.x);
			ret.y = Math.Max (ret.y, g.unitT[g.units[unit].type].selMaxPos.y);
		}
		return ret + calcPos(time);
	}
	
	/// <summary>
	/// returns minimum distance that paths branching off from this path should move away
	/// </summary>
	public long makePathMinDist(long time, List<int> units) {
		long ret = 0;
		foreach (int unit in segments[getNode (time)].units) {
			if (units.Contains (unit) && g.unitT[g.units[unit].type].makePathMinDist > ret) {
				ret = g.unitT[g.units[unit].type].makePathMinDist;
			}
		}
		return ret;
	}
	
	/// <summary>
	/// returns maximum distance that paths branching off from this path should move away
	/// </summary>
	public long makePathMaxDist(long time, List<int> units) {
		long ret = 0;
		foreach (int unit in segments[getNode (time)].units) {
			if (units.Contains (unit) && g.unitT[g.units[unit].type].makePathMaxDist > ret) {
				ret = g.unitT[g.units[unit].type].makePathMaxDist;
			}
		}
		return ret;
	}

	/// <summary>
	/// returns whether path is known to not be seen by another player at latest known time
	/// </summary>
	public bool unseen() {
		throw new NotImplementedException();
	}

	/// <summary>
	/// returns whether path is created (TODO: and contains units?) at specified time
	/// </summary>
	public bool exists(long time) {
		return time >= segments[0].timeStart && time >= moves[0].timeStart;
	}

	/// <summary>
	/// returns whether path exists and is being updated in the present (i.e., isn't time traveling)
	/// </summary>
	public bool isLive(long time) {
		return exists (time) && timeSimPast == long.MaxValue;
	}
}
