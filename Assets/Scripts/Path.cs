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
[ProtoContract(AsReferenceDefault = true)] // AsReferenceDefault needed for the way this is stored in Player.goLiveStackPaths
public class Path {
	[ProtoMember(1, AsReference = true)] public readonly Sim g;
	[ProtoMember(2)] public readonly int id; // index in path list
	[ProtoMember(3)] public readonly long speed; // in position units per millisecond
	[ProtoMember(4, AsReference = true)] public readonly Player player;
	[ProtoMember(5, AsReference = true)] public List<Segment> segments; // composition of the path over time, more recent segments are later in list
	[ProtoMember(6, AsReference = true)] public List<Move> moves; // how path moved over time, more recent moves are later in list
	[ProtoMember(10)] public int nSeeUnits; // max # units that should be on this path when it's seen by another player
	[ProtoMember(11)] public readonly int startTileX;
	[ProtoMember(12)] public readonly int startTileY;
	[ProtoMember(7)] public int tileX; // current position on visibility tiles
	[ProtoMember(8)] public int tileY;
	[ProtoMember(9)] public long timeSimPast; // time traveling simulation time if made in the past, otherwise set to long.MaxValue
	
	private Path() { } // for protobuf-net use only

	public Path(Sim simVal, int idVal, List<Unit> units, long startTime, FP.Vector startPos, bool startUnseen, int nSeeUnitsVal, int startTileXVal, int startTileYVal) {
		g = simVal;
		id = idVal;
		speed = units[0].type.speed;
		player = units[0].player;
		segments = new List<Segment> {
			new Segment(this, 0, startTime, units, startUnseen)
		};
		moves = new List<Move> {
			new Move(startTime, startPos)
		};
		nSeeUnits = nSeeUnitsVal;
		startTileX = startTileXVal;
		startTileY = startTileYVal;
		tileX = Sim.offMap + 1;
		tileY = Sim.offMap + 1;
		timeSimPast = (startTime >= g.timeSim) ? long.MaxValue : startTime / g.tileInterval * g.tileInterval;
	}

	public void updatePast(long curTime, bool validate) {
		while (timeSimPast <= Math.Min (curTime, g.timeSim) - g.tileInterval && segments.Last ().units.Count > 0) {
			int tXPrev, tYPrev;
			timeSimPast += g.tileInterval;
			updateTilePos (timeSimPast, out tXPrev, out tYPrev);
			if (validate && !g.tiles[tileX, tileY].exclusiveWhen(player, timeSimPast)) {
				segments.Last ().removeAllUnits (true);
			}
		}
	}
	
	public bool updateTilePos(long time, out int tXPrev, out int tYPrev) {
		if (time % g.tileInterval != 0) throw new ArgumentException("tile update time must be divisible by tileInterval");
		tXPrev = tileX;
		tYPrev = tileY;
		if (segments.Last ().units.Count == 0) {
			// path no longer contains units, so remove it from visibility tiles
			tileX = Sim.offMap;
		} else {
			Tile tile = tileWhen(time);
			tileX = tile.x;
			tileY = tile.y;
		}
		if (tileX == tXPrev && tileY == tYPrev) return false;
		// add path to visibility tiles
		for (int tX = Math.Max (0, tileX - g.tileVisRadius()); tX <= Math.Min (g.tileLen () - 1, tileX + g.tileVisRadius()); tX++) {
			for (int tY = Math.Max (0, tileY - g.tileVisRadius()); tY <= Math.Min (g.tileLen () - 1, tileY + g.tileVisRadius()); tY++) {
				if (!g.inVis(tX - tXPrev, tY - tYPrev) && g.inVis(tX - tileX, tY - tileY)) {
					if (g.tiles[tX, tY].pathVisLatest(this)) throw new InvalidOperationException("path " + id + " already sees tile (" + tX + ", " + tY + ")");
					g.tiles[tX, tY].pathVisToggle(this, time);
				}
			}
		}
		// remove path from visibility tiles
		for (int tX = Math.Max (0, tXPrev - g.tileVisRadius()); tX <= Math.Min (g.tileLen () - 1, tXPrev + g.tileVisRadius()); tX++) {
			for (int tY = Math.Max (0, tYPrev - g.tileVisRadius()); tY <= Math.Min (g.tileLen () - 1, tYPrev + g.tileVisRadius()); tY++) {
				if (g.inVis(tX - tXPrev, tY - tYPrev) && !g.inVis(tX - tileX, tY - tileY)) {
					if (!g.tiles[tX, tY].pathVisLatest(this)) throw new InvalidOperationException("path " + id + " already doesn't see tile (" + tX + ", " + tY + ")");
					g.tiles[tX, tY].pathVisToggle(this, time);
				}
			}
		}
		return true;
	}

