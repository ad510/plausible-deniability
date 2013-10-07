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
		public List<int> paths;
		public List<int> units;
		public bool unseen;
		
		public Node(long timeVal, List<int> unitsVal, bool unseenVal) {
			time = timeVal;
			paths = new List<int>();
			units = unitsVal;
			unseenVal = unseen;
		}
	}
	
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
		: this(simVal, idVal, units, startTime, startPos, simVal.tileAt(startPos).coherentWhen(simVal.units[units[0]].player, startTime)) {
	}

	/// <summary>
	/// ensure that if unit is moving in the past, it does not move off coherent areas
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
		// TODO: implement line below
		//if (!g.movedUnits.Contains(id)) g.movedUnits.Add(id); // indicate to delete and recalculate later TileMoveEvts for this unit
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
	/// inserts TileMoveEvt events for this unit into events for the time interval from timeMin to timeMax
	/// </summary>
	/// <remarks>due to fixed point imprecision in lineCalcX() and lineCalcY(), this sometimes adds events outside the requested time interval</remarks>
	public void addTileMoveEvts(ref SimEvtList events, long timeMin, long timeMax) {
		throw new NotImplementedException(); // Unit.cs version contains health check, not sure if relevant here
	}

	/// <summary>
	/// let unit be updated in the present (i.e., stop time traveling) starting at timeSim
	/// </summary>
	public void goLive() {
		throw new NotImplementedException();
	}

	/// <summary>
	/// allows unit to time travel and move along multiple paths starting at specified time
	/// </summary>
	public void cohere(long time) {
		throw new NotImplementedException();
	}

	/// <summary>
	/// stops allowing unit to time travel or move along multiple paths starting at timeSim
	/// </summary>
	public void decohere(long time = long.MaxValue) {
		throw new NotImplementedException();
	}

	/// <summary>
	/// removes specified unit from path if doing so wouldn't affect anything that another player saw, returns whether successful
	/// </summary>
	public bool removeUnit(long time, int unit) {
		// TODO: current version of removeUnit() is dumb and can only remove units at the last node, and does not check that it is allowed to do so
		if (time != nodes[nodes.Count - 1].time) return false;
		nodes[nodes.Count - 1].units.Remove (unit);
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
		int node = getNode (time);
		if (nodes[node].time != time) {
			nodes.Insert (node + 1, new Node(time, new List<int>(nodes[node].units), nodes[node].unseen));
			node++;
		}
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
	/// returns resource amount gained by this unit and its child units (subtracting cost to make the units)
	/// </summary>
	/// <param name="max">
	/// since different paths can have collected different resource amounts,
	/// determines whether to use paths that collected least or most resources in calculation
	/// </param>
	public long rscCollected(long time, int rscType, bool max, bool includeNonLiveChildren, bool alwaysUseReplacementPaths) {
		throw new NotImplementedException();
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
	/// returns whether unit can time travel or move along multiple paths at latest known time
	/// </summary>
	public bool coherent() {
		throw new NotImplementedException();
	}

	/// <summary>
	/// returns whether unit is created and has health at specified time
	/// </summary>
	public bool exists(long time) {
		throw new NotImplementedException();
	}

	/// <summary>
	/// returns whether unit exists and is being updated in the present (i.e., isn't time traveling)
	/// </summary>
	public bool isLive(long time) {
		throw new NotImplementedException();
	}
}
