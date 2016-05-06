// Written in 2013 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

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
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	private DeleteOtherPathsCmdEvt() { }
	
	public DeleteOtherPathsCmdEvt(long timeVal, long timeCmdVal, Dictionary<int, int[]> pathsVal)
		: base(timeVal, timeCmdVal, pathsVal) { }
	
	public override void apply (Sim g) {
		List<SegmentUnit> units = new List<SegmentUnit>();
		// convert paths list into valid deleteOtherPaths() argument (this is a bit ugly)
		foreach (KeyValuePair<int, int[]> path in paths) {
			Segment segment = g.paths[path.Key].activeSegment (timeCmd);
			if (segment != null) {
				foreach (int unit in path.Value) {
					units.Add (new SegmentUnit(segment, g.units[unit]));
				}
			}
		}
		g.deleteOtherPaths (units, true, false);
	}
}
