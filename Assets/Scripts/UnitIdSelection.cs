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
public struct UnitIdSelection {
	[ProtoMember(1)] public readonly int path;
	[ProtoMember(2)] public readonly int unit;
	[ProtoMember(3)] public readonly long time;
	
	public UnitIdSelection(int pathVal, int unitVal, long timeVal) {
		path = pathVal;
		unit = unitVal;
		time = timeVal;
	}
	
	public UnitIdSelection(UnitSelection unitSelection)
		: this(unitSelection.path.id, unitSelection.unit.id, unitSelection.time) {
	}
	
	public static Dictionary<Path, List<Unit>> pathsDict(Sim g, IEnumerable<UnitIdSelection> units, long time) {
		return Sim.inactiveSegmentUnitsToActivePathsDict (inactiveSegmentUnits (g, units), time);
	}
	
	public static IEnumerable<SegmentUnit> inactiveSegmentUnits(Sim g, IEnumerable<UnitIdSelection> units) {
		foreach (UnitIdSelection selection in units) {
			Segment segment = g.paths[selection.path].activeSegment (selection.time);
			if (segment.units.Contains (g.units[selection.unit])) yield return new SegmentUnit(segment, g.units[selection.unit]);
		}
	}
}
