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
    class Sim
    {
        // game structures
        public struct Player
        {
            public string name;
            public bool isUser; // whether actively controlled by either a human or AI
            public short user; // -1 = nobody, 0 = computer, 1+ = human
            public bool[] annihilates; // if units of this player annihilate units from each player
        }

        public struct UnitType
        {
            public string name;
            public string imgPath;
            /*public string sndSelect;
            public string sndMove;
            public string sndAnniCmd;
            public string sndAnnihilate;*/
            public long speed;
            public double selRadius;
        }

        public struct Scenario
        {
            public long mapSize;
            public long camSpeed;
            public FP.Vector camPos;
            public float drawScl;
            public float drawSclMin;
            public float drawSclMax;
            public Color4 backCol;
            public Color4 borderCol;
            public Color4 noVisCol;
            public Color4 playerVisCol;
            public Color4 unitVisCol;
            public Color4 coherentCol;
            //public string music;
            public long visRadius;
            public int nPlayers;
            public int nUnitT;
            public Player[] players;
            public UnitType[] unitT;
        }

        public struct UnitMove // unit movement (linearly interpolated between 2 points)
        {
            public long timeStart; // time when starts moving
            public long timeEnd; // time when finishes moving
            public FP.Vector vecStart; // z indicates rotation
            public FP.Vector vecEnd;

            public UnitMove(long timeStartVal, long timeEndVal, FP.Vector vecStartVal, FP.Vector vecEndVal)
            {
                timeStart = timeStartVal;
                timeEnd = timeEndVal;
                vecStart = vecStartVal;
                vecEnd = vecEndVal;
            }

            public UnitMove(long timeVal, FP.Vector vecVal)
                : this(timeVal, timeVal + 1, vecVal, vecVal)
            {
            }

            public static UnitMove fromSpeed(long timeStartVal, long speed, FP.Vector vecStartVal, FP.Vector vecEndVal)
            {
                return new UnitMove(timeStartVal, timeStartVal + new FP.Vector(vecEndVal - vecStartVal).length() / speed, vecStartVal, vecEndVal);
            }

            public FP.Vector calcPos(long time) // returns location at specified time
            {
                if (time >= timeEnd) return vecEnd;
                return vecStart + (vecEnd - vecStart) * FP.div(time - timeStart, timeEnd - timeStart);
            }

            public long timeAtX(long x)
            {
                return lineCalcX(new FP.Vector(timeStart, vecStart.x), new FP.Vector(timeEnd, vecEnd.x), x);
            }

            public long timeAtY(long y)
            {
                return lineCalcX(new FP.Vector(timeStart, vecStart.y), new FP.Vector(timeEnd, vecEnd.y), y);
            }
        }

        public struct Unit
        {
            public int type;
            public int player;
            public long timeCohere; // earliest time at which it's safe to time travel
            public long timeEnd; // time annihilated
            public int n; // number of moves
            public UnitMove[] m;
            public int mLive; // index of latest move that was live
            //public FP.Vector pos; // current position
            public int tileX, tileY; // current position on visibility tiles
            public bool coherent; // whether safe to time travel at simulation time

            public Unit(int typeVal, int playerVal, long startTime, FP.Vector startPos)
            {
                type = typeVal;
                player = playerVal;
                timeCohere = long.MaxValue;
                timeEnd = long.MaxValue;
                n = 1;
                m = new UnitMove[n];
                m[0] = new UnitMove(startTime, startPos);
                mLive = 0;
                //pos = startPos;
                tileX = -10000; // don't set to int.MinValue so doesn't overflow in inVis()
                tileY = -10000;
                coherent = false;
            }

            public void setN(int newSize)
            {
                int i = 0;
                for (i = n; i < Math.Min(newSize, m.Length); i++)
                {
                    m[i] = new UnitMove();
                }
                n = newSize;
                if (n > m.Length)
                    Array.Resize(ref m, n * 2);
            }

            public void addMove(UnitMove newMove)
            {
                setN(n + 1);
                m[n - 1] = newMove;
                if (newMove.timeStart >= timeSim) mLive = n - 1;
            }

            public FP.Vector calcPos(long time)
            {
                return m[getMove(time)].calcPos(time);
            }

            public int getMove(long time)
            {
                int ret = n - 1;
                while (ret >= 0 && time < m[ret].timeStart) ret--;
                return ret;
            }

            public void addMoveEvts(ref SimEvtList events, int id, long timeMin, long timeMax)
            {
                int move, moveLast;
                FP.Vector pos, posLast;
                int i, tX, tY, dir;
                if (timeMax < m[0].timeStart) return;
                moveLast = getMove(timeMin);
                move = getMove(timeMax);
                if (moveLast < 0)
                {
                    // put unit on visibility tiles for the first time
                    events.add(new MoveEvt(m[0].timeStart, id, (int)(m[0].vecStart.x >> FP.Precision), (int)(m[0].vecStart.y >> FP.Precision)));
                    moveLast = 0;
                }
                for (i = moveLast; i <= move; i++)
                {
                    posLast = (i == moveLast) ? m[i].calcPos(Math.Max(timeMin, m[0].timeStart)) : m[i].vecStart;
                    pos = (i == move) ? m[i].calcPos(timeMax) : m[i + 1].vecStart;
                    // moving between columns (x)
                    dir = (pos.x >= posLast.x) ? 0 : -1;
                    for (tX = (int)(Math.Min(pos.x, posLast.x) >> FP.Precision) + 1; tX <= (int)(Math.Max(pos.x, posLast.x) >> FP.Precision); tX++)
                    {
                        events.add(new MoveEvt(m[i].timeAtX(tX << FP.Precision), id, tX + dir, int.MinValue));
                    }
                    // moving between rows (y)
                    dir = (pos.y >= posLast.y) ? 0 : -1;
                    for (tY = (int)(Math.Min(pos.y, posLast.y) >> FP.Precision) + 1; tY <= (int)(Math.Max(pos.y, posLast.y) >> FP.Precision); tY++)
                    {
                        events.add(new MoveEvt(m[i].timeAtY(tY << FP.Precision), id, int.MinValue, tY + dir));
                    }
                }
            }
        }

        public class Tile
        {
            public Dictionary<int, List<long>> unitVis;
            public List<long>[] playerVis;
            public List<long>[] coherence;

            public Tile()
            {
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

            // returns if the specified tile is in the direct line of sight of a unit of specified player
            public bool playerDirectVisLatest(int player)
            {
                foreach (int i in unitVis.Keys)
                {
                    if (player == u[i].player && visLatest(unitVis[i])) return true;
                }
                return false;
            }

            public bool playerDirectVisWhen(int player, long time)
            {
                foreach (int i in unitVis.Keys)
                {
                    if (player == u[i].player && visWhen(unitVis[i], time)) return true;
                }
                return false;
            }

            // returns if the specified tile is either in the direct line of sight for specified player at latest time,
            // or if player can infer that other players' units aren't in specified tile at latest time
            public bool playerVisLatest(int player)
            {
                return visLatest(playerVis[player]);
            }

            // returns if the specified tile is either in the direct line of sight for specified player at specified time,
            // or if player can infer that other players' units aren't in specified tile at specified time
            public bool playerVisWhen(int player, long time)
            {
                return visWhen(playerVis[player], time);
            }

            public bool coherentLatest(int player)
            {
                return visLatest(coherence[player]);
            }

            // returns if it is impossible for other players' units to see this location
            // this isn't the actual definition of coherence, but this is an important concept in the game and I need a name for it
            public bool coherentWhen(int player, long time)
            {
                return visWhen(coherence[player], time);
            }

            private static bool visLatest(List<long> vis)
            {
                return vis.Count % 2 == 1;
            }

            private static bool visWhen(List<long> vis, long time)
            {
                for (int i = vis.Count - 1; i >= 0; i--)
                {
                    if (time >= vis[i]) return i % 2 == 0;
                }
                return false;
            }
        }

        // simulation events
        public abstract class SimEvt // base class for simulation events
        {
            public long time;

            public abstract void apply();
        }

        public class SimEvtList
        {
            private List<SimEvt> events;

            public SimEvtList()
            {
                events = new List<SimEvt>();
            }

            public void add(SimEvt evt)
            {
                int ins;
                for (ins = events.Count; ins >= 1 && evt.time < events[ins - 1].time; ins--) ;
                events.Insert(ins, evt);
            }

            public SimEvt pop()
            {
                if (events.Count == 0) return null;
                SimEvt ret = events[0];
                events.RemoveAt(0);
                return ret;
            }

            public long peekTime()
            {
                if (events.Count == 0) return long.MaxValue;
                return events[0].time;
            }
        }

        public class MoveEvt : SimEvt // event in which unit moves between visibility tiles
        {
            public int unit;
            public int tileX, tileY; // new tile position, set to int.MinValue to keep current value

            public MoveEvt(long timeVal, int unitVal, int tileXVal, int tileYVal)
            {
                time = timeVal;
                unit = unitVal;
                tileX = tileXVal;
                tileY = tileYVal;
            }

            public override void apply()
            {
                int i, tXPrev, tYPrev, tX, tY;
                if (tileX == int.MinValue) tileX = u[unit].tileX;
                if (tileY == int.MinValue) tileY = u[unit].tileY;
                tXPrev = u[unit].tileX;
                tYPrev = u[unit].tileY;
                u[unit].tileX = tileX;
                u[unit].tileY = tileY;
                // add unit to visibility tiles
                for (tX = tileX - tileVisRadius(); tX <= tileX + tileVisRadius(); tX++)
                {
                    for (tY = tileY - tileVisRadius(); tY <= tileY + tileVisRadius(); tY++)
                    {
                        if (!inVis(tX - tXPrev, tY - tYPrev) && inVis(tX - tileX, tY - tileY))
                        {
                            visAdd(unit, tX, tY, time);
                        }
                    }
                }
                // remove unit from visibility tiles
                for (tX = tXPrev - tileVisRadius(); tX <= tXPrev + tileVisRadius(); tX++)
                {
                    for (tY = tYPrev - tileVisRadius(); tY <= tYPrev + tileVisRadius(); tY++)
                    {
                        if (inVis(tX - tXPrev, tY - tYPrev) && !inVis(tX - tileX, tY - tileY))
                        {
                            visRemove(unit, tX, tY, time);
                        }
                    }
                }
                // update whether this unit may time travel
                if (tiles[tileX, tileY].coherentWhen(u[unit].player, time) != u[unit].coherent)
                {
                    u[unit].coherent = !u[unit].coherent;
                    u[unit].timeCohere = u[unit].coherent ? time : long.MaxValue;
                }
                if (tXPrev >= 0 && tXPrev < tileLen() && tYPrev >= 0 && tYPrev < tileLen()
                    && tileX >= 0 && tileX < tileLen() && tileY >= 0 && tileY < tileLen())
                {
                    // if this unit moved out of another player's visibility, remove that player's visibility here
                    for (i = 0; i < g.nPlayers; i++)
                    {
                        if (i != u[unit].player && tiles[tXPrev, tYPrev].playerDirectVisLatest(i) && !tiles[tileX, tileY].playerDirectVisLatest(i))
                        {
                            for (tX = Math.Max(0, tileX - 1); tX <= Math.Min(tileLen() - 1, tileX + 1); tX++)
                            {
                                for (tY = Math.Max(0, tileY - 1); tY <= Math.Min(tileLen() - 1, tileY + 1); tY++)
                                {
                                    // TODO: use more accurate time at tiles other than (tileX, tileY)
                                    events.add(new PlayerVisRemoveEvt(time, i, tX, tY));
                                }
                            }
                        }
                    }
                    // if this player can no longer directly see another player's unit, remove this player's visibility there
                    foreach (int i2 in tiles[tXPrev, tYPrev].unitVis.Keys)
                    {
                        if (u[i2].player != u[unit].player && inVis(u[i2].tileX - tXPrev, u[i2].tileY - tYPrev) && !tiles[u[i2].tileX, u[i2].tileY].playerDirectVisLatest(u[unit].player))
                        {
                            for (tX = Math.Max(0, u[i2].tileX - 1); tX <= Math.Min(tileLen() - 1, u[i2].tileX + 1); tX++)
                            {
                                for (tY = Math.Max(0, u[i2].tileY - 1); tY <= Math.Min(tileLen() - 1, u[i2].tileY + 1); tY++)
                                {
                                    // TODO: use more accurate time at tiles other than (p[i2].tileX, p[i2].tileY)
                                    events.add(new PlayerVisRemoveEvt(time, u[unit].player, tX, tY));
                                }
                            }
                        }
                    }
                }
            }
        }

        public class PlayerVisAddEvt : SimEvt // event in which a player starts seeing a tile
        {
            public int player;
            public int tileX, tileY;

            public PlayerVisAddEvt(long timeVal, int playerVal, int tileXVal, int tileYVal)
            {
                time = timeVal;
                player = playerVal;
                tileX = tileXVal;
                tileY = tileYVal;
            }

            public override void apply()
            {
                int i, tX, tY;
                // TODO: copy code from visAdd()
                // add events to add visibility to surrounding tiles (TODO: likely has bugs)
                for (tX = Math.Max(0, tileX - 1); tX <= Math.Min(tileLen() - 1, tileX + 1); tX++)
                {
                    for (tY = Math.Max(0, tileY - 1); tY <= Math.Min(tileLen() - 1, tileY + 1); tY++)
                    {
                        if ((tX != tileX || tY != tileY) && !tiles[tX, tY].playerVisLatest(player))
                        {
                            // TODO: use more accurate time
                            events.add(new PlayerVisAddEvt(time - (1 << FP.Precision) / maxSpeed, player, tX, tY));
                        }
                    }
                }
            }
        }

        public class PlayerVisRemoveEvt : SimEvt // event in which a player stops seeing a tile
        {
            public int player;
            public int tileX, tileY;

            public PlayerVisRemoveEvt(long timeVal, int playerVal, int tileXVal, int tileYVal)
            {
                time = timeVal;
                player = playerVal;
                tileX = tileXVal;
                tileY = tileYVal;
            }

            public override void apply()
            {
                int i, tX, tY;
                if (tiles[tileX, tileY].playerVisLatest(player) && !tiles[tileX, tileY].playerDirectVisLatest(player))
                {
                    tiles[tileX, tileY].playerVis[player].Add(time);
                    // check if a tile decohered for this player, or cohered for another player
                    for (i = 0; i < g.nPlayers; i++)
                    {
                        for (tX = Math.Max(0, tileX - tileVisRadius()); tX <= Math.Min(tileLen() - 1, tileX + tileVisRadius()); tX++)
                        {
                            for (tY = Math.Max(0, tileY - tileVisRadius()); tY <= Math.Min(tileLen() - 1, tileY + tileVisRadius()); tY++)
                            {
                                if (i == player && tiles[tX, tY].coherentLatest(i) && !calcCoherent(i, tX, tY, time))
                                {
                                    coherenceRemove(i, tX, tY, time);
                                }
                                else if (i != player && !tiles[tX, tY].coherentLatest(i) && calcCoherent(i, tX, tY, time))
                                {
                                    coherenceAdd(i, tX, tY, time);
                                }
                            }
                        }
                    }
                    // add events to remove visibility from surrounding tiles
                    for (tX = Math.Max(0, tileX - 1); tX <= Math.Min(tileLen() - 1, tileX + 1); tX++)
                    {
                        for (tY = Math.Max(0, tileY - 1); tY <= Math.Min(tileLen() - 1, tileY + 1); tY++)
                        {
                            if ((tX != tileX || tY != tileY) && tiles[tX, tY].playerVisLatest(player))
                            {
                                // TODO: use more accurate time
                                events.add(new PlayerVisRemoveEvt(time + (1 << FP.Precision) / maxSpeed, player, tX, tY));
                            }
                        }
                    }
                }
            }
        }

        // game variables
        public static Scenario g;
        public static int nUnits;
        public static Unit[] u;

        // helper variables
        public static Tile[,] tiles;
        public static SimEvtList events;
        public static long maxSpeed;
        public static long timeSim;
        public static long timeSimLast;

        public static void setNUnits(int newSize)
        {
            nUnits = newSize;
            if (nUnits > u.Length)
                Array.Resize(ref u, nUnits * 2);
        }

        public static void update(long curTime)
        {
            FP.Vector pos;
            int i;
            // do timing
            if (curTime <= timeSim)
            {
                updatePast(curTime);
                return;
            }
            timeSimLast = timeSim;
            timeSim = curTime;
            // tiles visible at previous latest live move may no longer be visible
            for (i = 0; i < nUnits; i++)
            {
                if (u[i].mLive < u[i].n - 1)
                {
                    u[i].mLive = u[i].n - 1;
                    pos = u[i].calcPos(timeSimLast + 1);
                    events.add(new MoveEvt(timeSimLast + 1, i, (int)(pos.x >> FP.Precision), (int)(pos.y >> FP.Precision)));
                }
            }
            // check if units moved between tiles
            for (i = 0; i < nUnits; i++)
            {
                u[i].addMoveEvts(ref events, i, timeSimLast, timeSim);
            }
            // apply simulation events
            while (events.peekTime() <= timeSim)
            {
                events.pop().apply();
            }
        }

        public static void updatePast(long curTime)
        {
            int i;
            // restore to last coherent/live state if unit moves off coherent area
            // TODO: choose check state times more intelligently
            // (how do I do that in multiplayer, when time traveling at same time as updating present?)
            for (i = 0; i < nUnits; i++)
            {
                if (curTime >= u[i].timeCohere && u[i].mLive < u[i].n - 1
                    && !tileAt(u[i].calcPos(curTime)).coherentWhen(u[i].player, curTime))
                {
                    u[i].setN(u[i].mLive + 1);
                }
            }
        }

        private static void visAdd(int unit, int tileX, int tileY, long time)
        {
            int i, tX, tY;
            bool filled = true;
            if (tileX >= 0 && tileX < tileLen() && tileY >= 0 && tileY < tileLen())
            {
                if (tiles[tileX, tileY].unitVisLatest(unit)) throw new InvalidOperationException("unit " + unit + " already sees tile (" + tileX + ", " + tileY + ")");
                // add unit to unit visibility tile
                tiles[tileX, tileY].unitVisToggle(unit, time);
                // TODO: use smarter playerVis adding algorithm
                // also, if opponent unit that can't make anything enters then exits region previously indirectly visible, should use smarter playerVis adding algorithm there
                if (!tiles[tileX, tileY].playerVisLatest(u[unit].player))
                {
                    tiles[tileX, tileY].playerVis[u[unit].player].Add(time);
                    // check if a tile cohered for this player, or decohered for another player
                    for (i = 0; i < g.nPlayers; i++)
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
                    for (tX = Math.Max(0, tileX - 1); tX <= Math.Min(tileLen() - 1, tileX + 1); tX++)
                    {
                        for (tY = Math.Max(0, tileY - 1); tY <= Math.Min(tileLen() - 1, tileY + 1); tY++)
                        {
                            if (!tiles[tileX, tileY].playerVisLatest(u[unit].player)) filled = false;
                        }
                    }
                    //if (filled) events.add(new PlayerVisAddEvt(time, u[unit].player, tileX, tileY));
                }
            }
        }

        private static void visRemove(int unit, int tileX, int tileY, long time)
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

        private static void coherenceAdd(int player, int tX, int tY, long time)
        {
            if (tiles[tX, tY].coherentLatest(player)) throw new InvalidOperationException("tile (" + tX + ", " + tY + ") is already coherent");
            tiles[tX, tY].coherence[player].Add(time);
            // this player's units that are on this tile may time travel starting now
            // TODO: actually safe to time travel at earlier times, as long as unit of same type is at same place when decoheres
            for (int i = 0; i < nUnits; i++)
            {
                if (player == u[i].player && tX == u[i].tileX && tY == u[i].tileY && !u[i].coherent)
                {
                    u[i].coherent = true;
                    u[i].timeCohere = time;
                }
            }
        }

        private static void coherenceRemove(int player, int tX, int tY, long time)
        {
            if (!tiles[tX, tY].coherentLatest(player)) throw new InvalidOperationException("tile (" + tX + ", " + tY + ") is already not coherent");
            tiles[tX, tY].coherence[player].Add(time);
            // this player's units that are on this tile may not time travel starting now
            for (int i = 0; i < nUnits; i++)
            {
                if (player == u[i].player && tX == u[i].tileX && tY == u[i].tileY && u[i].coherent)
                {
                    u[i].coherent = false;
                    u[i].timeCohere = long.MaxValue;
                }
            }
        }

        // calculates from player visibility tiles if it is impossible for other players' units to see this location
        private static bool calcCoherent(int player, int tileX, int tileY, long time)
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
            for (i = 0; i < g.nPlayers; i++)
            {
                if (i != player && tiles[tileX, tileY].playerVisWhen(i, time)) return false;
            }
            return true;
        }

        public static bool inVis(long tX, long tY)
        {
            //return Math.Max(Math.Abs(tX), Math.Abs(tY)) <= (int)(visRadius >> FP.Precision);
            return new FP.Vector(tX << FP.Precision, tY << FP.Precision).lengthSq() <= g.visRadius * g.visRadius;
        }

        public static int tileVisRadius()
        {
            return (int)(g.visRadius >> FP.Precision); // adding "+ 1" to this actually doesn't make a difference
        }

        public static Tile tileAt(FP.Vector pos)
        {
            return tiles[pos.x >> FP.Precision, pos.y >> FP.Precision];
        }

        public static int tileLen() // TODO: use unitVis.GetUpperBound instead of this function
        {
            return (int)((g.mapSize >> FP.Precision) + 1);
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
