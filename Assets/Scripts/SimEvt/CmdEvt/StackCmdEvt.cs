// Written in 2013-2014 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

/// <summary>
/// command to stack specified path(s) onto another specified path
/// </summary>
[ProtoContract]
public class StackCmdEvt : UnitCmdEvt {
	[ProtoMember(1)] public int stackPath; // path that paths will be stacked onto
	[ProtoMember(2)] public int nSeeUnits;
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	private StackCmdEvt() { }
	
	public StackCmdEvt(long timeVal, long timeCmdVal, Dictionary<int, int[]> pathsVal, int stackPathVal, int nSeeUnitsVal = int.MaxValue)
		: base(timeVal, timeCmdVal, pathsVal) {
		stackPath = stackPathVal;
		nSeeUnits = nSeeUnitsVal;
	}
	
	public override void apply (Sim g) {
		Dictionary<Path, List<Unit>> exPaths = existingPaths (g);
		List<int> movedPaths = new List<int> { stackPath };
		// move paths to final location of stackPath
		// related to ISSUE #20: if stackPathVal < 0 (pressing stack button will do that) then move all paths to their average location
		foreach (KeyValuePair<Path, List<Unit>> path in exPaths) {
			if (path.Key.speed == g.paths[stackPath].speed && path.Key.canMove (timeCmd, path.Value) && !movedPaths.Contains (path.Key.id)) {
				movedPaths.Add (path.Key.moveTo (timeCmd, new List<Unit>(path.Value), g.paths[stackPath].moves.Last ().vecEnd).id);
			}
		}
		// if able to move any of the paths, add events to stack them as they arrive
		g.addStackEvts (movedPaths, nSeeUnits);
	}
}
