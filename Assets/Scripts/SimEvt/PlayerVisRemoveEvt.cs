// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

/// <summary>
/// event in which a player stops seeing tiles
/// </summary>
[ProtoContract]
public class PlayerVisRemoveEvt : SimEvt {
	[ProtoMember(1)] public int player;
	[ProtoMember(2)] public List<FP.Vector> tiles;
	
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	private PlayerVisRemoveEvt() { }

	public PlayerVisRemoveEvt(long timeVal, int playerVal, int tileXVal, int tileYVal) {
		time = timeVal;
		player = playerVal;
		tiles = new List<FP.Vector>();
		tiles.Add (new FP.Vector(tileXVal, tileYVal));
	}

	public override void apply(Sim g) {
		for (int i = 0; i < tiles.Count; i++) {
			if (g.tiles[tiles[i].x, tiles[i].y].playerVisLatest(g.players[player]) && !g.tiles[tiles[i].x, tiles[i].y].playerDirectVisLatest(g.players[player])) {
				g.tiles[tiles[i].x, tiles[i].y].playerVis[player].Add(time);
				// add events to remove visibility from surrounding tiles
				for (int tX = Math.Max(0, (int)tiles[i].x - 1); tX <= Math.Min(g.tileLen() - 1, (int)tiles[i].x + 1); tX++) {
					for (int tY = Math.Max(0, (int)tiles[i].y - 1); tY <= Math.Min(g.tileLen() - 1, (int)tiles[i].y + 1); tY++) {
						if ((tX != tiles[i].x || tY != tiles[i].y) && g.tiles[tX, tY].playerVisLatest(g.players[player])) {
							// ISSUE #29: lose visibility in a circle instead of a square
							g.tiles[tX, tY].playerVisRemove(g.players[player], time + (1 << FP.Precision) / g.maxSpeed);
						}
					}
				}
			}
			else {
				tiles[i] = new FP.Vector(Sim.OffMap, Sim.OffMap);
			}
		}
		if (Sim.EnableNonLivePaths) {
			// check if a tile stopped being exclusive to this player, or became exclusive to another player
			int iPrev = -1;
			for (int i = 0; i < tiles.Count; i++) {
				if (tiles[i].x != Sim.OffMap) {
					for (int tX = Math.Max(0, (int)tiles[i].x - g.tileVisRadius()); tX <= Math.Min(g.tileLen() - 1, (int)tiles[i].x + g.tileVisRadius()); tX++) {
						for (int tY = Math.Max(0, (int)tiles[i].y - g.tileVisRadius()); tY <= Math.Min(g.tileLen() - 1, (int)tiles[i].y + g.tileVisRadius()); tY++) {
							if (g.inVis(tX - tiles[i].x, tY - tiles[i].y) && (iPrev == -1 || !g.inVis(tX - tiles[iPrev].x, tY - tiles[iPrev].y))) {
								foreach (Player player2 in g.players) {
									if (player2.id == player && g.tiles[tX, tY].exclusiveLatest(player2)) {
										g.tiles[tX, tY].exclusiveRemove(player2, time);
									}
									else if (player2.id != player && !g.tiles[tX, tY].exclusiveLatest(player2) && g.tiles[tX, tY].calcExclusive(player2)) {
										g.tiles[tX, tY].exclusiveAdd(player2, time);
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
}
