// Written in 2013 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

[ProtoContract]
public struct ProtoColor {
	[ProtoMember(1)] public float r;
	[ProtoMember(2)] public float g;
	[ProtoMember(3)] public float b;
	[ProtoMember(4)] public float a;
	
	public static explicit operator ProtoColor(Color color) {
		return new ProtoColor {
			r = color.r,
			g = color.g,
			b = color.b,
			a = color.a,
		};
	}
	
	public static explicit operator Color(ProtoColor color) {
		return new Color(color.r, color.g, color.b, color.a);
	}
}
