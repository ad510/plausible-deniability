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
		FP.Vector goalCenter, goal, rows = new FP.Vector(), offset = new FP.Vector();
		long spacing = 0;
		int count = 0, i = 0;
		base.apply(g);
		// count number of units able to move
		foreach (int path in paths.Keys) {
			if (g.paths[path].canMove(timeCmd)) {
				count++;
				// STACK TODO: implement line below
				//if (formation == Formation.Tight && g.unitT[g.paths[path].type].tightFormationSpacing > spacing) spacing = g.unitT[g.units[unit].type].tightFormationSpacing;
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
		foreach (int path in paths.Keys) {
			if (g.paths[path].canMove(timeCmd)) {
				if (formation == Formation.Tight || formation == Formation.Loose) {
					goal = goalCenter + new FP.Vector((i % rows.x) * spacing - offset.x, i / rows.x * spacing - offset.y);
				}
				else if (formation == Formation.Ring) {
					goal = goalCenter + offset.x * new FP.Vector(FP.cos(2 * FP.Pi * i / count), FP.sin(2 * FP.Pi * i / count));
				}
				else {
					throw new NotImplementedException("requested formation is not implemented");
				}
				g.paths[path].moveTo(timeCmd, goal);
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
		if (!autoRepeat) base.apply(g);
		// make unit at requested position, if possible
		foreach (int unit in units) {
			if (g.units[unit].canMakeChildUnit(timeCmd, false, type)) {
				FP.Vector curPos = g.units[unit].calcPos(timeCmd);
				if ((pos.x == curPos.x && pos.y == curPos.y) || (g.unitT[type].speed > 0 && g.unitT[type].makeOnUnitT < 0)) {
					// TODO: take time to make units?
					g.units[unit].makeChildUnit(timeCmd, false, type);
					if (g.units[g.nUnits - 1].canMove(timeCmd)) g.units[g.nUnits - 1].moveTo(timeCmd, pos); // move new unit out of the way
					return;
				}
			}
		}
		if (!autoRepeat) {
			// if none of specified units are at requested position,
			// try moving one to the correct position then trying again to make the unit
			int moveUnit = -1;
			foreach (int unit in units) {
				if (g.units[unit].canMakeChildUnit(timeCmd, false, type) && g.units[unit].canMove(timeCmd)
					&& (moveUnit < 0 || (g.units[unit].calcPos(timeCmd) - pos).lengthSq() < (g.units[moveUnit].calcPos(timeCmd) - pos).lengthSq())) {
					moveUnit = unit;
				}
			}
			if (moveUnit >= 0) {
				List<int> unitsList = new List<int>(units);
				moveUnit = g.units[moveUnit].moveTo(timeCmd, pos);
				unitsList.Insert(0, moveUnit); // in case replacement unit is moving to make the unit
				// STACK TODO: constructor arguments changed
				//g.events.add(new MakeUnitCmdEvt(g.units[moveUnit].moves[g.units[moveUnit].nMoves - 1].timeEnd, g.units[moveUnit].moves[g.units[moveUnit].nMoves - 1].timeEnd + 1,
				//	unitsList.ToArray(), type, pos, true));
			}
		}
	}
}

/// <summary>
/// command to make new path(s) that specified path(s) could take
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
		base.apply(g);
		foreach (KeyValuePair<int, int[]> path in paths) {
			if (g.paths[path.Key].canMove(timeCmd) && g.paths[path.Key].makePath (timeCmd, new List<int>(path.Value))) {
				g.paths[g.paths.Count - 1].moveTo(timeCmd, pos[path.Key]); // move new path out of the way
			}
		}
	}
}

