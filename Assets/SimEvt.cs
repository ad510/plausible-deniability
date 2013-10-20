// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

/* This file contains:
 * the different types of simulation events (including user commands) that can be applied at a specific time in the game
 * the base class for the simulation events
 * a list data type that specializes in storing simulation events
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using ProtoBuf;

/// <summary>
/// base class for simulation events
/// </summary>
[ProtoContract]
[ProtoInclude(10, typeof(CmdEvt))]
[ProtoInclude(15, typeof(GoLiveCmdEvt))]
public abstract class SimEvt {
	[ProtoMember(1)]
	public long time {get;set;}

	public abstract void apply(Sim g);
}

/// <summary>
/// base class for unit commands
/// </summary>
[ProtoContract]
[ProtoInclude(11, typeof(MoveCmdEvt))]
[ProtoInclude(12, typeof(MakeUnitCmdEvt))]
[ProtoInclude(13, typeof(MakePathCmdEvt))]
[ProtoInclude(14, typeof(DeletePathCmdEvt))]
[ProtoInclude(16, typeof(StackCmdEvt))]
public abstract class CmdEvt : SimEvt {
	[ProtoMember(1)]
	public long timeCmd {get;set;} // time is latest simulation time when command is given, timeCmd is when event takes place (may be in past)
	[ProtoMember(2)]
	public int[] units {get;set;} // STACK TODO: delete variable when no longer needed
	[ProtoMember(3)]
	public Dictionary<int, int[]> paths {get;set;}
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	protected CmdEvt() { }

	protected CmdEvt(long timeVal, long timeCmdVal, Dictionary<int, int[]> pathsVal) {
		time = timeVal;
		timeCmd = timeCmdVal;
		paths = pathsVal;
	}

	public override void apply(Sim g) {
		g.cmdHistory.add(this); // copy event to command history list (it should've already been popped from event list)
	}
	
	/// <summary>
	/// returns commanded paths and units that exist at timeCmd
	/// </summary>
	protected Dictionary<int, List<int>> existingPaths(Sim g) {
		Dictionary<int, List<int>> ret = new Dictionary<int, List<int>>();
		foreach (KeyValuePair<int, int[]> path in paths) {
			if (timeCmd >= g.paths[path.Key].nodes[0].time) {
				int node = g.paths[path.Key].getNode (timeCmd);
				List<int> existingUnits = new List<int>();
				foreach (int unit in path.Value) {
					if (g.paths[path.Key].nodes[node].units.Contains (unit)) {
						if (!existingUnits.Contains (unit)) existingUnits.Add (unit);
					}
				}
				if (existingUnits.Count > 0) ret.Add (path.Key, existingUnits);
			}
		}
		return ret;
	}
}

public enum Formation : byte { Tight, Loose, Ring };

/// <summary>
/// command to move unit(s)
/// </summary>
[ProtoContract]
public class MoveCmdEvt : CmdEvt {
	[ProtoMember(1)]
	public FP.Vector pos {get;set;} // where to move to
	[ProtoMember(2)]
	public Formation formation {get;set;}
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	public MoveCmdEvt() { }

	public MoveCmdEvt(long timeVal, long timeCmdVal, Dictionary<int, int[]> pathsVal, FP.Vector posVal, Formation formationVal)
		: base(timeVal, timeCmdVal, pathsVal) {
		pos = posVal;
		formation = formationVal;
	}

	public override void apply(Sim g) {
		Dictionary<int, List<int>> exPaths = existingPaths (g);
		FP.Vector goalCenter, goal, rows = new FP.Vector(), offset = new FP.Vector();
		long spacing = 0;
		int count = 0, i = 0;
		base.apply(g);
		// count number of units able to move
		foreach (KeyValuePair<int, List<int>> path in exPaths) {
			if (g.paths[path.Key].canMove(timeCmd)) {
				count++;
				if (formation == Formation.Tight) {
					// calculate spacing for tight formation
					foreach (int unit in path.Value) {
						if (g.unitT[g.units[unit].type].tightFormationSpacing > spacing) spacing = g.unitT[g.units[unit].type].tightFormationSpacing;
					}
				}
			}
		}
		if (count == 0) return;
		// calculate spacing
		// (if tight formation, then spacing was already calculated above)
		// TODO: loose formation should be triangular
		if (formation == Formation.Loose) {
			spacing = FP.mul(g.visRadius, FP.Sqrt2) >> FP.Precision << FP.Precision;
		}
		else if (formation == Formation.Ring) {
			spacing = (g.visRadius * 2 >> FP.Precision) - 1 << FP.Precision;
		}
		if (formation == Formation.Tight || formation == Formation.Loose) {
			rows.x = FP.sqrt(count);
			rows.y = (count - 1) / rows.x + 1;
			offset = (rows - new FP.Vector(1, 1)) * spacing / 2;
		}
		else if (formation == Formation.Ring) {
			offset.x = (count == 1) ? 0 : FP.div(spacing / 2, FP.sin(FP.Pi / count));
			offset.y = offset.x;
		}
		else {
			throw new NotImplementedException("requested formation is not implemented");
		}
		goalCenter = pos;
		if (goalCenter.x < Math.Min(offset.x, g.mapSize / 2)) goalCenter.x = Math.Min(offset.x, g.mapSize / 2);
		if (goalCenter.x > g.mapSize - Math.Min(offset.x, g.mapSize / 2)) goalCenter.x = g.mapSize - Math.Min(offset.x, g.mapSize / 2);
		if (goalCenter.y < Math.Min(offset.y, g.mapSize / 2)) goalCenter.y = Math.Min(offset.y, g.mapSize / 2);
		if (goalCenter.y > g.mapSize - Math.Min(offset.y, g.mapSize / 2)) goalCenter.y = g.mapSize - Math.Min(offset.y, g.mapSize / 2);
		// move units
		foreach (KeyValuePair<int, List<int>> path in exPaths) {
			if (g.paths[path.Key].canMove(timeCmd)) {
				if (formation == Formation.Tight || formation == Formation.Loose) {
					goal = goalCenter + new FP.Vector((i % rows.x) * spacing - offset.x, i / rows.x * spacing - offset.y);
				}
				else if (formation == Formation.Ring) {
					goal = goalCenter + offset.x * new FP.Vector(FP.cos(2 * FP.Pi * i / count), FP.sin(2 * FP.Pi * i / count));
				}
				else {
					throw new NotImplementedException("requested formation is not implemented");
				}
				g.paths[path.Key].moveTo(timeCmd, new List<int>(path.Value), goal);
				i++;
			}
		}
	}
}

/// <summary>
/// command to make a new unit
/// </summary>
[ProtoContract]
public class MakeUnitCmdEvt : CmdEvt {
	[ProtoMember(1)]
	public int type {get;set;}
	[ProtoMember(2)]
	public FP.Vector pos {get;set;}
	[ProtoMember(3)]
	bool autoRepeat {get;set;}
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	public MakeUnitCmdEvt() { }

	public MakeUnitCmdEvt(long timeVal, long timeCmdVal, Dictionary<int, int[]> pathsVal, int typeVal, FP.Vector posVal, bool autoRepeatVal = false)
		: base(timeVal, timeCmdVal, pathsVal) {
		type = typeVal;
		pos = posVal;
		autoRepeat = autoRepeatVal;
	}

	public override void apply(Sim g) {
		Dictionary<int, List<int>> exPaths = existingPaths (g);
		if (!autoRepeat) base.apply(g);
		// make unit at requested position, if possible
		foreach (int path in exPaths.Keys) {
			if (g.paths[path].canMakeUnitType (timeCmd, type)) {
				FP.Vector curPos = g.paths[path].calcPos(timeCmd);
				if ((pos.x == curPos.x && pos.y == curPos.y) || (g.unitT[type].speed > 0 && g.unitT[type].makeOnUnitT < 0)) {
					// TODO: take time to make units?
					List<int> unitList = new List<int>();
					unitList.Add (g.nUnits);
					g.setNUnits (g.nUnits + 1);
					g.units[g.nUnits - 1] = new Unit(g, g.nUnits - 1, type, g.paths[path].player());
					g.paths[path].makePath (timeCmd, unitList);
					if (g.paths[g.paths.Count - 1].canMove (timeCmd)) g.paths[g.paths.Count - 1].moveTo (timeCmd, pos); // move new unit out of the way
					return;
				}
			}
		}
		if (!autoRepeat) {
			// if none of specified paths are at requested position,
			// try moving one to the correct position then trying again to make the unit
			int movePath = -1;
			foreach (KeyValuePair<int, List<int>> path in exPaths) {
				if (g.unitsCanMake (path.Value, type) && g.paths[path.Key].canMove (timeCmd)
					&& (movePath < 0 || (g.paths[path.Key].calcPos(timeCmd) - pos).lengthSq() < (g.paths[movePath].calcPos(timeCmd) - pos).lengthSq())) {
					movePath = path.Key;
				}
			}
			if (movePath >= 0) {
				Dictionary<int, int[]> evtPaths = new Dictionary<int, int[]>(paths);
				movePath = g.paths[movePath].moveTo(timeCmd, new List<int>(exPaths[movePath]), pos);
				// STACK TODO: implement line below
				//unitsList.Insert(0, moveUnit); // in case replacement unit is moving to make the unit
				g.events.add(new MakeUnitCmdEvt(g.paths[movePath].moves[g.paths[movePath].moves.Count - 1].timeEnd, g.paths[movePath].moves[g.paths[movePath].moves.Count - 1].timeEnd + 1,
					evtPaths, type, pos, true));
			}
		}
	}
}

/// <summary>
/// command to make new path(s) branching off from specified path(s)
/// </summary>
[ProtoContract]
public class MakePathCmdEvt : CmdEvt {
	[ProtoMember(1)]
	public Dictionary<int, FP.Vector> pos {get;set;} // where new paths should move to
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	public MakePathCmdEvt() { }

	public MakePathCmdEvt(long timeVal, long timeCmdVal, Dictionary<int, int[]> pathsVal, Dictionary<int, FP.Vector> posVal)
		: base(timeVal, timeCmdVal, pathsVal) {
		pos = posVal;
	}

	public override void apply(Sim g) {
		Dictionary<int, List<int>> exPaths = existingPaths (g);
		base.apply(g);
		foreach (KeyValuePair<int, List<int>> path in exPaths) {
			if (g.paths[path.Key].canMove(timeCmd) && g.paths[path.Key].makePath (timeCmd, new List<int>(path.Value))) {
				g.paths[g.paths.Count - 1].moveTo(timeCmd, pos[path.Key]); // move new path out of the way
			}
		}
	}
}

/// <summary>
/// command to remove unit(s) from path(s)
/// </summary>
[ProtoContract]
public class DeletePathCmdEvt : CmdEvt {
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	public DeletePathCmdEvt() { }

	public DeletePathCmdEvt(long timeVal, long timeCmdVal, Dictionary<int, int[]> pathsVal)
		: base(timeVal, timeCmdVal, pathsVal) { }

	public override void apply(Sim g) {
		Dictionary<int, List<int>> exPaths = existingPaths (g);
		base.apply(g);
		foreach (KeyValuePair<int, List<int>> path in exPaths) {
			foreach (int unit in path.Value) {
				g.paths[path.Key].removeUnit (timeCmd, unit);
			}
		}
	}
}

/// <summary>
/// command to stack specified path(s) onto another specified path
/// </summary>
[ProtoContract]
public class StackCmdEvt : CmdEvt {
	[ProtoMember(1)]
	public int stackPath {get;set;} // path that paths will be stacked onto
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	public StackCmdEvt() { }
	
	public StackCmdEvt(long timeVal, long timeCmdVal, Dictionary<int, int[]> pathsVal, int stackPathVal)
		: base(timeVal, timeCmdVal, pathsVal) {
		stackPath = stackPathVal;
	}
	
	public override void apply (Sim g)
	{
		Dictionary<int, List<int>> exPaths = existingPaths (g);
		List<int> movedPaths = new List<int>();
		base.apply (g);
		// move paths to final location of stackPath
		// STACK TODO: if stackPathVal < 0 (pressing stack button will do that) then move all paths to their average location
		foreach (KeyValuePair<int, List<int>> path in exPaths) {
			if (g.paths[path.Key].speed () == g.paths[stackPath].speed () && g.paths[path.Key].canMove (timeCmd)) {
				movedPaths.Add (g.paths[path.Key].moveTo (timeCmd, new List<int>(path.Value), g.paths[stackPath].moves[g.paths[stackPath].moves.Count - 1].vecEnd));
			}
		}
		// if able to move any of the paths, add events to stack them as they arrive
		if (movedPaths.Count > 0) {
			if (!movedPaths.Contains (stackPath)) movedPaths.Add (stackPath);
			foreach (int path in movedPaths) {
				// in most cases only 1 path will stack onto stackPath,
				// but request to stack all moved paths anyway in case the path they're stacking onto moves away
				g.events.add (new StackEvt(g.paths[path].moves[g.paths[path].moves.Count - 1].timeEnd, movedPaths.ToArray ()));
			}
		}
	}
}

/// <summary>
/// command to make a player's time traveling units be updated in the present
/// </summary>
/// <remarks>this doesn't inherit from CmdEvt because it isn't a unit command</remarks>
[ProtoContract]
public class GoLiveCmdEvt : SimEvt {
	[ProtoMember(1)]
	public int player {get;set;}

	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	public GoLiveCmdEvt() { }

	public GoLiveCmdEvt(long timeVal, int playerVal) {
		time = timeVal;
		player = playerVal;
	}

	public override void apply(Sim g) {
		long timeTravelStart = long.MaxValue;
		int i;
		g.cmdHistory.add(this); // copy event to command history list (it should've already been popped from event list)
		for (i = 0; i < g.nUnits; i++) {
			if (player == g.units[i].player && g.units[i].exists(time) && !g.units[i].isLive(time)) {
				// ensure that time traveling units don't move off exclusive areas
				g.units[i].updatePast(time);
				// find earliest time that player's units started time traveling
				if (g.units[i].moves[0].timeStart < timeTravelStart) timeTravelStart = g.units[i].moves[0].timeStart;
			}
		}
		if (timeTravelStart != long.MaxValue) { // skip if player has no time traveling units
			// check if going live might lead to player having negative resources
			g.players[player].timeNegRsc = g.playerCheckNegRsc(player, timeTravelStart, true);
			if (g.players[player].timeNegRsc >= 0) {
				// indicate failure to go live, then return
				g.players[player].timeGoLiveFail = time;
				return;
			}
			// safe for units to become live, so do so
			for (i = 0; i < g.nUnits; i++) {
				if (player == g.units[i].player && g.units[i].exists(time) && !g.units[i].isLive(time)) g.units[i].goLive();
			}
		}
		// indicate success
		g.players[player].hasNonLiveUnits = false;
		g.players[player].timeGoLiveFail = long.MaxValue;
	}
}

/// <summary>
/// event to update various things at regular intervals
/// </summary>
public class UpdateEvt : SimEvt {
	public UpdateEvt(long timeVal) {
		time = timeVal;
	}

	public override void apply(Sim g) {
		FP.Vector pos;
		int cmdType;
		int i, j;
		if (g.networkView != null) {
			// apply received user commands (multiplayer only)
			for (i = 0; i < g.nUsers; i++) {
				if (g.users[i].timeSync < time) throw new InvalidOperationException("UpdateEvt is being applied before all commands were received from user " + i);
				if (time > 0 && g.users[i].checksums[time] != g.users[g.selUser].checksums[time]) g.synced = false;
				while (g.users[i].cmdReceived.peekTime () == time) {
					// TODO: could command be applied after another event with same time, causing desyncs in replays?
					g.users[i].cmdReceived.pop ().apply (g);
				}
				// delete old checksums
				foreach (long k in g.users[i].checksums.Keys.ToArray ()) {
					if (k < time) g.users[i].checksums.Remove (k);
				}
			}
			// send pending commands to other users
			foreach (SimEvt evt in g.cmdPending.events) {
				System.IO.MemoryStream stream = new System.IO.MemoryStream();
				evt.time = time + g.updateInterval; // set event time to when it will be applied
				Serializer.Serialize (stream, evt);
				if (evt is MoveCmdEvt) {
					cmdType = 11;
				}
				else if (evt is MakeUnitCmdEvt) {
					cmdType = 12;
				}
				else if (evt is MakePathCmdEvt) {
					cmdType = 13;
				}
				else if (evt is DeletePathCmdEvt) {
					cmdType = 14;
				}
				else if (evt is GoLiveCmdEvt) {
					cmdType = 15;
				}
				else if (evt is StackCmdEvt) {
					cmdType = 16;
				}
				else {
					throw new InvalidOperationException("pending command's type is not a command");
				}
				g.networkView.RPC ("addCmd", RPCMode.Others, g.selUser, cmdType, stream.ToArray ());
			}
			g.networkView.RPC ("allCmdsSent", RPCMode.Others, g.selUser, g.checksum);
			// move pending commands to cmdReceived
			g.users[g.selUser].cmdReceived = g.cmdPending;
			g.users[g.selUser].timeSync += g.updateInterval;
			g.cmdPending = new SimEvtList();
			g.users[g.selUser].checksums[time + g.updateInterval] = g.checksum;
		}
		// update units
		for (i = 0; i < g.paths.Count; i++) {
			if (g.paths[i].isLive (time)) {
				pos = g.paths[i].calcPos (time);
				foreach (int unit in g.paths[i].nodes[g.paths[i].getNode(time)].units) {
					if (time >= g.units[unit].timeAttack + g.unitT[g.units[unit].type].reload) {
						// done reloading, look for closest target to potentially attack
						int target = -1;
						long targetDistSq = g.unitT[g.units[unit].type].range * g.unitT[g.units[unit].type].range + 1;
						for (j = 0; j < g.paths.Count; j++) {
							if (i != j && g.paths[j].isLive (time) && g.players[g.paths[i].player ()].mayAttack[g.paths[j].player ()]) {
								foreach (int unit2 in g.paths[j].nodes[g.paths[j].getNode(time)].units) {
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
							foreach (int unit2 in g.paths[target].nodes[g.paths[target].getNode(time)].units) {
								if (g.unitT[g.units[unit].type].damage[g.units[unit2].type] > 0) {
									for (j = 0; j < g.unitT[g.units[unit].type].damage[g.units[unit2].type]; j++) g.units[unit2].takeHealth(time + 1, target);
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

/// <summary>
/// event to stack multiple paths together if they are at exactly the same place
/// </summary>
public class StackEvt : SimEvt {
	public int[] paths;
	
	public StackEvt(long timeVal, int[] pathsVal) {
		time = timeVal;
		paths = pathsVal;
	}
	
	public override void apply (Sim g)
	{
		bool[] pathsStacked = new bool[paths.Length];
		for (int i = 0; i < pathsStacked.Length; i++) {
			pathsStacked[i] = (time < g.paths[paths[i]].moves[0].timeStart);
		}
		// loop through each pair of unstacked paths
		for (int i = 0; i < paths.Length; i++) {
			if (!pathsStacked[i]) {
				FP.Vector iPos = g.paths[paths[i]].calcPos (time);
				int iNode = g.paths[paths[i]].getNode (time);
				for (int j = i + 1; j < paths.Length; j++) {
					FP.Vector jPos = g.paths[paths[j]].calcPos (time);
					int jNode = g.paths[paths[j]].getNode (time);
					// check that paths are at same position
					if (iPos.x == jPos.x && iPos.y == jPos.y) {
						// check whether allowed to stack the paths' units together
						List<int> stackUnits = new List<int>(g.paths[paths[i]].nodes[iNode].units);
						foreach (int unit in g.paths[paths[j]].nodes[jNode].units) {
							if (!stackUnits.Contains (unit)) stackUnits.Add (unit);
						}
						if (g.stackAllowed (stackUnits)) {
							// merge the paths onto path i
							iNode = g.paths[paths[i]].addConnectedPath (time, paths[j]);
							jNode = g.paths[paths[j]].getNode (time);
							g.paths[paths[i]].nodes[iNode].units = stackUnits;
							for (int k = g.paths[paths[j]].nodes[jNode].units.Count - 1; k >= 0; k--) {
								// STACK TODO: line below seems to always fail
								g.paths[paths[j]].removeUnit (time, g.paths[paths[j]].nodes[jNode].units[k]);
							}
							pathsStacked[i] = true;
							pathsStacked[j] = true;
						}
					}
				}
			}
		}
	}
}

/// <summary>
/// event in which path moves between visibility tiles
/// </summary>
/// <remarks>
/// when making this event, can't rely on a path's tileX and tileY being up-to-date
/// because the latest TileMoveEvts for that path might not be applied yet
/// </remarks>
public class TileMoveEvt : SimEvt {
	public int path;
	public int tileX, tileY; // new tile position, set to int.MinValue to keep current value

	public TileMoveEvt(long timeVal, int pathVal, int tileXVal, int tileYVal) {
		time = timeVal;
		path = pathVal;
		tileX = tileXVal;
		tileY = tileYVal;
	}

	public override void apply(Sim g) {
		if (g.paths[path].tileX == Sim.OffMap) return; // skip event if path no longer exists
		List<FP.Vector> playerVisAddTiles = new List<FP.Vector>();
		int exclusiveMinX = g.tileLen() - 1;
		int exclusiveMaxX = 0;
		int exclusiveMinY = g.tileLen() - 1;
		int exclusiveMaxY = 0;
		int i, tXPrev, tYPrev, tX, tY, tX2, tY2;
		if (tileX == int.MinValue) tileX = g.paths[path].tileX;
		if (tileY == int.MinValue) tileY = g.paths[path].tileY;
		tXPrev = g.paths[path].tileX;
		tYPrev = g.paths[path].tileY;
		g.paths[path].tileX = tileX;
		g.paths[path].tileY = tileY;
		// add path to visibility tiles
		for (tX = Math.Max (0, tileX - g.tileVisRadius()); tX <= Math.Min (g.tileLen () - 1, tileX + g.tileVisRadius()); tX++) {
			for (tY = Math.Max (0, tileY - g.tileVisRadius()); tY <= Math.Min (g.tileLen () - 1, tileY + g.tileVisRadius()); tY++) {
				if (!g.inVis(tX - tXPrev, tY - tYPrev) && g.inVis(tX - tileX, tY - tileY)) {
					if (g.tiles[tX, tY].pathVisLatest(path)) throw new InvalidOperationException("path " + path + " already sees tile (" + tX + ", " + tY + ")");
					// add path to path visibility tile
					g.tiles[tX, tY].pathVisToggle(path, time);
					if (!g.tiles[tX, tY].playerVisLatest(g.paths[path].player())) {
						g.tiles[tX, tY].playerVis[g.paths[path].player()].Add(time);
						playerVisAddTiles.Add(new FP.Vector(tX, tY));
						// check if this tile stopped being exclusive to another player
						for (i = 0; i < g.nPlayers; i++) {
							if (i != g.paths[path].player() && g.tiles[tX, tY].exclusiveLatest(i)) {
								g.exclusiveRemove(i, tX, tY, time);
							}
						}
					}
				}
			}
		}
		// remove path from visibility tiles
		for (tX = Math.Max (0, tXPrev - g.tileVisRadius()); tX <= Math.Min (g.tileLen () - 1, tXPrev + g.tileVisRadius()); tX++) {
			for (tY = Math.Max (0, tYPrev - g.tileVisRadius()); tY <= Math.Min (g.tileLen () - 1, tYPrev + g.tileVisRadius()); tY++) {
				if (g.inVis(tX - tXPrev, tY - tYPrev) && !g.inVis(tX - tileX, tY - tileY)) {
					if (!g.tiles[tX, tY].pathVisLatest(path)) throw new InvalidOperationException("path " + path + " already doesn't see tile (" + tX + ", " + tY + ")");
					// remove path from path visibility tile
					g.tiles[tX, tY].pathVisToggle(path, time);
					// check if player can't directly see this tile anymore
					if (g.tiles[tX, tY].playerVisLatest(g.paths[path].player()) && !g.tiles[tX, tY].playerDirectVisLatest(g.paths[path].player())) {
						long timePlayerVis = long.MaxValue;
						// find lowest time that surrounding tiles lost visibility
						for (tX2 = Math.Max(0, tX - 1); tX2 <= Math.Min(g.tileLen() - 1, tX + 1); tX2++) {
							for (tY2 = Math.Max(0, tY - 1); tY2 <= Math.Min(g.tileLen() - 1, tY + 1); tY2++) {
								if ((tX2 != tX || tY2 != tY) && !g.tiles[tX2, tY2].playerVisLatest(g.paths[path].player())) {
									if (g.tiles[tX2, tY2].playerVis[g.paths[path].player()].Count == 0) {
										timePlayerVis = long.MinValue;
									}
									else if (g.tiles[tX2, tY2].playerVis[g.paths[path].player()][g.tiles[tX2, tY2].playerVis[g.paths[path].player()].Count - 1] < timePlayerVis) {
										timePlayerVis = g.tiles[tX2, tY2].playerVis[g.paths[path].player()][g.tiles[tX2, tY2].playerVis[g.paths[path].player()].Count - 1];
									}
								}
							}
						}
						// if player can't see all neighboring tiles, they won't be able to tell if another player's unit moves into this tile
						// so remove this tile's visibility for this player
						if (timePlayerVis != long.MaxValue) {
							timePlayerVis = Math.Max(time, timePlayerVis + (1 << FP.Precision) / g.maxSpeed); // TODO: use more accurate time
							g.playerVisRemove(g.paths[path].player(), tX, tY, timePlayerVis);
						}
					}
				}
			}
		}
		// check if tiles became exclusive to this player
		foreach (FP.Vector vec in playerVisAddTiles) {
			if (vec.x < exclusiveMinX) exclusiveMinX = (int)vec.x;
			if (vec.x > exclusiveMaxX) exclusiveMaxX = (int)vec.x;
			if (vec.y < exclusiveMinY) exclusiveMinY = (int)vec.y;
			if (vec.y > exclusiveMaxY) exclusiveMaxY = (int)vec.y;
		}
		exclusiveMinX = Math.Max(0, exclusiveMinX - g.tileVisRadius());
		exclusiveMaxX = Math.Min(g.tileLen() - 1, exclusiveMaxX + g.tileVisRadius());
		exclusiveMinY = Math.Max(0, exclusiveMinY - g.tileVisRadius());
		exclusiveMaxY = Math.Min(g.tileLen() - 1, exclusiveMaxY + g.tileVisRadius());
		for (tX = exclusiveMinX; tX <= exclusiveMaxX; tX++) {
			for (tY = exclusiveMinY; tY <= exclusiveMaxY; tY++) {
				foreach (FP.Vector vec in playerVisAddTiles) {
					if (g.inVis(tX - vec.x, tY - vec.y)) {
						if (!g.tiles[tX, tY].exclusiveLatest(g.paths[path].player()) && g.calcExclusive(g.paths[path].player(), tX, tY)) {
							g.exclusiveAdd(g.paths[path].player(), tX, tY, time);
						}
						break;
					}
				}
			}
		}
		if (tileX >= 0 && tileX < g.tileLen() && tileY >= 0 && tileY < g.tileLen()) {
			// update whether this path is known to be unseen
			if (!g.paths[path].nodes[g.paths[path].nodes.Count - 1].unseen && g.tiles[tileX, tileY].exclusiveLatest(g.paths[path].player())) {
				g.paths[path].beUnseen(time);
			}
			else if (g.paths[path].nodes[g.paths[path].nodes.Count - 1].unseen && !g.tiles[tileX, tileY].exclusiveLatest(g.paths[path].player())) {
				g.paths[path].beSeen(time);
			}
			// if this path moved out of another player's visibility, remove that player's visibility here
			if (!g.players[g.paths[path].player()].immutable && tXPrev >= 0 && tXPrev < g.tileLen() && tYPrev >= 0 && tYPrev < g.tileLen()) {
				for (i = 0; i < g.nPlayers; i++) {
					if (i != g.paths[path].player() && g.tiles[tXPrev, tYPrev].playerDirectVisLatest(i) && !g.tiles[tileX, tileY].playerDirectVisLatest(i)) {
						for (tX = Math.Max(0, tileX - 1); tX <= Math.Min(g.tileLen() - 1, tileX + 1); tX++) {
							for (tY = Math.Max(0, tileY - 1); tY <= Math.Min(g.tileLen() - 1, tileY + 1); tY++) {
								// TODO?: use more accurate time at tiles other than (tileX, tileY)
								g.playerVisRemove(i, tX, tY, time);
							}
						}
					}
				}
			}
		}
		if (tXPrev >= 0 && tXPrev < g.tileLen() && tYPrev >= 0 && tYPrev < g.tileLen()) {
			// if this player can no longer directly see another player's path, remove this player's visibility there
			foreach (int j in g.tiles[tXPrev, tYPrev].pathVis.Keys) {
				if (g.paths[j].player() != g.paths[path].player() && !g.players[g.paths[j].player()].immutable //&& g.units[j].healthLatest() > 0 // STACK TODO: consider commented out part here
					&& g.inVis(g.paths[j].tileX - tXPrev, g.paths[j].tileY - tYPrev) && !g.tiles[g.paths[j].tileX, g.paths[j].tileY].playerDirectVisLatest(g.paths[path].player())) {
					for (tX = Math.Max(0, g.paths[j].tileX - 1); tX <= Math.Min(g.tileLen() - 1, g.paths[j].tileX + 1); tX++) {
						for (tY = Math.Max(0, g.paths[j].tileY - 1); tY <= Math.Min(g.tileLen() - 1, g.paths[j].tileY + 1); tY++) {
							// TODO?: use more accurate time at tiles other than (u[j].tileX, u[j].tileY)
							g.playerVisRemove(g.paths[path].player(), tX, tY, time);
						}
					}
				}
			}
		}
	}
}

/// <summary>
/// event in which a player stops seeing tiles
/// </summary>
public class PlayerVisRemoveEvt : SimEvt {
	public int player;
	public int nTiles;
	public FP.Vector[] tiles;

	public PlayerVisRemoveEvt(long timeVal, int playerVal, int tileXVal, int tileYVal) {
		time = timeVal;
		player = playerVal;
		nTiles = 1;
		tiles = new FP.Vector[1];
		tiles[0] = new FP.Vector(tileXVal, tileYVal);
	}

	public override void apply(Sim g) {
		int i, iPrev, j, tX, tY;
		for (i = 0; i < nTiles; i++) {
			if (g.tiles[tiles[i].x, tiles[i].y].playerVisLatest(player) && !g.tiles[tiles[i].x, tiles[i].y].playerDirectVisLatest(player)) {
				g.tiles[tiles[i].x, tiles[i].y].playerVis[player].Add(time);
				// add events to remove visibility from surrounding tiles
				for (tX = Math.Max(0, (int)tiles[i].x - 1); tX <= Math.Min(g.tileLen() - 1, (int)tiles[i].x + 1); tX++) {
					for (tY = Math.Max(0, (int)tiles[i].y - 1); tY <= Math.Min(g.tileLen() - 1, (int)tiles[i].y + 1); tY++) {
						if ((tX != tiles[i].x || tY != tiles[i].y) && g.tiles[tX, tY].playerVisLatest(player)) {
							// TODO: use more accurate time
							g.playerVisRemove(player, tX, tY, time + (1 << FP.Precision) / g.maxSpeed);
						}
					}
				}
			}
			else {
				tiles[i].x = Sim.OffMap;
			}
		}
		// check if a tile stopped being exclusive to this player, or became exclusive to another player
		iPrev = -1;
		for (i = 0; i < nTiles; i++) {
			if (tiles[i].x != Sim.OffMap) {
				for (tX = Math.Max(0, (int)tiles[i].x - g.tileVisRadius()); tX <= Math.Min(g.tileLen() - 1, (int)tiles[i].x + g.tileVisRadius()); tX++) {
					for (tY = Math.Max(0, (int)tiles[i].y - g.tileVisRadius()); tY <= Math.Min(g.tileLen() - 1, (int)tiles[i].y + g.tileVisRadius()); tY++) {
						if (g.inVis(tX - tiles[i].x, tY - tiles[i].y) && (iPrev == -1 || !g.inVis(tX - tiles[iPrev].x, tY - tiles[iPrev].y))) {
							for (j = 0; j < g.nPlayers; j++) {
								if (j == player && g.tiles[tX, tY].exclusiveLatest(j)) {
									g.exclusiveRemove(j, tX, tY, time);
								}
								else if (j != player && !g.tiles[tX, tY].exclusiveLatest(j) && g.calcExclusive(j, tX, tY)) {
									g.exclusiveAdd(j, tX, tY, time);
								}
							}
						}
					}
				}
				iPrev = i;
			}
		}
	}
}

/// <summary>
/// list of simulation events in order of ascending event time
/// </summary>
public class SimEvtList {
	public List<SimEvt> events;

	public SimEvtList() {
		events = new List<SimEvt>();
	}

	/// <summary>
	/// inserts specified event into list in order of ascending event time
	/// </summary>
	public void add(SimEvt evt) {
		int ins;
		for (ins = events.Count; ins >= 1 && evt.time < events[ins - 1].time; ins--) ;
		events.Insert(ins, evt);
	}

	/// <summary>
	/// pops the first (earliest) event from the list, returning null if list is empty
	/// </summary>
	public SimEvt pop() {
		if (events.Count == 0) return null;
		SimEvt ret = events[0];
		events.RemoveAt(0);
		return ret;
	}

	/// <summary>
	/// returns time of first (earliest) event in list, or long.MaxValue if list is empty
	/// </summary>
	public long peekTime() {
		if (events.Count == 0) return long.MaxValue;
		return events[0].time;
	}
}
