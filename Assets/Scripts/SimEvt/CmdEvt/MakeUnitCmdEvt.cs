// Copyright (c) 2013-2014 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

[ProtoContract]
public class MakeUnitCmdEvt : UnitCmdEvt {
	[ProtoMember(1)] public int type;
	[ProtoMember(2)] public FP.Vector pos;
	[ProtoMember(3)] bool autoTimeTravel;
	[ProtoMember(4)] bool autoRepeat;
	
	private MakeUnitCmdEvt() { } // for protobuf-net use only

	public MakeUnitCmdEvt(long timeVal, long timeCmdVal, Dictionary<int, int[]> pathsVal, int typeVal, FP.Vector posVal, bool autoTimeTravelVal, bool autoRepeatVal = false)
		: base(timeVal, timeCmdVal, pathsVal) {
		type = typeVal;
		pos = posVal;
		autoTimeTravel = autoTimeTravelVal;
		autoRepeat = autoRepeatVal;
	}

	public override void apply(Sim g) {
		Dictionary<Path, List<Unit>> exPaths = existingPaths (g);
		// make unit at requested position, if possible
		foreach (Path path in exPaths.Keys) {
			FP.Vector curPos = path.posWhen(timeCmd);
			if ((pos.x == curPos.x && pos.y == curPos.y) || (g.unitT[type].speed > 0 && g.unitT[type].makeOnUnitT == null)) {
				// TODO: take time to make units?
				Unit unit = new Unit(g, g.units.Count, g.unitT[type], path.player);
				g.units.Add (unit);
				if (path.makePath (timeCmd, new List<Unit> { unit })) {
					if (g.paths.Last ().canMove (timeCmd + 1)) { // move at timeCmd + 1 to avoid failing canBeAmbiguousParent() for non-live paths
						g.paths.Last ().moveToDirect (timeCmd + 1, pos); // move new unit out of the way
					}
				} else {
					g.units.RemoveAt (g.units.Count - 1);
				}
				return;
			}
		}
		if (!autoRepeat) {
			// if none of specified paths are at requested position,
			// try moving one to the correct position then trying again to make the unit
			Path movePath = null;
			foreach (KeyValuePair<Path, List<Unit>> path in exPaths) {
				if (g.unitsCanMake (path.Value, g.unitT[type]) && path.Key.canMove (timeCmd, path.Value)
					&& (movePath == null || (path.Key.posWhen(timeCmd) - pos).lengthSq() < (movePath.posWhen(timeCmd) - pos).lengthSq())) {
					bool newPathIsLive = (timeCmd >= g.timeSim && path.Key.timeSimPast == long.MaxValue);
					int i;
					for (i = 0; i < g.rscNames.Length; i++) {
						if (path.Key.player.resource(timeCmd, i, !newPathIsLive) < g.unitT[type].rscCost[i]) break;
					}
					if (i == g.rscNames.Length) movePath = path.Key;
				}
			}
			if (movePath != null) {
				Dictionary<int, int[]> evtPaths = new Dictionary<int, int[]>(paths);
				movePath = movePath.moveTo(timeCmd, new List<Unit>(exPaths[movePath]), pos, autoTimeTravel);
				if (!evtPaths.ContainsKey (movePath.id)) {
					// replacement path is moving to make the unit
					evtPaths[movePath.id] = new int[movePath.segments[0].units.Count];
					for (int i = 0; i < movePath.segments[0].units.Count; i++) {
						evtPaths[movePath.id][i] = movePath.segments[0].units[i].id;
					}
				}
				long evtTime = Math.Max (timeCmd, movePath.moves.Last ().timeEnd);
				g.events.addEvt(new MakeUnitCmdEvt(evtTime, evtTime, evtPaths, type, pos, autoTimeTravel, true));
			}
		}
	}
}
