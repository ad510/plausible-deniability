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
    public partial class frmApp : Form
    {
        public const string ErrStr = ".  The program will exit now."; // display this during an initialization or drawing crash

        SlimDX.Direct3D9.Device d3dOriginalDevice;
        int runMode;

        public frmApp()
        {
            InitializeComponent();
        }

        private void frmApp_Load(object sender, EventArgs e)
        {
            if (!modDX.init(this.Handle, true))
            {
                MessageBox.Show("Couldn't set up DirectX.  Make sure your video and audio drivers are up-to-date" + ErrStr + "\n\nError description: " + modDX.dxErr);
                Application.Exit();
                return;
            }
            modDX.setDefaultRes();
            this.Width = modDX.mode.Width;
            this.Height = modDX.mode.Height;
            this.Show();
            this.Focus();
            if (!modDX.init3d(out d3dOriginalDevice, this.Handle, modDX.mode.Width, modDX.mode.Height, modDX.mode.Format, new Vector3(), new Vector3(), (float)(Math.PI / 4), 1000))
            {
                MessageBox.Show("Couldn't set up Direct3D.  Make sure your video and audio drivers are up-to-date and that no other programs are currently using DirectX" + ErrStr + "\n\nError description: " + modDX.dxErr);
                Application.Exit();
                return;
            }
            runMode = 1;
            gameLoop();
            this.Close();
        }

        private void frmApp_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && runMode > 0)
            {
                runMode = 0;
                e.Cancel = true;
            }
        }

        private void frmApp_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            modDX.mouseDblClk();
        }

        private void frmApp_MouseDown(object sender, MouseEventArgs e)
        {
            int button = (int)e.Button / 0x100000;
            modDX.mouseDown(button, e.X, e.Y);
        }

        private void frmApp_MouseMove(object sender, MouseEventArgs e)
        {
            int button = (int)e.Button / 0x100000;
            int i;
            i = modDX.mouseMove(button, e.X, e.Y);
            if (i != -1)
            {
                if (modDX.mouseState[i] == 0)
                {
                    frmApp_MouseUp(this, new System.Windows.Forms.MouseEventArgs((MouseButtons)(button * 0x100000), 0, e.X, e.Y, 0));
                }
                else
                {
                    frmApp_MouseDown(this, new System.Windows.Forms.MouseEventArgs((MouseButtons)(button * 0x100000), 0, e.X, e.Y, 0));
                }
            }
        }

        private void frmApp_MouseUp(object sender, MouseEventArgs e)
        {
            int button = (int)e.Button / 0x100000;
            int mousePrevState = modDX.mouseState[button];
            modDX.mouseUp(button, e.X, e.Y);
        }

        private void gameLoop()
        {
            while (runMode == 1)
            {
                modDX.doEventsX();
                draw();
            }
        }

        private void draw()
        {
            int i;
            modDX.keyboardUpdate();
            for (i = 0; i < modDX.diKeyState.PressedKeys.Count; i++)
            {
                if (modDX.diKeyState.PressedKeys[i] == Key.Escape)
                {
                    frmApp_KeyDown(this, new System.Windows.Forms.KeyEventArgs(Keys.Escape));
                }
            }
            modDX.d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, new Color4(0, 0, 0), 1, 0);
            modDX.d3dDevice.BeginScene();
            modDX.d3dDevice.EndScene();
            modDX.d3dDevice.Present();
        }

        private void frmApp_KeyDown(object sender, KeyEventArgs e)
        {
            // in case DirectInput isn't working
            if (e.KeyCode == Keys.Escape)
            {
                runMode = 0;
            }
        }
    }
}
