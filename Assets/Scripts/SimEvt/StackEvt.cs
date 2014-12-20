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
	[ProtoMember(1, AsReference = true)] public List<Path> paths;
	[ProtoMember(2)] public int nSeeUnits;
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	private StackEvt() { }
	
	public StackEvt(long timeVal, List<Path> pathsVal, int nSeeUnitsVal) {
		time = timeVal;
		paths = pathsVal;
		nSeeUnits = nSeeUnitsVal;
	}
	
	public override void apply (Sim g) {
		bool[] pathsStacked = new bool[paths.Count];
		// only stack paths that contain units at this time
		for (int i = 0; i < paths.Count; i++) {
			paths[i].updatePast (time);
			Segment segment = paths[i].segments.Last ();
			pathsStacked[i] = (time < segment.timeStart || segment.units.Count == 0 || (time < g.timeSim && !segment.unseen));
		}
		// loop through each pair of unstacked paths
		for (int i = 0; i < paths.Count; i++) {
			if (!pathsStacked[i]) {
				FP.Vector iPos = paths[i].calcPos (time);
				Segment iSegment = paths[i].segments.Last ();
				for (int j = i + 1; j < paths.Count; j++) {
					if (!pathsStacked[j] && (paths[i].timeSimPast == long.MaxValue) == (paths[j].timeSimPast == long.MaxValue)) {
						FP.Vector jPos = paths[j].calcPos (time);
						Segment jSegment = paths[j].segments.Last ();
						// check that paths are at same position
						if (iPos.x == jPos.x && iPos.y == jPos.y) {
							// check whether allowed to stack the paths' units together
							List<Unit> stackUnits = iSegment.units.Union (jSegment.units).ToList ();
							if (g.stackAllowed (stackUnits, paths[i].speed, paths[i].player)) {
								// merge the paths onto path i
								iSegment = paths[i].connect (time, paths[j]);
								jSegment = paths[j].activeSegment (time);
								iSegment.units = stackUnits;
								jSegment.removeAllUnits ();
								paths[i].nSeeUnits = nSeeUnits;
								pathsStacked[i] = true;
								pathsStacked[j] = true;
							}
						}
					}
				}
			}
		}
		// if in past, try again to stack paths that failed to stack after going live
		if (time < g.timeSim && pathsStacked.Where (b => !b).Any ()) {
			var goLiveStackPaths = paths[0].player.goLiveStackPaths;
			if (!goLiveStackPaths.ContainsKey (nSeeUnits)) goLiveStackPaths[nSeeUnits] = new HashSet<Path>();
			foreach (Path path in paths) {
				goLiveStackPaths[nSeeUnits].Add (path);
			}
		}
	}
}
