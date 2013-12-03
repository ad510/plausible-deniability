// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// path that unit(s) of the same speed and player move along
/// (units that are on the same path stack on top of each other)
/// </summary>
public class Path {
	public readonly Sim g;
	public readonly int id; // index in path list
	public readonly long speed; // in position units per millisecond
	public readonly int player;
	public List<Segment> segments; // composition of the path over time, more recent segments are later in list
	public List<Move> moves; // how path moved over time, more recent moves are later in list
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
		if (curTime <= timeSimPast || segments.Last ().units.Count == 0) return;
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
			segments.Last ().removeAllUnits();
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
					segments.Last ().removeAllUnits();
					return;
				}
			} while ((evt = (TileMoveEvt)pastEvents.pop()) != null);
		}
		// update past simulation time
		timeSimPast = timeSimPastNext;
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
		if (segments.Last ().units.Count == 0 && segments[getSegment (timeMin)].units.Count > 0) {
			// path no longer contains any units
			// TODO: do this directly in takeHealth?
			g.events.add(new TileMoveEvt(segments.Last ().timeStart, id, Sim.OffMap, 0));
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
		segments[insertSegment(time)].unseen = true;
	}

	public void beSeen(long time) {
		int seg = insertSegment(time);
		List<KeyValuePair<Segment, int>> seenUnits = new List<KeyValuePair<Segment, int>>();
		segments[seg].unseen = false;
		foreach (int unit in segments[seg].units) {
			seenUnits.Add (new KeyValuePair<Segment, int>(segments[seg], unit));
		}
		if (!g.deleteOtherPaths (seenUnits)) throw new SystemException("failed to delete other paths of seen path");
	}

	/// <summary>
	/// makes a new path containing specified units, returns whether successful
	/// </summary>
	public bool makePath(long time, List<int> units) {
		if (canMakePath(time, units)) {
			int seg = insertSegment (time);
			g.paths.Add (new Path(g, g.paths.Count, g.unitT[g.units[units[0]].type].speed, player, units, time, calcPos (time), segments[seg].unseen));
			connect (time, g.paths.Count - 1);
			// if this path isn't live, new path can't be either
			if (timeSimPast != long.MaxValue) g.paths.Last ().timeSimPast = time;
			// indicate to calculate TileMoveEvts for new path starting at timeSim
			if (!g.movedPaths.Contains(g.paths.Count - 1)) g.movedPaths.Add(g.paths.Count - 1);
			// if new path isn't live, indicate that player now has a non-live path
			if (g.paths.Last ().timeSimPast != long.MaxValue) g.players[player].hasNonLivePaths = true;
			return true;
		}
		return false;
	}

	/// <summary>
	/// returns whether this path can make a new path as specified
	/// </summary>
	public bool canMakePath(long time, List<int> units) {
		if (units.Count == 0) return false;
		int seg = getSegment(time);
		if (seg < 0) return false;
		long[] rscCost = new long[g.rscNames.Length];
		foreach (int unit in units) {
			if (segments[seg].units.Contains (unit)) {
				// unit in path would be child path
				// check parent made before (not at same time as) child, so it's unambiguous who is the parent
				if (!canBeUnambiguousParent (time, seg, unit)) return false;
				// check parent unit won't be seen later
				if (!segments[seg].unseenAfter (unit)) return false;
			}
			else {
				if (!canMakeUnitType (time, g.units[unit].type)) return false;
				// unit in path would be non-path child unit
				for (int i = 0; i < g.rscNames.Length; i++) {
					rscCost[i] += g.unitT[g.units[unit].type].rscCost[i];
				}
			}
		}
		bool newPathIsLive = (time >= g.timeSim && timeSimPast == long.MaxValue);
		for (int i = 0; i < g.rscNames.Length; i++) {
			// TODO: may be more permissive by passing in max = true, but this really complicates removeUnit() algorithm (see planning notes)
			if (g.playerResource(player, time, i, false, !newPathIsLive) < rscCost[i]) return false;
		}
		return true;
	}
	
	public bool canMakeUnitType(long time, int type) {
		int seg = getSegment (time);
		if (seg >= 0) {
			foreach (int unit in segments[seg].units) {
				if (g.unitT[g.units[unit].type].canMake[type] && canBeUnambiguousParent (time, seg, unit)
					&& (time >= g.timeSim || segments[seg].unseenAfter (unit))) {
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
	private bool canBeUnambiguousParent(long time, int segment, int unit) {
		return segments[segment].timeStart < time || (segment > 0 && segments[segment - 1].units.Contains (unit));
	}

	/// <summary>
	/// connects this path to specified path at specified time,
	/// returns this path's segment where the paths were connected
	/// </summary>
	public int connect(long time, int path) {
		int seg = insertSegment (time);
		int seg2 = g.paths[path].insertSegment (time);
		if (!segments[seg].branches.Contains (g.paths[path].segments[seg2])) {
			segments[seg].branches.AddRange (g.paths[path].segments[seg2].branches);
			g.paths[path].segments[seg2].branches = segments[seg].branches;
		}
		return seg;
	}
	
	/// <summary>
	/// move towards specified location starting at specified time,
	/// return index of moved path (in case moving a subset of units in path)
	/// </summary>
	public int moveTo(long time, List<int> units, FP.Vector pos) {
		int path2 = id; // move this path by default
		int seg = getSegment (time);
		if (time < g.timeSim) {
			// move non-live path if in past
			// replacement paths currently not implemented, so make a new path every time a path is moved in the past
			if (!makePath (time, units)) throw new SystemException("make non-live path failed when moving units");
			path2 = g.paths.Count - 1;
		}
		else {
			foreach (int unit in segments[seg].units) {
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
			int seg = getSegment (time);
			if (seg < 0) return false;
			foreach (int unit in segments[seg].units) {
				if (!segments[seg].unseenAfter (unit)) return false;
			}
		}
		return true;
	}
	
	/// <summary>
	/// inserts a segment starting at specified time if no segment already starts at that time,
	/// returns index of that segment
	/// </summary>
	public int insertSegment(long time) {
		int seg = getSegment (time);
		if (seg >= 0 && segments[seg].timeStart == time) return seg;
		segments.Insert (seg + 1, new Segment(this, seg + 1, time, new List<int>(segments[seg].units), segments[seg].unseen));
		for (int i = seg + 2; i < segments.Count; i++) {
			segments[i].id = i;
		}
		return seg + 1;
	}
	
	/// <summary>
	/// returns index of segment that is active at specified time
	/// </summary>
	public int getSegment(long time) {
		int ret = segments.Count - 1;
		while (ret >= 0 && time < segments[ret].timeStart) ret--;
		return ret;
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
	/// returns minimum absolute position where clicking would select the path
	/// </summary>
	public FP.Vector selMinPos(long time) {
		FP.Vector ret = new FP.Vector(int.MaxValue, int.MaxValue);
		foreach (int unit in segments[getSegment(time)].units) {
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
		foreach (int unit in segments[getSegment(time)].units) {
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
		foreach (int unit in segments[getSegment (time)].units) {
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
		foreach (int unit in segments[getSegment (time)].units) {
			if (units.Contains (unit) && g.unitT[g.units[unit].type].makePathMaxDist > ret) {
				ret = g.unitT[g.units[unit].type].makePathMaxDist;
			}
		}
		return ret;
	}
}
