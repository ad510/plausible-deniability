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
