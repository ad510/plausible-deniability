// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class Tile {
	private readonly Sim g;
	public readonly int x, y;
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

	public Tile(Sim simVal, int xVal, int yVal) {
		g = simVal;
		x = xVal;
		y = yVal;
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
	/// makes this tile not visible to specified player starting at specified time, including effects on surrounding tiles
	/// </summary>
	public void playerVisRemove(Player player, long time) {
		// try adding tile to existing PlayerVisRemoveEvt with same player and time
		foreach (SimEvt evt in g.events.events) {
			if (evt is PlayerVisRemoveEvt) {
				PlayerVisRemoveEvt visEvt = (PlayerVisRemoveEvt)evt;
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
	public bool playerDirectVisWhen(Player player, long time) {
		foreach (int i in pathVis.Keys) {
			if (player == g.paths[i].player && visWhen(pathVis[i], time)) {
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

	/// <summary>
	/// makes this tile exclusive to specified player starting at specified time,
	/// including how that affects paths on this tile
	/// </summary>
	public void exclusiveAdd(Player player, long time) {
		if (exclusiveLatest(player)) throw new InvalidOperationException("tile (" + x + ", " + y + ") is already exclusive");
		exclusive[player.id].Add(time);
		// this player's paths that are on this tile may time travel starting now
		// TODO: actually safe to time travel at earlier times, as long as unit of same type is at same place when seen by another player
		foreach (Path path in g.paths) {
			if (player == path.player && x == path.tileX && y == path.tileY && !path.segments.Last ().unseen) {
				path.beUnseen(time);
			}
		}
	}

	/// <summary>
	/// makes this tile not exclusive to specified player starting at specified time,
	/// including how that affects paths on this tile
	/// </summary>
	public void exclusiveRemove(Player player, long time) {
		if (!exclusiveLatest(player)) throw new InvalidOperationException("tile (" + x + ", " + y + ") is already not exclusive");
		exclusive[player.id].Add(time);
		// this player's paths that are on this tile may not time travel starting now
		foreach (Path path in g.paths) {
			if (player == path.player && x == path.tileX && y == path.tileY && path.segments.Last ().unseen) {
				path.beSeen(time);
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
		// check that this player can see all nearby tiles
		for (int tX = Math.Max(0, x - g.tileVisRadius()); tX <= Math.Min(g.tileLen() - 1, x + g.tileVisRadius()); tX++) {
			for (int tY = Math.Max(0, y - g.tileVisRadius()); tY <= Math.Min(g.tileLen() - 1, y + g.tileVisRadius()); tY++) {
				if (g.inVis(tX - x, tY - y) && !g.tiles[tX, tY].playerVisLatest(player)) return false;
			}
		}
		// check that no other players can see this tile
		foreach (Player player2 in g.players) {
			if (player != player2 && !player2.immutable && playerVisLatest(player2)) return false;
		}
		return true;
	}

	/// <summary>
	/// returns if specified player can infer that no other player can see this tile at latest possible time
	/// </summary>
	public bool exclusiveLatest(Player player) {
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
		return visWhen(exclusive[player.id], time);
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