/// <summary>
/// command to delete path(s)
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
		base.apply(g);
		foreach (int unit in units) {
			// check if unit changed index due to a previous path deletion
			int unit2 = unit;
			for (int i = 0; i < g.unitIdChgs.Count / 2; i++) {
				if (unit2 == g.unitIdChgs[i * 2]) unit2 = g.unitIdChgs[i * 2 + 1];
			}
			if (unit2 >= 0) g.units[unit2].delete(timeCmd);
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
				// ensure that time traveling units don't move off coherent areas
				g.units[i].updatePast(time);
				// find earliest time that player's units started time traveling
				if (g.units[i].moves[0].timeStart < timeTravelStart) timeTravelStart = g.units[i].moves[0].timeStart;
			}
		}
		if (timeTravelStart != long.MaxValue) { // skip if player has no time traveling units
			// check if going live might lead to player having negative resources
			g.players[player].timeNegRsc = g.playerCheckNegRsc(player, timeTravelStart, true, true);
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
		int cmdType, target;
		long distSq, targetDistSq;
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
		for (i = 0; i < g.nUnits; i++) {
			if (g.units[i].isLive(time) && time >= g.units[i].timeAttack + g.unitT[g.units[i].type].reload) {
				// done reloading, look for closest target to potentially attack
				pos = g.units[i].calcPos(time);
				target = -1;
				targetDistSq = g.unitT[g.units[i].type].range * g.unitT[g.units[i].type].range + 1;
				for (j = 0; j < g.nUnits; j++) {
					if (i != j && g.units[j].isLive(time) && g.players[g.units[i].player].mayAttack[g.units[j].player] && g.unitT[g.units[i].type].damage[g.units[j].type] > 0) {
						distSq = (g.units[j].calcPos(time) - pos).lengthSq();
						if (distSq < targetDistSq) {
							target = j;
							targetDistSq = distSq;
						}
					}
				}
				if (target >= 0) {
					// attack target
					// take health with 1 ms delay so earlier units in array don't have unfair advantage
					for (j = 0; j < g.unitT[g.units[i].type].damage[g.units[target].type]; j++) g.units[target].takeHealth(time + 1);
					g.units[i].timeAttack = time;
				}
			}
		}
		// add events to move units between tiles
		// this shouldn't be done in Sim.update() because addTileMoveEvts() sometimes adds events before timeSim
		for (i = 0; i < g.nUnits; i++) {
			if (g.units[i].timeSimPast == long.MaxValue) g.units[i].addTileMoveEvts(ref g.events, time, time + g.updateInterval);
		}
		g.movedUnits.Clear();
		// add next UpdateEvt
		g.checksum = 0;
		g.events.add(new UpdateEvt(time + g.updateInterval));
		g.timeUpdateEvt = time;
	}
}

/// <summary>
/// event in which unit moves between visibility tiles
/// </summary>
/// <remarks>
/// when making this event, can't rely on a unit's tileX and tileY being up-to-date
/// because the latest TileMoveEvts for that unit might not be applied yet
/// </remarks>
public class TileMoveEvt : SimEvt {
	public int unit;
	public int tileX, tileY; // new tile position, set to int.MinValue to keep current value

	public TileMoveEvt(long timeVal, int unitVal, int tileXVal, int tileYVal) {
		time = timeVal;
		unit = unitVal;
		tileX = tileXVal;
		tileY = tileYVal;
	}

