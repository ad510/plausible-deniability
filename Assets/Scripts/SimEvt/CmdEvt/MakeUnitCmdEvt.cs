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
/// command to make a new unit
/// </summary>
[ProtoContract]
public class MakeUnitCmdEvt : UnitCmdEvt {
	[ProtoMember(1)] public int type;
	[ProtoMember(2)] public FP.Vector pos;
	[ProtoMember(3)] bool autoRepeat;
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	private MakeUnitCmdEvt() { }

	public MakeUnitCmdEvt(long timeVal, long timeCmdVal, Dictionary<int, int[]> pathsVal, int typeVal, FP.Vector posVal, bool autoRepeatVal = false)
		: base(timeVal, timeCmdVal, pathsVal) {
		type = typeVal;
		pos = posVal;
		autoRepeat = autoRepeatVal;
	}

	public override void apply(Sim g) {
		Dictionary<Path, List<Unit>> exPaths = existingPaths (g);
		// make unit at requested position, if possible
		foreach (Path path in exPaths.Keys) {
			FP.Vector curPos = path.calcPos(timeCmd);
			if ((pos.x == curPos.x && pos.y == curPos.y) || (g.unitT[type].speed > 0 && g.unitT[type].makeOnUnitT == null)) {
				// TODO: take time to make units?
				List<Unit> unitList = new List<Unit>();
				Unit unit = new Unit(g, g.units.Count, g.unitT[type], path.player);
				g.units.Add (unit);
				unitList.Add (unit);
				if (path.makePath (timeCmd, unitList)) {
					if (g.paths.Last ().canMove (timeCmd)) {
						g.paths.Last ().moveTo (timeCmd, pos); // move new unit out of the way
					}
				}
				else {
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
				if (g.unitsCanMake (path.Value, g.unitT[type]) && path.Key.canMove (timeCmd)
					&& (movePath == null || (path.Key.calcPos(timeCmd) - pos).lengthSq() < (movePath.calcPos(timeCmd) - pos).lengthSq())) {
					bool newPathIsLive = (time >= g.timeSim && path.Key.timeSimPast == long.MaxValue);
					int i;
					for (i = 0; i < g.rscNames.Length; i++) {
						// TODO: may be more permissive by passing in max = true, but this really complicates removeUnit() algorithm (see planning notes)
						if (path.Key.player.resource(time, i, false, !newPathIsLive) < g.unitT[type].rscCost[i]) break;
					}
					if (i == g.rscNames.Length) movePath = path.Key;
				}
			}
			if (movePath != null) {
				Dictionary<int, int[]> evtPaths = new Dictionary<int, int[]>(paths);
				movePath = movePath.moveTo(timeCmd, new List<Unit>(exPaths[movePath]), pos);
				if (!evtPaths.ContainsKey (movePath.id)) {
					// replacement path is moving to make the unit
					evtPaths[movePath.id] = new int[movePath.segments[0].units.Count];
					for (int i = 0; i < movePath.segments[0].units.Count; i++) {
						evtPaths[movePath.id][i] = movePath.segments[0].units[i].id;
					}
				}
				g.events.add(new MakeUnitCmdEvt(movePath.moves.Last ().timeEnd, movePath.moves.Last ().timeEnd,
					evtPaths, type, pos, true));
			}
		}
	}
}
