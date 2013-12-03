// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// top-level game simulation class (instances of this are often named "g")
/// </summary>
public class Sim {
	// constants
	public const int OffMap = -10000; // don't set to int.MinValue so doesn't overflow in inVis()
	public const int CompUser = -1;

	// game objects
	public class User {
		public SimEvtList cmdReceived; // commands received from this user to be applied in next update
		public long timeSync; // latest time at which commands from this user are ready to be applied
		public Dictionary<long, int> checksums; // checksums calculated by this user to be compared to our checksum (key is timeSync when checksum is received)
		
		public User() {
			cmdReceived = new SimEvtList();
			timeSync = -1;
			checksums = new Dictionary<long, int>();
		}
	}
	
	public class Player {
		// stored in scenario files
		public string name;
		public bool isUser; // whether actively participates in the game
		public int user; // -2 = nobody, -1 = computer, 0+ = human
		public long[] startRsc; // resources at beginning of game
		public bool[] mayAttack; // if this player's units may attack each other player's units
		// not stored in scenario files
		public bool immutable; // whether player's units will never unpredictably move or change
		public bool hasNonLivePaths; // whether currently might have time traveling paths (ok to sometimes incorrectly be set to true)
		public long timeGoLiveFail; // latest time that player's time traveling paths failed to go live (resets to long.MaxValue after success)
		public long timeNegRsc; // time that player could have negative resources if time traveling paths went live

		public Player() {
			hasNonLivePaths = false;
			timeGoLiveFail = long.MaxValue;
		}
	}

	public class UnitType {
		public string name;
		public string imgPath;
		public FP.Vector imgOffset; // how much to offset center of image when drawing it
		public long imgHalfHeight; // how tall half of image height should be drawn
		public FP.Vector selMinPos; // minimum relative position where clicking would select the unit
		public FP.Vector selMaxPos; // maximum relative position where clicking would select the unit
		/*public string sndSelect;
		public string sndMove;
		public string sndAttack;
		public string sndNoHealth;*/
		public int maxHealth;
		public long speed; // in position units per millisecond
		public long reload; // time needed to reload
		public long range; // range of attack
		public long tightFormationSpacing;
		public long makeUnitMinDist; // minimum distance that new units of this unit type move away
		public long makeUnitMaxDist; // maximum distance that new units of this unit type move away
		public long makePathMinDist; // minimum distance that new paths of this unit type move away
		public long makePathMaxDist; // maximum distance that new paths of this unit type move away
		public int makeOnUnitT; // unit type that this unit type should be made on top of
		public int[] damage; // damage done per attack to each unit type
		public bool[] canMake; // whether can make each unit type
		public long[] rscCost; // cost to make unit (may not be negative)
		public long[] rscCollectRate; // resources collected per each millisecond that unit exists (may not be negative)
	}

	public class Tile {
		private Sim g;
		/// <summary>
		/// stores times when each path started or stopped seeing this tile,
		/// in format pathVis[path][gain/lose visibility index]
		/// </summary>
		public Dictionary<int, List<long>> pathVis;
		/// <summary>
		/// stores times when each player started or stopped seeing this tile,
		/// in format playerVis[player][gain/lose visibility index]
		/// </summary>
		public List<long>[] playerVis;
		/// <summary>
		/// stores times when each player started or stopped knowing that no other player can see this tile,
		/// in format exclusive[player][gain/lose exclusivity index]
		/// </summary>
		public List<long>[] exclusive;

		public Tile(Sim simVal) {
			g = simVal;
			pathVis = new Dictionary<int,List<long>>();
			playerVis = new List<long>[g.players.Length];
			exclusive = new List<long>[g.players.Length];
			for (int i = 0; i < g.players.Length; i++) {
				playerVis[i] = new List<long>();
				exclusive[i] = new List<long>();
			}
		}

		/// <summary>
		/// toggles the visibility of this tile for specified path at specified time, without affecting player visibility
		/// (should only be called by TileMoveEvt.apply())
		/// </summary>
		public void pathVisToggle(int path, long time) {
			if (!pathVis.ContainsKey(path)) pathVis.Add(path, new List<long>());
			pathVis[path].Add(time);
		}

		/// <summary>
		/// returns if specified path can see this tile at latest possible time
		/// </summary>
		public bool pathVisLatest(int path) {
			return pathVis.ContainsKey(path) && visLatest(pathVis[path]);
		}

