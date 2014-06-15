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
/// command to remove units from all other paths that, if seen, could cause specified path(s) to disappear
/// </summary>
[ProtoContract]
public class DeleteOtherPathsCmdEvt : UnitCmdEvt {
	private DeleteOtherPathsCmdEvt() { } // for protobuf-net use only
	
	public DeleteOtherPathsCmdEvt(long timeVal, long timeCmdVal, Dictionary<int, int[]> pathsVal)
		: base(timeVal, timeCmdVal, pathsVal) { }
	
	public override void apply (Sim g) {
		List<SegmentUnit> units = new List<SegmentUnit>();
		// convert paths list into valid deleteOtherPaths() argument (this is a bit ugly)
		foreach (KeyValuePair<int, int[]> path in paths) {
			Segment segment = g.paths[path.Key].segmentWhen (timeCmd);
			if (segment != null) {
				foreach (int unit in path.Value) {
					units.Add (new SegmentUnit(segment, g.units[unit]));
				}
			}
		}
		g.deleteOtherPaths (units, true, false);
	}
}
