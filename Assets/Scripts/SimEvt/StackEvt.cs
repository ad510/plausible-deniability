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
/// event to stack multiple paths together if they are at exactly the same place
/// </summary>
[ProtoContract]
public class StackEvt : SimEvt {
	[ProtoMember(1)] public int[] paths;
	[ProtoMember(2)] public int nSeeUnits;
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	private StackEvt() { }
	
	public StackEvt(long timeVal, int[] pathsVal, int nSeeUnitsVal) {
		time = timeVal;
		paths = pathsVal;
		nSeeUnits = nSeeUnitsVal;
	}
	
	public override void apply (Sim g) {
		bool[] pathsStacked = new bool[paths.Length];
		// only stack paths that contain units at this time
		for (int i = 0; i < pathsStacked.Length; i++) {
			Segment segment = g.paths[paths[i]].activeSegment (time);
			pathsStacked[i] = (segment == null || segment.units.Count == 0);
		}
		// loop through each pair of unstacked paths
		for (int i = 0; i < paths.Length; i++) {
			if (!pathsStacked[i]) {
				FP.Vector iPos = g.paths[paths[i]].calcPos (time);
				Segment iSegment = g.paths[paths[i]].activeSegment (time);
				for (int j = i + 1; j < paths.Length; j++) {
					if (!pathsStacked[j]) {
						FP.Vector jPos = g.paths[paths[j]].calcPos (time);
						Segment jSegment = g.paths[paths[j]].activeSegment (time);
						// check that paths are at same position
						if (iPos.x == jPos.x && iPos.y == jPos.y) {
							// check whether allowed to stack the paths' units together
							List<Unit> stackUnits = iSegment.units.Union (jSegment.units).ToList ();
							if (g.stackAllowed (stackUnits, g.paths[paths[i]].speed, g.paths[paths[i]].player)) {
								// merge the paths onto path i
								iSegment = g.paths[paths[i]].connect (time, g.paths[paths[j]]);
								jSegment = g.paths[paths[j]].activeSegment (time);
								iSegment.units = stackUnits;
								jSegment.removeAllUnits ();
								g.paths[paths[i]].nSeeUnits = nSeeUnits;
								pathsStacked[i] = true;
								pathsStacked[j] = true;
							}
						}
					}
				}
			}
		}
	}
}
