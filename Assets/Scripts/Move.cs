// Written in 2013 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

/// <summary>
/// represents a single movement that starts at a specified location,
/// moves at constant velocity to a specified end location, then stops
/// </summary>
[ProtoContract]
public class Move {
	[ProtoMember(1)] public long timeStart; // time when starts moving
	[ProtoMember(2)] public long timeEnd; // time when finishes moving
	[ProtoMember(3)] public FP.Vector vecStart; // location at timeStart (if rotation is implemented, can store it in z value)
	[ProtoMember(4)] public FP.Vector vecEnd; // location at timeEnd
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	private Move() { }

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
	/// alternate method to create Move object that asks for speed (in position units per millisecond) instead of end time
	/// </summary>
	public static Move fromSpeed(long timeStartVal, long speed, FP.Vector vecStartVal, FP.Vector vecEndVal) {
		return new Move(timeStartVal, timeStartVal + (vecEndVal - vecStartVal).length() / speed, vecStartVal, vecEndVal);
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
