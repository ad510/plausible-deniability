// Written in 2014 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

/// <summary>
/// command to attack a path
/// </summary>
/// <remarks>this event always takes place in the present (time == timeCmd)</remarks>
[ProtoContract]
public class AttackCmdEvt : UnitCmdEvt {
	[ProtoMember(1)] int target; // path to attack
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	private AttackCmdEvt() { }
	
	public AttackCmdEvt(long timeVal, long timeCmdVal, Dictionary<int, int[]> pathsVal, int targetVal)
		: base(timeVal, timeCmdVal, pathsVal) {
		target = targetVal;
	}
	
	public override void apply (Sim g) {
		if (time != timeCmd) throw new InvalidOperationException("AttackCmdEvt must take place in the present (time == timeCmd)");
		Dictionary<Path, List<Unit>> exPaths = existingPaths(g);
		Segment targetSegment = g.paths[target].activeSegment (timeCmd);
		if (targetSegment == null || targetSegment.unseen || g.paths[target].timeSimPast != long.MaxValue) return;
		Tile targetTile = g.paths[target].activeTile (timeCmd);
		FP.Vector targetPos = g.paths[target].calcPos (timeCmd);
		foreach (KeyValuePair<Path, List<Unit>> path in exPaths) {
			if (path.Key.timeSimPast == long.MaxValue && path.Key.id != target && path.Key.player.mayAttack[g.paths[target].player.id]
				&& targetTile.playerDirectVisLatest (path.Key.player)) {
				long distSq = (targetPos - path.Key.calcPos(timeCmd)).lengthSq ();
				foreach (Unit unit in path.Value) {
					if ((unit.attacks.Count == 0 || timeCmd >= unit.attacks.Last().time + unit.type.reload) && distSq <= unit.type.range * unit.type.range) {
						unit.attack (timeCmd, targetSegment);
					}
				}
			}
		}
	}
}
