// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

[ProtoContract]
public class UnitType {
	[ProtoMember(2)] public int id; // index in unit type array
	[ProtoMember(3)] public string name;
	[ProtoMember(4)] public string imgPath;
	[ProtoMember(5)] public FP.Vector imgOffset; // how much to offset center of image when drawing it
	[ProtoMember(6)] public long imgHalfHeight; // how tall half of image height should be drawn
	[ProtoMember(7)] public FP.Vector selMinPos; // minimum relative position where clicking would select the unit
	[ProtoMember(8)] public FP.Vector selMaxPos; // maximum relative position where clicking would select the unit
	/*public string sndSelect;
	public string sndMove;
	public string sndAttack;
	public string sndNoHealth;*/
	[ProtoMember(9)] public int maxHealth;
	[ProtoMember(10)] public long speed; // in position units per millisecond
	[ProtoMember(11)] public long reload; // time needed to reload
	[ProtoMember(12)] public long range; // range of attack
	[ProtoMember(13)] public long tightFormationSpacing;
	[ProtoMember(14)] public long makeUnitMinDist; // minimum distance that new units of this unit type move away
	[ProtoMember(15)] public long makeUnitMaxDist; // maximum distance that new units of this unit type move away
	[ProtoMember(16)] public long makePathMinDist; // minimum distance that new paths of this unit type move away
	[ProtoMember(17)] public long makePathMaxDist; // maximum distance that new paths of this unit type move away
	[ProtoMember(18, AsReference = true)] public UnitType makeOnUnitT; // unit type that this unit type should be made on top of
	[ProtoMember(19)] public int[] damage; // damage done per attack to each unit type
	[ProtoMember(20)] public bool[] canMake; // whether can make each unit type
	[ProtoMember(21)] public long[] rscCost; // cost to make unit (may not be negative)
	[ProtoMember(22)] public long[] rscCollectRate; // resources collected per each millisecond that unit exists (may not be negative)
}
