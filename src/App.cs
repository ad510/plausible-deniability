// game form
// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SlimDX;
using SlimDX.D3DCompiler;
using SlimDX.Direct3D9;
using SlimDX.Windows;
using SlimDX.DirectSound;
using SlimDX.DirectInput;

namespace Decoherence
{
    public partial class App : Form
    {
        public const string ErrStr = ". The program will exit now."; // display this during an initialization or drawing crash
        public const double SelBoxMin = 100;

        string appPath;
        string modPath = "mod\\";
        SlimDX.Direct3D9.Device d3dOriginalDevice;
        int runMode;
        float winDiag;
        DX.Poly2D tlPoly;
        DX.Img2D testImg;
        DX.Img2D[] imgParticle;
        Random rand;
        List<int> selParticles;

        public App()
        {
            InitializeComponent();
        }

        private void App_Load(object sender, EventArgs e)
        {
            int i;
            /*System.Collections.Hashtable jsonObj;
            string str;
            bool b = false;*/
            appPath = Application.ExecutablePath.Substring(0, Application.ExecutablePath.LastIndexOf("bin\\"));
            rand = new Random();
            if (!DX.init(this.Handle, true))
            {
                MessageBox.Show("Couldn't set up DirectX. Make sure your video and audio drivers are up-to-date" + ErrStr + "\n\nError description: " + DX.dxErr);
                Application.Exit();
                return;
            }
            DX.setDefaultRes();
            this.Width = DX.mode.Width;
            this.Height = DX.mode.Height;
            winDiag = new Vector2(DX.mode.Width, DX.mode.Height).Length();
            this.Show();
            this.Focus();
            if (!DX.init3d(out d3dOriginalDevice, this.Handle, DX.mode.Width, DX.mode.Height, DX.mode.Format, new Vector3(), new Vector3(), (float)(Math.PI / 4), 1000))
            {
                MessageBox.Show("Couldn't set up Direct3D. Make sure your video and audio drivers are up-to-date and that no other programs are currently using DirectX" + ErrStr + "\n\nError description: " + DX.dxErr);
                Application.Exit();
                return;
            }
            // load scn (hard code for now)
            Sim.g.mapSize = 5 << FP.Precision;
            Sim.g.camSpeed = (2 << FP.Precision) / 1000;
            Sim.g.camPos = new FP.Vector(Sim.g.mapSize / 2, Sim.g.mapSize / 2, 0);
            Sim.g.drawScl = 0.1f;
            Sim.g.drawSclMin = 0.01f;
            Sim.g.drawSclMin = 1f;
            Sim.g.backCol = new Color4(0, 0, 0);
            Sim.g.borderCol = new Color4(1, 0.5f, 0);
            Sim.g.nMatterT = 2;
            Sim.g.matterT = new Sim.MatterType[Sim.g.nMatterT];
            Sim.g.matterT[0].name = "System of matter";
            Sim.g.matterT[0].isPlayer = true;
            Sim.g.matterT[0].player = 1;
            Sim.g.matterT[0].annihilates = new bool[Sim.g.nMatterT];
            Sim.g.matterT[0].annihilates[0] = false;
            Sim.g.matterT[0].annihilates[1] = true;
            Sim.g.matterT[1].name = "System of antimatter";
            Sim.g.matterT[1].isPlayer = true;
            Sim.g.matterT[1].player = 0;
            Sim.g.matterT[1].annihilates = new bool[Sim.g.nMatterT];
            Sim.g.matterT[1].annihilates[0] = true;
            Sim.g.matterT[1].annihilates[1] = false;
            Sim.g.nParticleT = 1;
            Sim.g.particleT = new Sim.ParticleType[Sim.g.nParticleT];
            imgParticle = new DX.Img2D[Sim.g.nParticleT];
            Sim.g.particleT[0].name = "Electron";
            Sim.g.particleT[0].imgPath = "test.png";
            Sim.g.particleT[0].speed = (1 << FP.Precision) / 1000;
            Sim.g.particleT[0].selRadius = 16;
            imgParticle[0].init();
            if (!imgParticle[0].open(appPath + modPath + Sim.g.particleT[0].imgPath, Color.White.ToArgb())) MessageBox.Show("Warning: failed to load " + modPath + Sim.g.particleT[0].imgPath);
            imgParticle[0].rotCenter.X = imgParticle[0].srcWidth / 2;
            imgParticle[0].rotCenter.Y = imgParticle[0].srcHeight / 2;
            Sim.nParticles = 5;
            Sim.p = new Sim.Particle[Sim.nParticles];
            for (i = 0; i < Sim.nParticles; i++)
            {
                Sim.p[i] = new Sim.Particle(0, 0, new FP.Vector((long)(rand.NextDouble() * Sim.g.mapSize), (long)(rand.NextDouble() * Sim.g.mapSize)));
            }
            selParticles = new List<int>();
            DX.timeNow = Environment.TickCount;
            DX.timeStart = DX.timeNow;
            /*str = new System.IO.StreamReader(appPath + modPath + "scn.json").ReadToEnd();
            jsonObj = (System.Collections.Hashtable)Procurios.Public.JSON.JsonDecode(str, ref b);
            if (!b)
            {
                MessageBox.Show("Scenario failed to load" + ErrStr);
                this.Close();
                return;
            }
            foreach (System.Collections.DictionaryEntry en in jsonObj)
            {
                if ((string)en.Key == "key")
                {
                    Sim.g.key = (long)en.Value;
                }
            }*/
            // set up test image
            testImg.init(new Color4(0.5f, 1f, 1f, 1f).ToArgb());
            if (!testImg.open(appPath + modPath + "test.png", System.Drawing.Color.White.ToArgb())) MessageBox.Show("Warning: Failed to load test.png");
            testImg.pos.X = DX.sx / 2;
            testImg.pos.Y = DX.sy / 2;
            testImg.rotCenter.X = testImg.srcWidth / 2;
            testImg.rotCenter.Y = testImg.srcHeight / 2;
            runMode = 1;
            gameLoop();
            this.Close();
        }