	public bool makePath(long time, List<Unit> units, bool ignoreSeen = false) {
		bool costsRsc;
		if (!canMakePath (time, units, out costsRsc, ignoreSeen)) return false;
		int tXPrev, tYPrev;
		Segment segment = insertSegment (time);
		Tile tile = tileWhen(time);
		g.paths.Add (new Path(g, g.paths.Count, units, time, posWhen(time), segment.unseen, nSeeUnits, tile.x, tile.y));
		g.paths.Last().updateTilePos(time / g.tileInterval * g.tileInterval, out tXPrev, out tYPrev);
		connect (time, g.paths.Last ());
		if (timeSimPast != long.MaxValue) g.paths.Last ().timeSimPast = time / g.tileInterval * g.tileInterval;
		if (g.paths.Last ().timeSimPast != long.MaxValue) player.hasNonLivePaths = true;
		if (units.Find (u => !segment.units.Contains (u)) != null) {
			foreach (Unit unit in segment.units) {
				// TODO: only have to do this for units that made a new unit
				unit.clearWaypoints (time);
				unit.addWaypoint (time, this);
			}
		}
		if (costsRsc) g.deleteOtherPaths (g.paths.Last ().segments[0].segmentUnits (), false, true);
		return true;
	}
	
	private bool canMakePath(long time, List<Unit> units, out bool costsRsc, bool ignoreSeen = false) {
		costsRsc = false;
		if (units.Count == 0) return false;
		Segment segment = segmentWhen(time);
		if (segment == null) return false;
		int newUnitCount = 0;
		long[] rscCost = new long[g.rscNames.Length];
		foreach (Unit unit in units) {
			if (segment.units.Contains (unit)) {
				// unit in path already exists
				SegmentUnit segmentUnit = new SegmentUnit(segment, unit); // get parent SegmentUnit
				if (!segmentUnit.canBeUnambiguousParent (time)) return false;
				if (!ignoreSeen && !segmentUnit.unseenAfter (time)) return false;
				if (!segmentUnit.hasChildrenAfter ()) return false;
			} else if (!(time == segment.timeStart && new SegmentUnit(segment, unit).prev ().Any ())) {
				// unit in path would be new unit
				if (!canMakeUnitType (time, unit.type)) return false;
				if (unit.type.speed > 0) newUnitCount++;
				for (int i = 0; i < g.rscNames.Length; i++) {
					rscCost[i] += unit.type.rscCost[i];
				}
			}
		}
		bool newPathIsLive = (time >= g.timeSim && timeSimPast == long.MaxValue);
		if (!newPathIsLive && !Sim.enableNonLivePaths) return false;
		if (player.populationLimit >= 0 && player.population (time) + newUnitCount > player.populationLimit) return false;
		for (int i = 0; i < g.rscNames.Length; i++) {
			if (rscCost[i] > 0 && player.resource(time, i, !newPathIsLive) < rscCost[i]) return false;
		}
		costsRsc = rscCost.Where (r => r > 0).Any ();
		return true;
	}
	
	public bool canMakeUnitType(long time, UnitType type) {
		Segment segment = segmentWhen (time);
		if (segment != null) {
			foreach (SegmentUnit segmentUnit in segment.segmentUnits ()) {
				if (segmentUnit.unit.type.canMake[type.id] && segmentUnit.canBeUnambiguousParent (time)
					&& (time >= g.timeSim || segmentUnit.unseenAfter (time))) {
					return true;
				}
			}
		}
		return false;
	}

	/// <returns>this path's segment where the paths were connected</returns>
	public Segment connect(long time, Path path) {
		Segment segment = insertSegment (time);
		Segment segment2 = path.insertSegment (time);
		if (!segment.branches.Contains (segment2)) {
			segment.branches.AddRange (segment2.branches);
			segment2.branches = segment.branches;
		}
		return segment;
	}
	
