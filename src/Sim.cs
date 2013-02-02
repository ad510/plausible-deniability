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
            public long tmStart; // time when starts moving
            public long tmEnd; // time when finishes moving
            public FP.Vector vecStart; // z indicates rotation
            public FP.Vector vecEnd;

            public ParticleMove(long tmStartVal, long tmEndVal, FP.Vector vecStartVal, FP.Vector vecEndVal)
            {
                tmStart = tmStartVal;
                tmEnd = tmEndVal;
                vecStart = vecStartVal;
                vecEnd = vecEndVal;
            }

            public ParticleMove(long tmVal, FP.Vector vecVal)
                : this(tmVal, tmVal + 1, vecVal, vecVal)
            {
            }

            public static ParticleMove fromSpeed(long tmStartVal, long speed, FP.Vector vecStartVal, FP.Vector vecEndVal)
            {
                return new ParticleMove(tmStartVal, tmStartVal + new FP.Vector(vecEndVal - vecStartVal).length() / speed, vecStartVal, vecEndVal);
            }

            public FP.Vector calcPos(long time) // returns location at specified time
            {
                if (time >= tmEnd)
                    return vecEnd;
                return vecStart + (vecEnd - vecStart) * FP.div((time - tmStart), (tmEnd - tmStart));
            }

            public long timeAtX(long x)
            {
                return lineCalcX(new FP.Vector(tmStart, vecStart.x), new FP.Vector(tmEnd, vecEnd.x), x);
            }

            public long timeAtY(long y)
            {
                return lineCalcX(new FP.Vector(tmStart, vecStart.y), new FP.Vector(tmEnd, vecEnd.y), y);
            }
        }

        public struct Particle
        {
            public int type;
            public int matter;
            public long tmCohere; // earliest time at which it's safe to time travel
            public long tmEnd; // time annihilated
            public int n; // number of moves
            public ParticleMove[] m;
            public int mLive; // index of latest move that was live
            public FP.Vector pos; // current position
            public int tX, tY; // current position on visibility tiles

            public Particle(int typeVal, int matterVal, long startTime, FP.Vector startPos)
            {
                type = typeVal;
                matter = matterVal;
                tmCohere = long.MaxValue;
                tmEnd = long.MaxValue;
                n = 1;
                m = new ParticleMove[n];
                m[0] = new ParticleMove(startTime, startPos);
                mLive = 0;
                pos = startPos;
                tX = (int)(pos.x >> FP.Precision);
                tY = (int)(pos.y >> FP.Precision);
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
                if (newMove.tmStart >= timeSim) mLive = n - 1;
            }

            public FP.Vector calcPos(long time)
            {
                return m[moveGet(time)].calcPos(time);
            }

            public int moveGet(long time)
            {
                int ret = n - 1;
                while (ret >= 0 && time < m[ret].tmStart) ret--;
                return ret;
            }
        }

        public struct TileMove
        {
            public int particle;
            public int tX, tY;
            public int dir; // 0 = +x, 1 = +y, 2 = -x, 3 = -y

            public TileMove(int particleVal, int tXVal, int tYVal, int dirVal)
            {
                particle = particleVal;
                tX = tXVal;
                tY = tYVal;
                dir = dirVal;
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

        public static void initVis(int particle, long time, bool initMatterVis)
        {
            FP.Vector pos;
            int tX, tY;
            pos = p[particle].calcPos(time);
            for (tX = Math.Max(0, (int)((pos.x >> FP.Precision) - (u.particleT[p[particle].type].visRadius >> FP.Precision))); tX <= Math.Min(tileLen() - 1, (int)((pos.x >> FP.Precision) + (u.particleT[p[particle].type].visRadius >> FP.Precision))); tX++)
            {
                for (tY = Math.Max(0, (int)((pos.y >> FP.Precision) - (u.particleT[p[particle].type].visRadius >> FP.Precision))); tY <= Math.Min(tileLen() - 1, (int)((pos.y >> FP.Precision) + (u.particleT[p[particle].type].visRadius >> FP.Precision))); tY++)
                {
                    particleVis[tX, tY].Add(particle);
                    if (initMatterVis && matterVis[p[particle].matter, tX, tY].Count % 2 == 0) matterVis[p[particle].matter, tX, tY].Add(time);
                }
            }
        }

        public static void update(long curTime)
        {
            Dictionary<long, TileMove> tileMoves = new Dictionary<long,TileMove>(); // TODO: don't use dictionary b/c it doesn't allow duplicate keys
            int move, moveLast;
            FP.Vector pos, posLast;
            long time;
            int i, i2, tX, tY, id, radius, dir;
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
                moveLast = Math.Max(0, p[i].moveGet(timeSimLast));
                move = p[i].moveGet(timeSim);
                if (move < 0) continue;
                for (i2 = moveLast; i2 <= move; i2++)
                {
                    posLast = (i2 == moveLast) ? p[i].m[i2].calcPos(timeSimLast) : p[i].m[i2].vecStart;
                    pos = (i2 == move) ? p[i].m[i2].calcPos(timeSim) : p[i].m[i2 + 1].vecStart;
                    // moving between columns (x)
                    dir = (pos.x >= posLast.x) ? 0 : -1;
                    for (tX = (int)(Math.Min(pos.x, posLast.x) >> FP.Precision) + 1; tX <= (int)(Math.Max(pos.x, posLast.x) >> FP.Precision); tX++)
                    {
                        time = p[i].m[i2].timeAtX(tX << FP.Precision);
                        while (tileMoves.ContainsKey(time)) time++; // TODO: this is a hack to prevent duplicate keys
                        tileMoves.Add(time, new TileMove(i, tX + dir, (int)(p[i].m[i2].calcPos(time).y >> FP.Precision), (dir == 0) ? 0 : 2));
                    }
                    // moving between rows (y)
                    dir = (pos.y >= posLast.y) ? 0 : -1;
                    for (tY = (int)(Math.Min(pos.y, posLast.y) >> FP.Precision) + 1; tY <= (int)(Math.Max(pos.y, posLast.y) >> FP.Precision); tY++)
                    {
                        time = p[i].m[i2].timeAtY(tY << FP.Precision);
                        while (tileMoves.ContainsKey(time)) time++; // TODO: this is a hack to prevent duplicate keys
                        tileMoves.Add(time, new TileMove(i, (int)(p[i].m[i2].calcPos(time).x >> FP.Precision), tY + dir, (dir == 0) ? 1 : 3));
                    }
                }
            }
            // add and remove particles from visibility tiles
            // TODO: still has some problems, use this line to see them:
            // if (DX.timeNow - DX.timeStart >= Sim.timeSim + 1000) Sim.update(DX.timeNow - DX.timeStart);
            foreach (KeyValuePair<long, TileMove> item in tileMoves)
            {
                id = item.Value.particle;
                radius = (int)(u.particleT[p[id].type].visRadius >> FP.Precision);
                if (item.Value.dir == 0) // +x
                {
                    p[id].tX = item.Value.tX;
                    for (tY = Math.Max(0, p[id].tY - radius); tY <= Math.Min(tileLen() - 1, p[id].tY + radius); tY++)
                    {
                        visAdd(id, p[id].tX + radius, tY, item.Key);
                        visRemove(id, p[id].tX - radius - 1, tY, item.Key);
                    }
                }
                else if (item.Value.dir == 1) // +y
                {
                    p[id].tY = item.Value.tY;
                    for (tX = Math.Max(0, p[id].tX - radius); tX <= Math.Min(tileLen() - 1, p[id].tX + radius); tX++)
                    {
                        visAdd(id, tX, p[id].tY + radius, item.Key);
                        visRemove(id, tX, p[id].tY - radius - 1, item.Key);
                    }
                }
                else if (item.Value.dir == 2) // -x
                {
                    p[id].tX = item.Value.tX;
                    for (tY = Math.Max(0, p[id].tY - radius); tY <= Math.Min(tileLen() - 1, p[id].tY + radius); tY++)
                    {
                        visAdd(id, p[id].tX - radius, tY, item.Key);
                        visRemove(id, p[id].tX + radius + 1, tY, item.Key);
                    }
                }
                else if (item.Value.dir == 3) // -y
                {
                    p[id].tY = item.Value.tY;
                    for (tX = Math.Max(0, p[id].tX - radius); tX <= Math.Min(tileLen() - 1, p[id].tX + radius); tX++)
                    {
                        visAdd(id, tX, p[id].tY - radius, item.Key);
                        visRemove(id, tX, p[id].tY + radius + 1, item.Key);
                    }
                }
            }
            // update earliest times it's safe for each particle to time travel
            // TODO: choose check state times more intelligently
            // TODO: actually safe to time travel at earlier times, as long as particle of same type is at same place when decoheres
            for (i = 0; i < nParticles; i++)
            {
                if ((timeSimLast < p[i].m[0].tmStart || !coherent(p[i].matter, (int)(p[i].calcPos(timeSimLast).x >> FP.Precision), (int)(p[i].calcPos(timeSimLast).y >> FP.Precision), timeSimLast))
                    && (timeSim >= p[i].m[0].tmStart && coherent(p[i].matter, (int)(p[i].calcPos(timeSim).x >> FP.Precision), (int)(p[i].calcPos(timeSim).y >> FP.Precision), timeSim)))
                {
                    p[i].tmCohere = timeSim;
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
                if (curTime >= p[i].tmCohere && p[i].mLive < p[i].n - 1
                    && !coherent(p[i].matter, (int)(p[i].calcPos(curTime).x >> FP.Precision), (int)(p[i].calcPos(curTime).y >> FP.Precision), curTime))
                {
                    p[i].setN(p[i].mLive + 1);
                }
            }
        }

        public static void visAdd(int particle, int tX, int tY, long time)
        {
            if (tX >= 0 && tX < tileLen() && tY >= 0 && tY < tileLen())
            {
                particleVis[tX, tY].Add(particle);
                if (matterVis[p[particle].matter, tX, tY].Count % 2 == 0) matterVis[p[particle].matter, tX, tY].Add(time);
            }
        }

        public static void visRemove(int particle, int tX, int tY, long time)
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
                    if (!matterVisWhen(matter, tX, tY, time)) return false;
                }
            }
            // check that no particles of different matter can see this tile
            for (i = 0; i < u.nMatterT; i++)
            {
                if (i != matter && matterVisWhen(i, tileX, tileY, time)) return false;
            }
            return true;
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
