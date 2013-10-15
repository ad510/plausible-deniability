﻿// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// RTS game unit that can do strange things when other players aren't looking
/// </summary>
public class Unit {
	/// <summary>
	/// represents a single unit movement that starts at a specified location,
	/// moves at constant velocity to a specified end location, then stops
	/// </summary>
	public class Move {
		public long timeStart; // time when starts moving
		public long timeEnd; // time when finishes moving
		public FP.Vector vecStart; // location at timeStart, z indicates rotation (TODO: implement rotation)
		public FP.Vector vecEnd; // location at timeEnd, z indicates rotation

		/// <summary>
		/// constructor that directly sets all instance variables
		/// </summary>
		public Move(long timeStartVal, long timeEndVal, FP.Vector vecStartVal, FP.Vector vecEndVal) {
			timeStart = timeStartVal;
			timeEnd = timeEndVal;
			vecStart = vecStartVal;
			vecEnd = vecEndVal;
		}

		/// <summary>
		/// constructor for nonmoving trajectory
		/// </summary>
		public Move(long timeVal, FP.Vector vecVal)
			: this(timeVal, timeVal + 1, vecVal, vecVal) {
		}

		/// <summary>
		/// alternate method to create Unit.Move object that asks for speed (in position units per millisecond) instead of end time
		/// </summary>
		public static Move fromSpeed(long timeStartVal, long speed, FP.Vector vecStartVal, FP.Vector vecEndVal) {
			return new Move(timeStartVal, timeStartVal + new FP.Vector(vecEndVal - vecStartVal).length() / speed, vecStartVal, vecEndVal);
		}

		/// <summary>
		/// returns location at specified time
		/// </summary>
		public FP.Vector calcPos(long time) {
			if (time >= timeEnd) return vecEnd;
			return vecStart + (vecEnd - vecStart) * FP.div(time - timeStart, timeEnd - timeStart);
		}

		/// <summary>
		/// returns time when position is at specified x value (inaccurate when x isn't between vecStart.x and vecEnd.x)
		/// </summary>
		public long timeAtX(long x) {
			return FP.lineCalcX(new FP.Vector(timeStart, vecStart.x), new FP.Vector(timeEnd, vecEnd.x), x);
		}

		/// <summary>
		/// returns time when position is at specified y value (inaccurate when y isn't between vecStart.y and vecEnd.y)
		/// </summary>
		public long timeAtY(long y) {
			return FP.lineCalcX(new FP.Vector(timeStart, vecStart.y), new FP.Vector(timeEnd, vecEnd.y), y);
		}
	}

	private Sim g;
	private int id; // index in unit array
	public int type;
	public int player;
	public int nMoves; // number of moves
	public Move[] moves; // array of moves (later moves are later in array)
	//public FP.Vector pos; // current position
	public int tileX, tileY; // current position on visibility tiles
	public int nTimeHealth;
	public long[] timeHealth; // times at which each health increment is removed
	public long timeAttack; // latest time that attacked a unit
	public long timeSimPast; // time traveling simulation time if made in the past, otherwise set to long.MaxValue
	public long timeUnseen; // earliest time at which unit is known to not be seen by another player (set to long.MaxValue if never)
	public int parent; // index of unit that made this unit (set to <0 if none)
	public bool isChildPath; // whether this is a temporary unit moving along an alternate path that its parent unit could take
	public bool replaceParentPath; // whether should replace parent unit's path with this unit's path when this unit becomes live
	public int nChildren;
	public int[] children; // indices of units that this unit made

	public Unit(Sim simVal, int typeVal, int playerVal) {
		g = simVal;
		type = typeVal;
		player = playerVal;
		nTimeHealth = 0;
		timeHealth = new long[g.unitT[type].maxHealth];
		timeAttack = long.MinValue;
	}

