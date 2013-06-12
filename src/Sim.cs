// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using SlimDX;

namespace Decoherence
{
    /// <summary>
    /// top-level game simulation class (instances of this are often named "g")
    /// </summary>
    public class Sim
    {
        // constants
        public const int OffMap = -10000; // don't set to int.MinValue so doesn't overflow in inVis()
        public const short HumanUser = 1; // change this if multiplayer is supported
        public const short CompUser = 0;

        // game objects
        public class Player
        {
            // stored in scenario files
            public string name;
            public bool isUser; // whether actively participates in the game
            public short user; // -1 = nobody, 0 = computer, 1+ = human
            public long[] startRsc; // resources at beginning of game
            public bool[] mayAttack; // if this player's units may attack each other player's units
            // not stored in scenario files
            public bool immutable; // whether player's units will never unpredictably move or change
            public bool hasNonLiveUnits; // whether currently might have time traveling units (ok to sometimes incorrectly be set to true)
            public long timeGoLiveFail; // latest time that player's time traveling units failed to go live (resets to long.MaxValue after success)
            public long timeNegRsc; // time that player could have negative resources if time traveling units went live

            public Player()
            {
                hasNonLiveUnits = false;
                timeGoLiveFail = long.MaxValue;
            }
        }

        public class UnitType
        {
            public string name;
            public string imgPath;
            /*public string sndSelect;
            public string sndMove;
            public string sndAttack;
            public string sndNoHealth;*/
            public int maxHealth;
            public long speed; // in position units per millisecond
            public long reload; // time needed to reload
            public long range; // range of attack
            public long tightFormationSpacing;
            public long makeUnitMinDist; // minimum distance that units that this unit type makes should move away
            public long makeUnitMaxDist; // maximum distance that units that this unit type makes should move away
            public double selRadius;
            public int makeOnUnitT; // unit type that this unit type should be made on top of
            public int[] damage; // damage done per attack to each unit type
            public bool[] canMake; // whether can make each unit type
            public long[] rscCost; // cost to make unit (may not be negative)
            public long[] rscCollectRate; // resources collected per each millisecond that unit exists (may not be negative)
        }

        public class Tile
        {
            private Sim g;
            /// <summary>
            /// stores times when each unit started or stopped seeing this tile,
            /// in format unitVis[unit][gain/lose visibility index]
            /// </summary>
            public Dictionary<int, List<long>> unitVis;
            /// <summary>
            /// stores times when each player started or stopped seeing this tile,
            /// in format playerVis[player][gain/lose visibility index]
            /// </summary>
            public List<long>[] playerVis;
            /// <summary>
            /// stores times when each player started or stopped knowing that no other player can see this tile,
            /// in format coherence[player][gain/lose coherence index]
            /// </summary>
            /// <remarks>
            /// This isn't the actual definition of coherence, but this is an important concept in the game and I need a name for it.
            /// The real meaning of coherence is described at http://arstechnica.com/technopaedia/2008/03/coherence
            /// </remarks>
            public List<long>[] coherence;

            public Tile(Sim simVal)
            {
                g = simVal;
                unitVis = new Dictionary<int,List<long>>();
                playerVis = new List<long>[g.nPlayers];
                coherence = new List<long>[g.nPlayers];
                for (int i = 0; i < g.nPlayers; i++)
                {
                    playerVis[i] = new List<long>();
                    coherence[i] = new List<long>();
                }
            }

            /// <summary>
            /// toggles the visibility of this tile for specified unit at specified time, without affecting player visibility
            /// (should only be called by TileMoveEvt.visAdd() or TileMoveEvt.visRemove())
            /// </summary>
            public void unitVisToggle(int unit, long time)
            {
                if (!unitVis.ContainsKey(unit)) unitVis.Add(unit, new List<long>());
                unitVis[unit].Add(time);
            }

            /// <summary>
            /// returns if specified unit can see this tile at latest possible time
            /// </summary>
            public bool unitVisLatest(int unit)
            {
                return unitVis.ContainsKey(unit) && visLatest(unitVis[unit]);
            }

            /// <summary>
            /// returns if specified unit can see this tile at specified time
            /// </summary>
            public bool unitVisWhen(int unit, long time)
            {
                return unitVis.ContainsKey(unit) && visWhen(unitVis[unit], time);
            }

