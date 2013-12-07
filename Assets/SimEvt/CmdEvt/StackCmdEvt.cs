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
/// command to stack specified path(s) onto another specified path
/// </summary>
[ProtoContract]
public class StackCmdEvt : CmdEvt {
	[ProtoMember(1)]
	public int stackPath {get;set;} // path that paths will be stacked onto
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	private StackCmdEvt() { }
	
	public StackCmdEvt(long timeVal, long timeCmdVal, Dictionary<int, int[]> pathsVal, int stackPathVal)
		: base(timeVal, timeCmdVal, pathsVal) {
		stackPath = stackPathVal;
	}
	
	public override void apply (Sim g) {
		Dictionary<int, List<int>> exPaths = existingPaths (g);
		List<int> movedPaths = new List<int>();
		base.apply (g);
		// move paths to final location of stackPath
		// TODO: if stackPathVal < 0 (pressing stack button will do that) then move all paths to their average location
		foreach (KeyValuePair<int, List<int>> path in exPaths) {
			if (g.paths[path.Key].speed == g.paths[stackPath].speed && g.paths[path.Key].canMove (timeCmd)) {
				movedPaths.Add (g.paths[path.Key].moveTo (timeCmd, new List<int>(path.Value), g.paths[stackPath].moves.Last ().vecEnd));
			}
		}
		// if able to move any of the paths, add events to stack them as they arrive
		if (movedPaths.Count > 0) {
			if (!movedPaths.Contains (stackPath)) movedPaths.Add (stackPath);
			foreach (int path in movedPaths) {
				// in most cases only 1 path will stack onto stackPath,
				// but request to stack all moved paths anyway in case the path they're stacking onto moves away
				g.events.add (new StackEvt(g.paths[path].moves.Last ().timeEnd, movedPaths.ToArray ()));
			}
		}
	}
}
