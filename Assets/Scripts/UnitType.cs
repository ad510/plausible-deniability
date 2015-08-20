// Written in 2013-2014 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

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
	[ProtoMember(24)] public FP.Vector laserPos; // relative position where laser is emitted from
	/*public string sndSelect;
	public string sndMove;
	public string sndAttack;
	public string sndNoHealth;*/
	[ProtoMember(9)] public int maxHealth;
	[ProtoMember(10)] public long speed; // in position units per millisecond
	[ProtoMember(11)] public long reload; // time needed to reload
	[ProtoMember(12)] public long range; // range of attack
	[ProtoMember(13)] public long tightFormationSpacing;
	[ProtoMember(23)] public int seePrecedence; // priority of being seen when sharing paths (lower values are higher priority)
	[ProtoMember(14)] public long makeUnitMinDist; // minimum distance that new units of this unit type move away
	[ProtoMember(15)] public long makeUnitMaxDist; // maximum distance that new units of this unit type move away
	[ProtoMember(16)] public long makePathMinDist; // minimum distance that new alternate paths of this unit type move away
	[ProtoMember(17)] public long makePathMaxDist; // maximum distance that new alternate paths of this unit type move away
	[ProtoMember(18, AsReference = true)] public UnitType makeOnUnitT; // unit type that this unit type should be made on top of
	[ProtoMember(19)] public int[] damage; // damage done per attack to each unit type
	[ProtoMember(20)] public bool[] canMake; // whether can make each unit type
	[ProtoMember(21)] public long[] rscCost; // cost to make unit (may not be negative)
	[ProtoMember(22)] public long[] rscCollectRate; // resources collected per each millisecond that unit exists (may not be negative)
}
