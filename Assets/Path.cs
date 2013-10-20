// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

// TODO: places with NotImplementedException

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class Path {
	public class Node {
		public long time;
		public List<int> paths; // indices of paths that connect to this path at node time
		public List<int> units; // indices of units on this path starting at node time
		public bool unseen; // whether path is known to not be seen by another player starting at node time
		
		public Node(long timeVal, List<int> unitsVal, bool unseenVal) {
			time = timeVal;
			paths = new List<int>();
			units = unitsVal;
			unseenVal = unseen;
		}
	}
	
	/// <summary>
	/// represents a single movement that starts at a specified location,
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
		/// alternate method to create Path.Move object that asks for speed (in position units per millisecond) instead of end time
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
	private int id; // index in path array
	public List<Node> nodes;
	public List<Move> moves; // later moves are later in list
	public int tileX, tileY; // current position on visibility tiles
	public long timeSimPast; // time traveling simulation time if made in the past, otherwise set to long.MaxValue

	public Path(Sim simVal, int idVal, List<int> units, long startTime, FP.Vector startPos, bool startUnseen) {
		g = simVal;
		id = idVal;
		nodes = new List<Node>();
		// TODO: ensure units all of same player and speed, and how to set unseen if no units
		nodes.Add (new Node(startTime, units, startUnseen));
		moves = new List<Move>();
		moves.Add (new Move(startTime, startPos));
		tileX = Sim.OffMap + 1;
		tileY = Sim.OffMap + 1;
		timeSimPast = (startTime > g.timeSim) ? long.MaxValue : startTime;
	}
	
	public Path(Sim simVal, int idVal, List<int> units, long startTime, FP.Vector startPos)
		: this(simVal, idVal, units, startTime, startPos, simVal.tileAt(startPos).exclusiveWhen(simVal.units[units[0]].player, startTime)) {
	}

	/// <summary>
	/// ensure that if unit is moving in the past, it does not move off exclusively seen areas
	/// </summary>
	public void updatePast(long curTime) {
		throw new NotImplementedException();
	}
	
	/// <summary>
	/// returns index of node that is active at specified time
	/// </summary>
	public int getNode(long time) {
		int ret = nodes.Count - 1;
		while (ret >= 0 && time < nodes[ret].time) ret--;
		return ret;
	}
	
	public int insertNode(long time) {
		int node = getNode (time);
		if (node < 0 || nodes[node].time == time) return node;
		nodes.Insert (node + 1, new Node(time, new List<int>(nodes[node].units), nodes[node].unseen));
		return node + 1;
	}
	
	/// <summary>
	/// move towards specified location starting at specified time,
	/// return index of moved path (in case moving a subset of units in path)
	/// </summary>
	public int moveTo(long time, List<int> units, FP.Vector pos) {
		int path2 = id; // move this path by default
		int node = getNode (time);
		foreach (int unit in nodes[node].units) {
			if (!units.Contains (unit)) {
				// some units in path aren't being moved, so make a new path
				// TODO: also try to delete unit from old path
				// TODO: this doesn't add path to tiles b/c new path's timeSimPast != long.MaxValue, should be fixed after implementing updatePast()
				if (!makePath (time, units)) throw new SystemException("make new path failed when moving units");
				path2 = g.paths.Count - 1;
				break;
			}
		}
		g.paths[path2].moveTo (time, pos);
		return path2;
	}

	/// <summary>
	/// move towards specified location starting at specified time
	/// </summary>
	public void moveTo(long time, FP.Vector pos) {
		FP.Vector curPos = calcPos(time);
		FP.Vector goalPos = pos;
		// don't move off map edge
		if (goalPos.x < 0) goalPos.x = 0;
		if (goalPos.x > g.mapSize) goalPos.x = g.mapSize;
		if (goalPos.y < 0) goalPos.y = 0;
		if (goalPos.y > g.mapSize) goalPos.y = g.mapSize;
		// add move
		moves.Add (Move.fromSpeed(time, speed(), curPos, goalPos));
		if (!g.movedPaths.Contains(id)) g.movedPaths.Add(id); // indicate to delete and recalculate later TileMoveEvts for this path
	}

	/// <summary>
	/// returns whether allowed to move at specified time
	/// </summary>
	public bool canMove(long time) {
		// TODO: check whether seen later, maybe make overloaded version that also checks units
		return time >= moves[0].timeStart && speed () > 0;
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
		int ret = moves.Count - 1;
		while (ret >= 0 && time < moves[ret].timeStart) ret--;
		return ret;
	}

	/// <summary>
	/// inserts TileMoveEvt events for this path into events for the time interval from timeMin to timeMax
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
			// put path on visibility tiles for the first time
			events.add(new TileMoveEvt(moves[0].timeStart, id, (int)(moves[0].vecStart.x >> FP.Precision), (int)(moves[0].vecStart.y >> FP.Precision)));
			moveLast = 0;
		}
		for (i = moveLast; i <= move; i = iNext) {
			// next move may not be i + 1 if times are out of order
			iNext = i + 1;
			for (j = iNext + 1; j < moves.Count; j++) {
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
		if (nodes[nodes.Count - 1].units.Count == 0 && nodes[getNode (timeMin)].units.Count > 0) {
			// path no longer contains any units
			g.events.add(new TileMoveEvt(nodes[nodes.Count - 1].time, id, Sim.OffMap, 0));
		}
	}

	/// <summary>
	/// let unit be updated in the present (i.e., stop time traveling) starting at timeSim
	/// </summary>
	public void goLive() {
		throw new NotImplementedException();
	}

	public void beUnseen(long time) {
		nodes[insertNode(time)].unseen = true;
	}

	public void beSeen(long time) {
		nodes[insertNode(time)].unseen = false;
		// TODO: delete all child paths made before time unseen
	}

	/// <summary>
	/// removes specified unit from path if doing so wouldn't affect anything that another player saw, returns whether successful
	/// </summary>
	public bool removeUnit(long time, int unit) {
		List<int> parentPaths = new List<int>();
		List<int> parentNodes = new List<int>();
		List<int> rmPaths = new List<int>();
		List<int> rmNodes = new List<int>();
		long minParentNodeTime = long.MaxValue;
		int i;
		parentPaths.Add (id);
		parentNodes.Add (getNode (time));
		// if this path already doesn't contain specified unit at specified time, return true
		if (parentNodes[0] < 0 || !nodes[parentNodes[0]].units.Contains (unit)) return true;
		// if unit was defined in scenario file, return false (we assume other players know the scenario's starting state)
		// TODO: this isn't quite right, if this unit created multiple paths we want to be able to delete some of them
		if (unit < g.nRootUnits) return false;
		// find all parent paths/nodes to start removal from
		for (i = 0; i < parentPaths.Count; i++) {
			while (true) {
				bool foundSharedParent = false;
				foreach (int path in g.paths[parentPaths[i]].nodes[parentNodes[i]].paths) {
					int node = g.paths[path].getNode (nodes[parentNodes[i]].time);
					if (g.paths[path].nodes[node].units.Contains (unit)) {
						// found a path with the same child unit as us
						int index = parentPaths.IndexOf (path);
						if (index < 0 || parentNodes[index] != node) {
							// currently not planning to remove unit from that path,
							// so stop search here so that shared parent won't be deleted
							foundSharedParent = true;
							break;
						}
						// don't worry about potentially redundant entry in deletion list (if part below adds it to list again),
						// removeUnitAfter() can take care of that
						// TODO: as long as removeUnitAfter() doesn't delete any nodes
					}
					if (isChildPathOf (path, unit, node, parentNodes[i])) {
						// found a parent path containing this unit, so remove unit from this path too
						parentPaths.Add (path);
						parentNodes.Add (node - 1);
					}
				}
				if (foundSharedParent) break;
				// if we are at earliest node containing this unit, break
				if (!isChildPathOf (parentPaths[i], unit, parentNodes[i], parentNodes[i])) break;
				// otherwise, look at previous node
				parentNodes[i]--;
			}
		}
		// remove unit recursively, starting at the parent paths/nodes we found
		for (i = 0; i < parentPaths.Count; i++) {
			if (!g.paths[parentPaths[i]].removeUnitAfter (parentNodes[i], unit, ref rmPaths, ref rmNodes)) break;
			minParentNodeTime = Math.Min (minParentNodeTime, g.paths[parentPaths[i]].nodes[parentNodes[i]].time);
		}
		// if a removeUnitAfter() call failed or removing unit might have led to player having negative resources,
		// add unit back to nodes it was removed from
		if (i < parentPaths.Count || g.playerCheckNegRsc (player (), minParentNodeTime, false) >= 0) {
			for (i = 0; i < rmPaths.Count; i++) {
				g.paths[rmPaths[i]].nodes[rmNodes[i]].units.Add (unit);
			}
			return false;
		}
		return true;
	}
	
	private bool removeUnitAfter(int node, int unit, ref List<int> rmPaths, ref List<int> rmNodes) {
		int curNode = node;
		while (nodes[curNode].units.Contains (unit)) {
			if (!nodes[curNode].unseen) return false;
			nodes[curNode].units.Remove (unit);
			rmPaths.Add (id);
			rmNodes.Add (curNode);
			curNode++;
			if (curNode == nodes.Count) break;
			// stop if reached another parent path for unit being removed
			foreach (int path2 in nodes[curNode].paths) {
				int node2 = g.paths[path2].getNode (nodes[curNode].time);
				if (node2 > 0 && g.paths[path2].nodes[node2 - 1].units.Contains (unit)) return true;
			}
			// check if any units in connected paths should be removed
			foreach (int path2 in nodes[curNode].paths) {
				int node2 = g.paths[path2].getNode (nodes[curNode].time);
				foreach (int unit2 in g.paths[path2].nodes[node2].units) {
					if (unit == unit2) {
						// delete unit from child path
						g.paths[path2].removeUnitAfter (node2, unit, ref rmPaths, ref rmNodes);
					}
					else if (g.unitT[g.units[unit].type].canMake[g.units[unit2].type] && !g.paths[path2].isChildPath (unit2, node2)) {
						// found a unit that deleted unit could have made, check if any other connected unit can make it
						// don't check path2 because I'm currently not planning any GUI to make a child unit in the same path as its parent
						bool foundAnotherParent = false;
						foreach (int path3 in g.paths[path2].nodes[node2].paths) {
							int node3 = g.paths[path3].getNode (nodes[curNode].time);
							if (node3 > 0 && g.unitsCanMake (g.paths[path3].nodes[node3 - 1].units, g.units[unit2].type)) {
								foundAnotherParent = true;
								break;
							}
						}
						if (!foundAnotherParent) {
							// no other connected unit can make that unit, so delete that unit too
							g.paths[path2].removeUnitAfter (node2, unit2, ref rmPaths, ref rmNodes);
						}
					}
				}
			}
		}
		return true;
	}

	/// <summary>
	/// makes a new path containing specified units, returns whether successful
	/// </summary>
	public bool makePath(long time, List<int> units) {
		if (canMakePath(time, units)) {
			int node = getNode (time);
			g.paths.Add (new Path(g, g.paths.Count, units, time, calcPos (time), nodes[node].unseen));
			foreach (int path in nodes[node].paths) {
				g.paths[g.paths.Count - 1].addConnectedPath (time, path);
			}
			addConnectedPath (time, g.paths.Count - 1);
			// indicate to calculate TileMoveEvts for new path starting at timeSim
			if (!g.movedPaths.Contains(g.paths.Count - 1)) g.movedPaths.Add(g.paths.Count - 1);
			return true;
		}
		return false;
	}

	/// <summary>
	/// returns whether this path can make a new path as specified
	/// </summary>
	public bool canMakePath(long time, List<int> units) {
		// TODO: check tile exclusivity if not live, whether unit types can be made, resources
		return true;
	}
	
	public bool canMakeUnitType(long time, int type) {
		if (time < nodes[0].time) return false;
		return g.unitsCanMake (nodes[getNode (time)].units, type);
	}

	/// <summary>
	/// returns index (in unit array) of path that isn't updated in the present and is therefore safe to move in the past
	/// </summary>
	private int prepareNonLivePath(long time) {
		throw new NotImplementedException();
	}
	
	/// <summary>
	/// connects this path to specified path (and vice versa) at specified time,
	/// returns this path's node where the paths were connected
	/// </summary>
	public int addConnectedPath(long time, int path) {
		int node = insertNode (time);
		if (!nodes[node].paths.Contains (path)) {
			nodes[node].paths.Add (path);
			g.paths[path].addConnectedPath (time, id);
		}
		return node;
	}

	/// <summary>
	/// mark existing unit as a child of this unit
	/// </summary>
	private void addChild(int unit) {
		throw new NotImplementedException();
	}

	/// <summary>
	/// non-recursively delete specified child unit
	/// </summary>
	private void deleteChild(int unit) {
		throw new NotImplementedException();
	}

	/// <summary>
	/// recursively delete all child units
	/// </summary>
	/// <remarks>this does not check whether deleting the units may lead to player having negative resources</remarks>
	private void deleteAllChildren() {
		throw new NotImplementedException();
	}

	/// <summary>
	/// delete child units made after the specified time
	/// </summary>
	/// <remarks>this does not check whether deleting the units may lead to player having negative resources</remarks>
	private void deleteChildrenAfter(long time) {
		throw new NotImplementedException();
	}

	/// <summary>
	/// change unit movement to make it look like this unit never existed
	/// </summary>
	private void deleteAllMoves() {
		throw new NotImplementedException();
	}

	/// <summary>
	/// returns resource amount gained by specified unit and its child units (subtracting cost to make child units)
	/// from specified node's time to specified time
	/// </summary>
	/// <param name="max">
	/// since different paths can have collected different resource amounts,
	/// determines whether to use paths that collected least or most resources in calculation
	/// </param>
	// TODO: this currently double-counts child paths/units if paths merge, fix this before enabling stacking
	public long rscCollected(long time, int node, int unit, int rscType, bool max, bool includeNonLiveChildren) {
		if (time < nodes[node].time) return 0; // if this node wasn't active yet, unit can't have collected anything
		int endNode = node;
		long timeCollectEnd = (g.units[unit].healthWhen(time) == 0) ? g.units[unit].timeHealth[g.units[unit].nTimeHealth - 1] : time;
		long ret = 0;
		while (endNode < nodes.Count - 1 && nodes[endNode + 1].time <= time) endNode++;
		for (int i = endNode; i > node && nodes[i - 1].units.Contains (unit); i--) {
			foreach (int path2 in nodes[i].paths) {
				if (includeNonLiveChildren || g.paths[path2].timeSimPast == long.MaxValue) {
					int node2 = g.paths[path2].getNode (nodes[i].time);
					if (g.paths[path2].isChildPathOf (id, unit, i, node2)) {
						// if child path is one of this unit's paths and collected more/less (depending on max parameter) resources than this path,
						// use that path for resource calculation
						long pathCollected = g.paths[path2].rscCollected (time, node2, unit, rscType, max, includeNonLiveChildren);
						if (max ^ (pathCollected < ret + g.unitT[g.units[unit].type].rscCollectRate[rscType] * (timeCollectEnd - g.paths[path2].nodes[node2].time))) {
							ret = pathCollected;
							timeCollectEnd = g.paths[path2].nodes[node2].time;
						}
					}
					else if (!g.paths[path2].isChildPath (unit, node2)) {
						foreach (int unit2 in g.paths[path2].nodes[node2].units) {
							if (g.unitT[g.units[unit].type].canMake[g.units[unit2].type]) {
								// add resources that non-path child unit gained
								ret += g.paths[path2].rscCollected (time, node2, unit2, rscType, max, includeNonLiveChildren);
								// subtract cost to make child unit
								ret -= g.unitT[g.units[unit2].type].rscCost[rscType];
							}
						}
					}
				}
			}
		}
		// add resources collected by this unit
		ret += g.unitT[g.units[unit].type].rscCollectRate[rscType] * (timeCollectEnd - nodes[node].time);
		return ret;
	}
	
	private bool isChildPath(int unit, int node) {
		if (isChildPathOf (id, unit, node, node)) return true;
		foreach (int path in nodes[node].paths) {
			if (isChildPathOf (path, unit, g.paths[path].getNode (nodes[node].time), node)) return true;
		}
		return false;
	}
	
	private bool isChildPathOf(int parentPath, int unit, int parentNode, int childNode) {
		if (g.paths[parentPath].nodes[parentNode].time != nodes[childNode].time) {
			throw new ArgumentException("parent and child nodes have different times");
		}
		return parentNode > 0 && g.paths[parentPath].nodes[parentNode - 1].units.Contains (unit)
			&& nodes[childNode].units.Contains (unit);
	}
	
	/// <summary>
	/// returns minimum absolute position where clicking would select the path
	/// </summary>
	public FP.Vector selMinPos(long time) {
		FP.Vector ret = new FP.Vector(int.MaxValue, int.MaxValue);
		foreach (int unit in nodes[getNode(time)].units) {
			ret.x = Math.Min (ret.x, g.unitT[g.units[unit].type].selMinPos.x);
			ret.y = Math.Min (ret.y, g.unitT[g.units[unit].type].selMinPos.y);
		}
		return ret + calcPos(time);
	}
	
	/// <summary>
	/// returns maximum absolute position where clicking would select the path
	/// </summary>
	public FP.Vector selMaxPos(long time) {
		FP.Vector ret = new FP.Vector(int.MinValue, int.MinValue);
		foreach (int unit in nodes[getNode(time)].units) {
			ret.x = Math.Max (ret.x, g.unitT[g.units[unit].type].selMaxPos.x);
			ret.y = Math.Max (ret.y, g.unitT[g.units[unit].type].selMaxPos.y);
		}
		return ret + calcPos(time);
	}
	
	/// <summary>
	/// returns speed of path, in position units per millisecond
	/// </summary>
	public long speed() {
		foreach (Node node in nodes) {
			if (node.units.Count > 0) return g.unitT[g.units[node.units[0]].type].speed;
		}
		throw new InvalidOperationException("path does not contain any units");
	}
	
	/// <summary>
	/// returns index of player that controls this path
	/// </summary>
	public int player() {
		foreach (Node node in nodes) {
			if (node.units.Count > 0) return g.units[node.units[0]].player;
		}
		throw new InvalidOperationException("path does not contain any units");
	}

	/// <summary>
	/// returns whether path is known to not be seen by another player at latest known time
	/// </summary>
	public bool unseen() {
		throw new NotImplementedException();
	}

	/// <summary>
	/// returns whether path is created (TODO: and contains units?) at specified time
	/// </summary>
	public bool exists(long time) {
		return time >= nodes[0].time && time >= moves[0].timeStart;
	}

	/// <summary>
	/// returns whether path exists and is being updated in the present (i.e., isn't time traveling)
	/// </summary>
	public bool isLive(long time) {
		return exists (time) && timeSimPast == long.MaxValue;
	}
}
