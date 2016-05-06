// Written in 2014 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

[ProtoContract]
public class UpdatePastCmdEvt : CmdEvt {
	[ProtoMember(1)] public long timeSimPast;
	[ProtoMember(2)] public int player;
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	private UpdatePastCmdEvt() { }
	
	public UpdatePastCmdEvt(long timeVal, long timeSimPastVal, int playerVal) {
		time = timeVal;
		timeSimPast = timeSimPastVal;
		player = playerVal;
	}
	
	public override void apply (Sim g) {
		g.players[player].updatePast (timeSimPast);
	}
}
