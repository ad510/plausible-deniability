// particles engine
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
            public Color4 visCol;
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
                if (time >= timeEnd)
                    return vecEnd;
                return vecStart + (vecEnd - vecStart) * FP.div((time - timeStart), (timeEnd - timeStart));
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
            public int tX, tY; // current position on visibility tiles

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
                tX = (int)(startPos.x >> FP.Precision);
                tY = (int)(startPos.y >> FP.Precision);
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
                long time;
                int i2, tX, tY, dir;
                moveLast = Math.Max(0, getMove(timeMin));
                move = getMove(timeMax);
                if (move < 0) return;
                for (i2 = moveLast; i2 <= move; i2++)
                {
                    posLast = (i2 == moveLast) ? m[i2].calcPos(timeMin) : m[i2].vecStart;
                    pos = (i2 == move) ? m[i2].calcPos(timeMax) : m[i2 + 1].vecStart;
                    // moving between columns (x)
                    dir = (pos.x >= posLast.x) ? 0 : -1;
                    for (tX = (int)(Math.Min(pos.x, posLast.x) >> FP.Precision) + 1; tX <= (int)(Math.Max(pos.x, posLast.x) >> FP.Precision); tX++)
                    {
                        time = m[i2].timeAtX(tX << FP.Precision);
                        events.add(new MoveEvt(time, id, tX + dir, (dir == 0) ? 1 : -1, 0));
                    }
                    // moving between rows (y)
                    dir = (pos.y >= posLast.y) ? 0 : -1;
                    for (tY = (int)(Math.Min(pos.y, posLast.y) >> FP.Precision) + 1; tY <= (int)(Math.Max(pos.y, posLast.y) >> FP.Precision); tY++)
                    {
                        time = m[i2].timeAtY(tY << FP.Precision);
                        events.add(new MoveEvt(time, id, tY + dir, 0, (dir == 0) ? 1 : -1));
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
        }

        public class MoveEvt : SimEvt // event in which particle moves between tiles
        {
            public int particle;
            public int tileCoor;
            public int dirX, dirY;

            public MoveEvt(long timeVal, int particleVal, int tileCoorVal, int dirXVal, int dirYVal)
            {
                time = timeVal;
                particle = particleVal;
                tileCoor = tileCoorVal;
                dirX = dirXVal;
                dirY = dirYVal;
            }

            public override void apply()
            {
                int tX, tY, radius;
                radius = (int)(u.particleT[p[particle].type].visRadius >> FP.Precision);
                if (dirX != 0) p[particle].tX = tileCoor;
                if (dirY != 0) p[particle].tY = tileCoor;
                for (tX = p[particle].tX - radius - Math.Max(dirX, 0); tX <= p[particle].tX + radius - Math.Min(dirX, 0); tX++)
                {
                    for (tY = p[particle].tY - radius - Math.Max(dirY, 0); tY <= p[particle].tY + radius - Math.Min(dirY, 0); tY++)
                    {
                        if (inVis(tX - p[particle].tX, tY - p[particle].tY, u.particleT[p[particle].type].visRadius) && !inVis(tX - p[particle].tX + dirX, tY - p[particle].tY + dirY, u.particleT[p[particle].type].visRadius))
                        {
                            visAdd(particle, tX, tY, time);
                        }
                        else if (!inVis(tX - p[particle].tX, tY - p[particle].tY, u.particleT[p[particle].type].visRadius) && inVis(tX - p[particle].tX + dirX, tY - p[particle].tY + dirY, u.particleT[p[particle].type].visRadius))
                        {
                            visRemove(particle, tX, tY, time);
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
        public static List<int>[,] particleVis;
        public static List<long>[,,] matterVis;
        public static long maxVisRadius;
        public static long timeSim;
        public static long timeSimLast;

        public static void setNParticles(int newSize)
        {
            nParticles = newSize;
            if (nParticles > p.Length)
                Array.Resize(ref p, nParticles * 2);
        }

        public static void initVis(long time, bool initMatterVis)
        {
            int i, tX, tY;
            for (tX = 0; tX < tileLen(); tX++)
            {
                for (tY = 0; tY < tileLen(); tY++)
                {
                    particleVis[tX, tY].Clear();
                }
            }
            for (i = 0; i < nParticles; i++)
            {
                initVis(i, time, initMatterVis);
            }
        }

        private static void initVis(int particle, long time, bool initMatterVis)
        {
            FP.Vector pos;
            int tX, tY;
            pos = p[particle].calcPos(time);
            p[particle].tX = (int)(pos.x >> FP.Precision);
            p[particle].tY = (int)(pos.y >> FP.Precision);
            for (tX = Math.Max(0, (int)((pos.x >> FP.Precision) - (u.particleT[p[particle].type].visRadius >> FP.Precision))); tX <= Math.Min(tileLen() - 1, (int)((pos.x >> FP.Precision) + (u.particleT[p[particle].type].visRadius >> FP.Precision))); tX++)
            {
                for (tY = Math.Max(0, (int)((pos.y >> FP.Precision) - (u.particleT[p[particle].type].visRadius >> FP.Precision))); tY <= Math.Min(tileLen() - 1, (int)((pos.y >> FP.Precision) + (u.particleT[p[particle].type].visRadius >> FP.Precision))); tY++)
                {
                    if (inVis(tX - (int)(pos.x >> FP.Precision), tY - (int)(pos.y >> FP.Precision), u.particleT[p[particle].type].visRadius))
                    {
                        particleVis[tX, tY].Add(particle);
                        if (initMatterVis && matterVis[p[particle].matter, tX, tY].Count % 2 == 0) matterVis[p[particle].matter, tX, tY].Add(time);
                    }
                }
            }
        }

        public static void update(long curTime)
        {
            SimEvtList events = new SimEvtList();
            SimEvt evt;
            int i, tX, tY;
            // do timing
            if (curTime < timeSim)
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
                    // TODO: optimize this
                    for (tX = 0; tX < tileLen(); tX++)
                    {
                        for (tY = 0; tY < tileLen(); tY++)
                        {
                            if (particleVis[tX, tY].Contains(i)) visRemove(i, tX, tY, timeSimLast);
                        }
                    }
                    initVis(i, timeSimLast, true);
                }
            }
            // check if particles moved between tiles
            for (i = 0; i < nParticles; i++)
            {
                p[i].mLive = p[i].n - 1; // TODO: set this the moment it goes live?
                p[i].addMoveEvts(ref events, i, timeSimLast, timeSim);
            }
            // apply simulation events
            while ((evt = events.pop()) != null)
            {
                evt.apply();
            }
            // update earliest times it's safe for each particle to time travel
            // TODO: choose check state times more intelligently
            // TODO: actually safe to time travel at earlier times, as long as particle of same type is at same place when decoheres
            for (i = 0; i < nParticles; i++)
            {
                if ((timeSimLast < p[i].m[0].timeStart || !coherent(p[i].matter, (int)(p[i].calcPos(timeSimLast).x >> FP.Precision), (int)(p[i].calcPos(timeSimLast).y >> FP.Precision), timeSimLast))
                    && (timeSim >= p[i].m[0].timeStart && coherent(p[i].matter, (int)(p[i].calcPos(timeSim).x >> FP.Precision), (int)(p[i].calcPos(timeSim).y >> FP.Precision), timeSim)))
                {
                    p[i].timeCohere = timeSim;
                }
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

        private static void visAdd(int particle, int tX, int tY, long time)
        {
            if (tX >= 0 && tX < tileLen() && tY >= 0 && tY < tileLen())
            {
                particleVis[tX, tY].Add(particle);
                if (matterVis[p[particle].matter, tX, tY].Count % 2 == 0) matterVis[p[particle].matter, tX, tY].Add(time);
            }
        }

        private static void visRemove(int particle, int tX, int tY, long time)
        {
            if (tX >= 0 && tX < tileLen() && tY >= 0 && tY < tileLen())
            {
                particleVis[tX, tY].Remove(particle);
                // TODO: use smarter matterVis removing algorithm described in notes.txt
                if (matterVis[p[particle].matter, tX, tY].Count % 2 == 1 && !matterDirectVis(p[particle].matter, tX, tY)) matterVis[p[particle].matter, tX, tY].Add(time);
            }
        }

        public static bool matterDirectVis(int matter, int tX, int tY)
        {
            for (int i = 0; i < particleVis[tX, tY].Count; i++)
            {
                if (matter == p[particleVis[tX, tY][i]].matter) return true;
            }
            return false;
        }

        public static bool matterVisWhen(int matter, int tX, int tY, long time)
        {
            for (int i = matterVis[matter, tX, tY].Count - 1; i >= 0; i--)
            {
                if (time >= matterVis[matter, tX, tY][i]) return i % 2 == 0;
            }
            return false;
        }

        // returns if it is impossible for other matter's particles to see this location
        // this isn't really the definition of coherence, but it's close enough for me
        public static bool coherent(int matter, int tileX, int tileY, long time)
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
