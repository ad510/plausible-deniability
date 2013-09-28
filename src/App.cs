// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections;
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
    /// <summary>
    /// game form (contains initialization and user interface code)
    /// </summary>
    public partial class App : Form
    {
        public const string ErrStr = ". The program will exit now."; // display this during an initialization or drawing crash
        public const double SelBoxMin = 100;
        public const float FntSize = 1f / 40;

        string appPath;
        string modPath = "mod\\";
        SlimDX.Direct3D9.Device d3dOriginalDevice;
        int runMode; // TODO: should this be enum?
        float winDiag;
        Sim g;
        DX.Img2D[] imgUnit;
        DX.Poly2D tlTile;
        DX.Poly2D tlPoly;
        SlimDX.Direct3D9.Font fnt;
        FP.Vector mouseSimPos;
        Random rand;
        int selPlayer;
        List<int> selUnits;
        int makeUnitType;
        long timeGame;
        bool paused;
        int speed;
        long timeSpeedChg;

        public App()
        {
            InitializeComponent();
        }

        private void App_Load(object sender, EventArgs e)
        {
            appPath = Application.ExecutablePath.Substring(0, Application.ExecutablePath.LastIndexOf("bin\\"));
            rand = new Random();
            if (!DX.init(this.Handle, true))
            {
                MessageBox.Show("Couldn't set up DirectX. Make sure your video and audio drivers are up-to-date" + ErrStr + "\n\nError description: " + DX.dxErr);
                Application.Exit();
                return;
            }
            DX.setDefaultRes();
            this.Width = DX.dispMode.Width;
            this.Height = DX.dispMode.Height;
            winDiag = new Vector2(DX.dispMode.Width, DX.dispMode.Height).Length();
            this.Show();
            this.Focus();
            if (!DX.init3d(out d3dOriginalDevice, this.Handle, DX.dispMode.Width, DX.dispMode.Height, DX.dispMode.Format, new Vector3(), new Vector3(), (float)(Math.PI / 4), 1000))
            {
                MessageBox.Show("Couldn't set up Direct3D. Make sure your video and audio drivers are up-to-date and that no other programs are currently using DirectX" + ErrStr + "\n\nError description: " + DX.dxErr);
                Application.Exit();
                return;
            }
            // TODO: make font, size, and color customizable by mod
            fnt = new SlimDX.Direct3D9.Font(DX.d3dDevice, new System.Drawing.Font("Arial", DX.resY * FntSize, GraphicsUnit.Pixel));
            if (!scnOpen(appPath + modPath + "scn.json"))
            {
                MessageBox.Show("Scenario failed to load" + ErrStr);
                this.Close();
                return;
            }
            gameLoop();
            this.Close();
        }

        /// <summary>
        /// loads scenario from json file and returns whether successful
        /// </summary>
        private bool scnOpen(string path)
        {
            int i, j, k;
            Hashtable json;
            ArrayList jsonA;
            bool b = false;
            // if this ever supports multiplayer games, host should load file & send data to other players, otherwise json double parsing may not match
            if (!System.IO.File.Exists(path)) return false;
            json = (Hashtable)Procurios.Public.JSON.JsonDecode(System.IO.File.ReadAllText(path), ref b);
            if (!b) return false;
            // base scenario
            g = new Sim();
            g.events = new SimEvtList();
            g.cmdHistory = new SimEvtList();
            g.unitIdChgs = new List<int>();
            g.timeSim = -1;
            g.timeUpdateEvt = long.MinValue;
            g.events.add(new UpdateEvt(-1));
            g.maxSpeed = 0;
            g.mapSize = jsonFP(json, "mapSize");
            g.updateInterval = (long)jsonDouble(json, "updateInterval");
            g.visRadius = jsonFP(json, "visRadius");
            g.camSpeed = jsonFP(json, "camSpeed");
            g.camPos = jsonFPVector(json, "camPos", new FP.Vector(g.mapSize / 2, g.mapSize / 2));
            g.drawScl = (float)jsonDouble(json, "drawScl");
            g.drawSclMin = (float)jsonDouble(json, "drawSclMin");
            g.drawSclMax = (float)jsonDouble(json, "drawSclMax");
            g.healthBarSize = jsonVector2(json, "healthBarSize");
            g.healthBarYOffset = (float)jsonDouble(json, "healthBarYOffset");
            g.backCol = jsonColor4(json, "backCol");
            g.borderCol = jsonColor4(json, "borderCol");
            g.noVisCol = jsonColor4(json, "noVisCol");
            g.playerVisCol = jsonColor4(json, "playerVisCol");
            g.unitVisCol = jsonColor4(json, "unitVisCol");
            g.coherentCol = jsonColor4(json, "coherentCol");
            g.pathCol = jsonColor4(json, "pathCol");
            g.healthBarBackCol = jsonColor4(json, "healthBarBackCol");
            g.healthBarFullCol = jsonColor4(json, "healthBarFullCol");
            g.healthBarEmptyCol = jsonColor4(json, "healthBarEmptyCol");
            //Sim.music = jsonString(json, "music");
            // resources
            g.nRsc = 0;
            jsonA = jsonArray(json, "resources");
            if (jsonA != null)
            {
                foreach (string rscName in jsonA)
                {
                    g.nRsc++;
                    Array.Resize(ref g.rscNames, g.nRsc);
                    g.rscNames[g.nRsc - 1] = rscName;
                }
            }
            // players
            g.nPlayers = 0;
            jsonA = jsonArray(json, "players");
            if (jsonA != null)
            {
                foreach (Hashtable jsonO in jsonA)
                {
                    Hashtable jsonO2 = jsonObject(jsonO, "startRsc");
                    Sim.Player player = new Sim.Player();
                    player.name = jsonString(jsonO, "name");
                    player.isUser = jsonBool(jsonO, "isUser");
                    player.user = (short)jsonDouble(jsonO, "user");
                    player.startRsc = new long[g.nRsc];
                    for (j = 0; j < g.nRsc; j++)
                    {
                        player.startRsc[j] = (jsonO2 != null) ? jsonFP(jsonO2, g.rscNames[j]) : 0;
                    }
                    g.nPlayers++;
                    Array.Resize(ref g.players, g.nPlayers);
                    g.players[g.nPlayers - 1] = player;
                }
                foreach (Hashtable jsonO in jsonA)
                {
                    ArrayList jsonA2 = jsonArray(jsonO, "mayAttack");
                    i = g.playerNamed(jsonString(jsonO, "name"));
                    g.players[i].mayAttack = new bool[g.nPlayers];
                    for (j = 0; j < g.nPlayers; j++)
                    {
                        g.players[i].mayAttack[j] = false;
                    }
                    if (jsonA2 != null)
                    {
                        foreach (string player in jsonA2)
                        {
                            if (g.playerNamed(player) >= 0)
                            {
                                g.players[i].mayAttack[g.playerNamed(player)] = true;
                            }
                        }
                    }
                }
                for (i = 0; i < g.nPlayers; i++)
                {
                    g.players[i].immutable = g.calcPlayerImmutable(i);
                }
            }
            // unit types
            g.nUnitT = 0;
            jsonA = jsonArray(json, "unitTypes");
            if (jsonA != null)
            {
                foreach (Hashtable jsonO in jsonA)
                {
                    Hashtable jsonO2 = jsonObject(jsonO, "rscCost");
                    Hashtable jsonO3 = jsonObject(jsonO, "rscCollectRate");
                    Sim.UnitType unitT = new Sim.UnitType();
                    unitT.name = jsonString(jsonO, "name");
                    unitT.imgPath = jsonString(jsonO, "imgPath");
                    unitT.maxHealth = (int)jsonDouble(jsonO, "maxHealth");
                    unitT.speed = jsonFP(jsonO, "speed");
                    if (unitT.speed > g.maxSpeed) g.maxSpeed = unitT.speed;
                    unitT.reload = (long)jsonDouble(jsonO, "reload");
                    unitT.range = jsonFP(jsonO, "range");
                    unitT.tightFormationSpacing = jsonFP(jsonO, "tightFormationSpacing");
                    unitT.makeUnitMinDist = jsonFP(jsonO, "makeUnitMinDist");
                    unitT.makeUnitMaxDist = jsonFP(jsonO, "makeUnitMaxDist");
                    unitT.selRadius = jsonDouble(jsonO, "selRadius");
                    unitT.rscCost = new long[g.nRsc];
                    unitT.rscCollectRate = new long[g.nRsc];
                    for (j = 0; j < g.nRsc; j++)
                    {
                        unitT.rscCost[j] = (jsonO2 != null) ? jsonFP(jsonO2, g.rscNames[j]) : 0;
                        unitT.rscCollectRate[j] = (jsonO3 != null) ? jsonFP(jsonO3, g.rscNames[j]) : 0;
                    }
                    g.nUnitT++;
                    Array.Resize(ref g.unitT, g.nUnitT);
                    g.unitT[g.nUnitT - 1] = unitT;
                }
                foreach (Hashtable jsonO in jsonA)
                {
                    Hashtable jsonO2 = jsonObject(jsonO, "damage");
                    ArrayList jsonA2 = jsonArray(jsonO, "canMake");
                    i = g.unitTypeNamed(jsonString(jsonO, "name"));
                    g.unitT[i].makeOnUnitT = g.unitTypeNamed(jsonString(jsonO, "makeOnUnitT"));
                    g.unitT[i].damage = new int[g.nUnitT];
                    for (j = 0; j < g.nUnitT; j++)
                    {
                        g.unitT[i].damage[j] = (jsonO2 != null) ? (int)jsonDouble(jsonO2, g.unitT[j].name) : 0;
                    }
                    g.unitT[i].canMake = new bool[g.nUnitT];
                    for (j = 0; j < g.nUnitT; j++)
                    {
                        g.unitT[i].canMake[j] = false;
                    }
                    if (jsonA2 != null)
                    {
                        foreach (string type in jsonA2)
                        {
                            if (g.unitTypeNamed(type) >= 0)
                            {
                                g.unitT[i].canMake[g.unitTypeNamed(type)] = true;
                            }
                        }
                    }
                }
            }
            imgUnit = new DX.Img2D[g.nUnitT * g.nPlayers];
            for (i = 0; i < g.nUnitT; i++)
            {
                for (j = 0; j < g.nPlayers; j++)
                {
                    k = i * g.nPlayers + j;
                    imgUnit[k].init();
                    if (!imgUnit[k].open(appPath + modPath + g.players[j].name + '.' + g.unitT[i].imgPath, Color.White.ToArgb()))
                    {
                        if (!imgUnit[k].open(appPath + modPath + g.unitT[i].imgPath, Color.White.ToArgb()))
                        {
                            MessageBox.Show("Warning: failed to load " + modPath + g.unitT[i].imgPath);
                        }
                    }
                    imgUnit[k].rotCenter.X = imgUnit[k].srcWidth / 2;
                    imgUnit[k].rotCenter.Y = imgUnit[k].srcHeight / 2;
                }
            }
            // tiles
            g.tiles = new Sim.Tile[g.tileLen(), g.tileLen()];
            for (i = 0; i < g.tileLen(); i++)
            {
                for (j = 0; j < g.tileLen(); j++)
                {
                    g.tiles[i, j] = new Sim.Tile(g);
                }
            }
            // units
            g.nUnits = 0;
            jsonA = jsonArray(json, "units");
            if (jsonA != null)
            {
                foreach (Hashtable jsonO in jsonA)
                {
                    g.setNUnits(g.nUnits + 1);
                    g.units[g.nUnits - 1] = new Unit(g, g.nUnits - 1, g.unitTypeNamed(jsonString(jsonO, "type")),
                        g.playerNamed(jsonString(jsonO, "player")), (long)jsonDouble(jsonO, "startTime"),
                        jsonFPVector(jsonO, "startPos", new FP.Vector((long)(rand.NextDouble() * g.mapSize), (long)(rand.NextDouble() * g.mapSize))));
                }
            }
            // tile graphics
            tlTile.primitive = PrimitiveType.TriangleList;
            tlTile.setNPoly(0);
            tlTile.nV[0] = g.tileLen() * g.tileLen() * 2;
            tlTile.poly[0].v = new DX.TLVertex[tlTile.nV[0] * 3];
            for (i = 0; i < tlTile.poly[0].v.Length; i++)
            {
                tlTile.poly[0].v[i].rhw = 1;
                tlTile.poly[0].v[i].z = 0;
            }
            // start game
            selPlayer = 0;
            while (g.players[selPlayer].user != Sim.HumanUser) selPlayer = (selPlayer + 1) % g.nPlayers;
            selUnits = new List<int>();
            makeUnitType = -1;
            paused = false;
            speed = 0;
            timeGame = 0;
            timeSpeedChg = Environment.TickCount - 1000;
            DX.timeNow = Environment.TickCount;
            runMode = 1;
            return true;
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
            mouseSimPos = drawToSimPos(new Vector3(DX.mouseX, DX.mouseY, 0));
        }

        private void App_MouseMove(object sender, MouseEventArgs e)
        {
            int button = (int)e.Button / 0x100000;
            int i;
            i = DX.mouseMove(button, e.X, e.Y);
            mouseSimPos = drawToSimPos(new Vector3(DX.mouseX, DX.mouseY, 0));
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
            Vector3 drawPos;
            int i;
            DX.mouseUp(button, e.X, e.Y);
            mouseSimPos = drawToSimPos(new Vector3(DX.mouseX, DX.mouseY, 0));
            if (button == 1)
            {
                if (makeUnitType >= 0)
                {
                    // make unit
                    // happens at timeGame + 1 so new unit starts out live if game is live
                    FP.Vector pos = makeUnitPos();
                    if (pos.x != Sim.OffMap) g.events.add(new MakeUnitCmdEvt(g.timeSim, timeGame + 1, selUnits.ToArray(), makeUnitType, pos));
                    makeUnitType = -1;
                }
                else
                {
                    // select units
                    if (!DX.keyState.IsPressed(Key.LeftControl) && !DX.keyState.IsPressed(Key.LeftShift)) selUnits.Clear();
                    for (i = 0; i < g.nUnits; i++)
                    {
                        if (selPlayer == g.units[i].player && timeGame >= g.units[i].moves[0].timeStart)
                        {
                            drawPos = simToDrawPos(g.units[i].calcPos(timeGame));
                            if (drawPos.X + g.unitT[g.units[i].type].selRadius >= Math.Min(DX.mouseDX[1], DX.mouseX)
                                && drawPos.X - g.unitT[g.units[i].type].selRadius <= Math.Max(DX.mouseDX[1], DX.mouseX)
                                && drawPos.Y + g.unitT[g.units[i].type].selRadius >= Math.Min(DX.mouseDY[1], DX.mouseY)
                                && drawPos.Y - g.unitT[g.units[i].type].selRadius <= Math.Max(DX.mouseDY[1], DX.mouseY))
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
            }
            else if (button == 2)
            {
                if (makeUnitType >= 0)
                {
                    // cancel making unit
                    makeUnitType = -1;
                }
                else
                {
                    // move selected units
                    g.events.add(new MoveCmdEvt(g.timeSim, timeGame + 1, selUnits.ToArray(), mouseSimPos,
                        DX.keyState.IsPressed(Key.LeftControl) ? Formation.Loose : DX.keyState.IsPressed(Key.LeftAlt) ? Formation.Ring : Formation.Tight));
                }
            }
        }

        private void gameLoop()
        {
            while (runMode == 1)
            {
                updateTime();
                g.updatePast(selPlayer, timeGame);
                //if (timeGame > Sim.timeSim + 1000) Sim.update(timeGame);
                g.update(timeGame);
                updateInput();
                draw();
            }
        }

        private void updateTime()
        {
            DX.doEventsX();
            if (!paused)
            {
                long timeGameDiff;
                if (DX.keyState != null && DX.keyState.IsPressed(Key.R))
                {
                    // rewind
                    timeGameDiff = -(DX.timeNow - DX.timeLast);
                }
                else if (DX.timeNow - DX.timeLast > g.updateInterval && timeGame + DX.timeNow - DX.timeLast >= g.timeSim)
                {
                    // cap time difference to a max amount
                    timeGameDiff = g.updateInterval;
                }
                else
                {
                    // normal speed
                    timeGameDiff = DX.timeNow - DX.timeLast;
                }
                timeGame += (long)(timeGameDiff * Math.Pow(2, speed)); // adjust game speed based on user setting
            }
        }

        private void updateInput()
        {
            int i, j;
            // handle changed unit indices
            for (i = 0; i < g.unitIdChgs.Count / 2; i++)
            {
                if (selUnits.Contains(g.unitIdChgs[i * 2]))
                {
                    if (g.unitIdChgs[i * 2 + 1] >= 0 && !selUnits.Contains(g.unitIdChgs[i * 2 + 1])) selUnits.Insert(selUnits.IndexOf(g.unitIdChgs[i * 2]), g.unitIdChgs[i * 2 + 1]);
                    selUnits.Remove(g.unitIdChgs[i * 2]);
                }
            }
            g.unitIdChgs.Clear();
            // handle changed keys
            DX.keyboardUpdate();
            foreach (Key key in DX.keysChanged)
            {
                if (key == Key.Space && DX.keyState.IsPressed(key))
                {
                    // change selected player
                    do
                    {
                        selPlayer = (selPlayer + 1) % g.nPlayers;
                    } while (g.players[selPlayer].user != Sim.HumanUser);
                    selUnits.Clear();
                    makeUnitType = -1;
                }
                else if (key == Key.P && DX.keyState.IsPressed(key))
                {
                    // pause/resume
                    paused = !paused;
                }
                else if ((key == Key.Equals || key == Key.PreviousTrack) && DX.keyState.IsPressed(key))
                {
                    // increase speed
                    speed++;
                    timeSpeedChg = Environment.TickCount;
                }
                else if (key == Key.Minus && DX.keyState.IsPressed(key))
                {
                    // decrease speed
                    speed--;
                    timeSpeedChg = Environment.TickCount;
                }
                else if (key >= Key.D1 && key <= Key.D9 && key - Key.D1 < g.nUnitT && DX.keyState.IsPressed(key))
                {
                    // make unit of specified type
                    int type = key - Key.D1;
                    foreach (int unit in selUnits)
                    {
                        if (g.units[unit].canMakeChildUnit(timeGame + 1, false, type))
                        {
                            if (g.unitT[type].speed > 0 && g.unitT[type].makeOnUnitT < 0)
                            {
                                int[] unitArray = new int[1];
                                unitArray[0] = unit;
                                // happens at timeGame + 1 so new unit starts out live if game is live
                                g.events.add(new MakeUnitCmdEvt(g.timeSim, timeGame + 1, unitArray, type, makeUnitMovePos(timeGame + 1, unit)));
                            }
                            else
                            {
                                makeUnitType = type;
                            }
                            break;
                        }
                    }
                }
                else if (key == Key.N && DX.keyState.IsPressed(key))
                {
                    // create new paths that selected units could take
                    if (selUnits.Count > 0)
                    {
                        FP.Vector[] pos = new FP.Vector[selUnits.Count];
                        for (i = 0; i < selUnits.Count; i++)
                        {
                            if (g.units[selUnits[i]].exists(timeGame + 1)) pos[i] = makeUnitMovePos(timeGame + 1, selUnits[i]);
                        }
                        // happens at timeGame + 1 so new path starts out live if game is live
                        g.events.add(new MakePathCmdEvt(g.timeSim, timeGame + 1, selUnits.ToArray(), pos));
                    }
                }
                else if (key == Key.Delete && DX.keyState.IsPressed(key))
                {
                    // delete selected paths
                    // happens at timeGame instead of timeGame + 1 so that when paused, making path then deleting parent path doesn't cause an error
                    if (selUnits.Count > 0) g.events.add(new DeletePathCmdEvt(g.timeSim, timeGame, selUnits.ToArray()));
                }
                else if (key == Key.R && DX.keyState.IsPressed(key) && DX.keyState.IsPressed(Key.LeftControl))
                {
                    // instant replay
                    timeGame = 0;
                    // hide traces of mischief
                    for (i = 0; i < g.nUnits; i++)
                    {
                        g.units[i].decohere();
                        g.units[i].tileX = Sim.OffMap + 1;
                    }
                    for (i = 0; i < g.tileLen(); i++)
                    {
                        for (j = 0; j < g.tileLen(); j++)
                        {
                            g.tiles[i, j] = new Sim.Tile(g);
                        }
                    }
                    for (i = 0; i < g.nUnits; i++)
                    {
                        g.units[i].addTileMoveEvts(ref g.events, -1, g.timeSim);
                    }
                    g.update(g.timeSim);
                    paused = true;
                }
            }
            // move camera
            if (DX.keyState.IsPressed(Key.LeftArrow) || DX.mouseX == 0 || (this.Left > 0 && DX.mouseX <= 15))
            {
                g.camPos.x -= g.camSpeed * (DX.timeNow - DX.timeLast);
                if (g.camPos.x < 0) g.camPos.x = 0;
            }
            if (DX.keyState.IsPressed(Key.RightArrow) || DX.mouseX == DX.resX - 1 || (this.Left + this.Width < Screen.PrimaryScreen.Bounds.Width && DX.mouseX >= DX.resX - 15))
            {
                g.camPos.x += g.camSpeed * (DX.timeNow - DX.timeLast);
                if (g.camPos.x > g.mapSize) g.camPos.x = g.mapSize;
            }
            if (DX.keyState.IsPressed(Key.UpArrow) || DX.mouseY == 0 || (this.Top > 0 && DX.mouseY <= 15))
            {
                g.camPos.y -= g.camSpeed * (DX.timeNow - DX.timeLast);
                if (g.camPos.y < 0) g.camPos.y = 0;
            }
            if (DX.keyState.IsPressed(Key.DownArrow) || DX.mouseY == DX.resY - 1 || (this.Top + this.Height < Screen.PrimaryScreen.Bounds.Height && DX.mouseY >= DX.resY - 15))
            {
                g.camPos.y += g.camSpeed * (DX.timeNow - DX.timeLast);
                if (g.camPos.y > g.mapSize) g.camPos.y = g.mapSize;
            }
        }

        private void draw()
        {
            Vector3 vec, vec2;
            FP.Vector fpVec;
            Color4 col;
            float f;
            int i, j, tX, tY;
            DX.d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, g.backCol, 1, 0);
            DX.d3dDevice.BeginScene();
            DX.d3dDevice.SetTexture(0, null);
            // visibility tiles
            // TODO: don't draw tiles off map
            for (tX = 0; tX < g.tileLen(); tX++)
            {
                for (tY = 0; tY < g.tileLen(); tY++)
                {
                    vec = simToDrawPos(new FP.Vector(tX << FP.Precision, tY << FP.Precision));
                    vec2 = simToDrawPos(new FP.Vector((tX + 1) << FP.Precision, (tY + 1) << FP.Precision));
                    i = (tX * g.tileLen() + tY) * 6;
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
                    col = g.noVisCol;
                    if (g.tiles[tX, tY].playerVisWhen(selPlayer, timeGame))
                    {
                        col += g.playerVisCol;
                        if (g.tiles[tX, tY].playerDirectVisWhen(selPlayer, timeGame)) col += g.unitVisCol;
                        if (g.tiles[tX, tY].coherentWhen(selPlayer, timeGame)) col += g.coherentCol;
                    }
                    for (j = i; j < i + 6; j++)
                    {
                        tlTile.poly[0].v[j].color = col.ToArgb();
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
                tlPoly.poly[0].v[i].color = g.borderCol.ToArgb();
                tlPoly.poly[0].v[i].rhw = 1;
                tlPoly.poly[0].v[i].z = 0;
            }
            vec = simToDrawPos(new FP.Vector());
            vec2 = simToDrawPos(new FP.Vector(g.mapSize, g.mapSize));
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
            // unit path lines
            for (i = 0; i < g.nUnits; i++)
            {
                if (unitDrawPos(i, ref vec) && g.units[i].isChildPath && timeGame >= g.units[g.units[i].parent].moves[0].timeStart)
                {
                    DX.d3dDevice.SetTexture(0, null);
                    tlPoly.primitive = PrimitiveType.LineStrip;
                    tlPoly.setNPoly(0);
                    tlPoly.nV[0] = 1;
                    tlPoly.poly[0].v = new DX.TLVertex[tlPoly.nV[0] + 1];
                    tlPoly.poly[0].v[0] = new DX.TLVertex(vec, g.pathCol.ToArgb(), 0, 0);
                    tlPoly.poly[0].v[1] = new DX.TLVertex(simToDrawPos(g.units[g.units[i].parent].calcPos(timeGame)), g.pathCol.ToArgb(), 0, 0);
                    tlPoly.draw();
                }
            }
            // units
            // TODO: scale unit images
            for (i = 0; i < g.nUnits; i++)
            {
                if (unitDrawPos(i, ref vec))
                {
                    j = g.units[i].type * g.nPlayers + g.units[i].player;
                    if (g.units[i].isLive(timeGame))
                    {
                        imgUnit[j].color = new Color4(1, 1, 1, 1).ToArgb();
                    }
                    else
                    {
                        imgUnit[j].color = new Color4(0.5f, 1, 1, 1).ToArgb(); // TODO: make transparency amount customizable
                    }
                    imgUnit[j].pos = vec;
                    imgUnit[j].draw();
                    if (DX.keyState.IsPressed(Key.LeftShift) && selUnits.Contains(i))
                    {
                        // show final position if holding shift
                        imgUnit[j].pos = simToDrawPos(g.units[i].moves[g.units[i].nMoves - 1].vecEnd);
                        imgUnit[j].draw();
                    }
                }
            }
            // unit to be made
            if (makeUnitType >= 0)
            {
                i = makeUnitType * g.nPlayers + selPlayer;
                fpVec = makeUnitPos();
                if (fpVec.x != Sim.OffMap)
                {
                    imgUnit[i].color = new Color4(1, 1, 1, 1).ToArgb();
                    imgUnit[i].pos = simToDrawPos(fpVec);
                }
                else
                {
                    imgUnit[i].color = new Color4(0.5f, 1, 1, 1).ToArgb(); // TODO: make transparency amount customizable
                    imgUnit[i].pos = new Vector3(DX.mouseX, DX.mouseY, 0);
                }
                imgUnit[i].draw();
            }
            // health bars
            foreach (int unit in selUnits)
            {
                if (unitDrawPos(unit, ref vec))
                {
                    j = g.units[unit].type * g.nPlayers + g.units[unit].player;
                    f = ((float)g.units[g.units[unit].rootParentPath()].healthWhen(timeGame)) / g.unitT[g.units[unit].type].maxHealth;
                    tlPoly.primitive = PrimitiveType.TriangleStrip;
                    tlPoly.setNPoly(0);
                    tlPoly.nV[0] = 2;
                    DX.d3dDevice.SetTexture(0, null);
                    // background
                    if (g.units[unit].healthWhen(timeGame) > 0)
                    {
                        tlPoly.poly[0].makeRec(vec.X + g.healthBarSize.X * winDiag * (-0.5f + f),
                            vec.X + g.healthBarSize.X * winDiag * 0.5f,
                            vec.Y - imgUnit[j].srcHeight / 2 - (g.healthBarYOffset - g.healthBarSize.Y / 2) * winDiag,
                            vec.Y - imgUnit[j].srcHeight / 2 - (g.healthBarYOffset + g.healthBarSize.Y / 2) * winDiag,
                            0, g.healthBarBackCol.ToArgb(), g.healthBarBackCol.ToArgb(), g.healthBarBackCol.ToArgb(), g.healthBarBackCol.ToArgb());
                        tlPoly.draw();
                    }
                    // foreground
                    col = g.healthBarEmptyCol + (g.healthBarFullCol - g.healthBarEmptyCol) * f;
                    tlPoly.poly[0].makeRec(vec.X + g.healthBarSize.X * winDiag * -0.5f,
                        vec.X + g.healthBarSize.X * winDiag * (-0.5f + f),
                        vec.Y - imgUnit[j].srcHeight / 2 - (g.healthBarYOffset - g.healthBarSize.Y / 2) * winDiag,
                        vec.Y - imgUnit[j].srcHeight / 2 - (g.healthBarYOffset + g.healthBarSize.Y / 2) * winDiag,
                        0, col.ToArgb(), col.ToArgb(), col.ToArgb(), col.ToArgb());
                    tlPoly.draw();
                }
            }
            // select box (if needed)
            // TODO: make color customizable by mod?
            if (DX.mouseState[1] > 0 && makeUnitType < 0 && SelBoxMin <= Math.Pow(DX.mouseDX[1] - DX.mouseX, 2) + Math.Pow(DX.mouseDY[1] - DX.mouseY, 2))
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
            DX.textDraw(fnt, new Color4(1, 1, 1, 1), (timeGame >= g.timeSim) ? "LIVE" : "TIME TRAVELING", 0, 0);
            if (paused) fnt.DrawString(null, "PAUSED", new Rectangle(0, 0, DX.resX, (int)(DX.resY * FntSize)), DrawTextFormat.Center | DrawTextFormat.Top, new Color4(1, 1, 1, 1));
            if (Environment.TickCount < timeSpeedChg) timeSpeedChg -= UInt32.MaxValue;
            if (Environment.TickCount < timeSpeedChg + 1000) DX.textDraw(fnt, new Color4(1, 1, 1, 1), "SPEED: " + Math.Pow(2, speed) + "x", 0, (int)(DX.resY * FntSize));
            if (g.players[selPlayer].timeGoLiveFail != long.MaxValue) DX.textDraw(fnt, new Color4(1, 1, 0, 0), "ERROR: Going live may cause you to have negative resources " + (timeGame - g.players[selPlayer].timeNegRsc) / 1000 + " second(s) ago.", 0, (int)(2 * DX.resY * FntSize));
            for (i = 0; i < g.nRsc; i++)
            {
                long rscMin = (long)Math.Floor(FP.toDouble(g.playerResource(selPlayer, timeGame, i, false, true, false)));
                long rscMax = (long)Math.Floor(FP.toDouble(g.playerResource(selPlayer, timeGame, i, true, true, false)));
                DX.textDraw(fnt, (rscMin >= 0) ? new Color4(1, 1, 1, 1) : new Color4(1, 1, 0, 0), g.rscNames[i] + ": " + rscMin + ((rscMax != rscMin) ? " to " + rscMax : ""), 0, (int)(DX.resY + (i - g.nRsc) * DX.resY * FntSize));
            }
            DX.d3dDevice.EndScene();
            DX.d3dDevice.Present();
        }

        private void App_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                // exit
                runMode = 0;
            }
        }

        private string jsonString(Hashtable json, string key, string defaultVal = "")
        {
            if (json.ContainsKey(key) && json[key] is string) return (string)json[key];
            return defaultVal;
        }

        private double jsonDouble(Hashtable json, string key, double defaultVal = 0)
        {
            if (json.ContainsKey(key) && json[key] is double) return (double)json[key];
            return defaultVal;
        }

        private bool jsonBool(Hashtable json, string key, bool defaultVal = false)
        {
            if (json.ContainsKey(key) && json[key] is bool) return (bool)json[key];
            return defaultVal;
        }

        private long jsonFP(Hashtable json, string key, long defaultVal = 0)
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

        private Hashtable jsonObject(Hashtable json, string key)
        {
            if (json.ContainsKey(key) && json[key] is Hashtable) return (Hashtable)json[key];
            return null;
        }

        private ArrayList jsonArray(Hashtable json, string key)
        {
            if (json.ContainsKey(key) && json[key] is ArrayList) return (ArrayList)json[key];
            return null;
        }

        private FP.Vector jsonFPVector(Hashtable json, string key, FP.Vector defaultVal = new FP.Vector())
        {
            if (json.ContainsKey(key) && json[key] is Hashtable)
            {
                return new FP.Vector(jsonFP((Hashtable)json[key], "x", defaultVal.x),
                    jsonFP((Hashtable)json[key], "y", defaultVal.y),
                    jsonFP((Hashtable)json[key], "z", defaultVal.z));
            }
            return defaultVal;
        }

        private Vector2 jsonVector2(Hashtable json, string key, Vector2 defaultVal = new Vector2())
        {
            if (json.ContainsKey(key) && json[key] is Hashtable)
            {
                return new Vector2((float)jsonDouble((Hashtable)json[key], "x", defaultVal.X),
                    (float)jsonDouble((Hashtable)json[key], "y", defaultVal.Y));
            }
            return defaultVal;
        }

        private Color4 jsonColor4(Hashtable json, string key)
        {
            if (json.ContainsKey(key) && json[key] is Hashtable)
            {
                return new Color4((float)jsonDouble((Hashtable)json[key], "a", 1),
                    (float)jsonDouble((Hashtable)json[key], "r", 0),
                    (float)jsonDouble((Hashtable)json[key], "g", 0),
                    (float)jsonDouble((Hashtable)json[key], "b", 0));
            }
            return new Color4();
        }

        /// <summary>
        /// returns where to make new unit, or (Sim.OffMap, 0) if mouse is at invalid position
        /// </summary>
        private FP.Vector makeUnitPos()
        {
            FP.Vector vec;
            if (mouseSimPos.x >= 0 && mouseSimPos.x <= g.mapSize && mouseSimPos.y >= 0 && mouseSimPos.y <= g.mapSize)
            {
                if (g.unitT[makeUnitType].makeOnUnitT >= 0)
                {
                    // selected unit type must be made on top of another unit of correct type
                    // TODO: prevent putting multiple units on same unit (unless on different paths of same unit and maybe some other cases)
                    for (int i = 0; i < g.nUnits; i++)
                    {
                        if (g.units[i].exists(timeGame))
                        {
                            vec = g.units[i].calcPos(timeGame);
                            if (g.units[i].type == g.unitT[makeUnitType].makeOnUnitT && g.tileAt(vec).playerVisWhen(selPlayer, timeGame)
                                && (new Vector3(DX.mouseX, DX.mouseY, 0) - simToDrawPos(vec)).LengthSquared() <= g.unitT[g.units[i].type].selRadius * g.unitT[g.units[i].type].selRadius)
                            {
                                return vec;
                            }
                        }
                    }
                }
                else
                {
                    return mouseSimPos;
                }
            }
            return new FP.Vector(Sim.OffMap, 0);
        }

        /// <summary>
        /// returns where new unit can move out of the way after specified unit makes it
        /// </summary>
        /// <remarks>chooses a random location between makeUnitMinDist and makeUnitMaxDist away from unit</remarks>
        private FP.Vector makeUnitMovePos(long time, int unit)
        {
            FP.Vector ret;
            do
            {
                ret = new FP.Vector((long)((rand.NextDouble() - 0.5) * g.unitT[g.units[unit].type].makeUnitMaxDist * 2),
                    (long)((rand.NextDouble() - 0.5) * g.unitT[g.units[unit].type].makeUnitMaxDist * 2));
            } while (ret.lengthSq() < g.unitT[g.units[unit].type].makeUnitMinDist * g.unitT[g.units[unit].type].makeUnitMinDist
                && ret.lengthSq() > g.unitT[g.units[unit].type].makeUnitMaxDist * g.unitT[g.units[unit].type].makeUnitMaxDist);
            return ret + g.units[unit].calcPos(time);
        }

        /// <summary>
        /// sets pos to where unit should be drawn at, and returns whether it should be drawn
        /// </summary>
        private bool unitDrawPos(int unit, ref Vector3 pos)
        {
            FP.Vector fpVec;
            if (!g.units[unit].exists(timeGame) || (selPlayer != g.units[unit].player && !g.units[unit].isLive(timeGame))) return false;
            fpVec = g.units[unit].calcPos(timeGame);
            if (selPlayer != g.units[unit].player && !g.tileAt(fpVec).playerVisWhen(selPlayer, timeGame)) return false;
            pos = simToDrawPos(fpVec);
            return true;
        }

        private float simToDrawScl(long coor)
        {
            return (float)(FP.toDouble(coor) * g.drawScl * winDiag);
        }

        private long drawToSimScl(float coor)
        {
            return FP.fromDouble(coor / winDiag / g.drawScl);
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
            return new Vector3(simToDrawScl(vec.x - g.camPos.x), simToDrawScl(vec.y - g.camPos.y), 0f) + new Vector3(DX.resX / 2, DX.resY / 2, 0f);
        }

        private FP.Vector drawToSimPos(Vector3 vec)
        {
            return new FP.Vector(drawToSimScl(vec.X - DX.resX / 2), drawToSimScl(vec.Y - DX.resY / 2)) + g.camPos;
        }
    }
}
