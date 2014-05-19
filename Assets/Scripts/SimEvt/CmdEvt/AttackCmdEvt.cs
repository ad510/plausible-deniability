// Copyright (c) 2014 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

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
		if (targetSegment == null || g.paths[target].timeSimPast != long.MaxValue) return;
		FP.Vector targetPos = g.paths[target].calcPos (timeCmd);
		foreach (KeyValuePair<Path, List<Unit>> path in exPaths) {
			if (path.Key.timeSimPast == long.MaxValue && path.Key.id != target && path.Key.player.mayAttack[g.paths[target].player.id]
				&& g.tileAt (targetPos).playerDirectVisLatest (path.Key.player)) {
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
