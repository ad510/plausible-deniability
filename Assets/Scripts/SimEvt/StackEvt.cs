// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// event to stack multiple paths together if they are at exactly the same place
/// </summary>
public class StackEvt : SimEvt {
	public int[] paths;
	
	public StackEvt(long timeVal, int[] pathsVal) {
		time = timeVal;
		paths = pathsVal;
	}
	
	public override void apply (Sim g) {
		bool[] pathsStacked = new bool[paths.Length];
		for (int i = 0; i < pathsStacked.Length; i++) {
			pathsStacked[i] = (time < g.paths[paths[i]].moves[0].timeStart);
		}
		// loop through each pair of unstacked paths
		for (int i = 0; i < paths.Length; i++) {
			if (!pathsStacked[i]) {
				FP.Vector iPos = g.paths[paths[i]].calcPos (time);
				Segment iSegment = g.paths[paths[i]].activeSegment (time);
				for (int j = i + 1; j < paths.Length; j++) {
					FP.Vector jPos = g.paths[paths[j]].calcPos (time);
					Segment jSegment = g.paths[paths[j]].activeSegment (time);
					// check that paths are at same position
					if (iPos.x == jPos.x && iPos.y == jPos.y) {
						// check whether allowed to stack the paths' units together
						List<int> stackUnits = new List<int>(iSegment.units);
						foreach (int unit in jSegment.units) {
							if (!stackUnits.Contains (unit)) stackUnits.Add (unit);
						}
						if (g.stackAllowed (stackUnits, g.paths[paths[i]].speed, g.paths[paths[i]].player)) {
							// merge the paths onto path i
							iSegment = g.paths[paths[i]].connect (time, g.paths[paths[j]]);
							jSegment = g.paths[paths[j]].activeSegment (time);
							iSegment.units = stackUnits;
							jSegment.removeAllUnits ();
							pathsStacked[i] = true;
							pathsStacked[j] = true;
						}
					}
				}
			}
		}
	}
}