		/// <summary>
		/// returns if specified path can see this tile at specified time
		/// </summary>
		public bool pathVisWhen(int path, long time) {
			return pathVis.ContainsKey(path) && visWhen(pathVis[path], time);
		}

		/// <summary>
		/// returns if this tile is in the direct line of sight of a unit of specified player at latest possible time
		/// </summary>
		public bool playerDirectVisLatest(int player) {
			foreach (int i in pathVis.Keys) {
				if (player == g.paths[i].player && visLatest(pathVis[i])) return true;
			}
			return false;
		}

		/// <summary>
		/// returns if this tile is in the direct line of sight of a unit of specified player at specified time
		/// </summary>
		public bool playerDirectVisWhen(int player, long time) {
			foreach (int i in pathVis.Keys) {
				if (player == g.paths[i].player && visWhen(pathVis[i], time)) return true;
			}
			return false;
		}

		/// <summary>
		/// returns if this tile is either in the direct line of sight for specified player at latest possible time,
		/// or if player can infer that other players' units aren't in specified tile at latest time
		/// </summary>
		public bool playerVisLatest(int player) {
			return visLatest(playerVis[player]);
		}

		/// <summary>
		/// returns playerVis gain/lose visibility index associated with specified time for specified player
		/// </summary>
		public int playerVisIndexWhen(int player, long time) {
			return visIndexWhen(playerVis[player], time);
		}

		/// <summary>
		/// returns if this tile is either in the direct line of sight for specified player at specified time,
		/// or if player can infer that other players' units aren't in specified tile at specified time
		/// </summary>
		public bool playerVisWhen(int player, long time) {
			return visWhen(playerVis[player], time);
		}

		/// <summary>
		/// returns if specified player can infer that no other player can see this tile at latest possible time
		/// </summary>
		public bool exclusiveLatest(int player) {
			return visLatest(exclusive[player]);
		}

		/// <summary>
		/// returns gain/lose exclusivity index associated with specified time for specified player
		/// </summary>
		public int exclusiveIndexWhen(int player, long time) {
			return visIndexWhen(exclusive[player], time);
		}

		/// <summary>
		/// returns if specified player can infer that no other player can see this tile at specified time
		/// </summary>
		public bool exclusiveWhen(int player, long time) {
			return visWhen(exclusive[player], time);
		}

		/// <summary>
		/// returns whether specified list indicates that the tile is visible at the latest possible time
		/// </summary>
		/// <remarks>
		/// The indices of the list are assumed to alternate between gaining visibility and losing visibility,
		/// where an empty list means not visible. So if there is an odd number of items in the list, the tile is visible.
		/// </remarks>
		private static bool visLatest(List<long> vis) {
			return vis.Count % 2 == 1;
		}

		/// <summary>
		/// returns index of specified list whose associated time is when the tile gained or lost visibility before the specified time
		/// </summary>
		/// <param name="vis">list of times in ascending order</param>
		private static int visIndexWhen(List<long> vis, long time) {
			int i;
			for (i = vis.Count - 1; i >= 0; i--) {
				if (time >= vis[i]) break;
			}
			return i;
		}

		/// <summary>
		/// returns whether specified list indicates that the tile is visible at specified time
		/// </summary>
		/// <remarks>
		/// The indices of the list are assumed to alternate between gaining visibility and losing visibility,
		/// where an even index means visible. So if the index associated with the specified time is even, the tile is visible.
		/// </remarks>
		private static bool visWhen(List<long> vis, long time) {
			return visIndexWhen(vis, time) % 2 == 0;
		}
	}

	// general simulation parameters
	public long mapSize;
	public long updateInterval;
	public long visRadius;

	// camera properties
	public FP.Vector camPos;
	public long camSpeed; // in position units per millisecond
	public float zoom; // size of simulation length unit relative to diagonal length of screen
	public float zoomMin;
	public float zoomMax;
	public float zoomSpeed;
	public float zoomMouseWheelSpeed;

	// UI scaling variables
	public float uiBarHeight; // height of UI bar relative to screen height
	public Vector2 healthBarSize; // size of health bar relative to diagonal length of screen
	public float healthBarYOffset; // how high to draw center of health bar above top of selectable part of unit

