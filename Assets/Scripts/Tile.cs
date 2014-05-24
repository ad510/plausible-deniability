// Copyright (c) 2013-2014 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

[ProtoContract]
public class Tile {
	[ProtoMember(1, AsReference = true)] private readonly Sim g;
	[ProtoMember(2)] public readonly int x;
	[ProtoMember(3)] public readonly int y;
	/// <summary>
	/// stores times when each path started or stopped seeing this tile,
	/// in format pathVis[path][gain/lose visibility index]
	/// </summary>
	[ProtoMember(4)] public Dictionary<int, List<long>> pathVis;
	/// <summary>
	/// stores times when each player started or stopped seeing this tile,
	/// in format playerVis[player][gain/lose visibility index]
	/// </summary>
	public List<long>[] playerVis;
	[ProtoMember(5)] private List<long> protoPlayerVis;
	/// <summary>
	/// stores times when each player started or stopped knowing that no other player can see this tile,
	/// in format exclusive[player][gain/lose exclusivity index]
	/// </summary>
	public List<long>[] exclusive;
	[ProtoMember(6)] private List<long> protoExclusive;
	/// <summary>
	/// stores where each unit can come from to get to this tile at any given time,
	/// in format waypoints[unit][waypoint index]
	/// </summary>
	[ProtoMember(7)] public Dictionary<int, List<Waypoint>> waypoints;
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	private Tile() { }

	public Tile(Sim simVal, int xVal, int yVal) {
		g = simVal;
		x = xVal;
		y = yVal;
		pathVis = new Dictionary<int,List<long>>();
		playerVis = new List<long>[g.players.Length];
		if (Sim.EnableNonLivePaths) {
			exclusive = new List<long>[g.players.Length];
			waypoints = new Dictionary<int, List<Waypoint>>();
		}
		for (int i = 0; i < g.players.Length; i++) {
			playerVis[i] = new List<long>();
			if (Sim.EnableNonLivePaths) exclusive[i] = new List<long>();
		}
	}
	
	[ProtoBeforeSerialization]
	private void beforeSerialize() {
		protoPlayerVis = new List<long>();
		for (int i = 0; i < playerVis.Length; i++) {
			protoPlayerVis.Add (playerVis[i].Count);
			protoPlayerVis.AddRange (playerVis[i]);
		}
		if (Sim.EnableNonLivePaths) {
			protoExclusive = new List<long>();
			for (int i = 0; i < exclusive.Length; i++) {
				protoExclusive.Add (exclusive[i].Count);
				protoExclusive.AddRange (exclusive[i]);
			}
		}
	}
	
	[ProtoAfterSerialization]
	private void afterSerialize() {
		protoPlayerVis = null;
		protoExclusive = null;
	}
	
	/// <summary>
	/// called manually from Sim.afterDeserialize()
	/// </summary>
	public void afterSimDeserialize() {
		if (pathVis == null) pathVis = new Dictionary<int, List<long>>();
		playerVis = new List<long>[g.players.Length];
		int player = 0;
		for (int i = 0; i < protoPlayerVis.Count; i += (int)protoPlayerVis[i] + 1) {
			playerVis[player] = protoPlayerVis.GetRange (i + 1, (int)protoPlayerVis[i]);
			player++;
		}
		protoPlayerVis = null;
		if (Sim.EnableNonLivePaths) {
			exclusive = new List<long>[g.players.Length];
			player = 0;
			for (int i = 0; i < protoExclusive.Count; i += (int)protoExclusive[i] + 1) {
				exclusive[player] = protoExclusive.GetRange (i + 1, (int)protoExclusive[i]);
				player++;
			}
		}
		protoExclusive = null;
	}

	/// <summary>
	/// toggles the visibility of this tile for specified path at specified time, without affecting player visibility
	/// (should only be called by TileUpdateEvt.apply())
	/// </summary>
	public void pathVisToggle(Path path, long time) {
		if (!pathVis.ContainsKey(path.id)) pathVis.Add(path.id, new List<long>());
		pathVis[path.id].Add(time);
	}

