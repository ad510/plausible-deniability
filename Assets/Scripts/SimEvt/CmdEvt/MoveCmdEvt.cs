// Written in 2013-2014 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

public enum Formation : byte { Tight, Loose, Ring };

[ProtoContract]
public class MoveCmdEvt : UnitCmdEvt {
	[ProtoMember(1)] public FP.Vector pos; // where to move to
	[ProtoMember(2)] public Formation formation;
	[ProtoMember(3)] public bool autoTimeTravel;
	
	private MoveCmdEvt() { } // for protobuf-net use only

	public MoveCmdEvt(long timeVal, long timeCmdVal, Dictionary<int, int[]> pathsVal, FP.Vector posVal, Formation formationVal, bool autoTimeTravelVal)
		: base(timeVal, timeCmdVal, pathsVal) {
		pos = posVal;
		formation = formationVal;
		autoTimeTravel = autoTimeTravelVal;
	}

	public override void apply(Sim g) {
		Dictionary<Path, List<Unit>> exPaths = existingPaths (g);
		List<List<Path>> formationOrder = new List<List<Path>>();
		List<int> nSeeUnits = new List<int>();
		FP.Vector goal, rows = new FP.Vector(), offset = new FP.Vector();
		long spacing = 0;
		// order paths in formation
		foreach (KeyValuePair<Path, List<Unit>> path in exPaths) {
			if (path.Key.canMove(timeCmd, path.Value)) {
				int i;
				for (i = 0; i < formationOrder.Count; i++) {
					if (path.Key.moves.Last ().vecEnd.x == formationOrder[i][0].moves.Last ().vecEnd.x
						&& path.Key.moves.Last ().vecEnd.y == formationOrder[i][0].moves.Last ().vecEnd.y) {
						SimEvt evt = g.events.FindLast (e => e is StackEvt && (e as StackEvt).paths.Contains (path.Key)
							&& formationOrder[i].Find (p => (e as StackEvt).paths.Contains (p)) != null);
						if (evt != null) {
							// path is trying to stack onto another commanded path
							formationOrder[i].Add (path.Key);
							nSeeUnits[i] = (evt as StackEvt).nSeeUnits;
							break;
						}
					}
				}
				if (i == formationOrder.Count) {
					// path isn't trying to stack onto another commanded path
					formationOrder.Add (new List<Path> { path.Key });
					nSeeUnits.Add (int.MaxValue);
				}
				if (formation == Formation.Tight) {
					// calculate spacing for tight formation
					foreach (Unit unit in path.Value) {
						if (unit.type.tightFormationSpacing > spacing) spacing = unit.type.tightFormationSpacing;
					}
				}
			}
		}
		if (formationOrder.Count == 0) return;
		// calculate spacing
		// (if tight formation, then spacing was already calculated above)
		// ISSUE #28: loose formation should be triangular
		if (formation == Formation.Loose) {
			spacing = FP.mul(g.visRadius, FP.sqrt2) >> FP.precision << FP.precision;
		} else if (formation == Formation.Ring) {
			spacing = (g.visRadius * 2 >> FP.precision) - 1 << FP.precision;
		}
		if (formation == Formation.Tight || formation == Formation.Loose) {
			rows.x = FP.sqrt(formationOrder.Count);
			rows.y = (formationOrder.Count - 1) / rows.x + 1;
			offset = (rows - new FP.Vector(1, 1)) * spacing / 2;
		} else if (formation == Formation.Ring) {
			offset.x = (formationOrder.Count == 1) ? 0 : FP.div(spacing / 2, FP.sin(FP.pi / formationOrder.Count));
			offset.y = offset.x;
		} else {
			throw new NotImplementedException("requested formation is not implemented");
		}
		if (pos.x < Math.Min(offset.x, g.mapSize / 2)) pos.x = Math.Min(offset.x, g.mapSize / 2);
		if (pos.x > g.mapSize - Math.Min(offset.x, g.mapSize / 2)) pos.x = g.mapSize - Math.Min(offset.x, g.mapSize / 2);
		if (pos.y < Math.Min(offset.y, g.mapSize / 2)) pos.y = Math.Min(offset.y, g.mapSize / 2);
		if (pos.y > g.mapSize - Math.Min(offset.y, g.mapSize / 2)) pos.y = g.mapSize - Math.Min(offset.y, g.mapSize / 2);
		// move units
		for (int i = 0; i < formationOrder.Count; i++) {
			List<Path> movedPaths = new List<Path>();
			foreach (Path path in formationOrder[i]) {
				if (formation == Formation.Tight || formation == Formation.Loose) {
					goal = pos + new FP.Vector((i % rows.x) * spacing - offset.x, i / rows.x * spacing - offset.y);
				} else if (formation == Formation.Ring) {
					goal = pos + offset.x * new FP.Vector(FP.cos(2 * FP.pi * i / formationOrder.Count), FP.sin(2 * FP.pi * i / formationOrder.Count));
				} else {
					throw new NotImplementedException("requested formation is not implemented");
				}
				movedPaths.Add (path.moveTo(timeCmd, new List<Unit>(exPaths[path]), goal, autoTimeTravel));
			}
			g.addStackEvts (movedPaths, nSeeUnits[i]);
		}
	}
}
