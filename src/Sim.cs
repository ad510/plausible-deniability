// game simulation
// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SlimDX;

namespace Decoherence
{
    public class Sim
    {
        // constants
        public const int OffMap = -10000; // don't set to int.MinValue so doesn't overflow in inVis()

        // game objects
        public class Player
        {
            public string name;
            public bool isUser; // whether actively controlled by either a human or AI
            public short user; // -1 = nobody, 0 = computer, 1+ = human
            public bool[] mayAttack; // if this player's units may attack each other player's units
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
            public long speed;
            public int[] damage; // damage done per attack to each unit type
            public long reload; // time needed to reload
            public long range; // range of attack
            public long tightFormationSpacing;
            public double selRadius;
        }

        public class Tile
        {
            private Sim g;
            public Dictionary<int, List<long>> unitVis;
            public List<long>[] playerVis;
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

            public void unitVisToggle(int unit, long time)
            {
                if (!unitVis.ContainsKey(unit)) unitVis.Add(unit, new List<long>());
                unitVis[unit].Add(time);
            }

            public bool unitVisLatest(int unit)
            {
                return unitVis.ContainsKey(unit) && visLatest(unitVis[unit]);
            }

            public bool unitVisWhen(int unit, long time)
            {
                return unitVis.ContainsKey(unit) && visWhen(unitVis[unit], time);
            }

            /// <summary>
            /// returns if the specified tile is in the direct line of sight of a unit of specified player
            /// </summary>
            public bool playerDirectVisLatest(int player)
            {
                foreach (int i in unitVis.Keys)
                {
                    if (player == g.u[i].player && visLatest(unitVis[i])) return true;
                }
                return false;
            }

            public bool playerDirectVisWhen(int player, long time)
            {
                foreach (int i in unitVis.Keys)
                {
                    if (player == g.u[i].player && visWhen(unitVis[i], time)) return true;
                }
                return false;
            }

            /// <summary>
            /// returns if the specified tile is either in the direct line of sight for specified player at latest time,
            /// or if player can infer that other players' units aren't in specified tile at latest time
            /// </summary>
            public bool playerVisLatest(int player)
            {
                return visLatest(playerVis[player]);
            }

            public int playerVisIndexWhen(int player, long time)
            {
                return visIndexWhen(playerVis[player], time);
            }

            /// <summary>
            /// returns if the specified tile is either in the direct line of sight for specified player at specified time,
            /// or if player can infer that other players' units aren't in specified tile at specified time
            /// </summary>
            public bool playerVisWhen(int player, long time)
            {
                return visWhen(playerVis[player], time);
            }

            public bool coherentLatest(int player)
            {
                return visLatest(coherence[player]);
            }

            public int coherentIndexWhen(int player, long time)
            {
                return visIndexWhen(coherence[player], time);
            }

            /// <summary>
            /// returns if it is impossible for other players' units to see this location
            /// </summary>
            /// <remarks>this isn't the actual definition of coherence, but this is an important concept in the game and I need a name for it</remarks>
            public bool coherentWhen(int player, long time)
            {
                return visWhen(coherence[player], time);
            }

            private static bool visLatest(List<long> vis)
            {
                return vis.Count % 2 == 1;
            }

            private static int visIndexWhen(List<long> vis, long time)
            {
                int i;
                for (i = vis.Count - 1; i >= 0; i--)
                {
                    if (time >= vis[i]) break;
                }
                return i;
            }

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
        public int nPlayers;
        public int nUnitT;
        public int nUnits;
        public Player[] players;
        public UnitType[] unitT;
        public Unit[] u;

        // helper variables
        public Tile[,] tiles;
        public SimEvtList events;
        public SimEvtList cmdHistory;
        public List<int> movedUnits; // indices of units that moved in the latest simulation event, invalidating later TileMoveEvts for that unit
        public List<int> unitIdChgs; // list of units that changed indices (old index followed by new index)
        public long maxSpeed; // speed of fastest unit (is max speed that players can gain or lose visibility)
        public long timeSim; // current simulation time

