﻿// particles engine
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
        // universe structures
        public struct MatterType
        {
            public string name;
            public bool isPlayer;
            public short player; // -1 = nobody, 0 = computer, 1+ = human
            public bool[] annihilates; // if particles of this matter type annihilate particles from each matter type
        }

        public struct ParticleType
        {
            public string name;
            public string imgPath;
            /*public string sndSelect;
            public string sndMove;
            public string sndAnniCmd;
            public string sndAnnihilate;*/
            public long speed;
            public long visRadius;
            public double selRadius;
        }

        public struct Universe
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
            public Color4 matterVisCol;
            public Color4 particleVisCol;
            public Color4 coherentCol;
            //public string music;
            public int nMatterT;
            public int nParticleT;
            public MatterType[] matterT;
            public ParticleType[] particleT;
        }

        public struct ParticleMove // particle movement (linearly interpolated between 2 points)
        {
            public long timeStart; // time when starts moving
            public long timeEnd; // time when finishes moving
            public FP.Vector vecStart; // z indicates rotation
            public FP.Vector vecEnd;

            public ParticleMove(long timeStartVal, long timeEndVal, FP.Vector vecStartVal, FP.Vector vecEndVal)
            {
                timeStart = timeStartVal;
                timeEnd = timeEndVal;
                vecStart = vecStartVal;
                vecEnd = vecEndVal;
            }

            public ParticleMove(long timeVal, FP.Vector vecVal)
                : this(timeVal, timeVal + 1, vecVal, vecVal)
            {
            }

            public static ParticleMove fromSpeed(long timeStartVal, long speed, FP.Vector vecStartVal, FP.Vector vecEndVal)
            {
                return new ParticleMove(timeStartVal, timeStartVal + new FP.Vector(vecEndVal - vecStartVal).length() / speed, vecStartVal, vecEndVal);
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

        public struct Particle
        {
            public int type;
            public int matter;
            public long timeCohere; // earliest time at which it's safe to time travel
            public long timeEnd; // time annihilated
            public int n; // number of moves
            public ParticleMove[] m;
            public int mLive; // index of latest move that was live
            //public FP.Vector pos; // current position
            public int tileX, tileY; // current position on visibility tiles
            public bool coherent; // whether safe to time travel at simulation time

            public Particle(int typeVal, int matterVal, long startTime, FP.Vector startPos)
            {
                type = typeVal;
                matter = matterVal;
                timeCohere = long.MaxValue;
                timeEnd = long.MaxValue;
                n = 1;
                m = new ParticleMove[n];
                m[0] = new ParticleMove(startTime, startPos);
                mLive = 0;
                //pos = startPos;
                tileX = int.MinValue;
                tileY = int.MinValue;
                coherent = false;
            }

            public void setN(int newSize)
            {
                int i = 0;
                for (i = n; i < Math.Min(newSize, m.Length); i++)
                {
                    m[i] = new ParticleMove();
                }
                n = newSize;
                if (n > m.Length)
                    Array.Resize(ref m, n * 2);
            }

            public void addMove(ParticleMove newMove)
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
                    // put particle on visibility tiles for the first time
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

        public class MoveEvt : SimEvt // event in which particle moves between visibility tiles
        {
            public int particle;
            public int tileX, tileY; // new tile position, set to int.MinValue to keep current value

            public MoveEvt(long timeVal, int particleVal, int tileXVal, int tileYVal)
            {
                time = timeVal;
                particle = particleVal;
                tileX = tileXVal;
                tileY = tileYVal;
            }

            public override void apply()
            {
                int i, tXPrev, tYPrev, tX, tY, radius;
                radius = (int)(u.particleT[p[particle].type].visRadius >> FP.Precision);
                if (tileX == int.MinValue) tileX = p[particle].tileX;
                if (tileY == int.MinValue) tileY = p[particle].tileY;
                tXPrev = p[particle].tileX;
                tYPrev = p[particle].tileY;
                p[particle].tileX = tileX;
                p[particle].tileY = tileY;
                // add particle to visibility tiles
                for (tX = tileX - radius; tX <= tileX + radius; tX++)
                {
                    for (tY = tileY - radius; tY <= tileY + radius; tY++)
                    {
                        if (!inVis(tX - tXPrev, tY - tYPrev, u.particleT[p[particle].type].visRadius)
                            && inVis(tX - tileX, tY - tileY, u.particleT[p[particle].type].visRadius))
                        {
                            visAdd(particle, tX, tY, time);
                        }
                    }
                }
                // remove particle from visibility tiles
                for (tX = tXPrev - radius; tX <= tXPrev + radius; tX++)
                {
                    for (tY = tYPrev - radius; tY <= tYPrev + radius; tY++)
                    {
                        if (inVis(tX - tXPrev, tY - tYPrev, u.particleT[p[particle].type].visRadius)
                            && !inVis(tX - tileX, tY - tileY, u.particleT[p[particle].type].visRadius))
                        {
                            visRemove(particle, tX, tY, time);
                        }
                    }
                }
                // update whether this particle may time travel
                if (coherent(p[particle].matter, tileX, tileY, time) != p[particle].coherent)
                {
                    p[particle].coherent = !p[particle].coherent;
                    p[particle].timeCohere = p[particle].coherent ? time : long.MaxValue;
                }
                // if this particle moved out of another matter type's visibility, remove that matter type's visibility here
                if (tXPrev >= 0 && tXPrev < tileLen() && tYPrev >= 0 && tYPrev < tileLen()
                    && tileX >= 0 && tileX < tileLen() && tileY >= 0 && tileY < tileLen())
                {
                    for (i = 0; i < u.nMatterT; i++)
                    {
                        if (i != p[particle].matter && matterDirectVis(i, tXPrev, tYPrev) && !matterDirectVis(i, tileX, tileY))
                        {
                            for (tX = Math.Max(0, tileX - 1); tX <= Math.Min(tileLen() - 1, tileX + 1); tX++)
                            {
                                for (tY = Math.Max(0, tileY - 1); tY <= Math.Min(tileLen() - 1, tileY + 1); tY++)
                                {
                                    // TODO: use more accurate time at tiles other than (tileX, tileY)
                                    events.add(new MatterVisRemoveEvt(time, i, tX, tY));
                                }
                            }
                        }
                    }
                }
            }
        }

        public class MatterVisAddEvt : SimEvt // event in which matter type starts seeing a tile
        {
            public int matter;
            public int tileX, tileY;

            public MatterVisAddEvt(long timeVal, int matterVal, int tileXVal, int tileYVal)
            {
                time = timeVal;
                matter = matterVal;
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
                        if ((tX != tileX || tY != tileY) && matterVis[matter, tX, tY].Count % 2 == 0)
                        {
                            // TODO: use more accurate time
                            events.add(new MatterVisAddEvt(time - (1 << FP.Precision) / maxSpeed, matter, tX, tY));
                        }
                    }
                }
            }
        }

        public class MatterVisRemoveEvt : SimEvt // event in which matter type stops seeing a tile
        {
            public int matter;
            public int tileX, tileY;

            public MatterVisRemoveEvt(long timeVal, int matterVal, int tileXVal, int tileYVal)
            {
                time = timeVal;
                matter = matterVal;
                tileX = tileXVal;
                tileY = tileYVal;
            }

            public override void apply()
            {
                int i, tX, tY;
                if (matterVis[matter, tileX, tileY].Count % 2 == 1 && !matterDirectVis(matter, tileX, tileY))
                {
                    matterVis[matter, tileX, tileY].Add(time);
                    // check if a tile decohered for this matter type, or cohered for another matter type
                    for (i = 0; i < u.nMatterT; i++)
                    {
                        for (tX = Math.Max(0, tileX - (int)(maxVisRadius >> FP.Precision)); tX <= Math.Min(tileLen() - 1, tileX + (int)(maxVisRadius >> FP.Precision)); tX++)
                        {
                            for (tY = Math.Max(0, tileY - (int)(maxVisRadius >> FP.Precision)); tY <= Math.Min(tileLen() - 1, tileY + (int)(maxVisRadius >> FP.Precision)); tY++)
                            {
                                if (i == matter && coherence[i, tX, tY].Count % 2 == 1 && !calcCoherent(i, tX, tY, time))
                                {
                                    coherenceRemove(i, tX, tY, time);
                                }
                                else if (i != matter && coherence[i, tX, tY].Count % 2 == 0 && calcCoherent(i, tX, tY, time))
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
                            if ((tX != tileX || tY != tileY) && matterVis[matter, tX, tY].Count % 2 == 1)
                            {
                                // TODO: use more accurate time
                                events.add(new MatterVisRemoveEvt(time + (1 << FP.Precision) / maxSpeed, matter, tX, tY));
                            }
                        }
                    }
                }
            }
        }

        // universe variables
        public static Universe u;
        public static int nParticles;
        public static Particle[] p;

        // helper variables
        public static SimEvtList events;
        public static List<int>[,] particleVis;
        public static List<long>[, ,] matterVis;
        public static List<long>[, ,] coherence;
        public static long maxSpeed;
        public static long maxVisRadius;
        public static long timeSim;
        public static long timeSimLast;

        public static void setNParticles(int newSize)
        {
            nParticles = newSize;
            if (nParticles > p.Length)
                Array.Resize(ref p, nParticles * 2);
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
            for (i = 0; i < nParticles; i++)
            {
                if (p[i].mLive < p[i].n - 1)
                {
                    p[i].mLive = p[i].n - 1;
                    pos = p[i].calcPos(timeSimLast + 1);
                    events.add(new MoveEvt(timeSimLast + 1, i, (int)(pos.x >> FP.Precision), (int)(pos.y >> FP.Precision)));
                }
            }
            // check if particles moved between tiles
            for (i = 0; i < nParticles; i++)
            {
                p[i].addMoveEvts(ref events, i, timeSimLast, timeSim);
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
            // restore to last coherent/live state if particle moves off coherent area
            // TODO: choose check state times more intelligently
            for (i = 0; i < nParticles; i++)
            {
                if (curTime >= p[i].timeCohere && p[i].mLive < p[i].n - 1
                    && !coherent(p[i].matter, (int)(p[i].calcPos(curTime).x >> FP.Precision), (int)(p[i].calcPos(curTime).y >> FP.Precision), curTime))
                {
                    p[i].setN(p[i].mLive + 1);
                }
            }
        }

        private static void visAdd(int particle, int tileX, int tileY, long time)
        {
            int i, tX, tY;
            bool filled = true;
            if (tileX >= 0 && tileX < tileLen() && tileY >= 0 && tileY < tileLen())
            {
                // add particle to particle visibility tile
                particleVis[tileX, tileY].Add(particle);
                // TODO: use smarter matterVis adding algorithm (requires matterDirectVis to work in past)
                if (matterVis[p[particle].matter, tileX, tileY].Count % 2 == 0)
                {
                    matterVis[p[particle].matter, tileX, tileY].Add(time);
                    // check if a tile cohered for this matter type, or decohered for another matter type
                    for (i = 0; i < u.nMatterT; i++)
                    {
                        for (tX = Math.Max(0, tileX - (int)(maxVisRadius >> FP.Precision)); tX <= Math.Min(tileLen() - 1, tileX + (int)(maxVisRadius >> FP.Precision)); tX++)
                        {
                            for (tY = Math.Max(0, tileY - (int)(maxVisRadius >> FP.Precision)); tY <= Math.Min(tileLen() - 1, tileY + (int)(maxVisRadius >> FP.Precision)); tY++)
                            {
                                if (i == p[particle].matter && coherence[i, tX, tY].Count % 2 == 0 && calcCoherent(i, tX, tY, time))
                                {
                                    coherenceAdd(i, tX, tY, time);
                                }
                                else if (i != p[particle].matter && coherence[i, tX, tY].Count % 2 == 1 && !calcCoherent(i, tX, tY, time))
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
                            if (matterVis[p[particle].matter, tileX, tileY].Count % 2 == 0) filled = false;
                        }
                    }
                    //if (filled) events.add(new MatterVisAddEvt(time, p[particle].matter, tileX, tileY));
                }
            }
        }

        private static void visRemove(int particle, int tileX, int tileY, long time)
        {
            int tX, tY;
            long timeMatterVis = long.MaxValue;
            if (tileX >= 0 && tileX < tileLen() && tileY >= 0 && tileY < tileLen())
            {
                // remove particle from particle visibility tile
                particleVis[tileX, tileY].Remove(particle);
                // check if particle's matter type can't directly see this tile anymore
                if (matterVis[p[particle].matter, tileX, tileY].Count % 2 == 1 && !matterDirectVis(p[particle].matter, tileX, tileY))
                {
                    // find lowest time that surrounding tiles lost visibility
                    for (tX = Math.Max(0, tileX - 1); tX <= Math.Min(tileLen() - 1, tileX + 1); tX++)
                    {
                        for (tY = Math.Max(0, tileY - 1); tY <= Math.Min(tileLen() - 1, tileY + 1); tY++)
                        {
                            if ((tX != tileX || tY != tileY) && matterVis[p[particle].matter, tX, tY].Count % 2 == 0)
                            {
                                if (matterVis[p[particle].matter, tX, tY].Count == 0)
                                {
                                    timeMatterVis = long.MinValue;
                                }
                                else if (matterVis[p[particle].matter, tX, tY][matterVis[p[particle].matter, tX, tY].Count - 1] < timeMatterVis)
                                {
                                    timeMatterVis = matterVis[p[particle].matter, tX, tY][matterVis[p[particle].matter, tX, tY].Count - 1];
                                }
                            }
                        }
                    }
                    // if matter type can't see all neighboring tiles, they won't be able to tell if another player's particle moves into this tile
                    // so remove this tile's visibility for this matter type
                    if (timeMatterVis != long.MaxValue)
                    {
                        timeMatterVis = Math.Max(time, timeMatterVis + (1 << FP.Precision) / maxSpeed); // TODO: use more accurate time
                        events.add(new MatterVisRemoveEvt(timeMatterVis, p[particle].matter, tileX, tileY));
                    }
                }
            }
        }

        private static void coherenceAdd(int matter, int tX, int tY, long time)
        {
            if (coherence[matter, tX, tY].Count % 2 == 0)
            {
                coherence[matter, tX, tY].Add(time);
                // particles of this matter type on this tile may time travel starting now
                // TODO: actually safe to time travel at earlier times, as long as particle of same type is at same place when decoheres
                for (int i = 0; i < nParticles; i++)
                {
                    if (matter == p[i].matter && tX == p[i].tileX && tY == p[i].tileY && !p[i].coherent)
                    {
                        p[i].coherent = true;
                        p[i].timeCohere = time;
                    }
                }
            }
            else
            {
                throw new InvalidOperationException("may not add coherence to tile that is already coherent");
            }
        }

        private static void coherenceRemove(int matter, int tX, int tY, long time)
        {
            if (coherence[matter, tX, tY].Count % 2 == 1)
            {
                coherence[matter, tX, tY].Add(time);
                // particles of this matter type on this tile may not time travel starting now
                for (int i = 0; i < nParticles; i++)
                {
                    if (matter == p[i].matter && tX == p[i].tileX && tY == p[i].tileY && p[i].coherent)
                    {
                        p[i].coherent = false;
                        p[i].timeCohere = long.MaxValue;
                    }
                }
            }
            else
            {
                throw new InvalidOperationException("may not remove coherence from tile that is already not coherent");
            }
        }

        // returns if the specified tile is in the direct line of sight of a particle of specified matter type
        public static bool matterDirectVis(int matter, int tX, int tY)
        {
            for (int i = 0; i < particleVis[tX, tY].Count; i++)
            {
                if (matter == p[particleVis[tX, tY][i]].matter) return true;
            }
            return false;
        }

        // returns if the specified tile is either in the direct line of sight for specified matter type at specified time,
        // or if matter type can infer that particles of other matter types aren't in specified tile at specified time
        public static bool matterVisWhen(int matter, int tX, int tY, long time)
        {
            for (int i = matterVis[matter, tX, tY].Count - 1; i >= 0; i--)
            {
                if (time >= matterVis[matter, tX, tY][i]) return i % 2 == 0;
            }
            return false;
        }

        // returns if it is impossible for other matter's particles to see this location
        // this isn't quite the actual definition of coherence, but this is an important concept in the game and I need a name for it
        public static bool coherent(int matter, int tX, int tY, long time)
        {
            for (int i = coherence[matter, tX, tY].Count - 1; i >= 0; i--)
            {
                if (time >= coherence[matter, tX, tY][i]) return i % 2 == 0;
            }
            return false;
        }

        // calculates from matter visibility tiles if it is impossible for other matter's particles to see this location
        public static bool calcCoherent(int matter, int tileX, int tileY, long time)
        {
            int i, tX, tY;
            // check that matter type can see all nearby tiles
            for (tX = Math.Max(0, tileX - (int)(maxVisRadius >> FP.Precision)); tX <= Math.Min(tileLen() - 1, tileX + (int)(maxVisRadius >> FP.Precision)); tX++)
            {
                for (tY = Math.Max(0, tileY - (int)(maxVisRadius >> FP.Precision)); tY <= Math.Min(tileLen() - 1, tileY + (int)(maxVisRadius >> FP.Precision)); tY++)
                {
                    if (inVis(tX - tileX, tY - tileY, maxVisRadius) && !matterVisWhen(matter, tX, tY, time)) return false;
                }
            }
            // check that no particles of different matter can see this tile
            for (i = 0; i < u.nMatterT; i++)
            {
                if (i != matter && matterVisWhen(i, tileX, tileY, time)) return false;
            }
            return true;
        }

        public static bool inVis(long tX, long tY, long visRadius)
        {
            //return Math.Max(Math.Abs(tX), Math.Abs(tY)) <= (int)(visRadius >> FP.Precision);
            return new FP.Vector(tX << FP.Precision, tY << FP.Precision).lengthSq() <= visRadius * visRadius;
        }

        public static int tileLen() // TODO: use particleVis.GetUpperBound instead of this function
        {
            return (int)((u.mapSize >> FP.Precision) + 1);
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
