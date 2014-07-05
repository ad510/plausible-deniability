// Copyright (c) 2014 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

[ProtoContract]
public class TileUpdateEvt : SimEvt {
	private TileUpdateEvt() { } // for protobuf-net use only
	
	public TileUpdateEvt(long timeVal) {
		time = timeVal;
	}
	
	public override void apply (Sim g) {
		bool anyoneMoved = false;
		foreach (Path path in g.paths) {
			int tXPrev, tYPrev;
			if (path.timeSimPast == long.MaxValue && path.tileX != Sim.offMap && path.updateTilePos(time, out tXPrev, out tYPrev)) {
				anyoneMoved = true;
				int exclusiveMinX = g.tileLen() - 1;
				int exclusiveMaxX = 0;
				int exclusiveMinY = g.tileLen() - 1;
				int exclusiveMaxY = 0;
				// add path to visibility tiles
				for (int tX = Math.Max (0, path.tileX - g.tileVisRadius()); tX <= Math.Min (g.tileLen () - 1, path.tileX + g.tileVisRadius()); tX++) {
					for (int tY = Math.Max (0, path.tileY - g.tileVisRadius()); tY <= Math.Min (g.tileLen () - 1, path.tileY + g.tileVisRadius()); tY++) {
						if (!g.inVis(tX - tXPrev, tY - tYPrev) && g.inVis(tX - path.tileX, tY - path.tileY)) {
							if (!g.tiles[tX, tY].playerVisLatest(path.player)) {
								// make tile visible to this player
								g.tiles[tX, tY].playerVis[path.player.id].Add(time);
								path.player.unseenTiles--;
								if (Sim.enableNonLivePaths) {
									if (tX < exclusiveMinX) exclusiveMinX = tX;
									if (tX > exclusiveMaxX) exclusiveMaxX = tX;
									if (tY < exclusiveMinY) exclusiveMinY = tY;
									if (tY > exclusiveMaxY) exclusiveMaxY = tY;
								}
							}
							// check if this tile stopped being exclusive to another player
							if (Sim.enableNonLivePaths && !path.player.immutable) {
								foreach (Player player in g.players) {
									if (player != path.player && g.tiles[tX, tY].exclusiveLatest(player)) {
										g.tiles[tX, tY].exclusiveRemove(player, time);
									}
								}
							}
						}
					}
				}
				// remove path from visibility tiles
				for (int tX = Math.Max (0, tXPrev - g.tileVisRadius()); tX <= Math.Min (g.tileLen () - 1, tXPrev + g.tileVisRadius()); tX++) {
					for (int tY = Math.Max (0, tYPrev - g.tileVisRadius()); tY <= Math.Min (g.tileLen () - 1, tYPrev + g.tileVisRadius()); tY++) {
						if (g.inVis(tX - tXPrev, tY - tYPrev) && !g.inVis(tX - path.tileX, tY - path.tileY)) {
							// check if player can't directly see this tile anymore
							if (g.tiles[tX, tY].playerVisLatest(path.player) && !g.tiles[tX, tY].playerDirectVisLatest(path.player)) {
								long timePlayerVis = long.MaxValue;
								// find lowest time that surrounding tiles lost visibility
								for (int tX2 = Math.Max(0, tX - 1); tX2 <= Math.Min(g.tileLen() - 1, tX + 1); tX2++) {
									for (int tY2 = Math.Max(0, tY - 1); tY2 <= Math.Min(g.tileLen() - 1, tY + 1); tY2++) {
										if ((tX2 != tX || tY2 != tY) && !g.tiles[tX2, tY2].playerVisLatest(path.player)) {
											if (g.tiles[tX2, tY2].playerVis[path.player.id].Count == 0) {
												timePlayerVis = long.MinValue;
											}
											else if (g.tiles[tX2, tY2].playerVis[path.player.id].Last () < timePlayerVis) {
												timePlayerVis = g.tiles[tX2, tY2].playerVis[path.player.id].Last ();
											}
										}
									}
								}
								// if player can't see all neighboring tiles, they won't be able to tell if another player's unit moves into this tile
								// so remove this tile's visibility for this player
								if (timePlayerVis != long.MaxValue) {
									timePlayerVis = Math.Max(time, timePlayerVis + (1 << FP.precision) / g.maxSpeed); // ISSUE #29: lose visibility in a circle instead of a square
									g.tiles[tX, tY].playerVisRemove(path.player, timePlayerVis);
								}
							}
							// check if this tile became exclusive to a player using map hack
							if (Sim.enableNonLivePaths && !path.player.immutable) {
								foreach (Player player in g.players) {
									if (player != path.player && player.mapHack && !g.tiles[tX, tY].exclusiveLatest (player) && g.tiles[tX, tY].calcExclusive (player)) {
										g.tiles[tX, tY].exclusiveAdd(player, time);
									}
								}
							}
						}
					}
				}
				if (Sim.enableNonLivePaths) {
					// apply PlayerVisRemoveEvts that occur immediately
					foreach (SimEvt evt in g.events) {
						if (evt.time > time) break;
						if (evt is PlayerVisRemoveEvt) {
							PlayerVisRemoveEvt visEvt = evt as PlayerVisRemoveEvt;
							if (visEvt.player == path.player) {
								visEvt.apply (g);
								g.events.Remove (evt);
								break;
							}
						}
					}
					// check if tiles became exclusive to this player
					exclusiveMinX = Math.Max(0, exclusiveMinX - g.tileVisRadius());
					exclusiveMaxX = Math.Min(g.tileLen() - 1, exclusiveMaxX + g.tileVisRadius());
					exclusiveMinY = Math.Max(0, exclusiveMinY - g.tileVisRadius());
					exclusiveMaxY = Math.Min(g.tileLen() - 1, exclusiveMaxY + g.tileVisRadius());
					for (int tX = exclusiveMinX; tX <= exclusiveMaxX; tX++) {
						for (int tY = exclusiveMinY; tY <= exclusiveMaxY; tY++) {
							if (!g.tiles[tX, tY].exclusiveLatest(path.player) && g.tiles[tX, tY].calcExclusive(path.player)) {
								g.tiles[tX, tY].exclusiveAdd(path.player, time);
							}
						}
					}
					// add waypoints for units
					foreach (Unit unit in path.segmentWhen (time).units) {
						unit.addWaypoint (time, path);
					}
				}
				if (tXPrev >= 0 && tXPrev < g.tileLen() && tYPrev >= 0 && tYPrev < g.tileLen()) {
					// if this path moved out of another player's direct visibility, remove that player's visibility here
					if (path.tileX >= 0 && path.tileX < g.tileLen() && path.tileY >= 0 && path.tileY < g.tileLen()) {
						if (!path.player.immutable) {
							foreach (Player player in g.players) {
								if (player != path.player && g.tiles[tXPrev, tYPrev].playerDirectVisLatest(player) && !g.tiles[path.tileX, path.tileY].playerDirectVisLatest(player)) {
									for (int tX = Math.Max(0, path.tileX - 1); tX <= Math.Min(g.tileLen() - 1, path.tileX + 1); tX++) {
										for (int tY = Math.Max(0, path.tileY - 1); tY <= Math.Min(g.tileLen() - 1, path.tileY + 1); tY++) {
											// ISSUE #30: perhaps use more accurate time at tiles other than (path.tileX, path.tileY)
											g.tiles[tX, tY].playerVisRemove(player, time);
										}
									}
								}
							}
						}
					}
					// if this player can no longer directly see another player's path, remove this player's visibility there
					foreach (int i in g.tiles[tXPrev, tYPrev].pathVis.Keys) {
						Tile iTile = g.paths[i].tileWhen (time);
						if (g.paths[i].player != path.player && !g.paths[i].player.immutable && g.paths[i].segments.Last ().units.Count > 0
							&& g.inVis(iTile.x - tXPrev, iTile.y - tYPrev) && !iTile.playerDirectVisLatest(path.player)) {
							for (int tX = Math.Max(0, iTile.x - 1); tX <= Math.Min(g.tileLen() - 1, iTile.x + 1); tX++) {
								for (int tY = Math.Max(0, iTile.y - 1); tY <= Math.Min(g.tileLen() - 1, iTile.y + 1); tY++) {
									// ISSUE #30: perhaps use more accurate time at tiles other than (paths[i].tileX, paths[i].tileY)
									g.tiles[tX, tY].playerVisRemove(path.player, time);
								}
							}
						}
					}
				}
			}
		}
		if (anyoneMoved) {
			// update which paths are known to be unseen
			foreach (Path path in g.paths) {
				if (path.tileX >= 0 && path.tileX < g.tileLen() && path.tileY >= 0 && path.tileY < g.tileLen()) {
					if (!path.segments.Last ().unseen && g.tiles[path.tileX, path.tileY].exclusiveLatest(path.player)) {
						// path is now unseen
						Segment segment = path.insertSegment(time);
						segment.unseen = true;
						foreach (Unit unit in segment.units) {
							unit.addWaypoint (time, path);
						}
					}
					else if (path.segments.Last ().unseen && !g.tiles[path.tileX, path.tileY].exclusiveLatest(path.player)) {
						// path is now seen
						Segment segment = path.insertSegment(time);
						foreach (Unit unit in segment.units.OrderByDescending (u => u.type.seePrecedence)) {
							if (segment.units.Count <= path.nSeeUnits) break;
							new SegmentUnit(segment, unit).delete ();
						}
						path.nSeeUnits = int.MaxValue;
						if (!g.deleteOtherPaths (segment.segmentUnits(), false, true)) throw new SystemException("failed to delete other paths of seen path");
						segment.unseen = false;
						foreach (Unit unit in segment.units) {
							unit.clearWaypoints (time);
						}
					}
				}
			}
		}
		g.events.addEvt (new TileUpdateEvt(time + g.tileInterval));
	}
}
