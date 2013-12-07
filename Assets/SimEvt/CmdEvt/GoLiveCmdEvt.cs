// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

/// <summary>
/// command to make a player's time traveling paths be updated in the present
/// </summary>
/// <remarks>this doesn't inherit from CmdEvt because it isn't a unit command</remarks>
[ProtoContract]
public class GoLiveCmdEvt : SimEvt {
	[ProtoMember(1)]
	public int player {get;set;}

	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	private GoLiveCmdEvt() { }

	public GoLiveCmdEvt(long timeVal, int playerVal) {
		time = timeVal;
		player = playerVal;
	}

	public override void apply(Sim g) {
		long timeTravelStart = long.MaxValue;
		g.cmdHistory.add(this); // copy event to command history list (it should've already been popped from event list)
		foreach (Path path in g.paths) {
			if (player == path.player && path.segments.Last ().units.Count > 0 && path.timeSimPast != long.MaxValue) {
				// ensure that time traveling paths don't move off exclusive areas
				path.updatePast(time);
				// find earliest time that player's paths started time traveling
				if (path.segments[0].timeStart < timeTravelStart) timeTravelStart = path.segments[0].timeStart;
			}
		}
		if (timeTravelStart != long.MaxValue) { // skip if player has no time traveling paths
			// check if going live might lead to player having negative resources
			g.players[player].timeNegRsc = g.playerCheckNegRsc(player, timeTravelStart, true);
			if (g.players[player].timeNegRsc >= 0) {
				// indicate failure to go live, then return
				g.players[player].timeGoLiveFail = time;
				return;
			}
			// safe for paths to become live, so do so
			foreach (Path path in g.paths) {
				if (player == path.player && path.segments.Last ().units.Count > 0 && path.timeSimPast != long.MaxValue) path.goLive();
			}
		}
		// indicate success
		g.players[player].hasNonLivePaths = false;
		g.players[player].timeGoLiveFail = long.MaxValue;
	}
}
