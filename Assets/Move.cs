// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// represents a single movement that starts at a specified location,
/// moves at constant velocity to a specified end location, then stops
/// </summary>
public class Move {
	public long timeStart; // time when starts moving
	public long timeEnd; // time when finishes moving
	public FP.Vector vecStart; // location at timeStart, z indicates rotation (TODO: implement rotation)
	public FP.Vector vecEnd; // location at timeEnd, z indicates rotation

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
		return new Move(timeStartVal, timeStartVal + new FP.Vector(vecEndVal - vecStartVal).length() / speed, vecStartVal, vecEndVal);
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
