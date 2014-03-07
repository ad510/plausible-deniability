// Copyright (c) 2013-2014 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

/// <summary>
/// top-level game simulation class (instances of this are often named "g")
/// </summary>
[ProtoContract]
public class Sim {
	// constants
	public const bool EnableNonLivePaths = true;
	public const int OffMap = -10000; // don't set to int.MinValue so doesn't overflow in inVis()
	public const int CompUser = -1;

	// general simulation parameters
	[ProtoMember(1)] public long mapSize;
	[ProtoMember(2)] public long updateInterval;
	[ProtoMember(3)] public long visRadius;

	// camera properties
	[ProtoMember(4)] public FP.Vector camPos;
	[ProtoMember(5)] public long camSpeed; // in position units per millisecond
	[ProtoMember(6)] public float zoom; // size of simulation length unit relative to diagonal length of screen
	[ProtoMember(7)] public float zoomMin;
	[ProtoMember(8)] public float zoomMax;
	[ProtoMember(9)] public float zoomSpeed;
	[ProtoMember(10)] public float zoomMouseWheelSpeed;

	// UI properties
	[ProtoMember(11)] public float uiBarHeight; // height of UI bar relative to screen height
	[ProtoMember(12)] public Vector2 healthBarSize; // size of health bar relative to diagonal length of screen
	[ProtoMember(13)] public float healthBarYOffset; // how high to draw center of health bar above top of selectable part of unit

	// colors
	[ProtoMember(14)] public Color backCol;
	[ProtoMember(15)] public Color borderCol;
	[ProtoMember(16)] public Color noVisCol;
	[ProtoMember(17)] public Color playerVisCol;
	[ProtoMember(18)] public Color unitVisCol;
	[ProtoMember(19)] public Color exclusiveCol;
	[ProtoMember(20)] public Color pathCol;
	[ProtoMember(21)] public Color healthBarBackCol;
	[ProtoMember(22)] public Color healthBarFullCol;
	[ProtoMember(23)] public Color healthBarEmptyCol;

	// core game objects
	public User[] users;
	[ProtoMember(25)] public string[] rscNames;
	[ProtoMember(26, AsReference = true)] public Player[] players;
	[ProtoMember(27, AsReference = true)] public UnitType[] unitT;
	[ProtoMember(28, AsReference = true)] public List<Unit> units;
	[ProtoMember(29, AsReference = true)] public List<Path> paths;

	// helper variables not loaded from scenario file
	[ProtoMember(30)] public int selUser;
	public NetworkView networkView; // to do RPCs in multiplayer (set to null in single player)
	public Tile[,] tiles; // each tile is 1 fixed-point unit (2^FP.Precision raw integer units) wide, so bit shift by FP.Precision to convert between position and tile position
	[ProtoMember(31)] private Tile[] protoTiles;
	public FP.Vector lastUnseenTile;
	[ProtoMember(32)] public SimEvtList events; // simulation events to be applied
	[ProtoMember(33)] public SimEvtList cmdPending; // user commands to be sent to other users in the next update
	[ProtoMember(34)] public SimEvtList cmdHistory; // user commands that have already been applied
	public List<int> movedPaths; // indices of paths that moved in the latest simulation event, invalidating later TileMoveEvts for that path
	[ProtoMember(36)] public int nRootPaths; // number of paths that don't have a parent (because they were defined in scenario file); these are all at beginning of paths list
	[ProtoMember(37)] public long maxSpeed; // speed of fastest unit (is max speed that players can gain or lose visibility)
	[ProtoMember(42)] public List<MoveLine> deleteLines;
	[ProtoMember(43)] public List<MoveLine> keepLines;
	[ProtoMember(38)] public int checksum; // sent to other users during each UpdateEvt to check for multiplayer desyncs
	[ProtoMember(39)] public bool synced; // whether all checksums between users matched so far
	[ProtoMember(40)] public long timeSim; // current simulation time
	[ProtoMember(41)] public long timeUpdateEvt; // last time that an UpdateEvt was applied
	
	[ProtoBeforeSerialization]
	private void beforeSerialize() {
		foreach (Path path in paths) {
			foreach (Segment segment in path.segments) {
				if (segment.branches != null) {
					foreach (Segment segment2 in segment.branches) {
						if (segment != segment2) segment2.branches = null;
					}
				}
			}
		}
		protoTiles = new Tile[tileLen () * tileLen ()];
		for (int tX = 0; tX < tileLen (); tX++) {
			for (int tY = 0; tY < tileLen (); tY++) {
				protoTiles[tX * tileLen () + tY] = tiles[tX, tY];
			}
		}
	}
	
