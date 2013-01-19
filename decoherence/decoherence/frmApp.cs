using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DirectX = Microsoft.DirectX;
using Vector3 = Microsoft.DirectX.Vector3;
using Direct3D = Microsoft.DirectX.Direct3D;
using DirectSound = Microsoft.DirectX.DirectSound;
using DirectInput = Microsoft.DirectX.DirectInput;

namespace decoherence
{
    public partial class frmApp : Form
    {
        public const string ErrStr = ".  The program will exit now."; // display this during an initialization or drawing crash

        Direct3D.Device d3dOriginalDevice;

        public frmApp()
        {
            InitializeComponent();
        }

        private void frmApp_Load(object sender, EventArgs e)
        {
            MessageBox.Show("load");
            // if I uncomment code below it fails before frmApp_Load is called because Managed DirectX not supported in .NET 4
            /*if (!modDX.init(this.Handle, true))
            {
                MessageBox.Show("Couldn't set up DirectX.  Make sure your video and audio drivers are up-to-date" + ErrStr + "\n\nError description: " + modDX.dxErr);
                Application.Exit();
                return;
            }
            modDX.setDefaultRes();
            if (!modDX.init3d(out d3dOriginalDevice, this.Handle, Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, modDX.mode.Format, new Vector3(), new Vector3(), (float)(Math.PI / 4), 1000))
            {
                MessageBox.Show("Couldn't set up Direct3D.  Make sure your video and audio drivers are up-to-date and that no other programs are currently using DirectX" + ErrStr + "\n\nError description: " + modDX.dxErr);
                Application.Exit();
                return;
            }*/
        }
    }
}
