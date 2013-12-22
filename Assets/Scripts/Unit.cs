// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// identity of a unit
/// </summary>
/// <remarks>how unit moves is stored in Path class, not here</remarks>
public class Unit {
	public readonly Sim g;
	public readonly int id; // index in unit list
	public readonly UnitType type;
	public readonly Player player;
	public int nTimeHealth;
	public long[] timeHealth; // times at which each health increment is removed (TODO: change how this is stored to allow switching units on a path)
	public long timeAttack; // latest time that attacked a unit

	public Unit(Sim simVal, int idVal, UnitType typeVal, Player playerVal) {
		g = simVal;
		id = idVal;
		type = typeVal;
		player = playerVal;
		nTimeHealth = 0;
		timeHealth = new long[type.maxHealth];
		timeAttack = long.MinValue;
	}

	/// <summary>
	/// remove 1 health increment at specified time
	/// </summary>
	public void takeHealth(long time, Path path) {
		if (nTimeHealth < type.maxHealth) {
			nTimeHealth++;
			timeHealth[nTimeHealth - 1] = time;
			if (nTimeHealth >= type.maxHealth) {
				// unit lost all health, so remove it from path
				Segment segment = path.insertSegment(time);
				segment.units.Remove (this);
				// if path no longer has any units, remove it from visibility tiles
				if (segment.nextOnPath () == null && segment.units.Count == 0 && segment.path.tileX != Sim.OffMap) {
					g.events.add(new TileMoveEvt(time, segment.path.id, Sim.OffMap, 0));
				}
			}
		}
	}

	/// <summary>
	/// returns health of this unit at latest possible time
	/// </summary>
	public int healthLatest() {
		return type.maxHealth - nTimeHealth;
	}

	/// <summary>
	/// returns health of this unit at specified time
	/// </summary>
	public int healthWhen(long time) {
		int i = nTimeHealth;
		while (i > 0 && time < timeHealth[i - 1]) i--;
		return type.maxHealth - i;
	}
}
