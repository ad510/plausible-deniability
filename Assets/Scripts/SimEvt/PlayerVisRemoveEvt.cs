// Copyright (c) 2013-2014 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

/// <remarks>should only be instantiated by Tile.playerVisRemove()</remarks>
[ProtoContract]
public class PlayerVisRemoveEvt : SimEvt {
	[ProtoMember(1, AsReference = true)] public Player player;
	[ProtoMember(2, AsReference = true)] public List<Tile> tiles;
	
	private PlayerVisRemoveEvt() { } // for protobuf-net use only

	public PlayerVisRemoveEvt(long timeVal, Player playerVal, Tile tileVal) {
		time = timeVal;
		player = playerVal;
		tiles = new List<Tile> { tileVal };
	}

	public override void apply(Sim g) {
		if (player.mapHack) return;
		// remove visibility from specified tiles
		for (int i = 0; i < tiles.Count; i++) {
			if (tiles[i].playerVisLatest(player) && !tiles[i].playerDirectVisLatest(player)) {
				tiles[i].playerVis[player.id].Add(time);
				player.unseenTiles++;
			} else {
				tiles[i] = null;
			}
		}
		// add events to remove visibility from surrounding tiles
		foreach (Tile tile in tiles) {
			if (tile != null) {
				for (int tX = Math.Max(0, tile.x - 1); tX <= Math.Min(g.tileLen() - 1, tile.x + 1); tX++) {
					for (int tY = Math.Max(0, tile.y - 1); tY <= Math.Min(g.tileLen() - 1, tile.y + 1); tY++) {
						if ((tX != tile.x || tY != tile.y) && g.tiles[tX, tY].playerVisLatest(player)) {
							// ISSUE #29: lose visibility in a circle instead of a square
							g.tiles[tX, tY].playerVisRemove(player, time + (1 << FP.precision) / g.maxSpeed);
						}
					}
				}
			}
		}
		if (Sim.enableNonLivePaths) {
			// check if a tile stopped being exclusive to this player, or became exclusive to another player
			Tile prevTile = null;
			foreach (Tile tile in tiles) {
				if (tile != null) {
					int tXMax = Math.Min(g.tileLen() - 1, tile.x + g.tileVisRadius());
					int tYMax = Math.Min(g.tileLen() - 1, tile.y + g.tileVisRadius());
					for (int tX = Math.Max(0, tile.x - g.tileVisRadius()); tX <= tXMax; tX++) {
						for (int tY = Math.Max(0, tile.y - g.tileVisRadius()); tY <= tYMax; tY++) {
							if (g.inVis(tX - tile.x, tY - tile.y) && (prevTile == null || !g.inVis(tX - prevTile.x, tY - prevTile.y))) {
								foreach (Player player2 in g.players) {
									if (player2 == player && g.tiles[tX, tY].exclusiveLatest(player2)) {
										g.tiles[tX, tY].exclusiveRemove(player2, time);
									} else if (player2 != player && !g.tiles[tX, tY].exclusiveLatest(player2) && g.tiles[tX, tY].calcExclusive(player2)) {
										g.tiles[tX, tY].exclusiveAdd(player2, time);
									}
								}
							}
						}
					}
					prevTile = tile;
				}
			}
		}
	}
}