	/// <summary>
	/// old unit constructor (before some functionality moved to Path class)
	/// </summary>
	public Unit(Sim simVal, int idVal, int typeVal, int playerVal, long startTime, FP.Vector startPos) {
		g = simVal;
		id = idVal;
		type = typeVal;
		player = playerVal;
		nMoves = 1;
		moves = new Move[nMoves];
		moves[0] = new Move(startTime, startPos);
		//pos = startPos;
		tileX = Sim.OffMap + 1;
		tileY = Sim.OffMap + 1;
		nTimeHealth = 0;
		timeHealth = new long[g.unitT[type].maxHealth];
		timeAttack = long.MinValue;
		timeSimPast = (startTime > g.timeSim) ? long.MaxValue : startTime;
		timeUnseen = g.tileAt(startPos).exclusiveWhen(player, startTime) ? startTime : long.MaxValue;
		parent = -1;
		isChildPath = false;
		replaceParentPath = false;
		nChildren = 0;
		children = new int[nChildren];
	}

	/// <summary>
	/// ensure that if unit is moving in the past, it does not move off exclusively seen areas
	/// </summary>
	public void updatePast(long curTime) {
		if (curTime <= timeSimPast || !exists(curTime)) return;
		long timeSimPastNext = Math.Min(curTime, g.timeSim);
		SimEvtList pastEvents = new SimEvtList();
		TileMoveEvt evt;
		FP.Vector pos;
		int tX, tY, exclusiveIndex;
		// delete unit if tile that unit starts on stops being exclusive since timeSimPast
		pos = calcPos(timeSimPast);
		tX = (int)(pos.x >> FP.Precision);
		tY = (int)(pos.y >> FP.Precision);
		// TODO: without modifications, line below may cause syncing problems in multiplayer b/c addTileMoveEvts() sometimes adds events before timeSimPast
		addTileMoveEvts(ref pastEvents, timeSimPast, timeSimPastNext);
		evt = (TileMoveEvt)pastEvents.pop();
		exclusiveIndex = g.tiles[tX, tY].exclusiveIndexWhen(player, (evt != null) ? evt.time - 1 : curTime);
		if (!g.tiles[tX, tY].exclusiveWhen(player, (evt != null) ? evt.time - 1 : curTime)
				|| g.tiles[tX, tY].exclusive[player][exclusiveIndex] > timeSimPast) {
			if (!delete(g.timeSim)) throw new SystemException("unit not deleted successfully after moving off exclusive area");
			return;
		}
		// delete unit if unit moves off exclusive area or tile that unit is on stops being exclusive
		if (evt != null) {
			do {
				if (evt.tileX != int.MinValue) tX = evt.tileX;
				if (evt.tileY != int.MinValue) tY = evt.tileY;
				exclusiveIndex = g.tiles[tX, tY].exclusiveIndexWhen(player, evt.time);
				if (!g.tiles[tX, tY].exclusiveWhen(player, evt.time)
						|| (exclusiveIndex + 1 < g.tiles[tX, tY].exclusive[player].Count() && g.tiles[tX, tY].exclusive[player][exclusiveIndex + 1] <= Math.Min(g.events.peekTime(), timeSimPastNext))) {
					if (!delete(g.timeSim)) throw new SystemException("unit not deleted successfully after moving off exclusive area");
					return;
				}
			} while ((evt = (TileMoveEvt)pastEvents.pop()) != null);
		}
		// update past simulation time
		timeSimPast = timeSimPastNext;
	}

	/// <summary>
	/// intelligently resize move array to specified size
	/// </summary>
	private void setN(int newSize) {
		int i = 0;
		for (i = nMoves; i < Math.Min(newSize, moves.Length); i++) {
			moves[i] = new Move(0, new FP.Vector());
		}
		nMoves = newSize;
		if (nMoves > moves.Length)
			Array.Resize(ref moves, nMoves * 2);
	}

