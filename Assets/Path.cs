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
		public long time; // TODO: rename to timeStart?
		/// <summary>
		/// segments that intersect at the beginning of this segment;
		/// all segments in this list use the same List instance so updating this list in one segment updates it in all intersecting segments
		/// (NOTE: protobuf-net won't like that)
		/// </summary>
		public List<Segment> intersects;
		public List<int> paths; // indices of paths that connect to this path at node time
		public List<int> units; // indices of units on this path segment
		public bool unseen; // whether path segment is known to not be seen by another player
		
		public Segment(Path pathVal, int idVal, long timeVal, List<int> unitsVal, bool unseenVal) {
			path = pathVal;
			g = path.g;
			id = idVal;
			time = timeVal;
			intersects = new List<Segment>();
			intersects.Add (this);
			paths = new List<int>();
			units = unitsVal;
			unseen = unseenVal;
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
			foreach (Segment seg in intersects) {
				if (seg.prevOnPath() != null) ret.Add (seg.prevOnPath());
			}
			return ret;
		}
		
		/// <summary>
		/// returns all segments that branch off from the end of this segment
		/// </summary>
		public List<Segment> next() {
			if (nextOnPath() == null) return new List<Segment>();
			return new List<Segment>(nextOnPath().intersects);
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
			removeAllUnits(long.MaxValue);
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
					removeAllUnits(long.MaxValue);
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
		while (ret >= 0 && time < segments[ret].time) ret--;
		return ret;
	}
	
	public int insertNode(long time) {
		int node = getNode (time);
		if (node >= 0 && segments[node].time == time) return node;
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
				if (!unseenAfter (node, unit)) return false;
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
			g.events.add(new TileMoveEvt(segments[segments.Count - 1].time, id, Sim.OffMap, 0));
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
	/// removes all units from path at specified time if doing so wouldn't affect anything that another player saw
	/// </summary>
	public void removeAllUnits(long time) {
		int node = getNode (time);
		while (segments[node].units.Count > 0) {
			if (!removeUnit(long.MaxValue, segments[node].units[0])) throw new SystemException("failed to remove a unit from path " + id);
		}
	}

	/// <summary>
	/// removes specified unit from path if doing so wouldn't affect anything that another player saw, returns whether successful
	/// </summary>
	public bool removeUnit(long time, int unit) {
		List<int> parentPaths = new List<int>();
		List<int> parentNodes = new List<int>();
		List<int> rmPaths = new List<int>();
		List<int> rmNodes = new List<int>();
		long minParentNodeTime = long.MaxValue;
		int i;
		parentPaths.Add (id);
		parentNodes.Add (getNode (time));
		// if this path already doesn't contain specified unit at specified time, return true
		if (parentNodes[0] < 0 || !segments[parentNodes[0]].units.Contains (unit)) return true;
		// find all parent paths/nodes to start removal from
		for (i = 0; i < parentPaths.Count; i++) {
			while (true) {
				// if reached beginning of a path defined in scenario file, return false (we assume other players know the scenario's starting state)
				if (parentPaths[i] < g.nRootPaths && parentNodes[i] == 0) return false;
				bool foundSharedParent = false;
				foreach (int path in g.paths[parentPaths[i]].segments[parentNodes[i]].paths) {
					int node = g.paths[path].getNode (g.paths[parentPaths[i]].segments[parentNodes[i]].time);
					if (g.paths[path].segments[node].units.Contains (unit)
						&& (g.paths[path].timeSimPast == long.MaxValue || timeSimPast != long.MaxValue)) {
						// found a path with the same child unit as us
						int index = parentPaths.IndexOf (path);
						if (index < 0 || parentNodes[index] != node) {
							// currently not planning to remove unit from that path,
							// so stop search here so that shared parent won't be deleted
							foundSharedParent = true;
							break;
						}
						// don't worry about potentially redundant entry in deletion list (if part below adds it to list again),
						// removeUnitAfter() can take care of that
						// TODO: as long as removeUnitAfter() doesn't delete any nodes
					}
					if (g.paths[parentPaths[i]].isChildPathOf (path, unit, node, parentNodes[i])) {
						// found a parent path containing this unit, so remove unit from this path too
						parentPaths.Add (path);
						parentNodes.Add (node - 1);
					}
				}
				if (foundSharedParent) break;
				// if we are at earliest node containing this unit, break
				if (!g.paths[parentPaths[i]].isChildPathOf (parentPaths[i], unit, parentNodes[i], parentNodes[i])) break;
				// otherwise, look at previous node
				parentNodes[i]--;
			}
		}
		// remove unit recursively, starting at the parent paths/nodes we found
		for (i = 0; i < parentPaths.Count; i++) {
			if (!g.paths[parentPaths[i]].removeUnitAfter (parentNodes[i], unit, ref rmPaths, ref rmNodes)) break;
			minParentNodeTime = Math.Min (minParentNodeTime, g.paths[parentPaths[i]].segments[parentNodes[i]].time);
		}
		// if a removeUnitAfter() call failed or removing unit might have led to player having negative resources,
		// add unit back to nodes it was removed from
		if (i < parentPaths.Count || g.playerCheckNegRsc (player, minParentNodeTime, false) >= 0) {
			for (i = 0; i < rmPaths.Count; i++) {
				g.paths[rmPaths[i]].segments[rmNodes[i]].units.Add (unit);
			}
			return false;
		}
		// remove paths that no longer contain units from visibility tiles
		foreach (int path in rmPaths.Distinct ()) {
			if (g.paths[path].tileX != Sim.OffMap && g.paths[path].segments[g.paths[path].segments.Count - 1].units.Count == 0) {
				g.events.add(new TileMoveEvt(g.timeSim, path, Sim.OffMap, 0));
			}
		}
		return true;
	}
	
	// TODO: this is similar to unseenAfter, is it possible to put shared code into a function?
	private bool removeUnitAfter(int node, int unit, ref List<int> rmPaths, ref List<int> rmNodes) {
		int curNode = node;
		while (segments[curNode].units.Contains (unit)) {
			if (!segments[curNode].unseen && segments[curNode].time < g.timeSim) return false;
			segments[curNode].units.Remove (unit);
			rmPaths.Add (id);
			rmNodes.Add (curNode);
			curNode++;
			if (curNode == segments.Count) break;
			// stop if reached another parent path for unit being removed
			foreach (int path2 in segments[curNode].paths) {
				int node2 = g.paths[path2].getNode (segments[curNode].time);
				if (node2 > 0 && g.paths[path2].segments[node2 - 1].units.Contains (unit)) return true;
			}
			// check if any units in connected paths should be removed
			foreach (int path2 in segments[curNode].paths) {
				int node2 = g.paths[path2].getNode (segments[curNode].time);
				for (int i = g.paths[path2].segments[node2].units.Count - 1; i >= 0; i--) { // iterate in reverse so don't have to decrement i when unit is removed
					int unit2 = g.paths[path2].segments[node2].units[i];
					if (unit == unit2) {
						// delete unit from child path
						g.paths[path2].removeUnitAfter (node2, unit, ref rmPaths, ref rmNodes);
					}
					else if (g.unitT[g.units[unit].type].canMake[g.units[unit2].type] && !g.paths[path2].isChildPath (unit2, node2)) {
						// found a unit that deleted unit could have made, check if any other connected unit can make it
						// don't check path2 because I'm currently not planning any GUI to make a child unit in the same path as its parent
						bool foundAnotherParent = false;
						foreach (int path3 in g.paths[path2].segments[node2].paths) {
							int node3 = g.paths[path3].getNode (segments[curNode].time);
							// don't check id != path3 b/c already removed unit from this path, and need to check if other units in this path can make the unit
							if (node3 > 0 && g.unitsCanMake (g.paths[path3].segments[node3 - 1].units, g.units[unit2].type)) {
								foundAnotherParent = true;
								break;
							}
						}
						if (!foundAnotherParent) {
							// no other connected unit can make that unit, so delete that unit too
							g.paths[path2].removeUnitAfter (node2, unit2, ref rmPaths, ref rmNodes);
						}
					}
				}
			}
		}
		return true;
	}
	
	// TODO: this is similar to removeUnitAfter, is it possible to put shared code into a function?
	private bool unseenAfter(int node, int unit) {
		int curNode = node;
		while (segments[curNode].units.Contains (unit)) {
			if (!segments[curNode].unseen) return false;
			curNode++;
			if (curNode == segments.Count) break;
			foreach (int path2 in segments[curNode].paths) {
				int node2 = g.paths[path2].getNode (segments[curNode].time);
				foreach (int unit2 in g.paths[path2].segments[node2].units) {
					if (unit == unit2) {
						// check child path
						if (!g.paths[path2].unseenAfter (node2, unit)) return false;
					}
					else if (g.unitT[g.units[unit].type].canMake[g.units[unit2].type] && !g.paths[path2].isChildPath (unit2, node2)) {
						// found a unit that this unit can make, check if any other connected unit can make it
						// don't check path2 because I'm currently not planning any GUI to make a child unit in the same path as its parent
						bool foundAnotherParent = false;
						foreach (int path3 in g.paths[path2].segments[node2].paths) {
							int node3 = g.paths[path3].getNode (segments[curNode].time);
							if (node3 > 0) {
								List<int> units3 = new List<int>(g.paths[path3].segments[node3 - 1].units);
								units3.Remove (unit);
								if (g.unitsCanMake (units3, g.units[unit2].type)) {
									foundAnotherParent = true;
									break;
								}
							}
						}
						if (!foundAnotherParent) {
							// no other connected unit can make that unit
							if (!g.paths[path2].unseenAfter (node2, unit2)) return false;
						}
					}
				}
			}
		}
		return true;
	}

	/// <summary>
	/// makes a new path containing specified units, returns whether successful
	/// </summary>
	public bool makePath(long time, List<int> units) {
		if (canMakePath(time, units)) {
			int node = insertNode (time);
			g.paths.Add (new Path(g, g.paths.Count, g.unitT[g.units[units[0]].type].speed, player, units, time, calcPos (time), segments[node].unseen));
			foreach (int path in segments[node].paths) {
				g.paths[path].addConnectedPath (time, g.paths.Count - 1);
			}
			addConnectedPath (time, g.paths.Count - 1);
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
				if (!unseenAfter (node, unit)) return false;
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
					&& (time >= g.timeSim || unseenAfter (node, unit))) {
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
		return segments[node].time < time || (node > 0 && segments[node - 1].units.Contains (unit));
	}

	/// <summary>
	/// returns index (in unit array) of path that isn't updated in the present and is therefore safe to move in the past
	/// </summary>
	private int prepareNonLivePath(long time) {
		throw new NotImplementedException();
	}
	
	/// <summary>
	/// connects this path to specified path (and vice versa) at specified time,
	/// returns this path's node where the paths were connected
	/// </summary>
	public int addConnectedPath(long time, int path) {
		int node = insertNode (time);
		if (!segments[node].paths.Contains (path)) {
			segments[node].paths.Add (path);
			g.paths[path].addConnectedPath (time, id);
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
	/// returns resource amount gained by specified unit and its child units (subtracting cost to make child units)
	/// from specified node's time to specified time
	/// </summary>
	/// <param name="max">
	/// since different paths can have collected different resource amounts,
	/// determines whether to use paths that collected least or most resources in calculation
	/// </param>
	// TODO: this currently double-counts child paths/units if paths merge, fix this before enabling stacking
	public long rscCollected(long time, int node, int unit, int rscType, bool max, bool includeNonLiveChildren) {
		if (time < segments[node].time) return 0; // if this node wasn't active yet, unit can't have collected anything
		int endNode = node;
		long timeCollectEnd = (g.units[unit].healthWhen(time) == 0) ? g.units[unit].timeHealth[g.units[unit].nTimeHealth - 1] : time;
		long ret = 0;
		while (endNode < segments.Count - 1 && segments[endNode].units.Contains (unit) && segments[endNode + 1].time <= time) endNode++;
		for (int i = endNode; i > node; i--) {
			foreach (int path2 in segments[i].paths) {
				if (includeNonLiveChildren || g.paths[path2].timeSimPast == long.MaxValue) {
					int node2 = g.paths[path2].getNode (segments[i].time);
					if (g.paths[path2].isChildPathOf (id, unit, i, node2)) {
						// if child path is one of this unit's paths and collected more/less (depending on max parameter) resources than this path,
						// use that path for resource calculation
						long pathCollected = g.paths[path2].rscCollected (time, node2, unit, rscType, max, includeNonLiveChildren);
						if (max ^ (pathCollected < ret + g.unitT[g.units[unit].type].rscCollectRate[rscType] * (timeCollectEnd - g.paths[path2].segments[node2].time))) {
							ret = pathCollected;
							timeCollectEnd = g.paths[path2].segments[node2].time;
						}
					}
					else if (!g.paths[path2].isChildPath (unit, node2)) {
						foreach (int unit2 in g.paths[path2].segments[node2].units) {
							if (g.unitT[g.units[unit].type].canMake[g.units[unit2].type]) {
								// add resources that non-path child unit gained
								ret += g.paths[path2].rscCollected (time, node2, unit2, rscType, max, includeNonLiveChildren);
								// subtract cost to make child unit
								ret -= g.unitT[g.units[unit2].type].rscCost[rscType];
							}
						}
					}
				}
			}
		}
		// add resources collected by this unit
		ret += g.unitT[g.units[unit].type].rscCollectRate[rscType] * (timeCollectEnd - segments[node].time);
		return ret;
	}
	
	private bool isChildPath(int unit, int node) {
		if (isChildPathOf (id, unit, node, node)) return true;
		foreach (int path in segments[node].paths) {
			if (isChildPathOf (path, unit, g.paths[path].getNode (segments[node].time), node)) return true;
		}
		return false;
	}
	
	private bool isChildPathOf(int parentPath, int unit, int parentNode, int childNode) {
		if (g.paths[parentPath].segments[parentNode].time != segments[childNode].time) {
			throw new ArgumentException("parent and child nodes have different times");
		}
		return parentNode > 0 && g.paths[parentPath].segments[parentNode - 1].units.Contains (unit)
			&& segments[childNode].units.Contains (unit);
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
		return time >= segments[0].time && time >= moves[0].timeStart;
	}

	/// <summary>
	/// returns whether path exists and is being updated in the present (i.e., isn't time traveling)
	/// </summary>
	public bool isLive(long time) {
		return exists (time) && timeSimPast == long.MaxValue;
	}
}
