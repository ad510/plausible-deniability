// Written in 2014 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

[ProtoContract]
public class UnitSelection {
	[ProtoMember(1, AsReference = true)] public readonly Path path;
	[ProtoMember(2, AsReference = true)] public readonly Unit unit;
	[ProtoMember(3)] public readonly long time;
	
	private UnitSelection() { } // for protobuf-net use only
	
	public UnitSelection(Path pathVal, Unit unitVal, long timeVal) {
		path = pathVal;
		unit = unitVal;
		time = timeVal;
	}
	
	public SegmentUnit segmentUnit() {
		return new SegmentUnit(segment(), unit);
	}
	
	public Segment segment() {
		return path.segmentWhen(time);
	}
}