	/// <summary>
	/// returns if specified path can see this tile at latest possible time
	/// </summary>
	public bool pathVisLatest(Path path) {
		return pathVis.ContainsKey(path.id) && visLatest(pathVis[path.id]);
	}

	/// <summary>
	/// returns if specified path can see this tile at specified time
	/// </summary>
	public bool pathVisWhen(Path path, long time) {
		return pathVis.ContainsKey(path.id) && visWhen(pathVis[path.id], time);
	}

	/// <summary>
	/// makes this tile not visible to specified player starting at specified time, including effects on surrounding tiles
	/// </summary>
	public void playerVisRemove(Player player, long time) {
		// try adding tile to existing PlayerVisRemoveEvt with same player and time
		foreach (SimEvt evt in g.events.events) {
			if (evt is PlayerVisRemoveEvt) {
				PlayerVisRemoveEvt visEvt = evt as PlayerVisRemoveEvt;
				if (player.id == visEvt.player && time == visEvt.time) {
					// check that tile pos isn't a duplicate (recently added tiles are more likely to be duplicates)
					for (int i = visEvt.tiles.Count - 1; i >= Math.Max(0, visEvt.tiles.Count - 20); i--) {
						if (x == visEvt.tiles[i].x && y == visEvt.tiles[i].y) return;
					}
					// ok to add tile to existing event
					visEvt.tiles.Add (new FP.Vector(x, y));
					return;
				}
			}
		}
		// if no such PlayerVisRemoveEvt exists, add a new one
		g.events.add(new PlayerVisRemoveEvt(time, player.id, x, y));
	}

	/// <summary>
	/// returns if this tile is in the direct line of sight of a unit of specified player at latest possible time
	/// </summary>
	public bool playerDirectVisLatest(Player player) {
		foreach (int i in pathVis.Keys) {
			if (player == g.paths[i].player && visLatest(pathVis[i])) return true;
		}
		return false;
	}

	/// <summary>
	/// returns if this tile is in the direct line of sight of a unit of specified player at specified time
	/// </summary>
	public bool playerDirectVisWhen(Player player, long time, bool checkUnits = true) {
		foreach (int i in pathVis.Keys) {
			if (player == g.paths[i].player && visWhen(pathVis[i], time)) {
				if (!checkUnits) return true;
				Segment segment = g.paths[i].activeSegment (time);
				if (segment != null && segment.units.Count > 0) return true;
			}
		}
		return false;
	}

	/// <summary>
	/// returns if this tile is either in the direct line of sight for specified player at latest possible time,
	/// or if player can infer that other players' units aren't in specified tile at latest time
	/// </summary>
	public bool playerVisLatest(Player player) {
		return visLatest(playerVis[player.id]);
	}

	/// <summary>
	/// returns playerVis gain/lose visibility index associated with specified time for specified player
	/// </summary>
	public int playerVisIndexWhen(Player player, long time) {
		return visIndexWhen(playerVis[player.id], time);
	}

	/// <summary>
	/// returns if this tile is either in the direct line of sight for specified player at specified time,
	/// or if player can infer that other players' units aren't in specified tile at specified time
	/// </summary>
	public bool playerVisWhen(Player player, long time) {
		return visWhen(playerVis[player.id], time);
	}
	
	public void exclusiveAdd(Player player, long time) {
		if (exclusiveLatest (player)) throw new InvalidOperationException("tile is already exclusive");
		exclusive[player.id].Add (time);
		for (int tX = Math.Max (0, x - 1); tX <= Math.Min (g.tileLen () - 1, x + 1); tX++) {
			for (int tY = Math.Max (0, y - 1); tY <= Math.Min (g.tileLen () - 1, y + 1); tY++) {
				if (tX != x || tY != y) {
					foreach (var waypoint in g.tiles[tX, tY].waypoints) {
						long halfMoveInterval = new FP.Vector(tX - x << FP.Precision, tY - y << FP.Precision).length() / g.units[waypoint.Key].type.speed / 2;
						if (player == g.units[waypoint.Key].player && Waypoint.active (waypoint.Value.Last ())
							&& time >= waypoint.Value.Last ().time + halfMoveInterval) {
							g.events.add (new WaypointAddEvt(time + halfMoveInterval, g.units[waypoint.Key], this, waypoint.Value.Last (), null));
						}
					}
				}
			}
		}
	}
	
