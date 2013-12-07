// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class Player {
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
}
