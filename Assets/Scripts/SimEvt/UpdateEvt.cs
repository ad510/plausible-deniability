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

/// <summary>
/// event to update various things at regular intervals
/// </summary>
public class UpdateEvt : SimEvt {
	public UpdateEvt(long timeVal) {
		time = timeVal;
	}

	public override void apply(Sim g) {
		if (g.networkView != null) {
			// apply received user commands (multiplayer only)
			for (int i = 0; i < g.users.Length; i++) {
				if (g.users[i].timeSync < time) throw new InvalidOperationException("UpdateEvt is being applied before all commands were received from user " + i);
				if (time > 0 && g.users[i].checksums[time] != g.users[g.selUser].checksums[time]) g.synced = false;
				while (g.users[i].cmdReceived.peekTime () == time) {
					// TODO: could command be applied after another event with same time, causing desyncs in replays?
					SimEvt evt = g.users[i].cmdReceived.pop ();
					evt.apply (g);
					g.cmdHistory.add (evt);
				}
				// delete old checksums
				foreach (long k in g.users[i].checksums.Keys.ToArray ()) {
					if (k < time) g.users[i].checksums.Remove (k);
				}
			}
			// send pending commands to other users
			foreach (SimEvt evt in g.cmdPending.events) {
				System.IO.MemoryStream stream = new System.IO.MemoryStream();
				CmdEvtTag cmdType;
				evt.time = time + g.updateInterval; // set event time to when it will be applied
				Serializer.Serialize (stream, evt);
				if (evt is MoveCmdEvt) {
					cmdType = CmdEvtTag.move;
				}
				else if (evt is MakeUnitCmdEvt) {
					cmdType = CmdEvtTag.makeUnit;
				}
				else if (evt is MakePathCmdEvt) {
					cmdType = CmdEvtTag.makePath;
				}
				else if (evt is DeletePathCmdEvt) {
					cmdType = CmdEvtTag.deletePath;
				}
				else if (evt is GoLiveCmdEvt) {
					cmdType = CmdEvtTag.goLive;
				}
				else if (evt is StackCmdEvt) {
					cmdType = CmdEvtTag.stack;
				}
				else if (evt is DeleteOtherPathsCmdEvt) {
					cmdType = CmdEvtTag.deleteOtherPaths;
				}
				else {
					throw new InvalidOperationException("pending command's type is not a command");
				}
				g.networkView.RPC ("addCmd", RPCMode.Others, g.selUser, (int)cmdType, stream.ToArray ());
			}
			g.networkView.RPC ("allCmdsSent", RPCMode.Others, g.selUser, g.checksum);
			// move pending commands to cmdReceived
			g.users[g.selUser].cmdReceived = g.cmdPending;
			g.users[g.selUser].timeSync += g.updateInterval;
			g.cmdPending = new SimEvtList();
			g.users[g.selUser].checksums[time + g.updateInterval] = g.checksum;
		}
		// update units
		for (int i = 0; i < g.paths.Count; i++) {
			Segment segment = g.paths[i].getSegment (time);
			if (segment != null && g.paths[i].timeSimPast == long.MaxValue) {
				FP.Vector pos = g.paths[i].calcPos (time);
				foreach (int unit in segment.units) {
					if (time >= g.units[unit].timeAttack + g.unitT[g.units[unit].type].reload) {
						// done reloading, look for closest target to potentially attack
						int target = -1;
						long targetDistSq = g.unitT[g.units[unit].type].range * g.unitT[g.units[unit].type].range + 1;
						for (int j = 0; j < g.paths.Count; j++) {
							Segment segment2 = g.paths[j].getSegment (time);
							if (i != j && segment2 != null && g.paths[j].timeSimPast == long.MaxValue && g.players[g.paths[i].player].mayAttack[g.paths[j].player]) {
								foreach (int unit2 in segment2.units) {
									if (g.unitT[g.units[unit].type].damage[g.units[unit2].type] > 0) {
										long distSq = (g.paths[j].calcPos (time) - pos).lengthSq ();
										if (distSq < targetDistSq) {
											target = j;
											targetDistSq = distSq;
											break;
										}
									}
								}
							}
						}
						if (target >= 0) {
							// attack every applicable unit in target path
							// take health with 1 ms delay so earlier units in array don't have unfair advantage
							foreach (int unit2 in g.paths[target].getSegment(time).units) {
								if (g.unitT[g.units[unit].type].damage[g.units[unit2].type] > 0) {
									for (int j = 0; j < g.unitT[g.units[unit].type].damage[g.units[unit2].type]; j++) g.units[unit2].takeHealth(time + 1, target);
									g.units[unit].timeAttack = time;
								}
							}
						}
					}
				}
			}
		}
		// add events to move paths between tiles
		// this shouldn't be done in Sim.update() because addTileMoveEvts() sometimes adds events before timeSim
		foreach (Path path in g.paths) {
			if (path.timeSimPast == long.MaxValue) path.addTileMoveEvts(ref g.events, time, time + g.updateInterval);
		}
		g.movedPaths.Clear();
		// add next UpdateEvt
		g.checksum = 0;
		g.events.add(new UpdateEvt(time + g.updateInterval));
		g.timeUpdateEvt = time;
	}
}