	// colors
	public Color backCol;
	public Color borderCol;
	public Color noVisCol;
	public Color playerVisCol;
	public Color unitVisCol;
	public Color exclusiveCol;
	public Color pathCol;
	public Color healthBarBackCol;
	public Color healthBarFullCol;
	public Color healthBarEmptyCol;

	// core game objects
	public User[] users;
	public string[] rscNames;
	public Player[] players;
	public UnitType[] unitT;
	public List<Unit> units;
	public List<Path> paths;

	// helper variables not loaded from scenario file
	public int selUser;
	public NetworkView networkView; // to do RPCs in multiplayer (set to null in single player)
	public Tile[,] tiles; // each tile is 1 fixed-point unit (2^FP.Precision raw integer units) wide, so bit shift by FP.Precision to convert between position and tile position
	public SimEvtList events; // simulation events to be applied
	public SimEvtList cmdPending; // user commands to be sent to other users in the next update
	public SimEvtList cmdHistory; // user commands that have already been applied
	public List<int> movedPaths; // indices of paths that moved in the latest simulation event, invalidating later TileMoveEvts for that path
	public int nRootPaths; // number of paths that don't have a parent (because they were defined in scenario file); these are all at beginning of paths list
	public long maxSpeed; // speed of fastest unit (is max speed that players can gain or lose visibility)
	public int checksum; // sent to other users during each UpdateEvt to check for multiplayer desyncs
	public bool synced; // whether all checksums between users matched so far
	public long timeSim; // current simulation time
	public long timeUpdateEvt; // last time that an UpdateEvt was applied

	/// <summary>
	/// master update method which updates the live game simulation to the specified time
	/// </summary>
	/// <remarks>this doesn't update time traveling units, must call updatePast() separately for each player</remarks>
	public void update(long curTime) {
		SimEvt evt;
		long timeSimNext = Math.Max(curTime, timeSim);
		int i;
		if (networkView == null) {
			// move pending user commands to event list (single player only)
			// TODO: could command be applied after another event with same time, causing desyncs in replays?
			while ((evt = cmdPending.pop ()) != null) events.add (evt);
		}
		// apply simulation events
		movedPaths = new List<int>();
		while (events.peekTime() <= timeSimNext) {
			evt = events.pop();
			timeSim = evt.time;
			evt.apply(this);
			// if event caused path(s) to move, delete and recalculate later events moving them between tiles
			if (movedPaths.Count > 0) {
				for (i = 0; i < events.events.Count; i++) {
					if (events.events[i] is TileMoveEvt && events.events[i].time > timeSim && movedPaths.Contains(((TileMoveEvt)events.events[i]).path)) {
						events.events.RemoveAt(i);
						i--;
					}
				}
				foreach (int path in movedPaths) {
					if (paths[path].timeSimPast == long.MaxValue) paths[path].addTileMoveEvts(ref events, timeSim, timeUpdateEvt + updateInterval);
				}
				movedPaths.Clear();
			}
			checksum++;
		}
		// update simulation time
		timeSim = timeSimNext;
	}

	/// <summary>
	/// update specified player's non-live (time traveling) paths
	/// </summary>
	public void updatePast(int player, long curTime) {
		if (players[player].hasNonLivePaths) {
			foreach (Path path in paths) {
				if (path.player == player) path.updatePast(curTime);
			}
			if (curTime >= timeSim && (players[player].timeGoLiveFail == long.MaxValue || timeSim >= players[player].timeGoLiveFail + updateInterval)) {
				cmdPending.add(new GoLiveCmdEvt(timeSim, player));
			}
		}
	}
	
	/// <summary>
	/// removes units from all other paths that, if seen, could cause specified units to be removed from specified segments;
	/// returns whether successful
	/// </summary>
	public bool deleteOtherPaths(List<KeyValuePair<Segment, int>> units) {
		List<KeyValuePair<Segment, int>> ancestors = new List<KeyValuePair<Segment, int>>(units);
		List<KeyValuePair<Segment, int>> prev = new List<KeyValuePair<Segment, int>>();
		bool success = true;
		for (int i = 0; i < ancestors.Count; i++) {
			foreach (Segment seg in ancestors[i].Key.prev (ancestors[i].Value)) {
				ancestors.Add (new KeyValuePair<Segment, int>(seg, ancestors[i].Value));
				if (ancestors[i].Key.path.timeSimPast == long.MaxValue || seg.path.timeSimPast != long.MaxValue) {
					prev.Add (new KeyValuePair<Segment, int>(seg, ancestors[i].Value));
				}
			}
			ancestors.AddRange (ancestors[i].Key.parents (ancestors[i].Value));
		}
		foreach (KeyValuePair<Segment, int> ancestor in prev) {
			foreach (Segment seg in ancestor.Key.next (ancestor.Value)) {
				if (!ancestors.Contains (new KeyValuePair<Segment, int>(seg, ancestor.Value))) {
					success &= seg.removeUnit (ancestor.Value);
				}
			}
		}
		return success;
	}