        public void setNUnits(int newSize)
        {
            nUnits = newSize;
            if (u == null || nUnits > u.Length)
                Array.Resize(ref u, nUnits * 2);
        }

        public void update(long curTime)
        {
            SimEvt evt;
            long timeSimNext = Math.Max(curTime, timeSim);
            int i;
            // check if units moved between tiles
            for (i = 0; i < nUnits; i++)
            {
                u[i].addTileMoveEvts(ref events, timeSim, timeSimNext);
            }
            // apply simulation events
            movedUnits = new List<int>();
            while (events.peekTime() <= timeSimNext)
            {
                evt = events.pop();
                timeSim = evt.time;
                evt.apply(this);
                // if event caused unit(s) to move, delete and recalculate later events moving them between tiles
                // (could this cause syncing problems due to events with the same time being applied in a different order on different computers?)
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
                        u[unit].addTileMoveEvts(ref events, timeSim, timeSimNext);
                    }
                    movedUnits.Clear();
                }
            }
            // update simulation time
            timeSim = timeSimNext;
        }

        public void visAdd(int unit, int tileX, int tileY, long time)
        {
            int i, tX, tY;
            if (tileX >= 0 && tileX < tileLen() && tileY >= 0 && tileY < tileLen())
            {
                if (tiles[tileX, tileY].unitVisLatest(unit)) throw new InvalidOperationException("unit " + unit + " already sees tile (" + tileX + ", " + tileY + ")");
                // add unit to unit visibility tile
                tiles[tileX, tileY].unitVisToggle(unit, time);
                // TODO: use smarter playerVis adding algorithm
                // also, if opponent units that can't make anything enter then exit region previously indirectly visible, should use smarter playerVis adding algorithm where last one exited
                if (!tiles[tileX, tileY].playerVisLatest(u[unit].player))
                {
                    tiles[tileX, tileY].playerVis[u[unit].player].Add(time);
                    // check if a tile cohered for this player, or decohered for another player
                    for (i = 0; i < nPlayers; i++)
                    {
                        for (tX = Math.Max(0, tileX - tileVisRadius()); tX <= Math.Min(tileLen() - 1, tileX + tileVisRadius()); tX++)
                        {
                            for (tY = Math.Max(0, tileY - tileVisRadius()); tY <= Math.Min(tileLen() - 1, tileY + tileVisRadius()); tY++)
                            {
                                if (i == u[unit].player && !tiles[tX, tY].coherentLatest(i) && calcCoherent(i, tX, tY, time))
                                {
                                    coherenceAdd(i, tX, tY, time);
                                }
                                else if (i != u[unit].player && tiles[tX, tY].coherentLatest(i) && !calcCoherent(i, tX, tY, time))
                                {
                                    coherenceRemove(i, tX, tY, time);
                                }
                            }
                        }
                    }
                }
            }
        }