	[ProtoAfterSerialization]
	private void afterSerialize() {
		foreach (Path path in paths) {
			foreach (Segment segment in path.segments) {
				if (segment.branches != null) {
					foreach (Segment segment2 in segment.branches) {
						if (segment2.branches == null) segment2.branches = segment.branches;
					}
				}
			}
		}
		protoTiles = null;
	}
	
	[ProtoAfterDeserialization]
	private void afterDeserialize() {
		if (rscNames == null) rscNames = new string[0];
		if (players == null) players = new Player[0];
		if (unitT == null) unitT = new UnitType[0];
		if (units == null) units = new List<Unit>();
		if (paths == null) paths = new List<Path>();
		tiles = new Tile[tileLen (), tileLen ()];
		for (int i = 0; i < protoTiles.Length; i++) {
			protoTiles[i].afterSimDeserialize ();
			tiles[i / tileLen (), i % tileLen ()] = protoTiles[i];
		}
		if (deleteLines == null) deleteLines = new List<MoveLine>();
		if (keepLines == null) keepLines = new List<MoveLine>();
		afterSerialize ();
	}

	/// <summary>
	/// master update method which updates the live game simulation to the specified time
	/// </summary>
	/// <remarks>this doesn't update time traveling units, must call updatePast() separately for each player</remarks>
	public void update(long curTime) {
		SimEvt evt;
		long timeSimNext = Math.Max(curTime, timeSim);
		if (networkView == null) {
			// move pending user commands to event list (single player only)
			// TODO: could command be applied after another event with same time, causing desyncs in replays?
			while ((evt = cmdPending.pop ()) != null) {
				events.add (evt);
				cmdHistory.add (evt);
			}
		}
		// apply simulation events
		movedPaths = new List<int>();
		while (events.peekTime() <= timeSimNext) {
			evt = events.pop();
			timeSim = evt.time;
			evt.apply(this);
			// if event caused path(s) to move, delete and recalculate later events moving them between tiles
			if (movedPaths.Count > 0) {
				for (int i = 0; i < events.events.Count; i++) {
					if (events.events[i] is TileMoveEvt && events.events[i].time > timeSim && movedPaths.Contains((events.events[i] as TileMoveEvt).path)) {
						events.events.RemoveAt(i);
						i--;
					}
				}
				foreach (int path in movedPaths) {
					if (paths[path].timeSimPast == long.MaxValue) paths[path].addTileMoveEvts(ref events, timeSim, timeUpdateEvt + updateInterval);
				}
				movedPaths.Clear();
			}
			checksum++;
		}
		// update simulation time
		timeSim = timeSimNext;
	}
	
	/// <summary>
	/// removes units from all other paths that, if seen, could cause specified units to be removed from specified segments;
	/// returns whether successful
	/// </summary>
	public bool deleteOtherPaths(IEnumerable<SegmentUnit> segmentUnits, bool addMoveLines = false) {
		HashSet<SegmentUnit> ancestors = new HashSet<SegmentUnit>();
		HashSet<SegmentUnit> prev = new HashSet<SegmentUnit>();
		bool success = true;
		bool deleted = false;
		foreach (SegmentUnit segmentUnit in segmentUnits) {
			addAncestors (segmentUnit, ancestors, prev);
		}
		foreach (SegmentUnit ancestor in prev) {
			foreach (SegmentUnit segmentUnit in ancestor.next ()) {
				if (!ancestors.Contains (segmentUnit)) {
					success &= segmentUnit.delete ();
					deleted = true;
				}
			}
		}
		if (addMoveLines && deleted) {
			// add kept unit lines
			// TODO: tweak time if deleted in past
			MoveLine keepLine = new MoveLine(timeSim, segmentUnits.First ().unit.player);
			foreach (SegmentUnit ancestor in ancestors) {
				if (segmentUnits.Where (u => u.unit == ancestor.unit).Any ()) {
					keepLine.vertices.AddRange (ancestor.segment.path.moveLines (ancestor.segment.timeStart,
						(ancestor.segment.nextOnPath () == null) ? keepLine.time : ancestor.segment.nextOnPath().timeStart));
				}
			}
			keepLines.Add (keepLine);
		}
		return success;
	}
	
