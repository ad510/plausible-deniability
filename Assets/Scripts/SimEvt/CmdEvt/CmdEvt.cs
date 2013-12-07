// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

/// <summary>
/// protobuf-net identifiers of each type of CmdEvt
/// </summary>
public enum CmdEvtTag {
	cmd = 10,
	move = 11,
	makeUnit = 12,
	makePath = 13,
	deletePath = 14,
	goLive = 15,
	stack = 16,
	deleteOtherPaths = 17,
}

/// <summary>
/// base class for unit commands
/// </summary>
[ProtoContract]
[ProtoInclude((int)CmdEvtTag.move, typeof(MoveCmdEvt))]
[ProtoInclude((int)CmdEvtTag.makeUnit, typeof(MakeUnitCmdEvt))]
[ProtoInclude((int)CmdEvtTag.makePath, typeof(MakePathCmdEvt))]
[ProtoInclude((int)CmdEvtTag.deletePath, typeof(DeletePathCmdEvt))]
[ProtoInclude((int)CmdEvtTag.deleteOtherPaths, typeof(DeleteOtherPathsCmdEvt))]
[ProtoInclude((int)CmdEvtTag.stack, typeof(StackCmdEvt))]
public abstract class CmdEvt : SimEvt {
	[ProtoMember(1)]
	public long timeCmd {get;set;} // time is latest simulation time when command is given, timeCmd is when event takes place (may be in past)
	[ProtoMember(2)]
	public Dictionary<int, int[]> paths {get;set;}
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	protected CmdEvt() { }

	protected CmdEvt(long timeVal, long timeCmdVal, Dictionary<int, int[]> pathsVal) {
		time = timeVal;
		timeCmd = timeCmdVal;
		paths = pathsVal;
	}
	
	[ProtoAfterDeserialization]
	protected void afterDeserialize() {
		if (paths == null) paths = new Dictionary<int, int[]>();
		foreach (int path in paths.Keys) {
			if (paths[path] == null) paths[path] = new int[0];
		}
	}

	public override void apply(Sim g) {
		g.cmdHistory.add(this); // copy event to command history list (it should've already been popped from event list)
	}
	
	/// <summary>
	/// returns commanded paths and units that exist at timeCmd
	/// </summary>
	protected Dictionary<int, List<int>> existingPaths(Sim g) {
		Dictionary<int, List<int>> ret = new Dictionary<int, List<int>>();
		foreach (KeyValuePair<int, int[]> path in paths) {
			int seg = g.paths[path.Key].getSegment (timeCmd);
			if (seg >= 0) {
				List<int> existingUnits = new List<int>();
				foreach (int unit in path.Value) {
					if (g.paths[path.Key].segments[seg].units.Contains (unit)) {
						if (!existingUnits.Contains (unit)) existingUnits.Add (unit);
					}
				}
				if (existingUnits.Count > 0) ret.Add (path.Key, existingUnits);
			}
		}
		return ret;
	}
}
