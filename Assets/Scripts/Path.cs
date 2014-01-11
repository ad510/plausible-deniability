// Copyright (c) 2013-2014 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

/// <summary>
/// path that unit(s) of the same speed and player move along
/// (units that are on the same path stack on top of each other)
/// </summary>
[ProtoContract]
public class Path {
	[ProtoMember(1, AsReference = true)] public readonly Sim g;
	[ProtoMember(2)] public readonly int id; // index in path list
	[ProtoMember(3)] public readonly long speed; // in position units per millisecond
	[ProtoMember(4, AsReference = true)] public readonly Player player;
	[ProtoMember(5, AsReference = true)] public List<Segment> segments; // composition of the path over time, more recent segments are later in list
	[ProtoMember(6, AsReference = true)] public List<Move> moves; // how path moved over time, more recent moves are later in list
	[ProtoMember(10)] public int nSeeUnits; // max # units that should be on this path when it's seen by another player
	[ProtoMember(7)] public int tileX; // current position on visibility tiles
	[ProtoMember(8)] public int tileY;
	[ProtoMember(9)] public long timeSimPast; // time traveling simulation time if made in the past, otherwise set to long.MaxValue
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	private Path() { }

	public Path(Sim simVal, int idVal, long speedVal, Player playerVal, List<Unit> units, long startTime, FP.Vector startPos, bool startUnseen, int nSeeUnitsVal) {
		g = simVal;
		id = idVal;
		speed = speedVal;
		player = playerVal;
		if (!g.stackAllowed (units, speed, player)) throw new ArgumentException("specified units may not be on the same path");
		segments = new List<Segment> {
			new Segment(this, 0, startTime, units, startUnseen)
		};
		moves = new List<Move> {
			new Move(startTime, startPos)
		};
		nSeeUnits = nSeeUnitsVal;
		tileX = Sim.OffMap + 1;
		tileY = Sim.OffMap + 1;
		timeSimPast = (startTime >= g.timeSim) ? long.MaxValue : startTime;
	}
	
