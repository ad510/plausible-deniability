﻿// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
		int tXPrev = g.paths[path].tileX;
		int tYPrev = g.paths[path].tileY;
		if (tileX == int.MinValue) tileX = g.paths[path].tileX;
		if (tileY == int.MinValue) tileY = g.paths[path].tileY;
		g.paths[path].tileX = tileX;
		g.paths[path].tileY = tileY;
		// add path to visibility tiles
		for (int tX = Math.Max (0, tileX - g.tileVisRadius()); tX <= Math.Min (g.tileLen () - 1, tileX + g.tileVisRadius()); tX++) {
			for (int tY = Math.Max (0, tileY - g.tileVisRadius()); tY <= Math.Min (g.tileLen () - 1, tileY + g.tileVisRadius()); tY++) {
				if (!g.inVis(tX - tXPrev, tY - tYPrev) && g.inVis(tX - tileX, tY - tileY)) {
					if (g.tiles[tX, tY].pathVisLatest(path)) throw new InvalidOperationException("path " + path + " already sees tile (" + tX + ", " + tY + ")");
					// add path to path visibility tile
					g.tiles[tX, tY].pathVisToggle(path, time);
					if (!g.tiles[tX, tY].playerVisLatest(g.paths[path].player)) {
						g.tiles[tX, tY].playerVis[g.paths[path].player].Add(time);
						playerVisAddTiles.Add(new FP.Vector(tX, tY));
						// check if this tile stopped being exclusive to another player
						for (int i = 0; i < g.players.Length; i++) {
							if (i != g.paths[path].player && g.tiles[tX, tY].exclusiveLatest(i)) {
								g.exclusiveRemove(i, tX, tY, time);
							}
						}
					}
				}
			}
		}
		// remove path from visibility tiles
		for (int tX = Math.Max (0, tXPrev - g.tileVisRadius()); tX <= Math.Min (g.tileLen () - 1, tXPrev + g.tileVisRadius()); tX++) {
			for (int tY = Math.Max (0, tYPrev - g.tileVisRadius()); tY <= Math.Min (g.tileLen () - 1, tYPrev + g.tileVisRadius()); tY++) {
				if (g.inVis(tX - tXPrev, tY - tYPrev) && !g.inVis(tX - tileX, tY - tileY)) {
					if (!g.tiles[tX, tY].pathVisLatest(path)) throw new InvalidOperationException("path " + path + " already doesn't see tile (" + tX + ", " + tY + ")");
					// remove path from path visibility tile
					g.tiles[tX, tY].pathVisToggle(path, time);
					// check if player can't directly see this tile anymore
					if (g.tiles[tX, tY].playerVisLatest(g.paths[path].player) && !g.tiles[tX, tY].playerDirectVisLatest(g.paths[path].player)) {
						long timePlayerVis = long.MaxValue;
						// find lowest time that surrounding tiles lost visibility
						for (int tX2 = Math.Max(0, tX - 1); tX2 <= Math.Min(g.tileLen() - 1, tX + 1); tX2++) {
							for (int tY2 = Math.Max(0, tY - 1); tY2 <= Math.Min(g.tileLen() - 1, tY + 1); tY2++) {
								if ((tX2 != tX || tY2 != tY) && !g.tiles[tX2, tY2].playerVisLatest(g.paths[path].player)) {
									if (g.tiles[tX2, tY2].playerVis[g.paths[path].player].Count == 0) {
										timePlayerVis = long.MinValue;
									}
									else if (g.tiles[tX2, tY2].playerVis[g.paths[path].player].Last () < timePlayerVis) {
										timePlayerVis = g.tiles[tX2, tY2].playerVis[g.paths[path].player].Last ();
									}
								}
							}
						}
						// if player can't see all neighboring tiles, they won't be able to tell if another player's unit moves into this tile
						// so remove this tile's visibility for this player
						if (timePlayerVis != long.MaxValue) {
							timePlayerVis = Math.Max(time, timePlayerVis + (1 << FP.Precision) / g.maxSpeed); // TODO: use more accurate time
							g.playerVisRemove(g.paths[path].player, tX, tY, timePlayerVis);
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
		for (int tX = exclusiveMinX; tX <= exclusiveMaxX; tX++) {
			for (int tY = exclusiveMinY; tY <= exclusiveMaxY; tY++) {
				foreach (FP.Vector vec in playerVisAddTiles) {
					if (g.inVis(tX - vec.x, tY - vec.y)) {
						if (!g.tiles[tX, tY].exclusiveLatest(g.paths[path].player) && g.calcExclusive(g.paths[path].player, tX, tY)) {
							g.exclusiveAdd(g.paths[path].player, tX, tY, time);
						}
						break;
					}
				}
			}
		}
		if (tileX >= 0 && tileX < g.tileLen() && tileY >= 0 && tileY < g.tileLen()) {
			// update whether this path is known to be unseen
			if (!g.paths[path].segments.Last ().unseen && g.tiles[tileX, tileY].exclusiveLatest(g.paths[path].player)) {
				g.paths[path].beUnseen(time);
			}
			else if (g.paths[path].segments.Last ().unseen && !g.tiles[tileX, tileY].exclusiveLatest(g.paths[path].player)) {
				g.paths[path].beSeen(time);
			}
			// if this path moved out of another player's visibility, remove that player's visibility here
			if (!g.players[g.paths[path].player].immutable && tXPrev >= 0 && tXPrev < g.tileLen() && tYPrev >= 0 && tYPrev < g.tileLen()) {
				for (int i = 0; i < g.players.Length; i++) {
					if (i != g.paths[path].player && g.tiles[tXPrev, tYPrev].playerDirectVisLatest(i) && !g.tiles[tileX, tileY].playerDirectVisLatest(i)) {
						for (int tX = Math.Max(0, tileX - 1); tX <= Math.Min(g.tileLen() - 1, tileX + 1); tX++) {
							for (int tY = Math.Max(0, tileY - 1); tY <= Math.Min(g.tileLen() - 1, tileY + 1); tY++) {
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
			foreach (int i in g.tiles[tXPrev, tYPrev].pathVis.Keys) {
				if (g.paths[i].player != g.paths[path].player && !g.players[g.paths[i].player].immutable && g.paths[i].segments.Last ().units.Count > 0
					&& g.inVis(g.paths[i].tileX - tXPrev, g.paths[i].tileY - tYPrev) && !g.tiles[g.paths[i].tileX, g.paths[i].tileY].playerDirectVisLatest(g.paths[path].player)) {
					for (int tX = Math.Max(0, g.paths[i].tileX - 1); tX <= Math.Min(g.tileLen() - 1, g.paths[i].tileX + 1); tX++) {
						for (int tY = Math.Max(0, g.paths[i].tileY - 1); tY <= Math.Min(g.tileLen() - 1, g.paths[i].tileY + 1); tY++) {
							// TODO?: use more accurate time at tiles other than (paths[i].tileX, paths[i].tileY)
							g.playerVisRemove(g.paths[path].player, tX, tY, time);
						}
					}
				}
			}
		}
	}
}