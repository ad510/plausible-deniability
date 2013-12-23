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
				else if (evt is DeleteOtherPathsCmdEvt) {
					cmdType = CmdEvtTag.deleteOtherPaths;
				}
				else if (evt is StackCmdEvt) {
					cmdType = CmdEvtTag.stack;
				}
				else if (evt is GoLiveCmdEvt) {
					cmdType = CmdEvtTag.goLive;
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
		foreach (Path path in g.paths) {
			Segment segment = path.activeSegment (time);
			if (segment != null && path.timeSimPast == long.MaxValue) {
				FP.Vector pos = path.calcPos (time);
				foreach (Unit unit in segment.units) {
					if (time >= unit.timeAttack + unit.type.reload) {
						// done reloading, look for closest target to potentially attack
						Path target = null;
						long targetDistSq = unit.type.range * unit.type.range + 1;
						foreach (Path path2 in g.paths) {
							Segment segment2 = path2.activeSegment (time);
							if (path != path2 && segment2 != null && path2.timeSimPast == long.MaxValue && path.player.mayAttack[path2.player.id]) {
								foreach (Unit unit2 in segment2.units) {
									if (unit.type.damage[unit2.type.id] > 0) {
										long distSq = (path2.calcPos (time) - pos).lengthSq ();
										if (distSq < targetDistSq) {
											target = path2;
											targetDistSq = distSq;
											break;
										}
									}
								}
							}
						}
						if (target != null) {
							// attack every applicable unit in target path
							// take health with 1 ms delay so earlier units in array don't have unfair advantage
							foreach (Unit unit2 in target.activeSegment(time).units) {
								if (unit.type.damage[unit2.type.id] > 0) {
									for (int j = 0; j < unit.type.damage[unit2.type.id]; j++) unit2.takeHealth(time + 1, target);
									unit.timeAttack = time;
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
