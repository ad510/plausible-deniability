// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class UnitType {
	public int id; // index in unit type array
	public string name;
	public string imgPath;
	public FP.Vector imgOffset; // how much to offset center of image when drawing it
	public long imgHalfHeight; // how tall half of image height should be drawn
	public FP.Vector selMinPos; // minimum relative position where clicking would select the unit
	public FP.Vector selMaxPos; // maximum relative position where clicking would select the unit
	/*public string sndSelect;
	public string sndMove;
	public string sndAttack;
	public string sndNoHealth;*/
	public int maxHealth;
	public long speed; // in position units per millisecond
	public long reload; // time needed to reload
	public long range; // range of attack
	public long tightFormationSpacing;
	public long makeUnitMinDist; // minimum distance that new units of this unit type move away
	public long makeUnitMaxDist; // maximum distance that new units of this unit type move away
	public long makePathMinDist; // minimum distance that new paths of this unit type move away
	public long makePathMaxDist; // maximum distance that new paths of this unit type move away
	public UnitType makeOnUnitT; // unit type that this unit type should be made on top of
	public int[] damage; // damage done per attack to each unit type
	public bool[] canMake; // whether can make each unit type
	public long[] rscCost; // cost to make unit (may not be negative)
	public long[] rscCollectRate; // resources collected per each millisecond that unit exists (may not be negative)
}