        private void App_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && runMode > 0)
            {
                runMode = 0;
                e.Cancel = true;
            }
        }

        private void App_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            DX.mouseDblClk();
        }

        private void App_MouseDown(object sender, MouseEventArgs e)
        {
            int button = (int)e.Button / 0x100000;
            DX.mouseDown(button, e.X, e.Y);
        }

        private void App_MouseMove(object sender, MouseEventArgs e)
        {
            int button = (int)e.Button / 0x100000;
            int i;
            i = DX.mouseMove(button, e.X, e.Y);
            if (i != -1)
            {
                if (DX.mouseState[i] == 0)
                {
                    App_MouseUp(this, new System.Windows.Forms.MouseEventArgs((MouseButtons)(button * 0x100000), 0, e.X, e.Y, 0));
                }
                else
                {
                    App_MouseDown(this, new System.Windows.Forms.MouseEventArgs((MouseButtons)(button * 0x100000), 0, e.X, e.Y, 0));
                }
            }
        }

        private void App_MouseUp(object sender, MouseEventArgs e)
        {
            int button = (int)e.Button / 0x100000;
            int mousePrevState = DX.mouseState[button];
            FP.Vector mouseSimPos = drawToSimPos(new Vector3(e.X, e.Y, 0));
            int i;
            DX.mouseUp(button, e.X, e.Y);
            if (button == 1) // select
            {
                selParticles.Clear();
                for (i = 0; i < Sim.nParticles; i++)
                {
                    if ((simToDrawPos(Sim.p[i].calcPos(DX.timeNow - DX.timeStart)) - new Vector3(DX.mouseX, DX.mouseY, 0)).LengthSquared() <= Math.Pow(Sim.g.particleT[Sim.p[i].type].selRadius, 2))
                    {
                        selParticles.Add(i);
                        break;
                    }
                }
            }
            else if (button == 2) // move
            {
                if (mouseSimPos.x >= 0 && mouseSimPos.x <= Sim.g.mapSize && mouseSimPos.y >= 0 && mouseSimPos.y <= Sim.g.mapSize)
                {
                    foreach (int id in selParticles)
                    {
                        Sim.p[id].addMove(Sim.ParticleMove.fromSpeed(DX.timeNow - DX.timeStart, Sim.g.particleT[Sim.p[id].type].speed, Sim.p[id].calcPos(DX.timeNow - DX.timeStart), mouseSimPos));
                    }
                }
            }
        }

        private void gameLoop()
        {
            while (runMode == 1)
            {
                DX.doEventsX();
                inputHandle();
                draw();
            }
        }

        private void inputHandle()
        {
            int i;
            DX.keyboardUpdate();
            // handle changed keys
            for (i = 0; i < DX.diKeysChanged.Count; i++)
            {
                if (DX.diKeysChanged[i] == Key.Escape && DX.diKeyState.IsPressed(DX.diKeysChanged[i]))
                {
                    App_KeyDown(this, new System.Windows.Forms.KeyEventArgs(Keys.Escape));
                }
            }
            // move camera
            if (DX.diKeyState.IsPressed(Key.LeftArrow) || DX.mouseX == 0 || (this.Left > 0 && DX.mouseX <= 15))
            {
                Sim.g.camPos.x -= Sim.g.camSpeed * (DX.timeNow - DX.timeLast);
                if (Sim.g.camPos.x < 0) Sim.g.camPos.x = 0;
            }
            if (DX.diKeyState.IsPressed(Key.RightArrow) || DX.mouseX == DX.sx - 1 || (this.Left + this.Width < Screen.PrimaryScreen.Bounds.Width && DX.mouseX >= DX.sx - 15))
            {
                Sim.g.camPos.x += Sim.g.camSpeed * (DX.timeNow - DX.timeLast);
                if (Sim.g.camPos.x > Sim.g.mapSize) Sim.g.camPos.x = Sim.g.mapSize;
            }
            if (DX.diKeyState.IsPressed(Key.UpArrow) || DX.mouseY == 0 || (this.Top > 0 && DX.mouseY <= 15))
            {
                Sim.g.camPos.y -= Sim.g.camSpeed * (DX.timeNow - DX.timeLast);
                if (Sim.g.camPos.y < 0) Sim.g.camPos.y = 0;
            }
            if (DX.diKeyState.IsPressed(Key.DownArrow) || DX.mouseY == DX.sy - 1 || (this.Top + this.Height < Screen.PrimaryScreen.Bounds.Height && DX.mouseY >= DX.sy - 15))
            {
                Sim.g.camPos.y += Sim.g.camSpeed * (DX.timeNow - DX.timeLast);
                if (Sim.g.camPos.y > Sim.g.mapSize) Sim.g.camPos.y = Sim.g.mapSize;
            }
        }

        private void draw()
        {
            int i;
            Vector3 vec, vec2;
            DX.d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Sim.g.backCol, 1, 0);
            DX.d3dDevice.BeginScene();
            // test image
            testImg.scl = new Vector3((float)Math.Sin(DX.timeNow / 1000f) * 0.5f + 1);
            testImg.rot = DX.timeNow / 1000f;
            testImg.scl2 = new Vector3((float)Math.Sin(DX.timeNow / 2000f) * 0.5f + 1, 1, 0);
            testImg.draw();
            // map border
            DX.d3dDevice.SetTexture(0, null);
            tlPoly.primitive = PrimitiveType.LineStrip;
            tlPoly.setNPoly(0);
            tlPoly.nV[0] = 4;
            tlPoly.poly[0].v = new DX.TLVertex[tlPoly.nV[0] + 1];
            for (i = 0; i < 4; i++)
            {
                tlPoly.poly[0].v[i].color = Sim.g.borderCol.ToArgb();
                tlPoly.poly[0].v[i].rhw = 1;
                tlPoly.poly[0].v[i].z = 0;
            }
            vec = simToDrawPos(new FP.Vector());
            vec2 = simToDrawPos(new FP.Vector(Sim.g.mapSize, Sim.g.mapSize));
            tlPoly.poly[0].v[0].x = vec.X;
            tlPoly.poly[0].v[0].y = vec.Y;
            tlPoly.poly[0].v[1].x = vec2.X;
            tlPoly.poly[0].v[1].y = vec.Y;
            tlPoly.poly[0].v[2].x = vec2.X;
            tlPoly.poly[0].v[2].y = vec2.Y;
            tlPoly.poly[0].v[3].x = vec.X;
            tlPoly.poly[0].v[3].y = vec2.Y;
            tlPoly.poly[0].v[4] = tlPoly.poly[0].v[0];
            tlPoly.draw();
            // particles
            // TODO: scale particle images
            // TODO: setting alpha is temporary hack
            for (i = 0; i < Sim.nParticles; i++)
            {
                if (selParticles.Contains(i))
                {
                    imgParticle[Sim.p[i].type].color = -1;
                }
                else
                {
                    imgParticle[Sim.p[i].type].color = new Color4(0.5f, 1, 1, 1).ToArgb();
                }
                imgParticle[Sim.p[i].type].pos = simToDrawPos(Sim.p[i].calcPos(DX.timeNow - DX.timeStart));
                imgParticle[Sim.p[i].type].draw();
            }
            // select box (if needed)
            /*if (DX.mouseState[1] > 0 && SelBoxMin <= Math.Pow(DX.mouseDX[1] - DX.mouseX, 2) + Math.Pow(DX.mouseDY[1] - DX.mouseY, 2))
            {
                DX.d3dDevice.SetTexture(0, null);
                tlPoly.primitive = PrimitiveType.LineStrip;
                tlPoly.setNPoly(0);
                tlPoly.nV[0] = 4;
                tlPoly.poly[0].v = new DX.TLVertex[tlPoly.nV[0] + 1];
                for (i = 0; i < 4; i++)
                {
                    tlPoly.poly[0].v[i].color = DX.ColWhite;
                    tlPoly.poly[0].v[i].rhw = 1;
                    tlPoly.poly[0].v[i].z = 0;
                }
                tlPoly.poly[0].v[0].x = DX.mouseDX[1];
                tlPoly.poly[0].v[0].y = DX.mouseDY[1];
                tlPoly.poly[0].v[1].x = DX.mouseX;
                tlPoly.poly[0].v[1].y = DX.mouseDY[1];
                tlPoly.poly[0].v[2].x = DX.mouseX;
                tlPoly.poly[0].v[2].y = DX.mouseY;
                tlPoly.poly[0].v[3].x = DX.mouseDX[1];
                tlPoly.poly[0].v[3].y = DX.mouseY;
                tlPoly.poly[0].v[4] = tlPoly.poly[0].v[0];
                tlPoly.draw();
            }*/
            DX.d3dDevice.EndScene();
            DX.d3dDevice.Present();
        }

        private void App_KeyDown(object sender, KeyEventArgs e)
        {
            // in case DirectInput isn't working
            if (e.KeyCode == Keys.Escape)
            {
                runMode = 0;
            }
        }

        private float simToDrawScl(long coor)
        {
            return (float)(FP.toDouble(coor) * Sim.g.drawScl * winDiag);
        }

        private long drawToSimScl(float coor)
        {
            return FP.fromDouble(coor / winDiag / Sim.g.drawScl);
        }

        private Vector3 simToDrawScl(FP.Vector vec)
        {
            return new Vector3(simToDrawScl(vec.x), simToDrawScl(vec.y), simToDrawScl(vec.z));
        }

        private FP.Vector drawToSimScl(Vector3 vec)
        {
            return new FP.Vector(drawToSimScl(vec.X), drawToSimScl(vec.Y), drawToSimScl(vec.Z));
        }

        private Vector3 simToDrawPos(FP.Vector vec)
        {
            return new Vector3(simToDrawScl(vec.x - Sim.g.camPos.x), simToDrawScl(vec.y - Sim.g.camPos.y), 0f) + new Vector3(DX.sx / 2, DX.sy / 2, 0f);
        }

        private FP.Vector drawToSimPos(Vector3 vec)
        {
            return new FP.Vector(drawToSimScl(vec.X - DX.sx / 2), drawToSimScl(vec.Y - DX.sy / 2)) + Sim.g.camPos;
        }
    }
}
