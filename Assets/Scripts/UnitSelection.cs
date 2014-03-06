// Copyright (c) 2014 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class UnitSelection {
	public readonly Path path;
	public readonly Unit unit;
	public readonly long time;
	
	public UnitSelection(Path pathVal, Unit unitVal, long timeVal) {
		path = pathVal;
		unit = unitVal;
		time = timeVal;
	}
	
	public static List<UnitSelection> fromPathsDict(Dictionary<Path, List<Unit>> pathsDict, long time) {
		List<UnitSelection> ret = new List<UnitSelection>();
		foreach (KeyValuePair<Path, List<Unit>> path in pathsDict) {
			foreach (Unit unit in path.Value) {
				ret.Add (new UnitSelection(path.Key, unit, time));
			}
		}
		return ret;
	}
	
	public static Dictionary<Path, List<Unit>> pathsDict(IEnumerable<UnitSelection> units, long time) {
		return Sim.inactiveSegmentUnitsToActivePathsDict (inactiveSegmentUnits (units), time);
	}
	
	public static IEnumerable<SegmentUnit> inactiveSegmentUnits(IEnumerable<UnitSelection> units) {
		foreach (UnitSelection selection in units) {
			Segment segment = selection.path.activeSegment (selection.time);
			if (segment.units.Contains (selection.unit)) yield return new SegmentUnit(segment, selection.unit);
		}
	}
}