	/// <summary>
	/// add specified move to end of move array
	/// </summary>
	/// <remarks>
	/// if caller also adds a TileMoveEvt, must ensure that it isn't deleted in update()
	/// (add allowOverride variable in TileMoveEvt if necessary)
	/// </remarks>
	private void addMove(Move newMove) {
		setN(nMoves + 1);
		moves[nMoves - 1] = newMove;
		if (!g.movedPaths.Contains(id)) g.movedPaths.Add(id); // indicate to delete and recalculate later TileMoveEvts for this unit
	}

	/// <summary>
	/// move towards specified location starting at specified time,
	/// return index of moved unit (in case moving a replacement path instead of this unit)
	/// </summary>
	public int moveTo(long time, FP.Vector pos) {
		int unit2 = (time < g.timeSim) ? prepareNonLivePath(time) : id; // move replacement unit instead of live unit if in past
		FP.Vector curPos = calcPos(time);
		FP.Vector goalPos = pos;
		// don't move off map edge
		if (goalPos.x < 0) goalPos.x = 0;
		if (goalPos.x > g.mapSize) goalPos.x = g.mapSize;
		if (goalPos.y < 0) goalPos.y = 0;
		if (goalPos.y > g.mapSize) goalPos.y = g.mapSize;
		// add move
		g.units[unit2].addMove(Move.fromSpeed(time, g.unitT[g.units[unit2].type].speed, curPos, goalPos));
		return unit2;
	}

	/// <summary>
	/// returns whether allowed to move at specified time
	/// </summary>
	public bool canMove(long time) {
		return exists(time) && (time >= g.timeSim || time >= timeUnseen) && g.unitT[type].speed > 0;
	}

	/// <summary>
	/// returns location at specified time
	/// </summary>
	public FP.Vector calcPos(long time) {
		return moves[getMove(time)].calcPos(time);
	}

	/// <summary>
	/// returns index of move that is occurring at specified time
	/// </summary>
	public int getMove(long time) {
		int ret = nMoves - 1;
		while (ret >= 0 && time < moves[ret].timeStart) ret--;
		return ret;
	}

	/// <summary>
	/// inserts TileMoveEvt events for this unit into events for the time interval from timeMin to timeMax
	/// </summary>
	/// <remarks>due to fixed point imprecision in lineCalcX() and lineCalcY(), this sometimes adds events outside the requested time interval</remarks>
	public void addTileMoveEvts(ref SimEvtList events, long timeMin, long timeMax) {
		int move, moveLast;
		FP.Vector pos, posLast;
		int i, j, iNext, tX, tY, dir;
		if (timeMax < moves[0].timeStart) return;
		moveLast = getMove(timeMin);
		move = getMove(timeMax);
		if (moveLast < 0) {
			// put unit on visibility tiles for the first time
			events.add(new TileMoveEvt(moves[0].timeStart, id, (int)(moves[0].vecStart.x >> FP.Precision), (int)(moves[0].vecStart.y >> FP.Precision)));
			moveLast = 0;
		}
		for (i = moveLast; i <= move; i = iNext) {
			// next move may not be i + 1 if times are out of order
			iNext = i + 1;
			for (j = iNext + 1; j < nMoves; j++) {
				if (moves[j].timeStart <= moves[iNext].timeStart) iNext = j;
			}
			posLast = (i == moveLast) ? moves[i].calcPos(Math.Max(timeMin, moves[0].timeStart)) : moves[i].vecStart;
			pos = (i == move) ? moves[i].calcPos(timeMax) : moves[iNext].vecStart;
			// moving between columns (x)
			dir = (pos.x >= posLast.x) ? 0 : -1;
			for (tX = (int)(Math.Min(pos.x, posLast.x) >> FP.Precision) + 1; tX <= (int)(Math.Max(pos.x, posLast.x) >> FP.Precision); tX++) {
				events.add(new TileMoveEvt(moves[i].timeAtX(tX << FP.Precision), id, tX + dir, int.MinValue));
			}
			// moving between rows (y)
			dir = (pos.y >= posLast.y) ? 0 : -1;
			for (tY = (int)(Math.Min(pos.y, posLast.y) >> FP.Precision) + 1; tY <= (int)(Math.Max(pos.y, posLast.y) >> FP.Precision); tY++) {
				events.add(new TileMoveEvt(moves[i].timeAtY(tY << FP.Precision), id, int.MinValue, tY + dir));
			}
		}
		if (healthLatest() == 0 && healthWhen(timeMin) > 0) {
			// unit lost all health
			g.events.add(new TileMoveEvt(timeHealth[nTimeHealth - 1], id, Sim.OffMap, 0));
		}
	}

