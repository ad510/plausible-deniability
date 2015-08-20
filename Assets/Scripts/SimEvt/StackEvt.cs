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
	[ProtoMember(1, AsReference = true)] public List<Path> paths;
	[ProtoMember(2)] public int nSeeUnits;
	
	private StackEvt() { } // for protobuf-net use only
	
	public StackEvt(long timeVal, List<Path> pathsVal, int nSeeUnitsVal) {
		time = timeVal;
		paths = pathsVal;
		nSeeUnits = nSeeUnitsVal;
	}
	
	public override void apply (Sim g) {
		bool[] pathsStacked = new bool[paths.Count];
		// only stack paths that contain units at this time
		for (int i = 0; i < paths.Count; i++) {
			paths[i].updatePast (time, true);
			Segment segment = paths[i].segments.Last ();
			pathsStacked[i] = (time < segment.timeStart || segment.units.Count == 0 || (time < g.timeSim && !segment.unseen));
		}
		// loop through each pair of unstacked paths
		for (int i = 0; i < paths.Count; i++) {
			if (!pathsStacked[i]) {
				FP.Vector iPos = paths[i].posWhen (time);
				Segment iSegment = paths[i].segments.Last ();
				for (int j = i + 1; j < paths.Count; j++) {
					// check whether allowed to stack these paths
					if (!pathsStacked[j] && (paths[i].timeSimPast == long.MaxValue) == (paths[j].timeSimPast == long.MaxValue)) {
						FP.Vector jPos = paths[j].posWhen (time);
						Segment jSegment = paths[j].segments.Last ();
						if (iPos.x == jPos.x && iPos.y == jPos.y) {
							List<Unit> stackUnits = iSegment.units.Union (jSegment.units).ToList ();
							if (stackUnits.Find (u => u.type.speed != paths[i].speed || u.player != paths[i].player) == null) {
								// merge the paths onto path i
								iSegment = paths[i].connect (time, paths[j]);
								jSegment = paths[j].segmentWhen (time);
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
