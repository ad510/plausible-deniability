// Copyright (c) 2014 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

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
