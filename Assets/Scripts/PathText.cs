// Written in 2014 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

[ProtoContract]
public class PathText {
	[ProtoMember(1)] public long timeStart;
	[ProtoMember(2, AsReference = true)] public Path path;
	[ProtoMember(3)] public string text;
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	private PathText() { }
	
	public PathText(long timeStart, Path path, string text) {
		this.timeStart = timeStart;
		this.path = path;
		this.text = text;
	}
}
