﻿// game form
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
        public const float FntSize = 1f / 40;

        string appPath;
        string modPath = "mod\\";
        SlimDX.Direct3D9.Device d3dOriginalDevice;
        int runMode;
        float winDiag;
        DX.Img2D imgSelect;
        DX.Img2D[] imgUnit;
        DX.Poly2D tlTile;
        DX.Poly2D tlPoly;
        SlimDX.Direct3D9.Font fnt;
        Random rand;
        int selPlayer;
        List<int> selUnits;
        bool paused;

        public App()
        {
            InitializeComponent();
        }

        private void App_Load(object sender, EventArgs e)
        {
            int i, i2, i3;
            System.Collections.Hashtable json;
            System.Collections.ArrayList jsonA;
            string str;
            bool b = false;
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
            // fonts (TODO: make font, size, and color customizable by mod)
            fnt = new SlimDX.Direct3D9.Font(DX.d3dDevice, new System.Drawing.Font("Arial", DX.sy * FntSize, GraphicsUnit.Pixel));
            // selected unit image (TODO: make this customizable by mod)
            imgSelect.init();
            if (!imgSelect.open(appPath + modPath + "select.png", Color.White.ToArgb())) MessageBox.Show("Warning: failed to load " + modPath + "select.png");
            imgSelect.rotCenter.X = imgSelect.srcWidth / 2;
            imgSelect.rotCenter.Y = imgSelect.srcHeight / 2;
            // load scenario from file
            // if this ever supports multiplayer games, host should load file & send data to other players, otherwise json double parsing may not match
            str = new System.IO.StreamReader(appPath + modPath + "scn.json").ReadToEnd();
            json = (System.Collections.Hashtable)Procurios.Public.JSON.JsonDecode(str, ref b);
            if (!b)
            {
                MessageBox.Show("Scenario failed to load" + ErrStr);
                this.Close();
                return;
            }
            Sim.g = new Sim.Scenario();
            Sim.events = new Sim.SimEvtList();
            Sim.maxSpeed = 0;
            Sim.g.mapSize = jsonFP(json, "mapSize");
            Sim.g.camSpeed = jsonFP(json, "camSpeed");
            Sim.g.camPos = jsonFPVector(json, "camPos", new FP.Vector(Sim.g.mapSize / 2, Sim.g.mapSize / 2));
            Sim.g.drawScl = (float)jsonDouble(json, "drawScl");
            Sim.g.drawSclMin = (float)jsonDouble(json, "drawSclMin");
            Sim.g.drawSclMax = (float)jsonDouble(json, "drawSclMax");
            Sim.g.backCol = jsonColor4(json, "backCol");
            Sim.g.borderCol = jsonColor4(json, "borderCol");
            Sim.g.noVisCol = jsonColor4(json, "noVisCol");
            Sim.g.playerVisCol = jsonColor4(json, "playerVisCol");
            Sim.g.unitVisCol = jsonColor4(json, "unitVisCol");
            Sim.g.coherentCol = jsonColor4(json, "coherentCol");
            //Sim.g.music = jsonString(json, "music");
            Sim.g.visRadius = jsonFP(json, "visRadius");
            jsonA = jsonArray(json, "players");
            if (jsonA != null)
            {
                foreach (System.Collections.Hashtable jsonO in jsonA)
                {
                    Sim.Player player = new Sim.Player();
                    player.name = jsonString(jsonO, "name");
                    player.isUser = jsonBool(jsonO, "isUser");
                    player.user = (short)jsonDouble(jsonO, "user");
                    Sim.g.nPlayers++;
                    Array.Resize(ref Sim.g.players, Sim.g.nPlayers);
                    Sim.g.players[Sim.g.nPlayers - 1] = player;
                }
            }
            jsonA = jsonArray(json, "unitTypes");
            if (jsonA != null)
            {
                foreach (System.Collections.Hashtable jsonO in jsonA)
                {
                    Sim.UnitType unitT = new Sim.UnitType();
                    unitT.name = jsonString(jsonO, "name");
                    unitT.imgPath = jsonString(jsonO, "imgPath");
                    unitT.speed = jsonFP(jsonO, "speed");
                    unitT.selRadius = jsonDouble(jsonO, "selRadius");
                    if (unitT.speed > Sim.maxSpeed) Sim.maxSpeed = unitT.speed;
                    Sim.g.nUnitT++;
                    Array.Resize(ref Sim.g.unitT, Sim.g.nUnitT);
                    Sim.g.unitT[Sim.g.nUnitT - 1] = unitT;
                }
            }
            imgUnit = new DX.Img2D[Sim.g.nUnitT * Sim.g.nPlayers];
            for (i = 0; i < Sim.g.nUnitT; i++)
            {
                for (i2 = 0; i2 < Sim.g.nPlayers; i2++)
                {
                    i3 = i * Sim.g.nUnitT + i2;
                    imgUnit[i3].init();
                    if (!imgUnit[i3].open(appPath + modPath + Sim.g.players[i2].name + '.' + Sim.g.unitT[i].imgPath, Color.White.ToArgb())) MessageBox.Show("Warning: failed to load " + modPath + Sim.g.players[i2].name + '.' + Sim.g.unitT[i].imgPath);
                    imgUnit[i3].rotCenter.X = imgUnit[i3].srcWidth / 2;
                    imgUnit[i3].rotCenter.Y = imgUnit[i3].srcHeight / 2;
                }
            }
            Sim.tiles = new Sim.Tile[Sim.tileLen(), Sim.tileLen()];
            for (i = 0; i < Sim.tileLen(); i++)
            {
                for (i2 = 0; i2 < Sim.tileLen(); i2++)
                {
                    Sim.tiles[i, i2] = new Sim.Tile();
                }
            }
            // TODO: load units from file too
            Sim.nUnits = 20;
            Sim.u = new Sim.Unit[Sim.nUnits];
            for (i = 0; i < Sim.nUnits; i++)
            {
                Sim.u[i] = new Sim.Unit(0, i / (Sim.nUnits / 2), 0, new FP.Vector((long)(rand.NextDouble() * Sim.g.mapSize), (long)(rand.NextDouble() * Sim.g.mapSize)));
            }
            selUnits = new List<int>();
            tlTile.primitive = PrimitiveType.TriangleList;
            tlTile.setNPoly(0);
            tlTile.nV[0] = Sim.tileLen() * Sim.tileLen() * 2;
            tlTile.poly[0].v = new DX.TLVertex[tlTile.nV[0] * 3];
            for (i = 0; i < tlTile.poly[0].v.Length; i++)
            {
                tlTile.poly[0].v[i].rhw = 1;
                tlTile.poly[0].v[i].z = 0;
            }
            // start game
            Sim.timeSim = -1;
            DX.timeNow = Environment.TickCount;
            DX.timeStart = DX.timeNow;
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
            FP.Vector curPos, goal;
            Vector3 drawPos;
            long spacing;
            int i;
            DX.mouseUp(button, e.X, e.Y);
            if (button == 1) // select
            {
                if (!DX.diKeyState.IsPressed(Key.LeftControl) && !DX.diKeyState.IsPressed(Key.LeftShift)) selUnits.Clear();
                for (i = 0; i < Sim.nUnits; i++)
                {
                    if (selPlayer == Sim.u[i].player && DX.timeNow - DX.timeStart >= Sim.u[i].m[0].timeStart)
                    {
                        drawPos = simToDrawPos(Sim.u[i].calcPos(DX.timeNow - DX.timeStart));
                        if (drawPos.X + Sim.g.unitT[Sim.u[i].type].selRadius >= Math.Min(DX.mouseDX[1], DX.mouseX)
                            && drawPos.X - Sim.g.unitT[Sim.u[i].type].selRadius <= Math.Max(DX.mouseDX[1], DX.mouseX)
                            && drawPos.Y + Sim.g.unitT[Sim.u[i].type].selRadius >= Math.Min(DX.mouseDY[1], DX.mouseY)
                            && drawPos.Y - Sim.g.unitT[Sim.u[i].type].selRadius <= Math.Max(DX.mouseDY[1], DX.mouseY))
                        {
                            if (selUnits.Contains(i))
                            {
                                selUnits.Remove(i);
                            }
                            else
                            {
                                selUnits.Add(i);
                            }
                            if (SelBoxMin > Math.Pow(DX.mouseDX[1] - DX.mouseX, 2) + Math.Pow(DX.mouseDY[1] - DX.mouseY, 2)) break;
                        }
                    }
                }
            }
            else if (button == 2) // move
            {
                if (mouseSimPos.x >= 0 && mouseSimPos.x <= Sim.g.mapSize && mouseSimPos.y >= 0 && mouseSimPos.y <= Sim.g.mapSize)
                {
                    i = 0;
                    foreach (int unit in selUnits)
                    {
                        if (DX.timeNow - DX.timeStart >= Sim.timeSim || (DX.timeNow - DX.timeStart >= Sim.u[unit].timeCohere && Sim.u[unit].coherent))
                        {
                            curPos = Sim.u[unit].calcPos(DX.timeNow - DX.timeStart);
                            if (curPos.x <= Sim.OffMap << FP.Precision) continue;
                            // TODO: loose formation should be triangular
                            if (DX.diKeyState.IsPressed(Key.LeftControl))
                            {
                                spacing = FP.mul(Sim.g.visRadius, FP.fromDouble(Math.Sqrt(2))) >> FP.Precision << FP.Precision;
                            }
                            else
                            {
                                spacing = 1 << FP.Precision;
                            }
                            goal = mouseSimPos + new FP.Vector((i % (int)Math.Ceiling(Math.Sqrt(selUnits.Count))) * spacing, (long)Math.Floor(i / Math.Ceiling(Math.Sqrt(selUnits.Count))) * spacing);
                            if (goal.x < 0) goal.x = 0;
                            if (goal.x > Sim.g.mapSize) goal.x = Sim.g.mapSize;
                            if (goal.y < 0) goal.y = 0;
                            if (goal.y > Sim.g.mapSize) goal.y = Sim.g.mapSize;
                            Sim.u[unit].addMove(Sim.UnitMove.fromSpeed(DX.timeNow - DX.timeStart, Sim.g.unitT[Sim.u[unit].type].speed, curPos, goal));
                            i++;
                        }
                    }
                }
            }
        }

        private void gameLoop()
        {
            while (runMode == 1)
            {
                updateTime();
                Sim.update(DX.timeNow - DX.timeStart);
                updateInput();
                draw();
            }
        }

        private void updateTime()
        {
            DX.doEventsX();
            if (paused)
            {
                DX.timeStart += DX.timeNow - DX.timeLast;
            }
            else if (DX.diKeyState != null && DX.diKeyState.IsPressed(Key.R))
            {
                DX.timeStart += 2 * (DX.timeNow - DX.timeLast);
            }
            // TODO: cap time difference to a max amount
        }

        private void updateInput()
        {
            int i;
            DX.keyboardUpdate();
            // handle changed keys
            for (i = 0; i < DX.diKeysChanged.Count; i++)
            {
                if (DX.diKeysChanged[i] == Key.Escape && DX.diKeyState.IsPressed(DX.diKeysChanged[i]))
                {
                    // exit (esc)
                    App_KeyDown(this, new System.Windows.Forms.KeyEventArgs(Keys.Escape));
                }
                else if (DX.diKeysChanged[i] == Key.P && DX.diKeyState.IsPressed(DX.diKeysChanged[i]))
                {
                    // pause (p)
                    paused = !paused;
                }
                else if (DX.diKeysChanged[i] == Key.Space && DX.diKeyState.IsPressed(DX.diKeysChanged[i]))
                {
                    // change selected player (spacebar)
                    selPlayer = (selPlayer + 1) % Sim.g.nPlayers;
                    selUnits.Clear();
                }
                else if (DX.diKeysChanged[i] == Key.A && DX.diKeyState.IsPressed(DX.diKeysChanged[i]))
                {
                    // create amplitudes from selected units (a)
                    foreach (int unit in selUnits)
                    {
                        Sim.u[unit].makeChildAmp(unit, DX.timeNow - DX.timeStart + 1);
                    }
                }
                else if (DX.diKeysChanged[i] == Key.Delete && DX.diKeyState.IsPressed(DX.diKeysChanged[i]))
                {
                    // delete selected amplitudes
                    foreach (int unit in selUnits)
                    {
                        Sim.u[unit].deleteAmp(unit);
                    }
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
            Vector3 vec, vec2;
            FP.Vector fpVec;
            Color4 col;
            int i, i2, tX, tY;
            DX.d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Sim.g.backCol, 1, 0);
            DX.d3dDevice.BeginScene();
            DX.d3dDevice.SetTexture(0, null);
            // visibility tiles
            // TODO: don't draw tiles off map
            for (tX = 0; tX < Sim.tileLen(); tX++)
            {
                for (tY = 0; tY < Sim.tileLen(); tY++)
                {
                    vec = simToDrawPos(new FP.Vector(tX << FP.Precision, tY << FP.Precision));
                    vec2 = simToDrawPos(new FP.Vector((tX + 1) << FP.Precision, (tY + 1) << FP.Precision));
                    i = (tX * Sim.tileLen() + tY) * 6;
                    tlTile.poly[0].v[i].x = vec.X;
                    tlTile.poly[0].v[i].y = vec.Y;
                    tlTile.poly[0].v[i + 1].x = vec2.X;
                    tlTile.poly[0].v[i + 1].y = vec2.Y;
                    tlTile.poly[0].v[i + 2].x = vec2.X;
                    tlTile.poly[0].v[i + 2].y = vec.Y;
                    tlTile.poly[0].v[i + 3].x = vec.X;
                    tlTile.poly[0].v[i + 3].y = vec.Y;
                    tlTile.poly[0].v[i + 4].x = vec.X;
                    tlTile.poly[0].v[i + 4].y = vec2.Y;
                    tlTile.poly[0].v[i + 5].x = vec2.X;
                    tlTile.poly[0].v[i + 5].y = vec2.Y;
                    col = Sim.g.noVisCol;
                    if (Sim.tiles[tX, tY].playerVisWhen(selPlayer, DX.timeNow - DX.timeStart))
                    {
                        col += Sim.g.playerVisCol;
                        if (Sim.tiles[tX, tY].playerDirectVisWhen(selPlayer, DX.timeNow - DX.timeStart)) col += Sim.g.unitVisCol;
                        if (Sim.tiles[tX, tY].coherentWhen(selPlayer, DX.timeNow - DX.timeStart)) col += Sim.g.coherentCol;
                    }
                    for (i2 = i; i2 < i + 6; i2++)
                    {
                        tlTile.poly[0].v[i2].color = col.ToArgb();
                    }
                }
            }
            tlTile.draw();
            // map border
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
            // units
            // TODO: scale unit images
            for (i = 0; i < Sim.nUnits; i++)
            {
                if (DX.timeNow - DX.timeStart < Sim.u[i].m[0].timeStart) continue;
                i2 = Sim.u[i].type * Sim.g.nUnitT + Sim.u[i].player;
                fpVec = Sim.u[i].calcPos(DX.timeNow - DX.timeStart);
                if (fpVec.x <= Sim.OffMap << FP.Precision) continue;
                if (selPlayer != Sim.u[i].player && !Sim.tileAt(fpVec).playerVisWhen(selPlayer, DX.timeNow - DX.timeStart)) continue;
                if (Sim.u[i].n > Sim.u[i].mLive + 1 && DX.timeNow - DX.timeStart >= Sim.u[i].m[Sim.u[i].mLive + 1].timeStart)
                {
                    imgUnit[i2].color = new Color4(0.5f, 1, 1, 1).ToArgb(); // TODO: make transparency amount customizable
                }
                else
                {
                    imgUnit[i2].color = new Color4(1, 1, 1, 1).ToArgb();
                }
                imgUnit[i2].pos = simToDrawPos(fpVec);
                imgUnit[i2].draw();
                if (selUnits.Contains(i))
                {
                    imgSelect.pos = imgUnit[i2].pos;
                    imgSelect.draw();
                }
            }
            // select box (if needed)
            // TODO: make color customizable by mod?
            if (DX.mouseState[1] > 0 && SelBoxMin <= Math.Pow(DX.mouseDX[1] - DX.mouseX, 2) + Math.Pow(DX.mouseDY[1] - DX.mouseY, 2))
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
            }
            // text
            DX.textDraw(fnt, new Color4(1, 1, 1, 1), (DX.timeNow - DX.timeStart >= Sim.timeSim) ? "LIVE" : "TIME TRAVELING", 0, 0);
            if (paused) DX.textDraw(fnt, new Color4(1, 1, 1, 1), "PAUSED", 0, (int)(DX.sy * FntSize));
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

        private string jsonString(System.Collections.Hashtable json, string key, string defaultVal = "")
        {
            if (json.ContainsKey(key) && json[key] is string) return (string)json[key];
            return defaultVal;
        }

        private double jsonDouble(System.Collections.Hashtable json, string key, double defaultVal = 0)
        {
            if (json.ContainsKey(key) && json[key] is double) return (double)json[key];
            return defaultVal;
        }

        private bool jsonBool(System.Collections.Hashtable json, string key, bool defaultVal = false)
        {
            if (json.ContainsKey(key) && json[key] is bool) return (bool)json[key];
            return defaultVal;
        }

        private long jsonFP(System.Collections.Hashtable json, string key, long defaultVal = 0)
        {
            if (json.ContainsKey(key))
            {
                if (json[key] is double) return FP.fromDouble((double)json[key]);
                if (json[key] is string)
                {
                    // parse as hex string, so no rounding errors when converting from double
                    // allow beginning string with '-' to specify negative number, as alternative to prepending with f's
                    long ret;
                    if (long.TryParse(((string)json[key]).TrimStart('-'), System.Globalization.NumberStyles.HexNumber, null, out ret))
                    {
                        return ((string)json[key])[0] == '-' ? -ret : ret;
                    }
                    return defaultVal;
                }
            }
            return defaultVal;
        }

        private System.Collections.Hashtable jsonObject(System.Collections.Hashtable json, string key)
        {
            if (json.ContainsKey(key) && json[key] is System.Collections.Hashtable) return (System.Collections.Hashtable)json[key];
            return null;
        }

        private System.Collections.ArrayList jsonArray(System.Collections.Hashtable json, string key)
        {
            if (json.ContainsKey(key) && json[key] is System.Collections.ArrayList) return (System.Collections.ArrayList)json[key];
            return null;
        }

        private FP.Vector jsonFPVector(System.Collections.Hashtable json, string key, FP.Vector defaultVal = new FP.Vector())
        {
            if (json.ContainsKey(key) && json[key] is System.Collections.Hashtable)
            {
                return new FP.Vector(jsonFP((System.Collections.Hashtable)json[key], "x", defaultVal.x),
                    jsonFP((System.Collections.Hashtable)json[key], "y", defaultVal.y),
                    jsonFP((System.Collections.Hashtable)json[key], "z", defaultVal.z));
            }
            return defaultVal;
        }

        private Color4 jsonColor4(System.Collections.Hashtable json, string key)
        {
            if (json.ContainsKey(key) && json[key] is System.Collections.Hashtable)
            {
                return new Color4((float)jsonDouble((System.Collections.Hashtable)json[key], "a", 1),
                    (float)jsonDouble((System.Collections.Hashtable)json[key], "r", 0),
                    (float)jsonDouble((System.Collections.Hashtable)json[key], "g", 0),
                    (float)jsonDouble((System.Collections.Hashtable)json[key], "b", 0));
            }
            return new Color4();
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