        public void visRemove(int unit, int tileX, int tileY, long time)
        {
            int tX, tY;
            long timePlayerVis = long.MaxValue;
            if (tileX >= 0 && tileX < tileLen() && tileY >= 0 && tileY < tileLen())
            {
                if (!tiles[tileX, tileY].unitVisLatest(unit)) throw new InvalidOperationException("unit " + unit + " already doesn't see tile (" + tileX + ", " + tileY + ")");
                // remove unit from unit visibility tile
                tiles[tileX, tileY].unitVisToggle(unit, time);
                // check if player can't directly see this tile anymore
                if (tiles[tileX, tileY].playerVisLatest(u[unit].player) && !tiles[tileX, tileY].playerDirectVisLatest(u[unit].player))
                {
                    // find lowest time that surrounding tiles lost visibility
                    for (tX = Math.Max(0, tileX - 1); tX <= Math.Min(tileLen() - 1, tileX + 1); tX++)
                    {
                        for (tY = Math.Max(0, tileY - 1); tY <= Math.Min(tileLen() - 1, tileY + 1); tY++)
                        {
                            if ((tX != tileX || tY != tileY) && !tiles[tX, tY].playerVisLatest(u[unit].player))
                            {
                                if (tiles[tX, tY].playerVis[u[unit].player].Count == 0)
                                {
                                    timePlayerVis = long.MinValue;
                                }
                                else if (tiles[tX, tY].playerVis[u[unit].player][tiles[tX, tY].playerVis[u[unit].player].Count - 1] < timePlayerVis)
                                {
                                    timePlayerVis = tiles[tX, tY].playerVis[u[unit].player][tiles[tX, tY].playerVis[u[unit].player].Count - 1];
                                }
                            }
                        }
                    }
                    // if player can't see all neighboring tiles, they won't be able to tell if another player's unit moves into this tile
                    // so remove this tile's visibility for this player
                    if (timePlayerVis != long.MaxValue)
                    {
                        timePlayerVis = Math.Max(time, timePlayerVis + (1 << FP.Precision) / maxSpeed); // TODO: use more accurate time
                        events.add(new PlayerVisRemoveEvt(timePlayerVis, u[unit].player, tileX, tileY));
                    }
                }
            }
        }

        public void coherenceAdd(int player, int tX, int tY, long time)
        {
            if (tiles[tX, tY].coherentLatest(player)) throw new InvalidOperationException("tile (" + tX + ", " + tY + ") is already coherent");
            tiles[tX, tY].coherence[player].Add(time);
            // this player's units that are on this tile may time travel starting now
            // TODO: actually safe to time travel at earlier times, as long as unit of same type is at same place when decoheres
            for (int i = 0; i < nUnits; i++)
            {
                if (player == u[i].player && tX == u[i].tileX && tY == u[i].tileY && !u[i].coherent)
                {
                    u[i].cohere(time);
                }
            }
        }

        public void coherenceRemove(int player, int tX, int tY, long time)
        {
            if (!tiles[tX, tY].coherentLatest(player)) throw new InvalidOperationException("tile (" + tX + ", " + tY + ") is already not coherent");
            tiles[tX, tY].coherence[player].Add(time);
            // this player's units that are on this tile may not time travel starting now
            for (int i = 0; i < nUnits; i++)
            {
                if (player == u[i].player && tX == u[i].tileX && tY == u[i].tileY && u[i].coherent)
                {
                    u[i].decohere(time);
                }
            }
        }

        /// <summary>
        /// calculates from player visibility tiles if it is impossible for other players' units to see this location
        /// </summary>
        public bool calcCoherent(int player, int tileX, int tileY, long time)
        {
            int i, tX, tY;
            // check that this player can see all nearby tiles
            for (tX = Math.Max(0, tileX - tileVisRadius()); tX <= Math.Min(tileLen() - 1, tileX + tileVisRadius()); tX++)
            {
                for (tY = Math.Max(0, tileY - tileVisRadius()); tY <= Math.Min(tileLen() - 1, tileY + tileVisRadius()); tY++)
                {
                    if (inVis(tX - tileX, tY - tileY) && !tiles[tX, tY].playerVisWhen(player, time)) return false;
                }
            }
            // check that no other players can see this tile
            for (i = 0; i < nPlayers; i++)
            {
                if (i != player && tiles[tileX, tileY].playerVisWhen(i, time)) return false;
            }
            return true;
        }

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

        public static long lineCalcX(FP.Vector p1, FP.Vector p2, long y)
        {
            return FP.mul(y - p1.y, FP.div(p2.x - p1.x, p2.y - p1.y)) + p1.x;
        }

        public static long lineCalcY(FP.Vector p1, FP.Vector p2, long x)
        {
            return FP.mul(x - p1.x, FP.div(p2.y - p1.y, p2.x - p1.x)) + p1.y; // easily derived from point-slope form
        }
    }
}
