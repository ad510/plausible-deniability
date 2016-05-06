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
public struct ProtoVector3 {
	[ProtoMember(1)] public float x;
	[ProtoMember(2)] public float y;
	[ProtoMember(3)] public float z;
	
	public static explicit operator ProtoVector3(Vector2 vec) {
		return new ProtoVector3 {
			x = vec.x,
			y = vec.y,
		};
	}
	
	public static explicit operator Vector2(ProtoVector3 vec) {
		return new Vector2(vec.x, vec.y);
	}
	
	public static explicit operator ProtoVector3(Vector3 vec) {
		return new ProtoVector3 {
			x = vec.x,
			y = vec.y,
			z = vec.z,
		};
	}
	
	public static explicit operator Vector3(ProtoVector3 vec) {
		return new Vector3(vec.x, vec.y, vec.z);
	}
}
