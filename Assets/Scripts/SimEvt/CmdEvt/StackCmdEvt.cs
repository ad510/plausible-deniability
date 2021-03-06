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
/// command to stack specified path(s) onto another specified path
/// </summary>
[ProtoContract]
public class StackCmdEvt : UnitCmdEvt {
	[ProtoMember(1)] public int stackPath; // path that paths will be stacked onto
	[ProtoMember(2)] public bool autoTimeTravel;
	[ProtoMember(3)] public int nSeeUnits;
	
	private StackCmdEvt() { } // for protobuf-net use only
	
	public StackCmdEvt(long timeVal, long timeCmdVal, Dictionary<int, int[]> pathsVal, int stackPathVal, bool autoTimeTravelVal, int nSeeUnitsVal = int.MaxValue)
		: base(timeVal, timeCmdVal, pathsVal) {
		stackPath = stackPathVal;
		autoTimeTravel = autoTimeTravelVal;
		nSeeUnits = nSeeUnitsVal;
	}
	
	public override void apply (Sim g) {
		Dictionary<Path, List<Unit>> exPaths = existingPaths (g);
		List<Path> movedPaths = new List<Path> { g.paths[stackPath] };
		// move paths to final location of stackPath
		// related to ISSUE #20: if stackPathVal < 0 (pressing stack button will do that) then move all paths to their average location
		foreach (KeyValuePair<Path, List<Unit>> path in exPaths) {
			if (path.Key.speed == g.paths[stackPath].speed && path.Key.canMove (timeCmd, path.Value) && !movedPaths.Contains (path.Key)) {
				movedPaths.Add (path.Key.moveTo (timeCmd, new List<Unit>(path.Value), g.paths[stackPath].moves.Last ().vecEnd, autoTimeTravel));
			}
		}
		// if able to move any of the paths, add events to stack them as they arrive
		g.addStackEvts (movedPaths, nSeeUnits);
	}
}