            /// <summary>
            /// returns if this tile is in the direct line of sight of a unit of specified player at latest possible time
            /// </summary>
            public bool playerDirectVisLatest(int player)
            {
                foreach (int i in unitVis.Keys)
                {
                    if (player == g.u[i].player && visLatest(unitVis[i])) return true;
                }
                return false;
            }

            /// <summary>
            /// returns if this tile is in the direct line of sight of a unit of specified player at specified time
            /// </summary>
            public bool playerDirectVisWhen(int player, long time)
            {
                foreach (int i in unitVis.Keys)
                {
                    if (player == g.u[i].player && visWhen(unitVis[i], time)) return true;
                }
                return false;
            }

            /// <summary>
            /// returns if this tile is either in the direct line of sight for specified player at latest possible time,
            /// or if player can infer that other players' units aren't in specified tile at latest time
            /// </summary>
            public bool playerVisLatest(int player)
            {
                return visLatest(playerVis[player]);
            }

            /// <summary>
            /// returns playerVis gain/lose visibility index associated with specified time for specified player
            /// </summary>
            public int playerVisIndexWhen(int player, long time)
            {
                return visIndexWhen(playerVis[player], time);
            }

            /// <summary>
            /// returns if this tile is either in the direct line of sight for specified player at specified time,
            /// or if player can infer that other players' units aren't in specified tile at specified time
            /// </summary>
            public bool playerVisWhen(int player, long time)
            {
                return visWhen(playerVis[player], time);
            }

            /// <summary>
            /// returns if specified player can infer that no other player can see this tile at latest possible time
            /// </summary>
            public bool coherentLatest(int player)
            {
                return visLatest(coherence[player]);
            }

            /// <summary>
            /// returns coherence gain/lose visibility index associated with specified time for specified player
            /// </summary>
            public int coherentIndexWhen(int player, long time)
            {
                return visIndexWhen(coherence[player], time);
            }

            /// <summary>
            /// returns if specified player can infer that no other player can see this tile at specified time
            /// </summary>
            public bool coherentWhen(int player, long time)
            {
                return visWhen(coherence[player], time);
            }

            /// <summary>
            /// returns whether specified list indicates that the tile is visible at the latest possible time
            /// </summary>
            /// <remarks>
            /// The indices of the list are assumed to alternate between gaining visibility and losing visibility,
            /// where an empty list means not visible. So if there is an odd number of items in the list, the tile is visible.
            /// </remarks>
            private static bool visLatest(List<long> vis)
            {
                return vis.Count % 2 == 1;
            }

