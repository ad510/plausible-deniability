// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

public enum Formation : byte { Tight, Loose, Ring };

/// <summary>
/// command to move unit(s)
/// </summary>
[ProtoContract]
public class MoveCmdEvt : UnitCmdEvt {
	[ProtoMember(1)]
	public FP.Vector pos {get;set;} // where to move to
	[ProtoMember(2)]
	public Formation formation {get;set;}
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	private MoveCmdEvt() { }

	public MoveCmdEvt(long timeVal, long timeCmdVal, Dictionary<int, int[]> pathsVal, FP.Vector posVal, Formation formationVal)
		: base(timeVal, timeCmdVal, pathsVal) {
		pos = posVal;
		formation = formationVal;
	}

	public override void apply(Sim g) {
		Dictionary<Path, List<Unit>> exPaths = existingPaths (g);
		FP.Vector goalCenter, goal, rows = new FP.Vector(), offset = new FP.Vector();
		long spacing = 0;
		int count = 0, i = 0;
		// count number of units able to move
		foreach (KeyValuePair<Path, List<Unit>> path in exPaths) {
			if (path.Key.canMove(timeCmd)) {
				count++;
				if (formation == Formation.Tight) {
					// calculate spacing for tight formation
					foreach (Unit unit in path.Value) {
						if (unit.type.tightFormationSpacing > spacing) spacing = unit.type.tightFormationSpacing;
					}
				}
			}
		}
		if (count == 0) return;
		// calculate spacing
		// (if tight formation, then spacing was already calculated above)
		// TODO: loose formation should be triangular
		if (formation == Formation.Loose) {
			spacing = FP.mul(g.visRadius, FP.Sqrt2) >> FP.Precision << FP.Precision;
		}
		else if (formation == Formation.Ring) {
			spacing = (g.visRadius * 2 >> FP.Precision) - 1 << FP.Precision;
		}
		if (formation == Formation.Tight || formation == Formation.Loose) {
			rows.x = FP.sqrt(count);
			rows.y = (count - 1) / rows.x + 1;
			offset = (rows - new FP.Vector(1, 1)) * spacing / 2;
		}
		else if (formation == Formation.Ring) {
			offset.x = (count == 1) ? 0 : FP.div(spacing / 2, FP.sin(FP.Pi / count));
			offset.y = offset.x;
		}
		else {
			throw new NotImplementedException("requested formation is not implemented");
		}
		goalCenter = pos;
		if (goalCenter.x < Math.Min(offset.x, g.mapSize / 2)) goalCenter.x = Math.Min(offset.x, g.mapSize / 2);
		if (goalCenter.x > g.mapSize - Math.Min(offset.x, g.mapSize / 2)) goalCenter.x = g.mapSize - Math.Min(offset.x, g.mapSize / 2);
		if (goalCenter.y < Math.Min(offset.y, g.mapSize / 2)) goalCenter.y = Math.Min(offset.y, g.mapSize / 2);
		if (goalCenter.y > g.mapSize - Math.Min(offset.y, g.mapSize / 2)) goalCenter.y = g.mapSize - Math.Min(offset.y, g.mapSize / 2);
		// move units
		foreach (KeyValuePair<Path, List<Unit>> path in exPaths) {
			if (path.Key.canMove(timeCmd)) {
				if (formation == Formation.Tight || formation == Formation.Loose) {
					goal = goalCenter + new FP.Vector((i % rows.x) * spacing - offset.x, i / rows.x * spacing - offset.y);
				}
				else if (formation == Formation.Ring) {
					goal = goalCenter + offset.x * new FP.Vector(FP.cos(2 * FP.Pi * i / count), FP.sin(2 * FP.Pi * i / count));
				}
				else {
					throw new NotImplementedException("requested formation is not implemented");
				}
				path.Key.moveTo(timeCmd, new List<Unit>(path.Value), goal);
				i++;
			}
		}
	}
}
