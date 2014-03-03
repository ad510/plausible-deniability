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
/// base class for unit commands
/// </summary>
[ProtoContract]
[ProtoInclude(10, typeof(MoveCmdEvt))]
[ProtoInclude(11, typeof(MakeUnitCmdEvt))]
[ProtoInclude(12, typeof(MakePathCmdEvt))]
[ProtoInclude(13, typeof(DeletePathCmdEvt))]
[ProtoInclude(14, typeof(DeleteOtherPathsCmdEvt))]
[ProtoInclude(15, typeof(StackCmdEvt))]
[ProtoInclude(16, typeof(SharePathsCmdEvt))]
public abstract class UnitCmdEvt : CmdEvt {
	[ProtoMember(1)] public long timeCmd; // time is latest simulation time when command is given, timeCmd is when event takes place (may be in past)
	[ProtoMember(2)] public Dictionary<int, int[]> paths; // key is path index, value is list of unit indices
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	protected UnitCmdEvt() { }

	protected UnitCmdEvt(long timeVal, long timeCmdVal, Dictionary<int, int[]> pathsVal) {
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
	
	/// <summary>
	/// returns commanded paths and units that exist at timeCmd
	/// </summary>
	protected Dictionary<Path, List<Unit>> existingPaths(Sim g) {
		Dictionary<Path, List<Unit>> ret = new Dictionary<Path, List<Unit>>();
		foreach (KeyValuePair<int, int[]> path in paths) {
			Segment segment = g.paths[path.Key].activeSegment (timeCmd);
			if (segment != null) {
				List<Unit> existingUnits = new List<Unit>();
				foreach (int unit in path.Value) {
					if (segment.units.Contains (g.units[unit])) {
						if (!existingUnits.Contains (g.units[unit])) existingUnits.Add (g.units[unit]);
					}
				}
				if (existingUnits.Count > 0) ret.Add (g.paths[path.Key], existingUnits);
			}
		}
		return ret;
	}
	
	public static Dictionary<int, int[]> argFromPathDict(Dictionary<Path, List<Unit>> paths) {
		Dictionary<int, int[]> ret = new Dictionary<int, int[]>();
		foreach (KeyValuePair<Path, List<Unit>> path in paths) {
			ret[path.Key.id] = new int[path.Value.Count];
			for (int i = 0; i < path.Value.Count; i++) {
				ret[path.Key.id][i] = path.Value[i].id;
			}
		}
		return ret;
	}
}