            /// <summary>
            /// returns index of specified list whose associated time is when the tile gained or lost visibility before the specified time
            /// </summary>
            /// <param name="vis">list of times in ascending order</param>
            private static int visIndexWhen(List<long> vis, long time)
            {
                int i;
                for (i = vis.Count - 1; i >= 0; i--)
                {
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
            private static bool visWhen(List<long> vis, long time)
            {
                return visIndexWhen(vis, time) % 2 == 0;
            }
        }

        // game variables
        public long mapSize;
        public long updateInterval;
        public long visRadius;
        public long camSpeed;
        public FP.Vector camPos;
        public float drawScl;
        public float drawSclMin;
        public float drawSclMax;
        public Vector2 healthBarSize;
        public float healthBarYOffset;
        public Color4 backCol;
        public Color4 borderCol;
        public Color4 noVisCol;
        public Color4 playerVisCol;
        public Color4 unitVisCol;
        public Color4 coherentCol;
        public Color4 pathCol;
        public Color4 healthBarBackCol;
        public Color4 healthBarFullCol;
        public Color4 healthBarEmptyCol;
        //public string music;
        public int nRsc;
        public int nPlayers;
        public int nUnitT;
        public int nUnits;
        public string[] rscNames;
        public Player[] players;
        public UnitType[] unitT;
        public Unit[] u;

        // helper variables not loaded from scenario file
        public Tile[,] tiles; // each tile is 1 fixed-point unit (2^FP.Precision raw integer units) wide, so bit shift by FP.Precision to convert between position and tile position
        public int[] tileVisBorder; // # of tiles units can see to each side, x units along an axis
        public SimEvtList events; // simulation events to be applied
        public SimEvtList cmdHistory; // user commands that have already been applied
        public List<int> movedUnits; // indices of units that moved in the latest simulation event, invalidating later TileMoveEvts for that unit
        public List<int> unitIdChgs; // list of units that changed indices (old index followed by new index)
        public long maxSpeed; // speed of fastest unit (is max speed that players can gain or lose visibility)
        public long timeSim; // current simulation time
        public long timeUpdateEvt; // last time that an UpdateEvt was applied

        /// <summary>
        /// intelligently resize unit array to specified size
        /// </summary>
        public void setNUnits(int newSize)
        {
            nUnits = newSize;
            if (u == null || nUnits > u.Length)
                Array.Resize(ref u, nUnits * 2);
        }

        /// <summary>
        /// master update method which updates the live game simulation to the specified time
        /// </summary>
        /// <remarks>this doesn't update time traveling units, must call updatePast() separately for each player</remarks>
        public void update(long curTime)
        {
            SimEvt evt;
            long timeSimNext = Math.Max(curTime, timeSim);
            int i;
            // apply simulation events
            movedUnits = new List<int>();
            while (events.peekTime() <= timeSimNext)
            {
                evt = events.pop();
                timeSim = evt.time;
                evt.apply(this);
                // if event caused unit(s) to move, delete and recalculate later events moving them between tiles
                if (movedUnits.Count > 0)
                {
                    for (i = 0; i < events.events.Count; i++)
                    {
                        if (events.events[i] is TileMoveEvt && events.events[i].time > timeSim && movedUnits.Contains(((TileMoveEvt)events.events[i]).unit))
                        {
                            events.events.RemoveAt(i);
                            i--;
                        }
                    }
                    foreach (int unit in movedUnits)
                    {
                        if (u[unit].timeSimPast == long.MaxValue) u[unit].addTileMoveEvts(ref events, timeSim, timeUpdateEvt + updateInterval);
                    }
                    movedUnits.Clear();
                }
            }
            // update simulation time
            timeSim = timeSimNext;
        }

        /// <summary>
        /// update specified player's non-live (time traveling) units
        /// </summary>
        public void updatePast(int player, long curTime)
        {
            if (players[player].hasNonLiveUnits)
            {
                for (int i = 0; i < nUnits; i++)
                {
                    if (u[i].player == player) u[i].updatePast(curTime);
                }
                if (curTime >= timeSim && (players[player].timeGoLiveFail == long.MaxValue || timeSim >= players[player].timeGoLiveFail + updateInterval))
                {
                    events.add(new GoLiveCmdEvt(timeSim, player));
                }
            }
        }

        /// <summary>
        /// makes specified tile not visible to specified player starting at specified time, including effects on surrounding tiles
        /// </summary>
        public void playerVisRemove(int player, int tileX, int tileY, long time)
        {
            // try adding tile to existing PlayerVisRemoveEvt with same player and time
            foreach (SimEvt evt in events.events)
            {
                if (evt is PlayerVisRemoveEvt)
                {
                    PlayerVisRemoveEvt visEvt = (PlayerVisRemoveEvt)evt;
                    if (player == visEvt.player && time == visEvt.time)
                    {
                        // check that tile pos isn't a duplicate (recently added tiles are more likely to be duplicates)
                        for (int i = visEvt.nTiles - 1; i >= Math.Max(0, visEvt.nTiles - 20); i--)
                        {
                            if (tileX == visEvt.tiles[i].X && tileY == visEvt.tiles[i].Y) return;
                        }
                        // ok to add tile to existing event
                        visEvt.nTiles++;
                        if (visEvt.nTiles > visEvt.tiles.Length)
                            Array.Resize(ref visEvt.tiles, visEvt.nTiles * 2);
                        visEvt.tiles[visEvt.nTiles - 1] = new Point(tileX, tileY);
                        return;
                    }
                }
            }
            // if no such PlayerVisRemoveEvt exists, add a new one
            events.add(new PlayerVisRemoveEvt(time, player, tileX, tileY));
        }

        /// <summary>
        /// makes specified tile "coherent" for specified player starting at specified time,
        /// including how that affects units on that tile
        /// </summary>
        public void coherenceAdd(int player, int tileX, int tileY, long time)
        {
            if (tiles[tileX, tileY].coherentLatest(player)) throw new InvalidOperationException("tile (" + tileX + ", " + tileY + ") is already coherent");
            tiles[tileX, tileY].coherence[player].Add(time);
            // this player's units that are on this tile may time travel starting now
            // TODO: actually safe to time travel at earlier times, as long as unit of same type is at same place when decoheres
            for (int i = 0; i < nUnits; i++)
            {
                if (player == u[i].player && tileX == u[i].tileX && tileY == u[i].tileY && !u[i].coherent())
                {
                    u[i].cohere(time);
                }
            }
        }

        /// <summary>
        /// makes specified tile not "coherent" for specified player starting at specified time,
        /// including how that affects units on that tile
        /// </summary>
        public void coherenceRemove(int player, int tileX, int tileY, long time)
        {
            if (!tiles[tileX, tileY].coherentLatest(player)) throw new InvalidOperationException("tile (" + tileX + ", " + tileY + ") is already not coherent");
            tiles[tileX, tileY].coherence[player].Add(time);
            // this player's units that are on this tile may not time travel starting now
            for (int i = 0; i < nUnits; i++)
            {
                if (player == u[i].player && tileX == u[i].tileX && tileY == u[i].tileY && u[i].coherent())
                {
                    u[i].decohere();
                }
            }
        }

        /// <summary>
        /// calculates from player visibility tiles if specified player can infer that no other player can see specified tile at latest possible time
        /// </summary>
        /// <remarks>
        /// The worst-case scenario would then be that every tile that this player can't see contains another player's unit
        /// of the type with the greatest visibility radius (though all units in this game have the same visibility radius).
        /// If no other player could see the specified tile in this worst case scenario,
        /// the player can infer that he/she is the only player that can see this tile.
        /// </remarks>
        public bool calcCoherent(int player, int tileX, int tileY, bool leftTileIsAccurate, bool aboveTileIsAccurate)
        {
            int i, tX, tY;
            // check that this player can see all nearby tiles
            // if an adjacent tile is known to be coherent, only need to check "new" tiles on the other side
            if (leftTileIsAccurate && (tileX <= 0 || tiles[tileX - 1, tileY].coherentLatest(player)))
            {
                if (aboveTileIsAccurate && (tileY <= 0 || tiles[tileX, tileY - 1].coherentLatest(player)))
                {
                    for (tX = tileX; tX <= Math.Min(tileLen() - 1, tileX + tileVisRadius()); tX++)
                    {
                        tY = tileY + tileVisBorder[tX - tileX];
                        if (tY < tileLen() && !tiles[tX, tY].playerVisLatest(player)) return false;
                    }
                }
                else
                {
                    for (tY = Math.Max(0, tileY - tileVisRadius()); tY <= Math.Min(tileLen() - 1, tileY + tileVisRadius()); tY++)
                    {
                        tX = tileX + tileVisBorder[Math.Abs(tY - tileY)];
                        if (tX < tileLen() && !tiles[tX, tY].playerVisLatest(player)) return false;
                    }
                }
            }
            else if (aboveTileIsAccurate && (tileY <= 0 || tiles[tileX, tileY - 1].coherentLatest(player)))
            {
                for (tX = Math.Max(0, tileX - tileVisRadius()); tX <= Math.Min(tileLen() - 1, tileX + tileVisRadius()); tX++)
                {
                    tY = tileY + tileVisBorder[Math.Abs(tX - tileX)];
                    if (tY < tileLen() && !tiles[tX, tY].playerVisLatest(player)) return false;
                }
            }
            else
            {
                for (tX = Math.Max(0, tileX - tileVisRadius()); tX <= Math.Min(tileLen() - 1, tileX + tileVisRadius()); tX++)
                {
                    for (tY = Math.Max(0, tileY - tileVisRadius()); tY <= Math.Min(tileLen() - 1, tileY + tileVisRadius()); tY++)
                    {
                        if (inVis(tX - tileX, tY - tileY) && !tiles[tX, tY].playerVisLatest(player)) return false;
                    }
                }
            }
            // check that no other players can see this tile
            for (i = 0; i < nPlayers; i++)
            {
                if (i != player && !players[i].immutable && tiles[tileX, tileY].playerVisLatest(i)) return false;
            }
            return true;
        }

        /// <summary>
        /// returns amount of specified resource that specified player has at specified time
        /// </summary>
        /// <param name="max">
        /// since different paths can have collected different resource amounts,
        /// determines whether to use paths that collected least or most resources in calculation
        /// </param>
        public long playerResource(int player, long time, int rscType, bool max, bool includeNonLiveChildren, bool alwaysUseReplacementPaths)
        {
            long ret = players[player].startRsc[rscType];
            for (int i = 0; i < nUnits; i++)
            {
                if (u[i].player == player && u[i].parent < 0) ret += u[i].rscCollected(time, rscType, max, includeNonLiveChildren, alwaysUseReplacementPaths);
            }
            return ret;
        }

        /// <summary>
        /// checks whether specified player could have negative resources since timeMin in worst case decoherence scenario
        /// </summary>
        /// <returns>a time that player could have negative resources, or -1 if no such time found</returns>
        public long playerCheckNegRsc(int player, long timeMin, bool includeNonLiveChildren, bool alwaysUseReplacementPaths)
        {
            int i, j;
            for (i = 0; i < nUnits; i++)
            {
                // check all times since timeMin that a unit of specified player was made
                // note that new units are made at timeCmd + 1
                if (player == u[i].player && u[i].m[0].timeStart >= timeMin && u[i].m[0].timeStart <= timeSim + 1)
                {
                    for (j = 0; j < nRsc; j++)
                    {
                        if (playerResource(player, u[i].m[0].timeStart, j, false, includeNonLiveChildren, alwaysUseReplacementPaths) < 0)
                        {
                            return u[i].m[0].timeStart;
                        }
                    }
                }
            }
            return -1;
        }

        /// <summary>
        /// returns whether specified player's units will never unpredictably move or change
        /// </summary>
        public bool calcPlayerImmutable(int player)
        {
            // check that player isn't an active participant and isn't controlled by anyone
            if (players[player].isUser || players[player].user >= 0) return false;
            // check that no one can attack this player
            for (int i = 0; i < nPlayers; i++)
            {
                if (players[i].mayAttack[player]) return false;
            }
            return true;
        }

        /// <summary>
        /// returns if a hypothetical unit at the origin could see tile with specified (positive or negative) x and y indices
        /// </summary>
        public bool inVis(long tX, long tY)
        {
            //return Math.Max(Math.Abs(tX), Math.Abs(tY)) <= (int)(g.visRadius >> FP.Precision);
            return new FP.Vector(tX << FP.Precision, tY << FP.Precision).lengthSq() <= visRadius * visRadius;
        }

        public int tileVisRadius()
        {
            return (int)(visRadius >> FP.Precision); // adding "+ 1" to this actually doesn't make a difference
        }

        public Tile tileAt(FP.Vector pos)
        {
            return tiles[pos.x >> FP.Precision, pos.y >> FP.Precision];
        }

        public int tileLen() // TODO: use unitVis.GetUpperBound instead of this function
        {
            return (int)((mapSize >> FP.Precision) + 1);
        }

        /// <summary>
        /// returns index of resource with specified name, or -1 if no such resource
        /// </summary>
        public int resourceNamed(string name)
        {
            for (int i = 0; i < nRsc; i++)
            {
                if (name == rscNames[i]) return i;
            }
            return -1;
        }

        /// <summary>
        /// returns index of player with specified name, or -1 if no such player
        /// </summary>
        public int playerNamed(string name)
        {
            for (int i = 0; i < nPlayers; i++)
            {
                if (name == players[i].name) return i;
            }
            return -1;
        }

        /// <summary>
        /// returns index of unit type with specified name, or -1 if no such unit type
        /// </summary>
        public int unitTypeNamed(string name)
        {
            for (int i = 0; i < nUnitT; i++)
            {
                if (name == unitT[i].name) return i;
            }
            return -1;
        }
    }
}