	public void exclusiveRemove(Player player, long time) {
		if (!exclusiveLatest(player)) throw new InvalidOperationException("tile is already not exclusive");
		exclusive[player.id].Add (time);
		foreach (var waypoint in waypoints) {
			if (player == g.units[waypoint.Key].player && Waypoint.active (waypoint.Value.Last ())) {
				waypointAdd (g.units[waypoint.Key], time, null, null);
			}
		}
	}

	/// <summary>
	/// calculates from player visibility tiles if specified player can infer that no other player can see this tile at latest possible time
	/// </summary>
	/// <remarks>
	/// The worst-case scenario would then be that every tile that this player can't see contains another player's unit
	/// of the type with the greatest visibility radius (though all units in this game have the same visibility radius).
	/// If no other player could see this tile in this worst case scenario,
	/// the player can infer that he/she is the only player that can see this tile.
	/// </remarks>
	public bool calcExclusive(Player player) {
		if (Sim.EnableNonLivePaths && player.unseenTiles != 0) {
			// check that this player can see all nearby tiles
			if (g.inVis(g.lastUnseenTile.x - x, g.lastUnseenTile.y - y) && !g.tiles[g.lastUnseenTile.x, g.lastUnseenTile.y].playerVisLatest(player)) return false;
			int tXMin = Math.Max(0, x - g.tileVisRadius());
			int tYMin = Math.Max(0, y - g.tileVisRadius());
			for (int tX = Math.Min(g.tileLen() - 1, x + g.tileVisRadius()); tX >= tXMin; tX--) {
				for (int tY = Math.Min(g.tileLen() - 1, y + g.tileVisRadius()); tY >= tYMin; tY--) {
					if (g.inVis(tX - x, tY - y) && !g.tiles[tX, tY].playerVisLatest(player)) {
						g.lastUnseenTile.x = tX;
						g.lastUnseenTile.y = tY;
						return false;
					}
				}
			}
		}
		// check that no other players can see this tile
		foreach (Player player2 in g.players) {
			if (player != player2 && !player2.immutable && playerDirectVisLatest(player2)) return false;
		}
		return true;
	}

	/// <summary>
	/// returns if specified player can infer that no other player can see this tile at latest possible time
	/// </summary>
	public bool exclusiveLatest(Player player) {
		if (!Sim.EnableNonLivePaths) return calcExclusive(player);
		return visLatest(exclusive[player.id]);
	}

	/// <summary>
	/// returns gain/lose exclusivity index associated with specified time for specified player
	/// </summary>
	public int exclusiveIndexWhen(Player player, long time) {
		return visIndexWhen(exclusive[player.id], time);
	}

	/// <summary>
	/// returns if specified player can infer that no other player can see this tile at specified time
	/// </summary>
	public bool exclusiveWhen(Player player, long time) {
		if (!Sim.EnableNonLivePaths && time == g.timeSim) return calcExclusive (player);
		return visWhen(exclusive[player.id], time);
	}
	
	public Waypoint waypointAdd(Unit unit, long time, Waypoint prev, UnitSelection start) {
		if (!waypoints.ContainsKey (unit.id)) waypoints[unit.id] = new List<Waypoint>();
		Waypoint waypoint = new Waypoint(time, this, prev, start);
		waypoints[unit.id].Add (waypoint);
		return waypoint;
	}
	
	public Waypoint waypointLatest(Unit unit) {
		return waypoints.ContainsKey (unit.id) ? waypoints[unit.id].LastOrDefault () : null;
	}
	
	public Waypoint waypointWhen(Unit unit, long time) {
		if (waypoints.ContainsKey (unit.id)) {
			for (int i = waypoints[unit.id].Count - 1; i >= 0; i--) {
				if (time >= waypoints[unit.id][i].time) return waypoints[unit.id][i];
			}
		}
		return null;
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