	/// <summary>
	/// remove 1 health increment at specified time
	/// </summary>
	public void takeHealth(long time) {
		if (nTimeHealth < g.unitT[type].maxHealth) {
			nTimeHealth++;
			timeHealth[nTimeHealth - 1] = time;
			if (nTimeHealth >= g.unitT[type].maxHealth) {
				// unit lost all health
				if (!g.movedPaths.Contains(id)) g.movedPaths.Add(id); // indicate to delete and recalculate later TileMoveEvts for this unit
			}
		}
	}

	/// <summary>
	/// returns health of this unit at latest possible time
	/// </summary>
	public int healthLatest() {
		return g.unitT[type].maxHealth - nTimeHealth;
	}

	/// <summary>
	/// returns health of this unit at specified time
	/// </summary>
	public int healthWhen(long time) {
		int i = nTimeHealth;
		while (i > 0 && time < timeHealth[i - 1]) i--;
		return g.unitT[type].maxHealth - i;
	}

	/// <summary>
	/// let unit be updated in the present (i.e., stop time traveling) starting at timeSim
	/// </summary>
	public void goLive() {
		timeSimPast = long.MaxValue;
		if (replaceParentPath) {
			replaceParentPath = false;
			g.units[parent].deleteChildrenAfter(moves[0].timeStart);
			movePathToParent();
		}
		else {
			FP.Vector pos = calcPos(g.timeSim);
			g.events.add(new TileMoveEvt(g.timeSim, id, (int)(pos.x >> FP.Precision), (int)(pos.y >> FP.Precision)));
		}
	}

	/// <summary>
	/// allows unit to time travel and move along multiple paths starting at specified time
	/// </summary>
	public void beUnseen(long time) {
		timeUnseen = time;
	}

	/// <summary>
	/// stops allowing unit to time travel or move along multiple paths starting at timeSim
	/// </summary>
	public void beSeen(long time = long.MaxValue) {
		if (time > timeUnseen) timeUnseen = time;
		// delete all child paths made before time unseen
		for (int i = 0; i < nChildren; i++) {
			if (g.units[children[i]].isChildPath && g.units[children[i]].moves[0].timeStart < timeUnseen) {
				if (!g.units[children[i]].delete(time)) throw new SystemException("child unit not deleted successfully");
				i--;
			}
		}
		if (parent >= 0) {
			if (isChildPath) {
				int parentPathTemp = parent;
				g.units[parent].deleteChildrenAfter(moves[0].timeStart);
				movePathToParent();
				g.units[parentPathTemp].beSeen();
			}
			else {
				g.units[parent].beSeen(moves[0].timeStart);
			}
		}
	}

