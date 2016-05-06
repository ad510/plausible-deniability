// Written in 2013 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

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
					if (g.paths.Last ().canMove (timeCmd + 1)) { // move at timeCmd + 1 to avoid failing canBeAmbiguousParent() for non-live paths
						g.paths.Last ().moveToDirect (timeCmd + 1, pos); // move new unit out of the way
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
				if (g.unitsCanMake (path.Value, g.unitT[type]) && path.Key.canMove (timeCmd, path.Value)
					&& (movePath == null || (path.Key.calcPos(timeCmd) - pos).lengthSq() < (movePath.calcPos(timeCmd) - pos).lengthSq())) {
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
				movePath = movePath.moveTo(timeCmd, new List<Unit>(exPaths[movePath]), pos);
				if (!evtPaths.ContainsKey (movePath.id)) {
					// replacement path is moving to make the unit
					evtPaths[movePath.id] = new int[movePath.segments[0].units.Count];
					for (int i = 0; i < movePath.segments[0].units.Count; i++) {
						evtPaths[movePath.id][i] = movePath.segments[0].units[i].id;
					}
				}
				long evtTime = Math.Max (timeCmd, movePath.moves.Last ().timeEnd);
				g.events.add(new MakeUnitCmdEvt(evtTime, evtTime, evtPaths, type, pos, true));
			}
		}
	}
}
