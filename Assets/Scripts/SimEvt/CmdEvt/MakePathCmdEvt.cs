// Written in 2013 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

/// <summary>
/// command to make new path(s) that existing unit(s) could alternately move along
/// </summary>
[ProtoContract]
public class MakePathCmdEvt : UnitCmdEvt {
	[ProtoMember(1)] public Dictionary<int, FP.Vector> pos; // where new paths should move to
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	private MakePathCmdEvt() { }

	public MakePathCmdEvt(long timeVal, long timeCmdVal, Dictionary<int, int[]> pathsVal, Dictionary<int, FP.Vector> posVal)
		: base(timeVal, timeCmdVal, pathsVal) {
		pos = posVal;
	}

	public override void apply(Sim g) {
		Dictionary<Path, List<Unit>> exPaths = existingPaths (g);
		foreach (KeyValuePair<Path, List<Unit>> path in exPaths) {
			if (path.Key.canMove(timeCmd) && path.Key.makePath (timeCmd, new List<Unit>(path.Value))) {
				g.paths.Last ().moveTo(timeCmd, pos[path.Key.id]); // move new path out of the way
			}
		}
	}
}