	/// <summary>
	/// delete this unit if doing so wouldn't affect anything that another player saw, returns whether successful
	/// </summary>
	public bool delete(long time, bool skipRscCheck = false) {
		bool hasUnseenChild = false;
		int i;
		if (nChildren > 0) {
			// take the path of the latest child path (overwriting our current moves in the process)
			int path = -1;
			for (i = 0; i < nChildren; i++) {
				if (g.units[children[i]].isChildPath // child unit must be a child path
					&& (g.units[children[i]].isLive(time) || (!isLive(time) && g.units[children[i]].exists(time))) // child unit must be live, unless this unit isn't
					&& g.units[children[i]].moves[0].timeStart <= time // child unit may not be made after the specified time
					&& (path < 0 || g.units[children[i]].moves[0].timeStart > g.units[path].moves[0].timeStart)) { // child unit must be made after current latest child
					path = children[i];
				}
				else if (g.units[children[i]].timeUnseen != g.units[children[i]].moves[0].timeStart) {
					hasUnseenChild = true;
				}
			}
			if (path >= 0) {
				deleteChildrenAfter(g.units[path].moves[0].timeStart); // delete non-live child units made after the child path that we will take
				g.units[path].movePathToParent();
				return true;
			}
		}
		if (parent >= 0 && timeUnseen == moves[0].timeStart && !hasUnseenChild) {
			// if we can't become a child unit but have a parent unit and were never seen by another player, delete this unit completely
			if (timeSimPast == long.MaxValue && !isChildPath && !skipRscCheck) {
				// check if deleting unit might lead to player having negative resources
				// (don't need to check this if taking the path of another unit b/c no combination of paths are allowed to give player negative resources)
				Move m0Original = moves[0];
				moves[0] = new Move(long.MaxValue - 1, new FP.Vector(Sim.OffMap, 0)); // simulate this unit (and implicitly its child units) not existing during the check
				if (g.playerCheckNegRsc(player, m0Original.timeStart, false, false) >= 0) {
					moves[0] = m0Original;
					return false;
				}
			}
			if (replaceParentPath) {
				g.unitIdChgs.Add(id);
				g.unitIdChgs.Add(parent);
			}
			deleteAllChildren();
			g.units[parent].deleteChild(id);
			return true;
		}
		return false; // deleting this unit would change something that another player saw (assuming they also know the scenario's starting state)
	}

	/// <summary>
	/// makes a new unit made by this unit, returns whether successful
	/// </summary>
	public bool makeChildUnit(long time, bool isChildPathVal, int typeVal = -1) {
		if (canMakeChildUnit(time, isChildPathVal, typeVal)) {
			FP.Vector pos = calcPos(time);
			// make new unit
			g.setNUnits(g.nUnits + 1);
			g.units[g.nUnits - 1] = new Unit(g, g.nUnits - 1, isChildPathVal ? type : typeVal, player, time, pos);
			// indicate that we are the new unit's parent
			addChild(g.nUnits - 1);
			// if this unit isn't live, new unit can't be either
			if (!isLive(time)) g.units[g.nUnits - 1].timeSimPast = time;
			// set whether new unit is a temporary unit moving along an alternate path that this unit could take
			g.units[g.nUnits - 1].isChildPath = isChildPathVal;
			// indicate to calculate TileMoveEvts for new unit starting at timeSim
			if (!g.movedPaths.Contains(g.nUnits - 1)) g.movedPaths.Add(g.nUnits - 1);
			// if new unit isn't live, indicate that player now has a non-live unit
			if (!g.units[g.nUnits - 1].isLive(time)) g.players[player].hasNonLiveUnits = true;
			return true;
		}
		return false;
	}

	/// <summary>
	/// returns whether this unit can make a new unit
	/// </summary>
	public bool canMakeChildUnit(long time, bool isChildPathVal, int typeVal = -1) {
		if (exists(time)) {
			if (isChildPathVal) {
				if (time >= timeUnseen) return true;
			}
			else {
				bool newUnitIsLive = (time >= g.timeSim && isLive(time));
				if (g.unitT[type].canMake[typeVal] && (time >= g.timeSim || time >= timeUnseen)) {
					for (int i = 0; i < g.nRsc; i++) {
						// TODO: may be more permissive by passing in max = true, but this really complicates delete() algorithm (see planning notes)
						if (g.playerResource(player, time, i, false, !newUnitIsLive, !newUnitIsLive) < g.unitT[typeVal].rscCost[i]) return false;
					}
					return true;
				}
			}
		}
		return false;
	}