	/// <returns>path that was moved (might not be original path)</returns>
	public Path moveTo(long time, List<Unit> units, FP.Vector pos, bool autoTimeTravel) {
		Path movedPath;
		FP.Vector goalPos = pos;
		// don't move off map edge
		if (goalPos.x < 0) goalPos.x = 0;
		if (goalPos.x > g.mapSize) goalPos.x = g.mapSize;
		if (goalPos.y < 0) goalPos.y = 0;
		if (goalPos.y > g.mapSize) goalPos.y = g.mapSize;
		if (autoTimeTravel && units.Find (u => {
			Waypoint waypoint = g.tileAt (goalPos).waypointWhen (u, time);
			return !Waypoint.active (waypoint) || Move.fromSpeed (waypoint.time, speed, waypoint.tile.centerPos (), goalPos).timeEnd > time;
		}) == null) {
			// move units with automatic time travel
			List<Path> movedPaths = new List<Path>();
			long stackTime = long.MinValue;
			foreach (Unit unit in units) {
				Waypoint waypoint = g.tileAt (goalPos).waypointWhen (unit, time);
				// make moves list using waypoints
				List<Move> waypointMoves = new List<Move> {
					Move.fromSpeed (waypoint.time, speed, waypoint.tile.centerPos (), goalPos)
				};
				while (waypoint.prev != null) {
					waypointMoves.Insert (0, new Move(waypoint.time - (waypointMoves[0].vecStart - waypoint.prev.tile.centerPos()).length () / speed,
						waypoint.time, waypoint.prev.tile.centerPos (), waypointMoves[0].vecStart));
					waypoint = waypoint.prev;
				}
				waypointMoves.Insert (0, new Move(waypoint.start[0].time, waypoint.time, waypoint.start[0].path.posWhen (waypoint.start[0].time), waypointMoves[0].vecStart));
				// do path smoothing
				for (int i = 0; i < waypointMoves.Count; i++) {
					int j;
					for (j = i + 1; j < waypointMoves.Count; j++) {
						Move move = Move.fromSpeed(waypointMoves[i].timeStart, speed, waypointMoves[i].vecStart, waypointMoves[j].vecEnd);
						long timeMove = (waypointMoves[i].timeStart / g.tileInterval + 1) * g.tileInterval;
						while (timeMove < waypointMoves[j].timeEnd && g.tileAt(move.posWhen(timeMove)).exclusiveWhen(player, timeMove)) {
							timeMove += g.tileInterval;
						}
						if (timeMove < waypointMoves[j].timeEnd) break;
					}
					j--;
					if (j > i) {
						waypointMoves[i] = Move.fromSpeed(waypointMoves[i].timeStart, speed, waypointMoves[i].vecStart, waypointMoves[j].vecEnd);
						waypointMoves.RemoveRange(i + 1, j - i);
					}
				}
				// if unit not found on start waypoint, add it back to past segments
				for (int i = 0; i < waypoint.start.Count - 1; i++) {
					Segment segment = waypoint.start[i + 1].path.insertSegment (waypoint.start[i].time);
					while (segment.timeStart != waypoint.start[i + 1].time) {
						segment = segment.prevOnPath ();
						if (segment.units.Contains (unit)) {
							i = waypoint.start.Count;
							break;
						}
						segment.units.Add (unit);
						segment.deletedUnits.Remove(unit);
					}
				}
				// make non-live path moving along waypoints
				if (!waypoint.start[0].segment().path.makePath (waypointMoves[0].timeStart, new List<Unit> { unit })) {
					throw new SystemException("make auto time travel path failed when moving units");
				}
				g.paths.Last ().moves = waypointMoves;
				g.paths.Last ().updatePast(time, false);
				// add kept unit line
				MoveLine keepLine = new MoveLine(time, player);
				keepLine.vertices.AddRange (g.paths.Last ().moveLines (waypointMoves[0].timeStart, time));
				g.keepLines.Add (keepLine);
				g.alternatePaths.Add (g.paths.Last ());
				movedPaths.Add (g.paths.Last ());
				stackTime = Math.Max (stackTime, waypointMoves.Last ().timeEnd);
			}
			player.updatePast (time);
			if (units.Count > 1) new StackEvt(stackTime, movedPaths, nSeeUnits).apply (g);
			movedPath = movedPaths.Find (p => p.segments.Last ().units.Count > 0);
		} else {
			// move units normally (without automatic time travel)
			movedPath = this; // move this path by default
			if (time < g.timeSim) {
				// move non-live path if in past
				// if this path already isn't live, a better approach might be removing later segments and moves then moving this path, like pre-stacking versions (see ISSUE #27)
				if (!makePath (time, units)) throw new SystemException("make non-live path failed when moving units");
				movedPath = g.paths.Last ();
			} else {
				foreach (Unit unit in segmentWhen(time).units) {
					if (!units.Contains (unit)) {
						// some units in path aren't being moved, so make a new path
						if (!makePath (time, units, true)) throw new SystemException("make new path failed when moving units");
						movedPath = g.paths.Last ();
						break;
					}
				}
			}
			movedPath.moveToDirect (time, pos);
			if (movedPath != this) g.alternatePaths.Add (movedPath);
		}
		if (movedPath != this && (movedPath.timeSimPast == long.MaxValue || timeSimPast != long.MaxValue)) {
			// new path was moved, so try to remove units that moved from current path
			Segment segment = segmentWhen (time);
			foreach (Unit unit in units) {
				new SegmentUnit(segment, unit).delete ();
			}
		}
		return movedPath;
	}

