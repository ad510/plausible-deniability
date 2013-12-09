// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class Player {
	public Sim g;
	public int id; // index in player array
	// stored in scenario files
	public string name;
	public bool isUser; // whether actively participates in the game
	public int user; // -2 = nobody, -1 = computer, 0+ = human
	public long[] startRsc; // resources at beginning of game
	public bool[] mayAttack; // if this player's units may attack each other player's units
	// not stored in scenario files
	public bool immutable; // whether player's units will never unpredictably move or change
	public bool hasNonLivePaths; // whether currently might have time traveling paths (ok to sometimes incorrectly be set to true)
	public long timeGoLiveFail; // latest time that player's time traveling paths failed to go live (resets to long.MaxValue after success)
	public long timeNegRsc; // time that player could have negative resources if time traveling paths went live

	public Player() {
		hasNonLivePaths = false;
		timeGoLiveFail = long.MaxValue;
	}

	/// <summary>
	/// update player's non-live (time traveling) paths
	/// </summary>
	public void updatePast(long curTime) {
		if (hasNonLivePaths) {
			foreach (Path path in g.paths) {
				if (this == path.player) path.updatePast(curTime);
			}
			if (curTime >= g.timeSim && (timeGoLiveFail == long.MaxValue || g.timeSim >= timeGoLiveFail + g.updateInterval)) {
				g.cmdPending.add(new GoLiveCmdEvt(g.timeSim, id));
			}
		}
	}

	/// <summary>
	/// returns amount of specified resource that player has at specified time
	/// </summary>
	/// <param name="max">
	/// since different paths can have collected different resource amounts,
	/// determines whether to use paths that collected least or most resources in calculation
	/// </param>
	public long resource(long time, int rscType, bool max, bool includeNonLiveChildren) {
		long ret = startRsc[rscType];
		for (int i = 0; i < g.nRootPaths; i++) {
			if (this == g.paths[i].player) {
				foreach (int unit in g.paths[i].segments[0].units) {
					// TODO: this will double-count units that are in multiple paths at beginning of scenario
					ret += g.paths[i].segments[0].rscCollected(time, g.units[unit], rscType, max, includeNonLiveChildren);
				}
			}
		}
		return ret;
	}

	/// <summary>
	/// checks whether player could have negative resources since timeMin in worst case scenario of which paths are seen
	/// </summary>
	/// <returns>a time that player could have negative resources, or -1 if no such time found</returns>
	public long checkNegRsc(long timeMin, bool includeNonLiveChildren) {
		foreach (Path path in g.paths) {
			// check all times since timeMin that a path of specified player was made
			if (this == path.player && path.segments[0].timeStart >= timeMin) {
				for (int i = 0; i < g.rscNames.Length; i++) {
					if (resource(path.segments[0].timeStart, i, false, includeNonLiveChildren) < 0) {
						return path.segments[0].timeStart;
					}
				}
			}
		}
		return -1;
	}

	/// <summary>
	/// returns whether player's units will never unpredictably move or change
	/// </summary>
	public bool calcImmutable() {
		// check that player isn't an active participant and isn't controlled by anyone
		if (isUser || user >= Sim.CompUser) return false;
		// check that no one can attack this player
		foreach (Player player2 in g.players) {
			if (player2.mayAttack[id]) return false;
		}
		return true;
	}
}
