// Written in 2014 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

[ProtoContract]
public class WaypointAddEvt : SimEvt {
	[ProtoMember(1, AsReference = true)] public Unit unit;
	[ProtoMember(2, AsReference = true)] public Tile tile;
	[ProtoMember(3, AsReference = true)] public Waypoint prev;
	[ProtoMember(4)] public List<UnitSelection> start;
	
	private WaypointAddEvt() { } // for protobuf-net use only
	
	public WaypointAddEvt(long timeVal, Unit unitVal, Tile tileVal, Waypoint prevVal, List<UnitSelection> startVal) {
		time = timeVal;
		unit = unitVal;
		tile = tileVal;
		prev = prevVal;
		start = startVal;
	}
	
	public override void apply (Sim g) {
		if (tile.exclusiveLatest (unit.player) && !Waypoint.active (tile.waypointLatest (unit))
		    && ((prev != null && prev == prev.tile.waypointLatest (unit))
		        || (start != null && start.Last().segment().units.Contains(unit) && start.Last().segmentUnit().unseenAfter(start.Last().time)))) {
			// add waypoint to specified tile
			Waypoint waypoint = tile.waypointAdd (unit, time, prev, start);
			// add events to add waypoints to surrounding tiles
			for (int tX = Math.Max (0, tile.x - 1); tX <= Math.Min (g.tileLen () - 1, tile.x + 1); tX++) {
				for (int tY = Math.Max (0, tile.y - 1); tY <= Math.Min (g.tileLen () - 1, tile.y + 1); tY++) {
					if (tX != tile.x || tY != tile.y) {
						g.events.addEvt (new WaypointAddEvt(time + new FP.Vector(tX - tile.x << FP.precision, tY - tile.y << FP.precision).length() / unit.type.speed,
							unit, g.tiles[tX, tY], waypoint, null));
					}
				}
			}
		}
	}
}
