// Written in 2014 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

[ProtoContract]
public class Attack {
	[ProtoMember(1)] public readonly long time;
	[ProtoMember(2, AsReference = true)] public readonly Path target;
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	private Attack() { }
	
	public Attack(long timeVal, Path targetVal) {
		time = timeVal;
		target = targetVal;
	}
}
