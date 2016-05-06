// Written in 2014 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

[ProtoContract]
public class TileUpdateEvt : SimEvt {
	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	private TileUpdateEvt() { }
	
	public TileUpdateEvt(long timeVal) {
		time = timeVal;
	}
	
	public override void apply (Sim g) {
		bool anyoneMoved = false;
		foreach (Path path in g.paths) {
			if (path.timeSimPast == long.MaxValue && path.tileX != Sim.OffMap) {
				int tXPrev = path.tileX;
				int tYPrev = path.tileY;
				path.updateTilePos (time);
				if (path.tileX != tXPrev || path.tileY != tYPrev) {
					anyoneMoved = true;
					int exclusiveMinX = g.tileLen() - 1;
					int exclusiveMaxX = 0;
					int exclusiveMinY = g.tileLen() - 1;
					int exclusiveMaxY = 0;
					// add path to visibility tiles
					for (int tX = Math.Max (0, path.tileX - g.tileVisRadius()); tX <= Math.Min (g.tileLen () - 1, path.tileX + g.tileVisRadius()); tX++) {
						for (int tY = Math.Max (0, path.tileY - g.tileVisRadius()); tY <= Math.Min (g.tileLen () - 1, path.tileY + g.tileVisRadius()); tY++) {
							if (!g.inVis(tX - tXPrev, tY - tYPrev) && g.inVis(tX - path.tileX, tY - path.tileY)) {
								if (g.tiles[tX, tY].pathVisLatest(path)) throw new InvalidOperationException("path " + path.id + " already sees tile (" + tX + ", " + tY + ")");
								// add path to path visibility tile
								g.tiles[tX, tY].pathVisToggle(path, time);
								if (!g.tiles[tX, tY].playerVisLatest(path.player)) {
									g.tiles[tX, tY].playerVis[path.player.id].Add(time);
									path.player.unseenTiles--;
									if (Sim.EnableNonLivePaths) {
										if (tX < exclusiveMinX) exclusiveMinX = tX;
										if (tX > exclusiveMaxX) exclusiveMaxX = tX;
										if (tY < exclusiveMinY) exclusiveMinY = tY;
										if (tY > exclusiveMaxY) exclusiveMaxY = tY;
									}
								}
								// check if this tile stopped being exclusive to another player
								if (Sim.EnableNonLivePaths && !path.player.immutable) {
									foreach (Player player in g.players) {
										if (player != path.player && g.tiles[tX, tY].exclusiveLatest(player)) {
											g.tiles[tX, tY].exclusive[player.id].Add(time);
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
								if (!g.tiles[tX, tY].pathVisLatest(path)) throw new InvalidOperationException("path " + path.id + " already doesn't see tile (" + tX + ", " + tY + ")");
								// remove path from path visibility tile
								g.tiles[tX, tY].pathVisToggle(path, time);
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
										timePlayerVis = Math.Max(time, timePlayerVis + (1 << FP.Precision) / g.maxSpeed); // ISSUE #29: lose visibility in a circle instead of a square
										g.tiles[tX, tY].playerVisRemove(path.player, timePlayerVis);
									}
								}
								// check if this tile became exclusive to a player using map hack
								if (Sim.EnableNonLivePaths && !path.player.immutable) {
									foreach (Player player in g.players) {
										if (player != path.player && player.mapHack && !g.tiles[tX, tY].exclusiveLatest (player) && g.tiles[tX, tY].calcExclusive (player)) {
											g.tiles[tX, tY].exclusive[player.id].Add(time);
										}
									}
								}
							}
						}
					}
					if (Sim.EnableNonLivePaths) {
						// apply PlayerVisRemoveEvts that occur immediately
						foreach (SimEvt evt in g.events.events) {
							if (evt.time > time) break;
							if (evt is PlayerVisRemoveEvt) {
								PlayerVisRemoveEvt visEvt = evt as PlayerVisRemoveEvt;
								if (visEvt.player == path.player.id) {
									visEvt.apply (g);
									g.events.events.Remove (evt);
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
									g.tiles[tX, tY].exclusive[path.player.id].Add(time);
								}
							}
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
							Tile iTile = g.paths[i].activeTile (time);
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
		}
		if (anyoneMoved) {
			// update which paths are known to be unseen
			foreach (Path path in g.paths) {
				if (path.tileX >= 0 && path.tileX < g.tileLen() && path.tileY >= 0 && path.tileY < g.tileLen()) {
					if (!path.segments.Last ().unseen && g.tiles[path.tileX, path.tileY].exclusiveLatest(path.player)) {
						// path is now unseen
						path.insertSegment(time).unseen = true;
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
					}
				}
			}
		}
		g.events.add (new TileUpdateEvt(time + g.tileInterval));
	}
}
