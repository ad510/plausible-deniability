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
					if (g.tiles[tX, tY].calcExclusive (g.players[player]) != g.tiles[tX, tY].exclusiveLatest (g.players[player])) {
						g.tiles[tX, tY].exclusive[player].Add(time);
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
