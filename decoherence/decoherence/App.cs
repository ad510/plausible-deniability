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
        public const string ErrStr = ".  The program will exit now."; // display this during an initialization or drawing crash
        public const double SelBoxMin = 100;

        SlimDX.Direct3D9.Device d3dOriginalDevice;
        int runMode;
        DX.Poly2D tlPoly;

        public App()
        {
            InitializeComponent();
        }

        private void App_Load(object sender, EventArgs e)
        {
            if (!DX.init(this.Handle, true))
            {
                MessageBox.Show("Couldn't set up DirectX.  Make sure your video and audio drivers are up-to-date" + ErrStr + "\n\nError description: " + DX.dxErr);
                Application.Exit();
                return;
            }
            DX.setDefaultRes();
            this.Width = DX.mode.Width;
            this.Height = DX.mode.Height;
            this.Show();
            this.Focus();
            if (!DX.init3d(out d3dOriginalDevice, this.Handle, DX.mode.Width, DX.mode.Height, DX.mode.Format, new Vector3(), new Vector3(), (float)(Math.PI / 4), 1000))
            {
                MessageBox.Show("Couldn't set up Direct3D.  Make sure your video and audio drivers are up-to-date and that no other programs are currently using DirectX" + ErrStr + "\n\nError description: " + DX.dxErr);
                Application.Exit();
                return;
            }
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
            DX.mouseUp(button, e.X, e.Y);
        }

        private void gameLoop()
        {
            while (runMode == 1)
            {
                DX.doEventsX();
                draw();
            }
        }

        private void draw()
        {
            int i;
            DX.keyboardUpdate();
            for (i = 0; i < DX.diKeysChanged.Count; i++)
            {
                if (DX.diKeysChanged[i] == Key.Escape && DX.diKeyState.IsPressed(DX.diKeysChanged[i]))
                {
                    App_KeyDown(this, new System.Windows.Forms.KeyEventArgs(Keys.Escape));
                }
            }
            DX.d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, new Color4(0, 0, 0), 1, 0);
            DX.d3dDevice.BeginScene();
            // select box (if needed)
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
    }
}
