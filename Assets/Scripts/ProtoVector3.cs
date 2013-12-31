// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

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
