// Written in 2013-2014 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

[ProtoContract]
public class DeletePathCmdEvt : UnitCmdEvt {
	private DeletePathCmdEvt() { } // for protobuf-net use only

	public DeletePathCmdEvt(long timeVal, long timeCmdVal, Dictionary<int, int[]> pathsVal)
		: base(timeVal, timeCmdVal, pathsVal) { }

	public override void apply(Sim g) {
		Dictionary<Path, List<Unit>> exPaths = existingPaths (g);
		foreach (KeyValuePair<Path, List<Unit>> path in exPaths) {
			foreach (Unit unit in path.Value) {
				new SegmentUnit(path.Key.segmentWhen (timeCmd), unit).delete (true);
			}
		}
	}
}