	/// <summary>
	/// makes specified tile not visible to specified player starting at specified time, including effects on surrounding tiles
	/// </summary>
	public void playerVisRemove(int player, int tileX, int tileY, long time) {
		// try adding tile to existing PlayerVisRemoveEvt with same player and time
		foreach (SimEvt evt in events.events) {
			if (evt is PlayerVisRemoveEvt) {
				PlayerVisRemoveEvt visEvt = (PlayerVisRemoveEvt)evt;
				if (player == visEvt.player && time == visEvt.time) {
					// check that tile pos isn't a duplicate (recently added tiles are more likely to be duplicates)
					for (int i = visEvt.tiles.Count - 1; i >= Math.Max(0, visEvt.tiles.Count - 20); i--) {
						if (tileX == visEvt.tiles[i].x && tileY == visEvt.tiles[i].y) return;
					}
					// ok to add tile to existing event
					visEvt.tiles.Add (new FP.Vector(tileX, tileY));
					return;
				}
			}
		}
		// if no such PlayerVisRemoveEvt exists, add a new one
		events.add(new PlayerVisRemoveEvt(time, player, tileX, tileY));
	}

	/// <summary>
	/// makes specified tile exclusive to specified player starting at specified time,
	/// including how that affects paths on that tile
	/// </summary>
	public void exclusiveAdd(int player, int tileX, int tileY, long time) {
		if (tiles[tileX, tileY].exclusiveLatest(player)) throw new InvalidOperationException("tile (" + tileX + ", " + tileY + ") is already exclusive");
		tiles[tileX, tileY].exclusive[player].Add(time);
		// this player's paths that are on this tile may time travel starting now
		// TODO: actually safe to time travel at earlier times, as long as unit of same type is at same place when seen by another player
		foreach (Path path in paths) {
			if (player == path.player && tileX == path.tileX && tileY == path.tileY && !path.segments.Last ().unseen) {
				path.beUnseen(time);
			}
		}
	}

	/// <summary>
	/// makes specified tile not exclusive to specified player starting at specified time,
	/// including how that affects paths on that tile
	/// </summary>
	public void exclusiveRemove(int player, int tileX, int tileY, long time) {
		if (!tiles[tileX, tileY].exclusiveLatest(player)) throw new InvalidOperationException("tile (" + tileX + ", " + tileY + ") is already not exclusive");
		tiles[tileX, tileY].exclusive[player].Add(time);
		// this player's paths that are on this tile may not time travel starting now
		foreach (Path path in paths) {
			if (player == path.player && tileX == path.tileX && tileY == path.tileY && path.segments.Last ().unseen) {
				path.beSeen(time);
			}
		}
	}

	/// <summary>
	/// calculates from player visibility tiles if specified player can infer that no other player can see specified tile at latest possible time
	/// </summary>
	/// <remarks>
	/// The worst-case scenario would then be that every tile that this player can't see contains another player's unit
	/// of the type with the greatest visibility radius (though all units in this game have the same visibility radius).
	/// If no other player could see the specified tile in this worst case scenario,
	/// the player can infer that he/she is the only player that can see this tile.
	/// </remarks>
	public bool calcExclusive(int player, int tileX, int tileY) {
		int i, tX, tY;
		// check that this player can see all nearby tiles
		for (tX = Math.Max(0, tileX - tileVisRadius()); tX <= Math.Min(tileLen() - 1, tileX + tileVisRadius()); tX++) {
			for (tY = Math.Max(0, tileY - tileVisRadius()); tY <= Math.Min(tileLen() - 1, tileY + tileVisRadius()); tY++) {
				if (inVis(tX - tileX, tY - tileY) && !tiles[tX, tY].playerVisLatest(player)) return false;
			}
		}
		// check that no other players can see this tile
		for (i = 0; i < players.Length; i++) {
			if (i != player && !players[i].immutable && tiles[tileX, tileY].playerVisLatest(i)) return false;
		}
		return true;
	}

