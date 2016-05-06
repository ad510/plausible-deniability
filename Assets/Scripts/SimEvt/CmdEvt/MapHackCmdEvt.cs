// Written in 2014 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

[ProtoContract]
public class MapHackCmdEvt : CmdEvt {
	[ProtoMember(1)] public int player;
	[ProtoMember(2)] public bool mapHack;

	/// <summary>
	/// empty constructor for protobuf-net use only
	/// </summary>
	private MapHackCmdEvt() { }

	public MapHackCmdEvt(long timeVal, int playerVal, bool mapHackVal) {
		time = timeVal;
		player = playerVal;
		mapHack = mapHackVal;
	}

	public override void apply(Sim g) {
		if (!g.players[player].mapHack && mapHack) {
			for (int tX = 0; tX < g.tileLen (); tX++) {
				for (int tY = 0; tY < g.tileLen (); tY++) {
					if (!g.tiles[tX, tY].playerVisLatest (g.players[player])) {
						g.tiles[tX, tY].playerVis[player].Add (time);
					}
				}
			}
			g.players[player].unseenTiles = 0;
			for (int tX = 0; tX < g.tileLen (); tX++) {
				for (int tY = 0; tY < g.tileLen (); tY++) {
					bool exclusiveOld = g.tiles[tX, tY].exclusiveLatest (g.players[player]);
					bool exclusiveNew = g.tiles[tX, tY].calcExclusive (g.players[player]);
					if (!exclusiveOld && exclusiveNew) {
						g.tiles[tX, tY].exclusiveAdd(g.players[player], time);
					}
					else if (exclusiveOld && !exclusiveNew) {
						g.tiles[tX, tY].exclusiveRemove(g.players[player], time);
					}
				}
			}
		}
		else if (g.players[player].mapHack && !mapHack) {
			for (int tX = 0; tX < g.tileLen (); tX++) {
				for (int tY = 0; tY < g.tileLen (); tY++) {
					g.tiles[tX, tY].playerVisRemove (g.players[player], time);
				}
			}
		}
		g.players[player].mapHack = mapHack;
	}
}