	/// <summary>
	/// returns index (in unit array) of path that isn't updated in the present and is therefore safe to move in the past
	/// </summary>
	private int prepareNonLivePath(long time) {
		if (timeSimPast != long.MaxValue) {
			// this unit isn't live, prepare for a new move to be added at specified time
			deleteChildrenAfter(time);
			if (time < timeSimPast) timeSimPast = time;
			return id;
		}
		else {
			// this unit is live, make new child unit to replace this unit's path when the child unit becomes live
			for (int i = 0; i < nChildren; i++) {
				if (g.units[children[i]].replaceParentPath) {
					// delete existing replacement path before making a new one
					g.units[children[i]].delete(g.units[children[i]].moves[0].timeStart);
					break;
				}
			}
			makeChildUnit(time, true);
			g.units[children[nChildren - 1]].replaceParentPath = true;
			g.unitIdChgs.Add(id);
			g.unitIdChgs.Add(children[nChildren - 1]);
			return children[nChildren - 1];
		}
	}

	/// <summary>
	/// mark existing unit as a child of this unit
	/// </summary>
	private void addChild(int unit) {
		nChildren++;
		if (nChildren > children.Length)
			Array.Resize(ref children, nChildren * 2);
		children[nChildren - 1] = unit;
		g.units[unit].parent = id;
	}

	/// <summary>
	/// non-recursively delete specified child unit
	/// </summary>
	private void deleteChild(int unit) {
		int index;
		for (index = 0; index < nChildren && children[index] != unit; index++) ;
		if (index == nChildren) throw new ArgumentException("this unit didn't make unit " + unit);
		// remove child unit from list
		for (int i = index; i < nChildren - 1; i++) {
			children[i] = children[i + 1];
		}
		nChildren--;
		// delete child unit
		g.units[unit].deleteAllMoves();
		g.units[unit].parent = -1;
	}

	/// <summary>
	/// recursively delete all child units
	/// </summary>
	/// <remarks>this does not check whether deleting the units may lead to player having negative resources</remarks>
	private void deleteAllChildren() {
		for (int i = 0; i < nChildren; i++) {
			g.units[children[i]].deleteAllMoves();
			g.units[children[i]].parent = -1;
			g.units[children[i]].deleteAllChildren();
		}
		nChildren = 0;
	}

	/// <summary>
	/// delete child units made after the specified time
	/// </summary>
	/// <remarks>this does not check whether deleting the units may lead to player having negative resources</remarks>
	private void deleteChildrenAfter(long time) {
		for (int i = 0; i < nChildren; i++) {
			if (g.units[children[i]].moves[0].timeStart > time) {
				g.units[children[i]].delete(time, true);
				i--;
			}
		}
	}

	/// <summary>
	/// make parent unit take the path of this unit, then delete this unit
	/// </summary>
	private void movePathToParent() {
		FP.Vector pos = calcPos(g.timeSim);
		int i;
		// indicate that this unit changed indices
		g.unitIdChgs.Add(parent);
		g.unitIdChgs.Add(-1);
		g.unitIdChgs.Add(id);
		g.unitIdChgs.Add(parent);
		// move all moves to parent unit
		for (i = 0; i < nMoves; i++) {
			g.units[parent].addMove(moves[i]);
		}
		// move parent unit onto tile that we are currently on
		// can't pass in tileX and tileY because this unit's latest TileMoveEvts might not be applied yet
		g.events.add(new TileMoveEvt(g.timeSim, parent, (int)(pos.x >> FP.Precision), (int)(pos.y >> FP.Precision)));
		// move child units to parent unit
		for (i = 0; i < nChildren; i++) {
			g.units[parent].addChild(children[i]);
		}
		nChildren = 0;
		// delete this unit since it is now incorporated into its parent unit
		g.units[parent].deleteChild(id);
	}

