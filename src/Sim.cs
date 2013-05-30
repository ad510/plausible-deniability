﻿// game simulation
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

        public class Scenario
        {
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
            public Player[] players;
            public UnitType[] unitT;

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

        /// <summary>
        /// unit movement (linearly interpolated between 2 points)
        /// </summary>
        public class UnitMove
        {
            public long timeStart; // time when starts moving
            public long timeEnd; // time when finishes moving
            public FP.Vector vecStart; // z indicates rotation (TODO: implement rotation)
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

            /// <summary>
            /// returns location at specified time
            /// </summary>
            public FP.Vector calcPos(long time)
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

        public class Unit
        {
            private int id; // index in unit array
            public int type;
            public int player;
            public int n; // number of moves
            public UnitMove[] m;
            //public FP.Vector pos; // current position
            public int tileX, tileY; // current position on visibility tiles
            public int nTimeHealth;
            public long[] timeHealth; // times at which each health increment is removed
            public long timeAttack; // latest time that attacked a unit
            public long timeSimPast; // time traveling simulation time if made in the past, otherwise set to long.MaxValue
            public bool coherent; // whether safe to time travel at simulation time
            public long timeCohere; // earliest time at which it's safe to time travel
            public int parentPath; // index of unit whose path this unit split off from (set to <0 if none)
            public bool replaceParentPath; // whether should replace parent unit's path with this unit's path when this unit becomes live
            public int nChildPaths;
            public int[] childPaths; // indices of temporary units which move along alternate paths that this unit could take

            public Unit(int idVal, int typeVal, int playerVal, long startTime, FP.Vector startPos)
            {
                id = idVal;
                type = typeVal;
                player = playerVal;
                n = 1;
                m = new UnitMove[n];
                m[0] = new UnitMove(startTime, startPos);
                //pos = startPos;
                tileX = OffMap + 1;
                tileY = OffMap + 1;
                nTimeHealth = 0;
                timeHealth = new long[g.unitT[type].maxHealth];
                timeAttack = long.MinValue;
                timeSimPast = (startTime >= timeSim) ? long.MaxValue : startTime;
                coherent = tileAt(startPos).coherentWhen(player, startTime);
                timeCohere = coherent ? startTime : long.MaxValue;
                parentPath = -1;
                replaceParentPath = false;
                nChildPaths = 0;
                childPaths = new int[nChildPaths];
            }

            /// <summary>
            /// ensure that if unit is moving in the past, it does not move off coherent areas
            /// </summary>
            public void updatePast(long curTime)
            {
                SimEvtList pastEvents = new SimEvtList();
                TileMoveEvt evt;
                FP.Vector pos;
                int tX, tY, coherenceIndex;
                if (curTime <= timeSimPast || !exists(curTime)) return;
                // delete path if tile that unit starts on stops being coherent since timeSimPast
                pos = calcPos(timeSimPast);
                tX = (int)(pos.x >> FP.Precision);
                tY = (int)(pos.y >> FP.Precision);
                addTileMoveEvts(ref pastEvents, timeSimPast, Math.Min(curTime, timeSim));
                evt = (TileMoveEvt)pastEvents.pop();
                coherenceIndex = tiles[tX, tY].coherentIndexWhen(player, (evt != null) ? evt.time - 1 : curTime);
                if (!tiles[tX, tY].coherentWhen(player, (evt != null) ? evt.time - 1 : curTime)
                    || tiles[tX, tY].coherence[player][coherenceIndex] > timeSimPast)
                {
                    if (!deletePath(timeSim)) throw new SystemException("path not deleted successfully after moving off coherent area");
                    return;
                }
                // delete path if unit moves off coherent area or tile that unit is on stops being coherent
                if (evt != null)
                {
                    do
                    {
                        if (evt.tileX != int.MinValue) tX = evt.tileX;
                        if (evt.tileY != int.MinValue) tY = evt.tileY;
                        coherenceIndex = tiles[tX, tY].coherentIndexWhen(player, evt.time);
                        if (!tiles[tX, tY].coherentWhen(player, evt.time)
                            || (coherenceIndex + 1 < tiles[tX, tY].coherence[player].Count() && tiles[tX, tY].coherence[player][coherenceIndex + 1] <= Math.Min(events.peekTime(), Math.Min(curTime, timeSim))))
                        {
                            if (!deletePath(timeSim)) throw new SystemException("path not deleted successfully after moving off coherent area");
                            return;
                        }
                    } while ((evt = (TileMoveEvt)pastEvents.pop()) != null);
                }
                if (curTime >= timeSim)
                {
                    // unit becomes live
                    timeSimPast = long.MaxValue;
                    if (replaceParentPath)
                    {
                        replaceParentPath = false;
                        u[parentPath].deleteChildPathsAfter(m[0].timeStart);
                        moveToParentPath(timeSim);
                    }
                    else
                    {
                        events.add(new TileMoveEvt(timeSim, id, tX, tY));
                    }
                }
                else
                {
                    timeSimPast = curTime;
                }
            }

            /// <summary>
            /// resize move array
            /// </summary>
            public void setN(int newSize)
            {
                int i = 0;
                for (i = n; i < Math.Min(newSize, m.Length); i++)
                {
                    m[i] = new UnitMove(0, new FP.Vector());
                }
                n = newSize;
                if (n > m.Length)
                    Array.Resize(ref m, n * 2);
            }

            /// <summary>
            /// add specified move to end of move array
            /// </summary>
            /// <remarks>
            /// if caller also adds a TileMoveEvt, must ensure that it isn't deleted in update()
            /// (add allowOverride variable in TileMoveEvt if necessary)
            /// </remarks>
            public void addMove(UnitMove newMove)
            {
                setN(n + 1);
                m[n - 1] = newMove;
                if (!movedUnits.Contains(id)) movedUnits.Add(id); // indicate to delete and recalculate later TileMoveEvts for this unit
            }

            /// <summary>
            /// returns location at specified time
            /// </summary>
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

            public void addTileMoveEvts(ref SimEvtList events, long timeMin, long timeMax)
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
                    events.add(new TileMoveEvt(m[0].timeStart, id, (int)(m[0].vecStart.x >> FP.Precision), (int)(m[0].vecStart.y >> FP.Precision)));
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
                        events.add(new TileMoveEvt(m[i].timeAtX(tX << FP.Precision), id, tX + dir, int.MinValue));
                    }
                    // moving between rows (y)
                    dir = (pos.y >= posLast.y) ? 0 : -1;
                    for (tY = (int)(Math.Min(pos.y, posLast.y) >> FP.Precision) + 1; tY <= (int)(Math.Max(pos.y, posLast.y) >> FP.Precision); tY++)
                    {
                        events.add(new TileMoveEvt(m[i].timeAtY(tY << FP.Precision), id, int.MinValue, tY + dir));
                    }
                }
            }

            /// <summary>
            /// remove 1 health increment at specified time
            /// </summary>
            public void takeHealth(long time)
            {
                if (nTimeHealth < g.unitT[type].maxHealth)
                {
                    nTimeHealth++;
                    timeHealth[nTimeHealth - 1] = time;
                    if (nTimeHealth >= g.unitT[type].maxHealth)
                    {
                        // unit lost all health
                        events.add(new TileMoveEvt(time, id, OffMap, 0));
                    }
                }
            }

            public int healthLatest()
            {
                return g.unitT[type].maxHealth - nTimeHealth;
            }

            public int healthWhen(long time)
            {
                int i = nTimeHealth;
                while (i > 0 && time < timeHealth[i - 1]) i--;
                return g.unitT[type].maxHealth - i;
            }

            public void cohere(long time)
            {
                coherent = true;
                timeCohere = time;
            }

            public void decohere(long time)
            {
                coherent = false;
                timeCohere = long.MaxValue;
                deleteAllChildPaths(time);
                if (parentPath >= 0)
                {
                    int parentPathTemp = parentPath;
                    moveToParentPath(time);
                    u[parentPathTemp].decohere(time);
                }
            }

            /// <summary>
            /// if this unit has multiple paths, delete it (and child paths made after the specified time) and return true, otherwise return false
            /// </summary>
            public bool deletePath(long time)
            {
                int i, path;
                if (parentPath >= 0) deleteChildPathsAfter(time); // delete child paths made after the specified time
                if (nChildPaths > 0)
                {
                    // take the path of the latest child unit (overwriting our current moves in the process)
                    path = -1;
                    for (i = 0; i < nChildPaths; i++)
                    {
                        if ((u[childPaths[i]].isLive(time) || (!isLive(time) && u[childPaths[i]].exists(time))) // child path must be live, unless this unit isn't
                            && (path < 0 || u[childPaths[i]].m[0].timeStart > u[path].m[0].timeStart)) // child path must be made after current latest path
                        {
                            path = childPaths[i];
                        }
                    }
                    if (path >= 0)
                    {
                        deleteChildPathsAfter(u[path].m[0].timeStart); // delete non-live child paths made after the child path that we will take
                        u[path].moveToParentPath(time);
                        return true;
                    }
                }
                else if (parentPath >= 0)
                {
                    // if we don't have a child path but have a parent path, delete this unit completely
                    if (replaceParentPath)
                    {
                        unitIdChgs.Add(id);
                        unitIdChgs.Add(parentPath);
                    }
                    u[parentPath].deleteChildPath(id, time);
                    return true;
                }
                return false; // this unit is only moving along 1 path, so there are no other paths to replace this path if it's deleted
            }

            /// <summary>
            /// makes a temporary unit splitting off from this unit's path at specified time (if allowed), returns whether successful
            /// </summary>
            public bool makeChildPath(long time)
            {
                if (exists(time) && coherent && time >= timeCohere)
                {
                    FP.Vector pos = calcPos(time);
                    // make new unit
                    setNUnits(nUnits + 1);
                    u[nUnits - 1] = new Unit(nUnits - 1, type, player, time, pos);
                    // add it to child path list
                    addChildPath(nUnits - 1);
                    // indicate to calculate TileMoveEvts for new unit starting at timeSim
                    if (!movedUnits.Contains(nUnits - 1)) movedUnits.Add(nUnits - 1);
                    return true;
                }
                return false;
            }

            /// <summary>
            /// returns index (in unit array) of path that isn't updated in the present and is therefore safe to move in the past
            /// </summary>
            public int prepareNonLivePath(long time)
            {
                if (timeSimPast != long.MaxValue)
                {
                    // this unit isn't live, prepare for a new move to be added at specified time
                    deleteChildPathsAfter(time);
                    if (time < timeSimPast) timeSimPast = time;
                    return id;
                }
                else
                {
                    // this unit is live, make new child path to replace this unit's path when the child path becomes live
                    for (int i = 0; i < nChildPaths; i++)
                    {
                        if (u[childPaths[i]].replaceParentPath)
                        {
                            // delete existing replacement path before making a new one
                            u[childPaths[i]].deletePath(u[childPaths[i]].m[0].timeStart);
                            break;
                        }
                    }
                    makeChildPath(time);
                    u[childPaths[nChildPaths - 1]].replaceParentPath = true;
                    unitIdChgs.Add(id);
                    unitIdChgs.Add(childPaths[nChildPaths - 1]);
                    return childPaths[nChildPaths - 1];
                }
            }

            /// <summary>
            /// add specified unit to child path list
            /// </summary>
            private void addChildPath(int unit)
            {
                nChildPaths++;
                if (nChildPaths > childPaths.Length)
                    Array.Resize(ref childPaths, nChildPaths * 2);
                childPaths[nChildPaths - 1] = unit;
                u[unit].parentPath = id;
            }

            private void deleteChildPath(int unit, long time)
            {
                int index;
                for (index = 0; index < nChildPaths && childPaths[index] != unit; index++) ;
                if (index == nChildPaths) throw new ArgumentException("unit " + unit + " is not a child path");
                // remove child path from list
                for (int i = index; i < nChildPaths - 1; i++)
                {
                    childPaths[i] = childPaths[i + 1];
                }
                nChildPaths--;
                // delete child path
                u[unit].delete(time);
                u[unit].parentPath = -1;
            }

            /// <summary>
            /// recursively delete all child paths
            /// </summary>
            private void deleteAllChildPaths(long time)
            {
                for (int i = 0; i < nChildPaths; i++)
                {
                    u[childPaths[i]].delete(time);
                    u[childPaths[i]].parentPath = -1;
                    u[childPaths[i]].deleteAllChildPaths(time);
                }
                nChildPaths = 0;
            }

            /// <summary>
            /// delete child paths made after the specified time
            /// </summary>
            private void deleteChildPathsAfter(long time)
            {
                for (int i = 0; i < nChildPaths; i++)
                {
                    if (u[childPaths[i]].m[0].timeStart > time)
                    {
                        u[childPaths[i]].deletePath(time);
                        i--;
                    }
                }
            }

            /// <summary>
            /// make parent unit take the path of this unit, then delete this unit
            /// </summary>
            private void moveToParentPath(long time)
            {
                FP.Vector pos = calcPos(Math.Max(time, timeSim));
                int i;
                // indicate that this unit changed indices
                unitIdChgs.Add(parentPath);
                unitIdChgs.Add(-1);
                unitIdChgs.Add(id);
                unitIdChgs.Add(parentPath);
                // move all moves to parent unit
                for (i = 0; i < n; i++)
                {
                    u[parentPath].addMove(m[i]);
                }
                // move parent unit onto tile that we are currently on
                // can't pass in tileX and tileY because this unit's latest TileMoveEvts might not be applied yet
                events.add(new TileMoveEvt(Math.Max(time, timeSim), parentPath, (int)(pos.x >> FP.Precision), (int)(pos.y >> FP.Precision)));
                // move child units to parent unit
                for (i = 0; i < nChildPaths; i++)
                {
                    u[parentPath].addChildPath(childPaths[i]);
                }
                nChildPaths = 0;
                // delete this unit since it is now incorporated into its parent unit
                u[parentPath].deleteChildPath(id, time);
            }

            /// <summary>
            /// returns index of unit that is the root parent path of this unit
            /// </summary>
            public int rootParentPath()
            {
                int ret = id;
                while (u[ret].parentPath >= 0) ret = u[ret].parentPath;
                return ret;
            }

            /// <summary>
            /// make this unit as if it never existed
            /// </summary>
            private void delete(long time)
            {
                n = 0;
                m[0] = new UnitMove(long.MaxValue - 1, new FP.Vector(OffMap, 0));
                timeCohere = long.MaxValue;
                events.add(new TileMoveEvt(Math.Max(time, timeSim), id, OffMap, 0));
            }

            /// <summary>
            /// returns whether unit is created and has health at specified time
            /// </summary>
            public bool exists(long time)
            {
                return time >= m[0].timeStart && healthWhen(time) > 0;
            }

            /// <summary>
            /// returns whether unit exists and is being updated in the present (i.e., isn't time travelling)
            /// </summary>
            public bool isLive(long time)
            {
                return exists(time) && timeSimPast == long.MaxValue;
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

            /// <summary>
            /// returns if the specified tile is in the direct line of sight of a unit of specified player
            /// </summary>
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

        // simulation events

        /// <summary>
        /// base class for simulation events
        /// </summary>
        public abstract class SimEvt
        {
            public long time;

            public abstract void apply();
        }

        public class SimEvtList
        {
            public List<SimEvt> events;

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

        public enum Formation : byte { Tight, Loose, Ring };

        /// <summary>
        /// command to move unit(s)
        /// </summary>
        public class CmdMoveEvt : SimEvt
        {
            public long timeCmd; // time is latest simulation time when command is given, timeCmd is when units told to move (may be in past)
            public int[] units;
            public FP.Vector pos; // where to move to
            public Formation formation;

            public CmdMoveEvt(long timeVal, long timeCmdVal, int[] unitsVal, FP.Vector posVal, Formation formationVal)
            {
                time = timeVal;
                timeCmd = timeCmdVal;
                units = unitsVal;
                pos = posVal;
                formation = formationVal;
            }

            public override void apply()
            {
                FP.Vector curPos, goal, rows = new FP.Vector(), offset;
                long spacing = 0;
                int count = 0, i = 0;
                // copy event to command history list (it should've already been popped from event list)
                cmdHistory.add(this);
                // count number of units able to move
                foreach (int unit in units)
                {
                    if (u[unit].exists(timeCmd) && (timeCmd >= timeSim || (timeCmd >= u[unit].timeCohere && u[unit].coherent)))
                    {
                        count++;
                        if (formation == Formation.Tight && g.unitT[u[unit].type].tightFormationSpacing > spacing) spacing = g.unitT[u[unit].type].tightFormationSpacing;
                    }
                }
                if (count == 0) return;
                // calculate spacing
                // (if tight formation, then spacing was already calculated above)
                // TODO: loose formation should be triangular and not use sqrt
                if (formation == Formation.Loose)
                {
                    spacing = FP.mul(g.visRadius, FP.fromDouble(Math.Sqrt(2))) >> FP.Precision << FP.Precision;
                }
                else if (formation == Formation.Ring)
                {
                    spacing = (g.visRadius * 2 >> FP.Precision) - 1 << FP.Precision;
                }
                if (formation == Formation.Tight || formation == Formation.Loose)
                {
                    rows.x = (int)Math.Ceiling(Math.Sqrt(count)); // TODO: don't use sqrt
                    rows.y = (count - 1) / rows.x + 1;
                    offset = (rows - new FP.Vector(1, 1)) * spacing / 2;
                }
                else if (formation == Formation.Ring)
                {
                    offset.x = FP.div(spacing / 2, FP.fromDouble(Math.Sin(Math.PI / count))); // TODO: don't use sin
                    offset.y = offset.x;
                }
                else
                {
                    throw new NotImplementedException("requested formation is not implemented");
                }
                if (pos.x < Math.Min(offset.x, g.mapSize / 2)) pos.x = Math.Min(offset.x, g.mapSize / 2);
                if (pos.x > g.mapSize - Math.Min(offset.x, g.mapSize / 2)) pos.x = g.mapSize - Math.Min(offset.x, g.mapSize / 2);
                if (pos.y < Math.Min(offset.y, g.mapSize / 2)) pos.y = Math.Min(offset.y, g.mapSize / 2);
                if (pos.y > g.mapSize - Math.Min(offset.y, g.mapSize / 2)) pos.y = g.mapSize - Math.Min(offset.y, g.mapSize / 2);
                // move units
                foreach (int unit in units)
                {
                    if (u[unit].exists(timeCmd) && (timeCmd >= timeSim || (timeCmd >= u[unit].timeCohere && u[unit].coherent)))
                    {
                        int unit2 = unit;
                        curPos = u[unit].calcPos(timeCmd);
                        if (formation == Formation.Tight || formation == Formation.Loose)
                        {
                            goal = pos + new FP.Vector((i % rows.x) * spacing - offset.x, i / rows.x * spacing - offset.y);
                        }
                        else if (formation == Formation.Ring)
                        {
                            // TODO: don't use sin or cos
                            goal = pos + offset.x * new FP.Vector(FP.fromDouble(Math.Cos(2 * Math.PI * i / count)), FP.fromDouble(Math.Sin(2 * Math.PI * i / count)));
                        }
                        else
                        {
                            throw new NotImplementedException("requested formation is not implemented");
                        }
                        if (goal.x < 0) goal.x = 0;
                        if (goal.x > g.mapSize) goal.x = g.mapSize;
                        if (goal.y < 0) goal.y = 0;
                        if (goal.y > g.mapSize) goal.y = g.mapSize;
                        if (timeCmd < timeSim) unit2 = u[unit].prepareNonLivePath(timeCmd); // move replacement path instead of live path if in past
                        u[unit2].addMove(UnitMove.fromSpeed(timeCmd, g.unitT[u[unit2].type].speed, curPos, goal));
                        i++;
                    }
                }
            }
        }

        public enum UnitAction : byte { MakePath, DeletePath };

        /// <summary>
        /// command to apply an action to a set of units
        /// </summary>
        public class CmdUnitActionEvt : SimEvt
        {
            public long timeCmd; // time is latest simulation time when command is given, timeCmd is when action is applied (may be in past)
            public int[] units;
            public UnitAction action;

            public CmdUnitActionEvt(long timeVal, long timeCmdVal, int[] unitsVal, UnitAction actionVal)
            {
                time = timeVal;
                timeCmd = timeCmdVal;
                units = unitsVal;
                action = actionVal;
            }

            public override void apply()
            {
                cmdHistory.add(this); // copy event to command history list (it should've already been popped from event list)
                foreach (int unit in units)
                {
                    if (action == UnitAction.MakePath)
                    {
                        // happens at timeCmd + 1 so addTileMoveEvts() knows to initially put new unit on visibility tiles
                        // TODO: move new unit immediately after making it
                        u[unit].makeChildPath(timeCmd + 1);
                    }
                    else if (action == UnitAction.DeletePath)
                    {
                        // happens at timeCmd so that when paused, making path then deleting parent path doesn't move parent's tile pos off map
                        // (where child's tile pos initially is)
                        u[unit].deletePath(timeCmd);
                    }
                }
            }
        }

        /// <summary>
        /// event to update various things at regular intervals
        /// </summary>
        public class UpdateEvt : SimEvt
        {
            public UpdateEvt(long timeVal)
            {
                time = timeVal;
            }

            public override void apply()
            {
                FP.Vector pos;
                int target;
                long distSq, targetDistSq;
                int i, j;
                // update units
                for (i = 0; i < nUnits; i++)
                {
                    if (u[i].isLive(time) && time >= u[i].timeAttack + g.unitT[u[i].type].reload)
                    {
                        // done reloading, look for closest target to potentially attack
                        pos = u[i].calcPos(time);
                        target = -1;
                        targetDistSq = g.unitT[u[i].type].range * g.unitT[u[i].type].range + 1;
                        for (j = 0; j < nUnits; j++)
                        {
                            if (i != j && u[j].isLive(time) && g.players[u[i].player].mayAttack[u[j].player] && g.unitT[u[i].type].damage[u[j].type] > 0)
                            {
                                distSq = (u[j].calcPos(time) - pos).lengthSq();
                                if (distSq < targetDistSq)
                                {
                                    target = j;
                                    targetDistSq = distSq;
                                }
                            }
                        }
                        if (target >= 0)
                        {
                            // attack target
                            // take health with 1 ms delay so earlier units in array don't have unfair advantage
                            for (j = 0; j < g.unitT[u[i].type].damage[u[target].type]; j++) u[target].takeHealth(time + 1);
                            u[i].timeAttack = time;
                        }
                    }
                }
                events.add(new UpdateEvt(time + g.updateInterval));
            }
        }

        /// <summary>
        /// event in which unit moves between visibility tiles
        /// </summary>
        /// <remarks>
        /// when making this event, can't rely on a unit's tileX and tileY being up-to-date
        /// because the latest TileMoveEvts for that unit might not be applied yet
        /// </remarks>
        public class TileMoveEvt : SimEvt
        {
            public int unit;
            public int tileX, tileY; // new tile position, set to int.MinValue to keep current value

            public TileMoveEvt(long timeVal, int unitVal, int tileXVal, int tileYVal)
            {
                time = timeVal;
                unit = unitVal;
                tileX = tileXVal;
                tileY = tileYVal;
            }

            public override void apply()
            {
                int i, tXPrev, tYPrev, tX, tY;
                if (u[unit].tileX == OffMap) return; // skip event if unit no longer exists
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
                if (tileX >= 0 && tileX < tileLen() && tileY >= 0 && tileY < tileLen())
                {
                    // update whether this unit may time travel
                    if (!u[unit].coherent && tiles[tileX, tileY].coherentWhen(u[unit].player, time))
                    {
                        u[unit].cohere(time);
                    }
                    else if (u[unit].coherent && !tiles[tileX, tileY].coherentWhen(u[unit].player, time))
                    {
                        u[unit].decohere(time); // TODO: sometimes this is called when the tile should be coherent (reproduce by putting many units in ring formation when no enemy units on map)
                    }
                    if (tXPrev >= 0 && tXPrev < tileLen() && tYPrev >= 0 && tYPrev < tileLen())
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
                                        // TODO?: use more accurate time at tiles other than (tileX, tileY)
                                        events.add(new PlayerVisRemoveEvt(time, i, tX, tY));
                                    }
                                }
                            }
                        }
                    }
                }
                if (tXPrev >= 0 && tXPrev < tileLen() && tYPrev >= 0 && tYPrev < tileLen())
                {
                    // if this player can no longer directly see another player's unit, remove this player's visibility there
                    foreach (int j in tiles[tXPrev, tYPrev].unitVis.Keys)
                    {
                        if (u[j].player != u[unit].player && u[j].healthLatest() > 0 && inVis(u[j].tileX - tXPrev, u[j].tileY - tYPrev) && !tiles[u[j].tileX, u[j].tileY].playerDirectVisLatest(u[unit].player))
                        {
                            for (tX = Math.Max(0, u[j].tileX - 1); tX <= Math.Min(tileLen() - 1, u[j].tileX + 1); tX++)
                            {
                                for (tY = Math.Max(0, u[j].tileY - 1); tY <= Math.Min(tileLen() - 1, u[j].tileY + 1); tY++)
                                {
                                    // TODO?: use more accurate time at tiles other than (u[j].tileX, u[j].tileY)
                                    events.add(new PlayerVisRemoveEvt(time, u[unit].player, tX, tY));
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// event in which a player stops seeing a tile
        /// </summary>
        public class PlayerVisRemoveEvt : SimEvt
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
        public static SimEvtList cmdHistory;
        public static List<int> movedUnits; // indices of units that moved in the latest simulation event, invalidating later TileMoveEvts for that unit
        public static List<int> unitIdChgs; // list of units that changed indices (old index followed by new index)
        public static long maxSpeed; // speed of fastest unit (is max speed that players can gain or lose visibility)
        public static long timeSim; // current simulation time

        public static void setNUnits(int newSize)
        {
            nUnits = newSize;
            if (u == null || nUnits > u.Length)
                Array.Resize(ref u, nUnits * 2);
        }

        public static void update(long curTime)
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
                evt.apply();
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

        private static void visAdd(int unit, int tileX, int tileY, long time)
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
                    u[i].cohere(time);
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
                    u[i].decohere(time);
                }
            }
        }

        /// <summary>
        /// calculates from player visibility tiles if it is impossible for other players' units to see this location
        /// </summary>
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
            //return Math.Max(Math.Abs(tX), Math.Abs(tY)) <= (int)(g.visRadius >> FP.Precision);
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