	public Path(Sim simVal, int idVal, List<Unit> units, long startTime, FP.Vector startPos)
		: this(simVal, idVal, units[0].type.speed, units[0].player, units,
		startTime, startPos, simVal.tileAt(startPos).exclusiveWhen(units[0].player, startTime), int.MaxValue) {
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
		// ISSUE #26: without modifications, line below may cause syncing problems in multiplayer b/c addTileMoveEvts() sometimes adds events before timeSimPast
		addTileMoveEvts(ref pastEvents, timeSimPast, timeSimPastNext);
		evt = (TileMoveEvt)pastEvents.pop();
		exclusiveIndex = g.tiles[tX, tY].exclusiveIndexWhen(player, (evt != null) ? evt.time - 1 : curTime);
		if (!g.tiles[tX, tY].exclusiveWhen(player, (evt != null) ? evt.time - 1 : curTime)
			|| g.tiles[tX, tY].exclusive[player.id][exclusiveIndex] > timeSimPast) {
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
					|| (exclusiveIndex + 1 < g.tiles[tX, tY].exclusive[player.id].Count() && g.tiles[tX, tY].exclusive[player.id][exclusiveIndex + 1] <= Math.Min(g.events.peekTime(), timeSimPastNext))) {
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
		if (timeMax < moves[0].timeStart) return;
		int moveLast = Math.Max (0, activeMove(timeMin));
		int move = activeMove(timeMax);
		FP.Vector pos, posLast;
		int dir;
		for (int i = moveLast; i <= move; i++) {
			posLast = (i == moveLast) ? moves[i].calcPos(Math.Max(timeMin, moves[0].timeStart)) : moves[i].vecStart;
			pos = (i == move) ? moves[i].calcPos(timeMax) : moves[i + 1].vecStart;
			// moving between columns (x)
			dir = (pos.x >= posLast.x) ? 0 : -1;
			for (int tX = (int)(Math.Min(pos.x, posLast.x) >> FP.Precision) + 1; tX <= (int)(Math.Max(pos.x, posLast.x) >> FP.Precision); tX++) {
				events.add(new TileMoveEvt(moves[i].timeAtX(tX << FP.Precision), id, tX + dir, int.MinValue));
			}
			// moving between rows (y)
			dir = (pos.y >= posLast.y) ? 0 : -1;
			for (int tY = (int)(Math.Min(pos.y, posLast.y) >> FP.Precision) + 1; tY <= (int)(Math.Max(pos.y, posLast.y) >> FP.Precision); tY++) {
				events.add(new TileMoveEvt(moves[i].timeAtY(tY << FP.Precision), id, int.MinValue, tY + dir));
			}
		}
	}

	/// <summary>
	/// let path be updated in the present (i.e., stop time traveling) starting at timeSim
	/// </summary>
	public void goLive() {
		timeSimPast = long.MaxValue;
		FP.Vector pos = calcPos(g.timeSim);
		g.events.add(new TileMoveEvt(g.timeSim, id, (int)(pos.x >> FP.Precision), (int)(pos.y >> FP.Precision)));
		if (!g.movedPaths.Contains(id)) g.movedPaths.Add(id); // indicate to delete and recalculate later TileMoveEvts for this path
	}

	public void beUnseen(long time) {
		insertSegment(time).unseen = true;
	}

	public void beSeen(long time) {
		Segment segment = insertSegment(time);
		for (int i = segment.units.Count - 1; i >= 0 && segment.units.Count > nSeeUnits; i--) {
			new SegmentUnit(segment, segment.units[i]).delete ();
		}
		nSeeUnits = int.MaxValue;
		if (!g.deleteOtherPaths (segment.segmentUnits())) throw new SystemException("failed to delete other paths of seen path");
		segment.unseen = false;
	}

	/// <summary>
	/// makes a new path containing specified units, returns whether successful
	/// </summary>
	public bool makePath(long time, List<Unit> units) {
		if (canMakePath(time, units)) {
			Segment segment = insertSegment (time);
			FP.Vector pos = calcPos (time);
			g.paths.Add (new Path(g, g.paths.Count, units[0].type.speed, player, units, time, pos, segment.unseen, nSeeUnits));
			connect (time, g.paths.Last ());
			if (timeSimPast != long.MaxValue) g.paths.Last ().timeSimPast = time;
			if (g.paths.Last ().timeSimPast == long.MaxValue) {
				g.events.add(new TileMoveEvt(time, g.paths.Count - 1, (int)(pos.x >> FP.Precision), (int)(pos.y >> FP.Precision)));
			}
			else {
				player.hasNonLivePaths = true;
			}
			player.calcUnitCombinations ();
			return true;
		}
		return false;
	}

	/// <summary>
	/// returns whether this path can make a new path as specified
	/// </summary>
	public bool canMakePath(long time, List<Unit> units) {
		if (units.Count == 0) return false;
		Segment segment = activeSegment(time);
		if (segment == null) return false;
		long[] rscCost = new long[g.rscNames.Length];
		foreach (Unit unit in units) {
			if (segment.units.Contains (unit)) {
				// unit in path would be child path
				SegmentUnit segmentUnit = new SegmentUnit(segment, unit);
				// check parent made before (not at same time as) child, so it's unambiguous who is the parent
				if (!segmentUnit.canBeUnambiguousParent (time)) return false;
				// check parent unit won't be seen later
				if (!segmentUnit.unseenAfter ()) return false;
			}
			else {
				if (!canMakeUnitType (time, unit.type)) return false;
				// unit in path would be non-path child unit
				for (int i = 0; i < g.rscNames.Length; i++) {
					rscCost[i] += unit.type.rscCost[i];
				}
			}
		}
		bool newPathIsLive = (time >= g.timeSim && timeSimPast == long.MaxValue);
		if (!newPathIsLive && !Sim.EnableNonLivePaths) return false;
		for (int i = 0; i < g.rscNames.Length; i++) {
			// TODO: may be more permissive by passing in max = true, but this really complicates SegmentUnit.delete() algorithm (see planning notes)
			if (player.resource(time, i, false, !newPathIsLive) < rscCost[i]) return false;
		}
		return true;
	}
	
	public bool canMakeUnitType(long time, UnitType type) {
		Segment segment = activeSegment (time);
		if (segment != null) {
			foreach (SegmentUnit segmentUnit in segment.segmentUnits ()) {
				if (segmentUnit.unit.type.canMake[type.id] && segmentUnit.canBeUnambiguousParent (time)
					&& (time >= g.timeSim || segmentUnit.unseenAfter ())) {
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>
	/// connects this path to specified path at specified time,
	/// returns this path's segment where the paths were connected
	/// </summary>
	public Segment connect(long time, Path path) {
		Segment segment = insertSegment (time);
		Segment segment2 = path.insertSegment (time);
		if (!segment.branches.Contains (segment2)) {
			segment.branches.AddRange (segment2.branches);
			segment2.branches = segment.branches;
		}
		return segment;
	}
	
	/// <summary>
	/// move towards specified location starting at specified time,
	/// return moved path (in case moving a subset of units in path)
	/// </summary>
	public Path moveTo(long time, List<Unit> units, FP.Vector pos) {
		Path path2 = this; // move this path by default
		if (time < g.timeSim) {
			// move non-live path if in past
			// if this path already isn't live, a better approach is removing later segments and moves then moving this path, like pre-stacking versions
			// ISSUE #27: this fails if moving non-live unit immediately when it's made (b/c parent is ambiguous), or moving non-live unit when resources are negative
			if (!makePath (time, units)) throw new SystemException("make non-live path failed when moving units");
			path2 = g.paths.Last ();
		}
		else {
			foreach (Unit unit in activeSegment(time).units) {
				if (!units.Contains (unit)) {
					// some units in path aren't being moved, so make a new path
					if (!makePath (time, units)) throw new SystemException("make new path failed when moving units");
					path2 = g.paths.Last ();
					break;
				}
			}
		}
		if (path2 != this && (path2.timeSimPast == long.MaxValue || timeSimPast != long.MaxValue)) {
			// new path will be moved, so try to remove units that will move from this path
			Segment segment = activeSegment (time);
			foreach (Unit unit in units) {
				new SegmentUnit(segment, unit).delete ();
			}
		}
		path2.moveTo (time, pos);
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
		// ISSUE #32: moving unit is incorrectly disallowed when another unit on same segment is seen later (maybe make overloaded version that also checks units)
		if (time < moves[0].timeStart || speed <= 0) return false;
		if (time < g.timeSim) {
			Segment segment = activeSegment (time);
			if (segment == null || !Sim.EnableNonLivePaths) return false;
			foreach (SegmentUnit segmentUnit in segment.segmentUnits ()) {
				if (!segmentUnit.unseenAfter ()) return false;
			}
		}
		return true;
	}
	
	/// <summary>
	/// inserts a segment starting at specified time if no segment already starts at that time,
	/// returns that segment
	/// </summary>
	public Segment insertSegment(long time) {
		Segment segment = activeSegment (time);
		if (segment != null && segment.timeStart == time) return segment;
		segments.Insert (segment.id + 1, new Segment(this, segment.id + 1, time, new List<Unit>(segment.units), segment.unseen));
		for (int i = segment.id + 2; i < segments.Count; i++) {
			segments[i].id = i;
		}
		return segments[segment.id + 1];
	}
	
	/// <summary>
	/// returns segment that is active at specified time
	/// </summary>
	public Segment activeSegment(long time) {
		for (int i = segments.Count - 1; i >= 0; i--) {
			if (time >= segments[i].timeStart) return segments[i];
		}
		return null;
	}

	/// <summary>
	/// returns location at specified time
	/// </summary>
	public FP.Vector calcPos(long time) {
		return moves[activeMove(time)].calcPos(time);
	}

	/// <summary>
	/// returns index of move that is occurring at specified time
	/// </summary>
	public int activeMove(long time) {
		int ret = moves.Count - 1;
		while (ret >= 0 && time < moves[ret].timeStart) ret--;
		return ret;
	}

	/// <summary>
	/// returns minimum absolute position where clicking would select the path
	/// </summary>
	public FP.Vector selMinPos(long time) {
		FP.Vector ret = new FP.Vector(int.MaxValue, int.MaxValue);
		foreach (Unit unit in activeSegment(time).units) {
			ret.x = Math.Min (ret.x, unit.type.selMinPos.x);
			ret.y = Math.Min (ret.y, unit.type.selMinPos.y);
		}
		return ret + calcPos(time);
	}
	
	/// <summary>
	/// returns maximum absolute position where clicking would select the path
	/// </summary>
	public FP.Vector selMaxPos(long time) {
		FP.Vector ret = new FP.Vector(int.MinValue, int.MinValue);
		foreach (Unit unit in activeSegment(time).units) {
			ret.x = Math.Max (ret.x, unit.type.selMaxPos.x);
			ret.y = Math.Max (ret.y, unit.type.selMaxPos.y);
		}
		return ret + calcPos(time);
	}
	
	/// <summary>
	/// returns minimum distance that paths branching off from this path should move away
	/// </summary>
	public long makePathMinDist(long time, List<Unit> units) {
		long ret = 0;
		foreach (Unit unit in activeSegment (time).units) {
			if (units.Contains (unit) && unit.type.makePathMinDist > ret) {
				ret = unit.type.makePathMinDist;
			}
		}
		return ret;
	}
	
	/// <summary>
	/// returns maximum distance that paths branching off from this path should move away
	/// </summary>
	public long makePathMaxDist(long time, List<Unit> units) {
		long ret = 0;
		foreach (Unit unit in activeSegment (time).units) {
			if (units.Contains (unit) && unit.type.makePathMaxDist > ret) {
				ret = unit.type.makePathMaxDist;
			}
		}
		return ret;
	}
}
