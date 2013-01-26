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
            public string sndSelect;
            public string sndMove;
            public string sndAnniCmd;
            public string sndAnnihilate;
            public long speed;
            public long visRadius;
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
            public Color4 visCol;
            public Color4 coherentCol;
            public string music;
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
                : this(tmVal, tmVal, vecVal, vecVal)
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
        }

        public struct Particle
        {
            public int type;
            public int matter;
            public FP.Vector pos; // current position
            public long tmEnd; // time annihilated
            public int n; // number of moves
            public ParticleMove[] m;

            public Particle(int typeVal, int matterVal, FP.Vector startPos)
            {
                type = typeVal;
                matter = matterVal;
                pos = startPos;
                tmEnd = long.MaxValue;
                n = 1;
                m = new ParticleMove[n];
                m[0] = new ParticleMove(0, startPos);
                pos = startPos;
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

        // game variables
        public static Scenario g;
        public static int nParticles;
        public static Particle[] p;

        // helper variables
        public static List<int>[,] curVis;
        public static long timeSim;
        public static long timeSimLast;

        public static void setNParticles(int newSize)
        {
            nParticles = newSize;
            if (nParticles > p.Length)
                Array.Resize(ref p, nParticles * 2);
        }

        public static void initCurVis(long time)
        {
            int i, tX, tY;
            for (tX = 0; tX < tileLen(); tX++)
            {
                for (tY = 0; tY < tileLen(); tY++)
                {
                    curVis[tX, tY].Clear();
                }
            }
            for (i = 0; i < nParticles; i++)
            {
                p[i].pos = p[i].calcPos(time);
                for (tX = Math.Max(0, (int)((p[i].pos.x - g.particleT[p[i].type].visRadius) >> FP.Precision)); tX <= Math.Min(tileLen() - 1, (int)((p[i].pos.x + g.particleT[p[i].type].visRadius) >> FP.Precision)); tX++)
                {
                    for (tY = Math.Max(0, (int)((p[i].pos.y - g.particleT[p[i].type].visRadius) >> FP.Precision)); tY <= Math.Min(tileLen() - 1, (int)((p[i].pos.y + g.particleT[p[i].type].visRadius) >> FP.Precision)); tY++)
                    {
                        curVis[tX, tY].Add(i);
                    }
                }
            }
        }

        public static bool matterCurVis(int matter, int tX, int tY)
        {
            for (int i = 0; i < curVis[tX, tY].Count; i++)
            {
                if (matter == p[curVis[tX, tY][i]].matter) return true;
            }
            return false;
        }

        public static int tileLen() // TODO: use curVis.GetUpperBound instead of this function
        {
            return (int)((g.mapSize >> FP.Precision) + 1);
        }
    }
}
