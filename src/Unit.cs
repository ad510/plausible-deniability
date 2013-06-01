// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Decoherence
{
    /// <summary>
    /// RTS game unit that can do strange things when other players aren't looking
    /// </summary>
    public class Unit
    {
        /// <summary>
        /// represents a single unit movement that starts at a specified location,
        /// moves at constant velocity to a specified end location, then stops
        /// </summary>
        public class Move
        {
            public long timeStart; // time when starts moving
            public long timeEnd; // time when finishes moving
            public FP.Vector vecStart; // location at timeStart, z indicates rotation (TODO: implement rotation)
            public FP.Vector vecEnd; // location at timeEnd, z indicates rotation

            /// <summary>
            /// constructor that directly sets all instance variables
            /// </summary>
            public Move(long timeStartVal, long timeEndVal, FP.Vector vecStartVal, FP.Vector vecEndVal)
            {
                timeStart = timeStartVal;
                timeEnd = timeEndVal;
                vecStart = vecStartVal;
                vecEnd = vecEndVal;
            }

            /// <summary>
            /// constructor for nonmoving trajectory
            /// </summary>
            public Move(long timeVal, FP.Vector vecVal)
                : this(timeVal, timeVal + 1, vecVal, vecVal)
            {
            }

            /// <summary>
            /// alternate method to create Unit.Move object that asks for speed (in position units per millisecond) instead of end time
            /// </summary>
            public static Move fromSpeed(long timeStartVal, long speed, FP.Vector vecStartVal, FP.Vector vecEndVal)
            {
                return new Move(timeStartVal, timeStartVal + new FP.Vector(vecEndVal - vecStartVal).length() / speed, vecStartVal, vecEndVal);
            }

            /// <summary>
            /// returns location at specified time
            /// </summary>
            public FP.Vector calcPos(long time)
            {
                if (time >= timeEnd) return vecEnd;
                return vecStart + (vecEnd - vecStart) * FP.div(time - timeStart, timeEnd - timeStart);
            }

            /// <summary>
            /// returns time when position is at specified x value (inaccurate when x isn't between vecStart.x and vecEnd.x)
            /// </summary>
            public long timeAtX(long x)
            {
                return FP.lineCalcX(new FP.Vector(timeStart, vecStart.x), new FP.Vector(timeEnd, vecEnd.x), x);
            }

            /// <summary>
            /// returns time when position is at specified y value (inaccurate when y isn't between vecStart.y and vecEnd.y)
            /// </summary>
            public long timeAtY(long y)
            {
                return FP.lineCalcX(new FP.Vector(timeStart, vecStart.y), new FP.Vector(timeEnd, vecEnd.y), y);
            }
        }

        private Sim g;
        private int id; // index in unit array
        public int type;
        public int player;
        public int n; // number of moves
        public Move[] m; // array of moves (later moves are later in array)
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

        public Unit(Sim simVal, int idVal, int typeVal, int playerVal, long startTime, FP.Vector startPos)
        {
            g = simVal;
            id = idVal;
            type = typeVal;
            player = playerVal;
            n = 1;
            m = new Move[n];
            m[0] = new Move(startTime, startPos);
            //pos = startPos;
            tileX = Sim.OffMap + 1;
            tileY = Sim.OffMap + 1;
            nTimeHealth = 0;
            timeHealth = new long[g.unitT[type].maxHealth];
            timeAttack = long.MinValue;
            timeSimPast = (startTime >= g.timeSim) ? long.MaxValue : startTime;
            coherent = g.tileAt(startPos).coherentWhen(player, startTime);
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
            addTileMoveEvts(ref pastEvents, timeSimPast, Math.Min(curTime, g.timeSim));
            evt = (TileMoveEvt)pastEvents.pop();
            coherenceIndex = g.tiles[tX, tY].coherentIndexWhen(player, (evt != null) ? evt.time - 1 : curTime);
            if (!g.tiles[tX, tY].coherentWhen(player, (evt != null) ? evt.time - 1 : curTime)
                || g.tiles[tX, tY].coherence[player][coherenceIndex] > timeSimPast)
            {
                if (!deletePath(g.timeSim)) throw new SystemException("path not deleted successfully after moving off coherent area");
                return;
            }
            // delete path if unit moves off coherent area or tile that unit is on stops being coherent
            if (evt != null)
            {
                do
                {
                    if (evt.tileX != int.MinValue) tX = evt.tileX;
                    if (evt.tileY != int.MinValue) tY = evt.tileY;
                    coherenceIndex = g.tiles[tX, tY].coherentIndexWhen(player, evt.time);
                    if (!g.tiles[tX, tY].coherentWhen(player, evt.time)
                        || (coherenceIndex + 1 < g.tiles[tX, tY].coherence[player].Count() && g.tiles[tX, tY].coherence[player][coherenceIndex + 1] <= Math.Min(g.events.peekTime(), Math.Min(curTime, g.timeSim))))
                    {
                        if (!deletePath(g.timeSim)) throw new SystemException("path not deleted successfully after moving off coherent area");
                        return;
                    }
                } while ((evt = (TileMoveEvt)pastEvents.pop()) != null);
            }
            if (curTime >= g.timeSim)
            {
                // unit becomes live
                timeSimPast = long.MaxValue;
                if (replaceParentPath)
                {
                    replaceParentPath = false;
                    g.u[parentPath].deleteChildPathsAfter(m[0].timeStart);
                    moveToParentPath();
                }
                else
                {
                    g.events.add(new TileMoveEvt(g.timeSim, id, tX, tY));
                }
            }
            else
            {
                timeSimPast = curTime;
            }
        }

        /// <summary>
        /// intelligently resize move array to specified size
        /// </summary>
        public void setN(int newSize)
        {
            int i = 0;
            for (i = n; i < Math.Min(newSize, m.Length); i++)
            {
                m[i] = new Move(0, new FP.Vector());
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
        public void addMove(Move newMove)
        {
            setN(n + 1);
            m[n - 1] = newMove;
            if (!g.movedUnits.Contains(id)) g.movedUnits.Add(id); // indicate to delete and recalculate later TileMoveEvts for this unit
        }

        /// <summary>
        /// returns location at specified time
        /// </summary>
        public FP.Vector calcPos(long time)
        {
            return m[getMove(time)].calcPos(time);
        }

        /// <summary>
        /// returns index of move that is occurring at specified time
        /// </summary>
        public int getMove(long time)
        {
            int ret = n - 1;
            while (ret >= 0 && time < m[ret].timeStart) ret--;
            return ret;
        }

        /// <summary>
        /// inserts TileMoveEvt events for this unit into events for the time interval from timeMin to timeMax
        /// </summary>
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
                    g.events.add(new TileMoveEvt(time, id, Sim.OffMap, 0));
                }
            }
        }

        /// <summary>
        /// returns health of this unit at latest possible time
        /// </summary>
        public int healthLatest()
        {
            return g.unitT[type].maxHealth - nTimeHealth;
        }

        /// <summary>
        /// returns health of this unit at specified time
        /// </summary>
        public int healthWhen(long time)
        {
            int i = nTimeHealth;
            while (i > 0 && time < timeHealth[i - 1]) i--;
            return g.unitT[type].maxHealth - i;
        }

        /// <summary>
        /// allows unit to time travel and move along multiple paths starting at specified time
        /// </summary>
        public void cohere(long time)
        {
            coherent = true;
            timeCohere = time;
        }

        /// <summary>
        /// stops allowing unit to time travel or move along multiple paths starting at timeSim
        /// </summary>
        public void decohere()
        {
            coherent = false;
            timeCohere = long.MaxValue;
            deleteAllChildPaths();
            if (parentPath >= 0)
            {
                int parentPathTemp = parentPath;
                moveToParentPath();
                g.u[parentPathTemp].decohere();
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
                    if ((g.u[childPaths[i]].isLive(time) || (!isLive(time) && g.u[childPaths[i]].exists(time))) // child path must be live, unless this unit isn't
                        && (path < 0 || g.u[childPaths[i]].m[0].timeStart > g.u[path].m[0].timeStart)) // child path must be made after current latest path
                    {
                        path = childPaths[i];
                    }
                }
                if (path >= 0)
                {
                    deleteChildPathsAfter(g.u[path].m[0].timeStart); // delete non-live child paths made after the child path that we will take
                    g.u[path].moveToParentPath();
                    return true;
                }
            }
            else if (parentPath >= 0)
            {
                // if we don't have a child path but have a parent path, delete this unit completely
                if (replaceParentPath)
                {
                    g.unitIdChgs.Add(id);
                    g.unitIdChgs.Add(parentPath);
                }
                g.u[parentPath].deleteChildPath(id);
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
                g.setNUnits(g.nUnits + 1);
                g.u[g.nUnits - 1] = new Unit(g, g.nUnits - 1, type, player, time, pos);
                // add it to child path list
                addChildPath(g.nUnits - 1);
                // indicate to calculate TileMoveEvts for new unit starting at timeSim
                if (!g.movedUnits.Contains(g.nUnits - 1)) g.movedUnits.Add(g.nUnits - 1);
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
                    if (g.u[childPaths[i]].replaceParentPath)
                    {
                        // delete existing replacement path before making a new one
                        g.u[childPaths[i]].deletePath(g.u[childPaths[i]].m[0].timeStart);
                        break;
                    }
                }
                makeChildPath(time);
                g.u[childPaths[nChildPaths - 1]].replaceParentPath = true;
                g.unitIdChgs.Add(id);
                g.unitIdChgs.Add(childPaths[nChildPaths - 1]);
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
            g.u[unit].parentPath = id;
        }

        /// <summary>
        /// non-recursively delete specified child path
        /// </summary>
        private void deleteChildPath(int unit)
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
            g.u[unit].delete();
            g.u[unit].parentPath = -1;
        }

        /// <summary>
        /// recursively delete all child paths
        /// </summary>
        private void deleteAllChildPaths()
        {
            for (int i = 0; i < nChildPaths; i++)
            {
                g.u[childPaths[i]].delete();
                g.u[childPaths[i]].parentPath = -1;
                g.u[childPaths[i]].deleteAllChildPaths();
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
                if (g.u[childPaths[i]].m[0].timeStart > time)
                {
                    g.u[childPaths[i]].deletePath(time);
                    i--;
                }
            }
        }

        /// <summary>
        /// make parent unit take the path of this unit, then delete this unit
        /// </summary>
        private void moveToParentPath()
        {
            FP.Vector pos = calcPos(g.timeSim);
            int i;
            // indicate that this unit changed indices
            g.unitIdChgs.Add(parentPath);
            g.unitIdChgs.Add(-1);
            g.unitIdChgs.Add(id);
            g.unitIdChgs.Add(parentPath);
            // move all moves to parent unit
            for (i = 0; i < n; i++)
            {
                g.u[parentPath].addMove(m[i]);
            }
            // move parent unit onto tile that we are currently on
            // can't pass in tileX and tileY because this unit's latest TileMoveEvts might not be applied yet
            g.events.add(new TileMoveEvt(g.timeSim, parentPath, (int)(pos.x >> FP.Precision), (int)(pos.y >> FP.Precision)));
            // move child units to parent unit
            for (i = 0; i < nChildPaths; i++)
            {
                g.u[parentPath].addChildPath(childPaths[i]);
            }
            nChildPaths = 0;
            // delete this unit since it is now incorporated into its parent unit
            g.u[parentPath].deleteChildPath(id);
        }

        /// <summary>
        /// returns index of unit that is the root parent path of this unit
        /// </summary>
        public int rootParentPath()
        {
            int ret = id;
            while (g.u[ret].parentPath >= 0) ret = g.u[ret].parentPath;
            return ret;
        }

        /// <summary>
        /// make this unit as if it never existed
        /// </summary>
        private void delete()
        {
            n = 0;
            m[0] = new Move(long.MaxValue - 1, new FP.Vector(Sim.OffMap, 0));
            timeCohere = long.MaxValue;
            g.events.add(new TileMoveEvt(g.timeSim, id, Sim.OffMap, 0));
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
}
