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
/// identity of a unit
/// </summary>
/// <remarks>how unit moves is stored in Path class, not here</remarks>
[ProtoContract]
public class Unit {
	[ProtoMember(1, AsReference = true)] public readonly Sim g;
	[ProtoMember(2)] public readonly int id; // index in unit list
	[ProtoMember(3, AsReference = true)] public readonly UnitType type;
	[ProtoMember(4, AsReference = true)] public readonly Player player;
	[ProtoMember(5)] public int nTimeHealth;
	[ProtoMember(6)] public long[] timeHealth; // times at which each health increment is removed
	[ProtoMember(7)] public List<Attack> attacks;
	
	/// <summary>
	/// parameterless constructor for protobuf-net use only
	/// </summary>
	private Unit() {
		attacks = new List<Attack>();
	}

	public Unit(Sim simVal, int idVal, UnitType typeVal, Player playerVal) {
		g = simVal;
		id = idVal;
		type = typeVal;
		player = playerVal;
		nTimeHealth = 0;
		timeHealth = new long[type.maxHealth];
		attacks = new List<Attack>();
	}
	
	/// <summary>
	/// attack every applicable unit in target segment
	/// </summary>
	public void attack(long time, Segment target) {
		bool tookHealth = false;
		foreach (Unit targetUnit in target.units) {
			// take health with 1 ms delay so earlier units in array don't have unfair advantage
			for (int i = 0; i < type.damage[targetUnit.type.id]; i++) targetUnit.takeHealth (time + 1, target.path);
			tookHealth = true;
		}
		if (tookHealth) {
			attacks.Add(new Attack(time, target.path));
			g.deleteOtherPaths (from segment in g.activeSegments (time)
				where segment.units.Contains (this) && (target.path.calcPos (time) - segment.path.calcPos (time)).lengthSq () <= type.range * type.range
				select new SegmentUnit(segment, this),
				true, false);
			// TODO: ideally would just remove all waypoints that are out of range, but non-live path validation is currently too dumb to handle that
			clearWaypoints(time);
			foreach (Segment segment in g.activeSegments (time)) {
				if (segment.units.Contains (this)) addWaypoint(time, segment.path);
			}
		}
	}

	/// <summary>
	/// remove 1 health increment at specified time
	/// </summary>
	public void takeHealth(long time, Path path) {
		if (nTimeHealth < type.maxHealth) {
			nTimeHealth++;
			timeHealth[nTimeHealth - 1] = time;
			if (nTimeHealth >= type.maxHealth) {
				// unit lost all health, so remove it from path
				Segment segment = path.insertSegment(time);
				segment.units.Remove (this);
			}
		}
	}

	/// <summary>
	/// returns health of this unit at latest possible time
	/// </summary>
	public int healthLatest() {
		return type.maxHealth - nTimeHealth;
	}

	/// <summary>
	/// returns health of this unit at specified time
	/// </summary>
	public int healthWhen(long time) {
		int i = nTimeHealth;
		while (i > 0 && time < timeHealth[i - 1]) i--;
		return type.maxHealth - i;
	}
	
	public void clearWaypoints(long time) {
		foreach (Tile tile in g.tiles) {
			tile.waypoints.Remove (id);
		}
	}
	
	public void addWaypoint (long time, Path path) {
		if (type.speed > 0) {
			Tile tile = path.activeTile (time);
			if (!Waypoint.active (tile.waypointLatest (this))) {
				List<UnitSelection> start = new List<UnitSelection> { new UnitSelection(path, this, time) };
				SegmentUnit prev = new SegmentUnit(path.activeSegment (time), this);
				do {
					// remember segments that unit was previously on in case unit is later removed from them
					SegmentUnit cur = prev;
					prev = cur.prev ().FirstOrDefault ();
					if (prev.g == null || prev.segment.path != cur.segment.path || !prev.segment.unseen) {
						start.Add (new UnitSelection(cur.segment.path, this, cur.segment.timeStart));
					}
				} while (prev.g != null && prev.segment.unseen);
				g.events.add (new WaypointAddEvt(time + (tile.centerPos() - path.calcPos(time)).length () / type.speed,
					this, tile, null, start));
			}
		}
	}
}
