// Written in 2014 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

[ProtoContract]
public class Waypoint {
	[ProtoMember(1)] public readonly long time;
	[ProtoMember(2, AsReference = true)] public readonly Tile tile;
	[ProtoMember(3, AsReference = true)] public readonly Waypoint prev;
	[ProtoMember(4)] public readonly List<UnitSelection> start; // in reverse time order
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	private Waypoint() { }
	
	public Waypoint(long timeVal, Tile tileVal, Waypoint prevVal, List<UnitSelection> startVal) {
		time = timeVal;
		tile = tileVal;
		prev = prevVal;
		start = startVal;
	}
	
	public static bool active(Waypoint waypoint) {
		return waypoint != null && (waypoint.prev != null || waypoint.start != null);
	}
}