	public void moveToDirect(long time, FP.Vector pos) {
		FP.Vector curPos = posWhen(time);
		FP.Vector goalPos = pos;
		// don't move off map edge
		if (goalPos.x < 0) goalPos.x = 0;
		if (goalPos.x > g.mapSize) goalPos.x = g.mapSize;
		if (goalPos.y < 0) goalPos.y = 0;
		if (goalPos.y > g.mapSize) goalPos.y = g.mapSize;
		// add move
		moves.Add (Move.fromSpeed(time, speed, curPos, goalPos));
	}

	public bool canMove(long time, List<Unit> units = null) {
		if (time < moves[0].timeStart || speed <= 0) return false;
		if (time < g.timeSim) {
			if (!Sim.enableNonLivePaths) return false;
			if (units == null) {
				Segment segment = segmentWhen (time);
				if (segment == null) return false;
				units = segment.units;
			}
			bool temp;
			if (!canMakePath (time, units, out temp)) return false;
		}
		return true;
	}
	
	public IEnumerable<FP.Vector> moveLines(long timeStart, long timeEnd) {
		yield return posWhen (timeStart);
		for (int i = moveIndexWhen (timeStart) + 1; i <= moveIndexWhen (timeEnd); i++) {
			yield return moves[i].vecStart;
			yield return moves[i].vecStart;
		}
		yield return posWhen (timeEnd);
	}
	
	public Segment insertSegment(long time) {
		Segment segment = segmentWhen (time);
		if (segment != null && segment.timeStart == time) return segment;
		segments.Insert (segment.id + 1, new Segment(this, segment.id + 1, time, new List<Unit>(segment.units), segment.unseen));
		segments[segment.id + 1].deletedUnits = new List<Unit>(segment.deletedUnits);
		for (int i = segment.id + 2; i < segments.Count; i++) {
			segments[i].id = i;
		}
		return segments[segment.id + 1];
	}
	
	public Segment segmentWhen(long time) {
		for (int i = segments.Count - 1; i >= 0; i--) {
			if (time >= segments[i].timeStart) return segments[i];
		}
		return null;
	}
	
	/// <summary>
	/// returns tile that path is on at specified time
	/// </summary>
	public Tile tileWhen(long time) {
		long timeRounded = time / g.tileInterval * g.tileInterval;
		return (timeRounded >= moves[0].timeStart) ? g.tileAt(posWhen(timeRounded)) : g.tiles[startTileX, startTileY];
	}

	public FP.Vector posWhen(long time) {
		return moves[moveIndexWhen(time)].posWhen(time);
	}

	public int moveIndexWhen(long time) {
		int ret = moves.Count - 1;
		while (ret >= 0 && time < moves[ret].timeStart) ret--;
		return ret;
	}

	/// <summary>
	/// returns minimum absolute position where clicking would select the path
	/// </summary>
	public FP.Vector selMinPos(long time) {
		FP.Vector ret = new FP.Vector(int.MaxValue, int.MaxValue);
		foreach (Unit unit in segmentWhen(time).units) {
			ret.x = Math.Min (ret.x, unit.type.selMinPos.x);
			ret.y = Math.Min (ret.y, unit.type.selMinPos.y);
		}
		return ret + posWhen(time);
	}
	
	/// <summary>
	/// returns maximum absolute position where clicking would select the path
	/// </summary>
	public FP.Vector selMaxPos(long time) {
		FP.Vector ret = new FP.Vector(int.MinValue, int.MinValue);
		foreach (Unit unit in segmentWhen(time).units) {
			ret.x = Math.Max (ret.x, unit.type.selMaxPos.x);
			ret.y = Math.Max (ret.y, unit.type.selMaxPos.y);
		}
		return ret + posWhen(time);
	}
	
	/// <summary>
	/// returns minimum distance that paths branching off from this path should move away
	/// </summary>
	public long makePathMinDist(long time, List<Unit> units) {
		long ret = 0;
		foreach (Unit unit in segmentWhen (time).units) {
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
		foreach (Unit unit in segmentWhen (time).units) {
			if (units.Contains (unit) && unit.type.makePathMaxDist > ret) {
				ret = unit.type.makePathMaxDist;
			}
		}
		return ret;
	}
}
