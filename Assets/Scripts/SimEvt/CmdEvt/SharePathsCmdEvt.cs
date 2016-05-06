// Written in 2014 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

[ProtoContract]
public class SharePathsCmdEvt : UnitCmdEvt {
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	private SharePathsCmdEvt() { }
	
	public SharePathsCmdEvt(long timeVal, long timeCmdVal, Dictionary<int, int[]> pathsVal)
		: base(timeVal, timeCmdVal, pathsVal) { }
	
	public override void apply (Sim g) {
		Dictionary<Path, List<Unit>> exPaths = existingPaths (g);
		Dictionary<Path, List<Unit>> sharedUnits = new Dictionary<Path, List<Unit>>();
		int nSeeUnits = int.MaxValue;
		foreach (Path path in exPaths.Keys) {
			Segment segment = path.activeSegment (timeCmd);
			if (segment.units.Count < nSeeUnits) nSeeUnits = segment.units.Count;
			sharedUnits[path] = new List<Unit>(segment.units);
		}
		foreach (Path path in exPaths.Keys) {
			Dictionary<Path, List<Unit>> movePaths = new Dictionary<Path, List<Unit>>();
			foreach (KeyValuePair<Path, List<Unit>> path2 in exPaths) {
				if (path2.Key.speed == path.speed && path2.Key.canMove (timeCmd)) {
					List<Unit> moveUnits = path2.Value.FindAll (u => !sharedUnits[path].Contains (u));
					if (path2.Key.makePath (timeCmd, moveUnits)) {
						movePaths[g.paths.Last ()] = moveUnits;
						sharedUnits[path].AddRange (moveUnits);
					}
				}
			}
			new StackCmdEvt(time, timeCmd + 1, argFromPathDict (movePaths), path.id, nSeeUnits).apply (g);
		}
	}
}
