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
/// command to make new path(s) that existing unit(s) could alternately move along
/// </summary>
[ProtoContract]
public class MakePathCmdEvt : UnitCmdEvt {
	[ProtoMember(1)] public Dictionary<int, FP.Vector> pos {get;set;} // where new paths should move to
	
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