	private void addAncestors(SegmentUnit segmentUnit, HashSet<SegmentUnit> ancestors, HashSet<SegmentUnit> prev) {
		ancestors.Add (segmentUnit);
		foreach (SegmentUnit prevSegment in segmentUnit.prev ()) {
			if (prevSegment.segment.path.timeSimPast == long.MaxValue || prevSegment.segment.path.timeSimPast != long.MaxValue) {
				prev.Add (prevSegment);
			}
			addAncestors (prevSegment, ancestors, prev);
		}
		foreach (SegmentUnit parent in segmentUnit.parents ()) {
			addAncestors (parent, ancestors, prev);
		}
	}
	
	/// <summary>
	/// adds events to stack specified paths as they arrive
	/// </summary>
	public void addStackEvts(List<int> movedPaths2, int nSeeUnits) {
		if (movedPaths2.Count > 1) {
			foreach (int path in movedPaths2) {
				// in most cases only 1 path will stack onto stackPath,
				// but request to stack all moved paths anyway in case the path they're stacking onto moves away
				events.add (new StackEvt(paths[path].moves.Last ().timeEnd, movedPaths2.ToArray (), nSeeUnits));
			}
		}
	}
	
	public bool unitsCanMake(List<Unit> parentUnits, UnitType type) {
		foreach (Unit unit in parentUnits) {
			if (unit.type.canMake[type.id]) return true;
		}
		return false;
	}
	
	/// <summary>
	/// returns whether the specified units are allowed to be on the same path
	/// </summary>
	public bool stackAllowed(List<Unit> stackUnits, long speed, Player player) {
		if (stackUnits.Count == 0) return true;
		foreach (Unit unit in stackUnits) {
			if (unit.type.speed != speed || unit.player != player) {
				return false;
			}
		}
		return true;
	}
	
	/// <summary>
	/// iterates over all SegmentUnits active at specified time that are
	/// past, present, or future versions of specified SegmentUnits
	/// </summary>
	public IEnumerable<SegmentUnit> activeSegmentUnits(IEnumerable<SegmentUnit> segmentUnits, long time) {
		foreach (SegmentUnit segmentUnit in segmentUnits) {
			if (segmentUnit.segment.nextOnPath () != null && time >= segmentUnit.segment.nextOnPath ().timeStart) {
				foreach (SegmentUnit segmentUnit2 in activeSegmentUnits(segmentUnit.next (), time)) {
					yield return segmentUnit2;
				}
			}
			else if (time < segmentUnit.segment.timeStart) {
				foreach (SegmentUnit segmentUnit2 in activeSegmentUnits(segmentUnit.prev (), time)) {
					yield return segmentUnit2;
				}
			}
			else {
				yield return segmentUnit;
			}
		}
	}
	
	/// <summary>
	/// iterates over all path segments that are active at specified time
	/// </summary>
	public IEnumerable<Segment> activeSegments(long time) {
		foreach (Path path in paths) {
			Segment segment = path.activeSegment (time);
			if (segment != null && segment.units.Count > 0) yield return segment;
		}
	}

	/// <summary>
	/// returns if a hypothetical unit at the origin could see tile with specified (positive or negative) x and y indices
	/// </summary>
	public bool inVis(long tX, long tY) {
		//return Math.Max(Math.Abs(tX), Math.Abs(tY)) <= (int)(g.visRadius >> FP.Precision);
		return (tX << FP.Precision) * (tX << FP.Precision) + (tY << FP.Precision) * (tY << FP.Precision) <= visRadius * visRadius;
	}

	public int tileVisRadius() {
		return (int)(visRadius >> FP.Precision); // adding "+ 1" to this actually doesn't make a difference
	}

	public Tile tileAt(FP.Vector pos) {
		return tiles[pos.x >> FP.Precision, pos.y >> FP.Precision];
	}

	public int tileLen() { // when fixing ISSUE #31, use tiles.GetUpperBound instead of this function
		return (int)((mapSize >> FP.Precision) + 1);
	}

	/// <summary>
	/// returns index of resource with specified name, or -1 if no such resource
	/// </summary>
	public int resourceNamed(string name) {
		for (int i = 0; i < rscNames.Length; i++) {
			if (name == rscNames[i]) return i;
		}
		return -1;
	}

	/// <summary>
	/// returns player with specified name, or null if no such player
	/// </summary>
	public Player playerNamed(string name) {
		foreach (Player player in players) {
			if (name == player.name) return player;
		}
		return null;
	}

	/// <summary>
	/// returns unit type with specified name, or null if no such unit type
	/// </summary>
	public UnitType unitTypeNamed(string name) {
		foreach (UnitType type in unitT) {
			if (name == type.name) return type;
		}
		return null;
	}
}
