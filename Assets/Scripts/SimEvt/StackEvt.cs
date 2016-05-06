// Written in 2013-2014 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

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
		for (int i = 0; i < paths.Length; i++) {
			g.paths[paths[i]].updatePast (time);
			Segment segment = g.paths[paths[i]].segments.Last ();
			pathsStacked[i] = (time < segment.timeStart || segment.units.Count == 0 || (time < g.timeSim && !segment.unseen));
		}
		// loop through each pair of unstacked paths
		for (int i = 0; i < paths.Length; i++) {
			if (!pathsStacked[i]) {
				FP.Vector iPos = g.paths[paths[i]].calcPos (time);
				Segment iSegment = g.paths[paths[i]].segments.Last ();
				for (int j = i + 1; j < paths.Length; j++) {
					if (!pathsStacked[j] && (g.paths[paths[i]].timeSimPast == long.MaxValue) == (g.paths[paths[j]].timeSimPast == long.MaxValue)) {
						FP.Vector jPos = g.paths[paths[j]].calcPos (time);
						Segment jSegment = g.paths[paths[j]].segments.Last ();
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
		// if in past, try again to stack paths that failed to stack after going live
		if (time < g.timeSim && pathsStacked.Where (b => !b).Any ()) {
			var goLiveStackPaths = g.paths[paths[0]].player.goLiveStackPaths;
			if (!goLiveStackPaths.ContainsKey (nSeeUnits)) goLiveStackPaths[nSeeUnits] = new HashSet<int>();
			foreach (int path in paths) {
				goLiveStackPaths[nSeeUnits].Add (path);
			}
		}
	}
}
