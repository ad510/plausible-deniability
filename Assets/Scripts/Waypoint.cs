// Copyright (c) 2014 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

[ProtoContract(AsReferenceDefault = true)] // AsReferenceDefault needed for the way this is stored in Tile
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