	public override void apply(Sim g) {
		if (g.units[unit].tileX == Sim.OffMap) return; // skip event if unit no longer exists
		List<FP.Vector> playerVisAddTiles = new List<FP.Vector>();
		int coherenceMinX = g.tileLen() - 1;
		int coherenceMaxX = 0;
		int coherenceMinY = g.tileLen() - 1;
		int coherenceMaxY = 0;
		int i, tXPrev, tYPrev, tX, tY;
		if (tileX == int.MinValue) tileX = g.units[unit].tileX;
		if (tileY == int.MinValue) tileY = g.units[unit].tileY;
		tXPrev = g.units[unit].tileX;
		tYPrev = g.units[unit].tileY;
		g.units[unit].tileX = tileX;
		g.units[unit].tileY = tileY;
		// add unit to visibility tiles
		for (tX = tileX - g.tileVisRadius(); tX <= tileX + g.tileVisRadius(); tX++) {
			for (tY = tileY - g.tileVisRadius(); tY <= tileY + g.tileVisRadius(); tY++) {
				if (!g.inVis(tX - tXPrev, tY - tYPrev) && g.inVis(tX - tileX, tY - tileY)) {
					visAdd(g, unit, tX, tY, time, ref playerVisAddTiles);
				}
			}
		}
		// remove unit from visibility tiles
		for (tX = tXPrev - g.tileVisRadius(); tX <= tXPrev + g.tileVisRadius(); tX++) {
			for (tY = tYPrev - g.tileVisRadius(); tY <= tYPrev + g.tileVisRadius(); tY++) {
				if (g.inVis(tX - tXPrev, tY - tYPrev) && !g.inVis(tX - tileX, tY - tileY)) {
					visRemove(g, unit, tX, tY, time);
				}
			}
		}
		// check if tiles cohered for this player
		foreach (FP.Vector vec in playerVisAddTiles) {
			if (vec.x < coherenceMinX) coherenceMinX = (int)vec.x;
			if (vec.x > coherenceMaxX) coherenceMaxX = (int)vec.x;
			if (vec.y < coherenceMinY) coherenceMinY = (int)vec.y;
			if (vec.y > coherenceMaxY) coherenceMaxY = (int)vec.y;
		}
		coherenceMinX = Math.Max(0, coherenceMinX - g.tileVisRadius());
		coherenceMaxX = Math.Min(g.tileLen() - 1, coherenceMaxX + g.tileVisRadius());
		coherenceMinY = Math.Max(0, coherenceMinY - g.tileVisRadius());
		coherenceMaxY = Math.Min(g.tileLen() - 1, coherenceMaxY + g.tileVisRadius());
		for (tX = coherenceMinX; tX <= coherenceMaxX; tX++) {
			for (tY = coherenceMinY; tY <= coherenceMaxY; tY++) {
				foreach (FP.Vector vec in playerVisAddTiles) {
					if (g.inVis(tX - vec.x, tY - vec.y)) {
						if (!g.tiles[tX, tY].coherentLatest(g.units[unit].player) && g.calcCoherent(g.units[unit].player, tX, tY)) {
							g.coherenceAdd(g.units[unit].player, tX, tY, time);
						}
						break;
					}
				}
			}
		}
		if (tileX >= 0 && tileX < g.tileLen() && tileY >= 0 && tileY < g.tileLen()) {
			// update whether this unit may time travel
			if (!g.units[unit].coherent() && g.tiles[tileX, tileY].coherentLatest(g.units[unit].player)) {
				g.units[unit].cohere(time);
			}
			else if (g.units[unit].coherent() && !g.tiles[tileX, tileY].coherentLatest(g.units[unit].player)) {
				g.units[unit].decohere();
			}
			// if this unit moved out of another player's visibility, remove that player's visibility here
			if (!g.players[g.units[unit].player].immutable && tXPrev >= 0 && tXPrev < g.tileLen() && tYPrev >= 0 && tYPrev < g.tileLen()) {
				for (i = 0; i < g.nPlayers; i++) {
					if (i != g.units[unit].player && g.tiles[tXPrev, tYPrev].playerDirectVisLatest(i) && !g.tiles[tileX, tileY].playerDirectVisLatest(i)) {
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
			// if this player can no longer directly see another player's unit, remove this player's visibility there
			foreach (int j in g.tiles[tXPrev, tYPrev].unitVis.Keys) {
				if (g.units[j].player != g.units[unit].player && !g.players[g.units[j].player].immutable && g.units[j].healthLatest() > 0
					&& g.inVis(g.units[j].tileX - tXPrev, g.units[j].tileY - tYPrev) && !g.tiles[g.units[j].tileX, g.units[j].tileY].playerDirectVisLatest(g.units[unit].player)) {
					for (tX = Math.Max(0, g.units[j].tileX - 1); tX <= Math.Min(g.tileLen() - 1, g.units[j].tileX + 1); tX++) {
						for (tY = Math.Max(0, g.units[j].tileY - 1); tY <= Math.Min(g.tileLen() - 1, g.units[j].tileY + 1); tY++) {
							// TODO?: use more accurate time at tiles other than (u[j].tileX, u[j].tileY)
							g.playerVisRemove(g.units[unit].player, tX, tY, time);
						}
					}
				}
			}
		}
	}

	/// <summary>
	/// makes specified tile visible to specified unit starting at specified time, including effects on player visibility
	/// </summary>
	private static void visAdd(Sim g, int unit, int tileX, int tileY, long time, ref List<FP.Vector> playerVisAddTiles) {
		int i;
		if (tileX >= 0 && tileX < g.tileLen() && tileY >= 0 && tileY < g.tileLen()) {
			if (g.tiles[tileX, tileY].unitVisLatest(unit)) throw new InvalidOperationException("unit " + unit + " already sees tile (" + tileX + ", " + tileY + ")");
			// add unit to unit visibility tile
			g.tiles[tileX, tileY].unitVisToggle(unit, time);
			if (!g.tiles[tileX, tileY].playerVisLatest(g.units[unit].player)) {
				g.tiles[tileX, tileY].playerVis[g.units[unit].player].Add(time);
				playerVisAddTiles.Add(new FP.Vector(tileX, tileY));
				// check if this tile decohered for another player
				for (i = 0; i < g.nPlayers; i++) {
					if (i != g.units[unit].player && g.tiles[tileX, tileY].coherentLatest(i)) {
						g.coherenceRemove(i, tileX, tileY, time);
					}
				}
			}
		}
	}

	/// <summary>
	/// makes specified tile not visible to specified unit starting at specified time, including effects on player visibility
	/// </summary>
	private static void visRemove(Sim g, int unit, int tileX, int tileY, long time) {
		int tX, tY;
		long timePlayerVis = long.MaxValue;
		if (tileX >= 0 && tileX < g.tileLen() && tileY >= 0 && tileY < g.tileLen()) {
			if (!g.tiles[tileX, tileY].unitVisLatest(unit)) throw new InvalidOperationException("unit " + unit + " already doesn't see tile (" + tileX + ", " + tileY + ")");
			// remove unit from unit visibility tile
			g.tiles[tileX, tileY].unitVisToggle(unit, time);
			// check if player can't directly see this tile anymore
			if (g.tiles[tileX, tileY].playerVisLatest(g.units[unit].player) && !g.tiles[tileX, tileY].playerDirectVisLatest(g.units[unit].player)) {
				// find lowest time that surrounding tiles lost visibility
				for (tX = Math.Max(0, tileX - 1); tX <= Math.Min(g.tileLen() - 1, tileX + 1); tX++) {
					for (tY = Math.Max(0, tileY - 1); tY <= Math.Min(g.tileLen() - 1, tileY + 1); tY++) {
						if ((tX != tileX || tY != tileY) && !g.tiles[tX, tY].playerVisLatest(g.units[unit].player)) {
							if (g.tiles[tX, tY].playerVis[g.units[unit].player].Count == 0) {
								timePlayerVis = long.MinValue;
							}
							else if (g.tiles[tX, tY].playerVis[g.units[unit].player][g.tiles[tX, tY].playerVis[g.units[unit].player].Count - 1] < timePlayerVis) {
								timePlayerVis = g.tiles[tX, tY].playerVis[g.units[unit].player][g.tiles[tX, tY].playerVis[g.units[unit].player].Count - 1];
							}
						}
					}
				}
				// if player can't see all neighboring tiles, they won't be able to tell if another player's unit moves into this tile
				// so remove this tile's visibility for this player
				if (timePlayerVis != long.MaxValue) {
					timePlayerVis = Math.Max(time, timePlayerVis + (1 << FP.Precision) / g.maxSpeed); // TODO: use more accurate time
					g.playerVisRemove(g.units[unit].player, tileX, tileY, timePlayerVis);
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
		// check if a tile decohered for this player, or cohered for another player
		iPrev = -1;
		for (i = 0; i < nTiles; i++) {
			if (tiles[i].x != Sim.OffMap) {
				for (tX = Math.Max(0, (int)tiles[i].x - g.tileVisRadius()); tX <= Math.Min(g.tileLen() - 1, (int)tiles[i].x + g.tileVisRadius()); tX++) {
					for (tY = Math.Max(0, (int)tiles[i].y - g.tileVisRadius()); tY <= Math.Min(g.tileLen() - 1, (int)tiles[i].y + g.tileVisRadius()); tY++) {
						if (g.inVis(tX - tiles[i].x, tY - tiles[i].y) && (iPrev == -1 || !g.inVis(tX - tiles[iPrev].x, tY - tiles[iPrev].y))) {
							for (j = 0; j < g.nPlayers; j++) {
								if (j == player && g.tiles[tX, tY].coherentLatest(j)) {
									g.coherenceRemove(j, tX, tY, time);
								}
								else if (j != player && !g.tiles[tX, tY].coherentLatest(j) && g.calcCoherent(j, tX, tY)) {
									g.coherenceAdd(j, tX, tY, time);
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