	/// <summary>
	/// returns amount of specified resource that specified player has at specified time
	/// </summary>
	/// <param name="max">
	/// since different paths can have collected different resource amounts,
	/// determines whether to use paths that collected least or most resources in calculation
	/// </param>
	public long playerResource(int player, long time, int rscType, bool max, bool includeNonLiveChildren) {
		long ret = players[player].startRsc[rscType];
		for (int i = 0; i < nRootPaths; i++) {
			if (paths[i].player == player) {
				foreach (int unit in paths[i].segments[0].units) {
					// TODO: this will double-count units that are in multiple paths at beginning of scenario
					ret += paths[i].segments[0].rscCollected(time, unit, rscType, max, includeNonLiveChildren);
				}
			}
		}
		return ret;
	}

	/// <summary>
	/// checks whether specified player could have negative resources since timeMin in worst case scenario of which paths are seen
	/// </summary>
	/// <returns>a time that player could have negative resources, or -1 if no such time found</returns>
	public long playerCheckNegRsc(int player, long timeMin, bool includeNonLiveChildren) {
		foreach (Path path in paths) {
			// check all times since timeMin that a path of specified player was made
			// note that new paths are made at App.newCmdTime() + 1
			if (player == path.player && path.segments[0].timeStart >= timeMin && path.segments[0].timeStart <= timeSim + 1) {
				for (int i = 0; i < rscNames.Length; i++) {
					if (playerResource(player, path.segments[0].timeStart, i, false, includeNonLiveChildren) < 0) {
						return path.segments[0].timeStart;
					}
				}
			}
		}
		return -1;
	}

	/// <summary>
	/// returns whether specified player's units will never unpredictably move or change
	/// </summary>
	public bool calcPlayerImmutable(int player) {
		// check that player isn't an active participant and isn't controlled by anyone
		if (players[player].isUser || players[player].user >= CompUser) return false;
		// check that no one can attack this player
		foreach (Player player2 in players) {
			if (player2.mayAttack[player]) return false;
		}
		return true;
	}
	
	public bool unitsCanMake(List<int> parentUnits, int type) {
		foreach (int unit in parentUnits) {
			if (unitT[units[unit].type].canMake[type]) return true;
		}
		return false;
	}
	
	/// <summary>
	/// returns whether the specified units are allowed to be on the same path
	/// </summary>
	public bool stackAllowed(List<int> stackUnits, long speed, int player) {
		if (stackUnits.Count == 0) return true;
		foreach (int unit in stackUnits) {
			if (unitT[units[unit].type].speed != speed || units[unit].player != player) {
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// returns if a hypothetical unit at the origin could see tile with specified (positive or negative) x and y indices
	/// </summary>
	public bool inVis(long tX, long tY) {
		//return Math.Max(Math.Abs(tX), Math.Abs(tY)) <= (int)(g.visRadius >> FP.Precision);
		return new FP.Vector(tX << FP.Precision, tY << FP.Precision).lengthSq() <= visRadius * visRadius;
	}

	public int tileVisRadius() {
		return (int)(visRadius >> FP.Precision); // adding "+ 1" to this actually doesn't make a difference
	}

	public Tile tileAt(FP.Vector pos) {
		return tiles[pos.x >> FP.Precision, pos.y >> FP.Precision];
	}

	public int tileLen() { // TODO: use unitVis.GetUpperBound instead of this function
		return (int)((mapSize >> FP.Precision) + 1);
	}

	/// <summary>
	/// returns index of resource with specified name, or -1 if no such resource
	/// </summary>
	public int resourceNamed(string name) {
		for (int i = 0; i < rscNames.Length; i++) {
			if (name == rscNames[i]) return i;
		}
		return -1;
	}

	/// <summary>
	/// returns index of player with specified name, or -1 if no such player
	/// </summary>
	public int playerNamed(string name) {
		for (int i = 0; i < players.Length; i++) {
			if (name == players[i].name) return i;
		}
		return -1;
	}

	/// <summary>
	/// returns index of unit type with specified name, or -1 if no such unit type
	/// </summary>
	public int unitTypeNamed(string name) {
		for (int i = 0; i < unitT.Length; i++) {
			if (name == unitT[i].name) return i;
		}
		return -1;
	}
}
