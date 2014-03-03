// Copyright (c) 2014 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

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
			new StackCmdEvt(time, timeCmd, argFromPathDict (movePaths), path.id, nSeeUnits).apply (g);
		}
	}
}