	/// <summary>
	/// change unit movement to make it look like this unit never existed
	/// </summary>
	private void deleteAllMoves() {
		nMoves = 0;
		moves[0] = new Move(long.MaxValue - 1, new FP.Vector(Sim.OffMap, 0));
		timeUnseen = long.MaxValue;
		g.events.add(new TileMoveEvt(g.timeSim, id, Sim.OffMap, 0));
	}

	/// <summary>
	/// returns index of this unit's oldest ancestor (which was not made by another unit)
	/// </summary>
	public int rootParent() {
		int ret = id;
		while (g.units[ret].parent >= 0) ret = g.units[ret].parent;
		return ret;
	}

	/// <summary>
	/// returns index of oldest ancestor that this unit is an alternate path for
	/// </summary>
	public int rootParentPath() {
		int ret = id;
		while (g.units[ret].isChildPath) ret = g.units[ret].parent;
		return ret;
	}

	/// <summary>
	/// returns resource amount gained by this unit and its child units (subtracting cost to make the units)
	/// </summary>
	/// <param name="max">
	/// since different paths can have collected different resource amounts,
	/// determines whether to use paths that collected least or most resources in calculation
	/// </param>
	public long rscCollected(long time, int rscType, bool max, bool includeNonLiveChildren, bool alwaysUseReplacementPaths) {
		if (time < moves[0].timeStart) return 0; // if this unit isn't made yet, it can't have collected anything
		List<int> childrenList = new List<int>(children);
		long timeCollectEnd = (healthWhen(time) == 0) ? timeHealth[nTimeHealth - 1] : time;
		long pathCollected;
		bool foundReplacementPath = false;
		long ret = 0;
		childrenList.RemoveRange(nChildren, children.Length - nChildren);
		foreach (int child in childrenList.OrderByDescending(i => g.units[i].moves[0].timeStart)) {
			if (time >= g.units[child].moves[0].timeStart && (includeNonLiveChildren || g.units[child].timeSimPast == long.MaxValue)) {
				if (g.units[child].isChildPath) {
					if (!alwaysUseReplacementPaths || !foundReplacementPath) {
						// if child unit is one of this unit's paths and collected more/less (depending on max parameter) resources than this path,
						// use that path for resource calculation
						pathCollected = g.units[child].rscCollected(time, rscType, max, includeNonLiveChildren, alwaysUseReplacementPaths);
						if ((alwaysUseReplacementPaths && g.units[child].replaceParentPath)
								|| max ^ (pathCollected < ret + g.unitT[type].rscCollectRate[rscType] * (timeCollectEnd - g.units[child].moves[0].timeStart))) {
							ret = pathCollected;
							timeCollectEnd = g.units[child].moves[0].timeStart;
							if (alwaysUseReplacementPaths && g.units[child].replaceParentPath) foundReplacementPath = true;
						}
					}
				}
				else {
					// add resources that non-path child unit gained
					ret += g.units[child].rscCollected(time, rscType, max, includeNonLiveChildren, alwaysUseReplacementPaths);
				}
			}
		}
		// add resources collected by this unit
		ret += g.unitT[type].rscCollectRate[rscType] * (timeCollectEnd - moves[0].timeStart);
		// if unit was made by another unit, subtract cost to make it
		if (parent >= 0 && !isChildPath) ret -= g.unitT[type].rscCost[rscType];
		return ret;
	}

	/// <summary>
	/// returns whether unit is known to not be seen by another player at latest known time
	/// </summary>
	public bool unseen() {
		return timeUnseen != long.MaxValue;
	}

	/// <summary>
	/// returns whether unit is created and has health at specified time
	/// </summary>
	public bool exists(long time) {
		return time >= moves[0].timeStart && healthWhen(time) > 0;
	}

	/// <summary>
	/// returns whether unit exists and is being updated in the present (i.e., isn't time traveling)
	/// </summary>
	public bool isLive(long time) {
		return exists(time) && timeSimPast == long.MaxValue;
	}
}
