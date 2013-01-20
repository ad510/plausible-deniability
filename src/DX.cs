// directx engine
// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

// last updated 1/19/2013
// look into comments beginning with "TODO: "

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using SlimDX;
using SlimDX.Direct3D9;
using SlimDX.Windows;
using SlimDX.DirectSound;
using SlimDX.DirectInput;

static class DX
{
    // constants
    public static VertexFormat VertexFmt = (VertexFormat.Position | VertexFormat.Normal | VertexFormat.Texture1 | VertexFormat.Diffuse); // tells about vertex types to D3D
    public static VertexFormat TLVertexFmt = (VertexFormat.PositionRhw | VertexFormat.Diffuse | VertexFormat.Texture1);
    // # of usual screen resolutions - 1
    public const short NResCommon = 9;
    // # of usual screen bit depths - 1
    public const short NDepthCommon = 1;

    // color constants
    public const int ColWhite = 0xffffff;
    public const int ColGray = 0x111111;
    public const int ColLightGray = 0x969696;
    public const int ColRed = 0xff0000;
    public const int ColGreen = 0x1ff00;
    public const int ColBlue = 0x1ff;
    public const int ColBlack = 0x0;

    // to start directx
    public static string dxErr;
    //public static D3DX d3dX_; // makes various DX objects
    public static Direct3D d3d;
    public static SlimDX.Direct3D9.Device d3dDevice;
    public static DisplayMode mode; // current moniter settings
    public static PresentParameters d3dPP; // tells how to load 3d device
    public static Light d3dLight;
    public static DirectSound ds;
    public static SoundBufferDescription dsBuf;
    public static DirectInput di;
    public static Keyboard diKeyDevice;
    public static int[] sxCommon = new int[NResCommon + 1];
    public static int[] syCommon = new int[NResCommon + 1];
    public static bool[,] sPossible = new bool[NResCommon + 1, NDepthCommon + 1]; //possible usual moniter settings
    public static int sx; // screen width
    public static int sy; // screen height

    // to make objects
    public struct TLVertex
    {
        public float x;
        public float y;
        public float z;
        public float rhw;
        public int color;
        public float u;
        public float v;

        public TLVertex(float xVal, float yVal, float zVal, int colorVal, float uVal, float vVal)
        {
            x = xVal;
            y = yVal;
            z = zVal; // this is for z-buffer
            rhw = 1;
            color = colorVal;
            u = uVal;
            v = vVal;
        }
    }

    public struct Vertex
    {
        public float x;
        public float y;
        public float z;
        public Vector3 norm;
        public int color;
        public float u;
        public float v;

        public Vertex(float xVal, float yVal, float zVal, float uVal, float vVal, int colorVal = DX.ColWhite)
        {
            x = xVal;
            y = yVal;
            z = zVal;
            norm = new Vector3();
            color = colorVal;
            u = uVal;
            v = vVal;
        }
    }

    // for running loop
    public static Vector3 camSource;
    public static Vector3 camTarget;
    public static Matrix matView;
    public static Matrix matProj; // projection matrix
    public static KeyboardState diKeyState;
    public static bool[] diLastKeyState = new bool[256];
    public static List<Key> diKeysChanged = new List<Key>();
    public static long timeNow;
    public static long timeLast;
    public static long timeStart;
    public static long timeFpsLast;
    public static float fps;
    public static int fpsCounter;

    // mouse tracking variables (for C# mouse tracking)
    public static byte[] mouseState = new byte[9]; // 0 = not down, 1 = down, 2 = double click
    public static int mouseKey; // last mouse button pressed (for use with double clicking)
    public static int[] mouseDX = new int[9]; // down x
    public static int[] mouseDY = new int[9]; // down y
    public static int[] mouseUX = new int[9]; // up x
    public static int[] mouseUY = new int[9]; // up y
    public static int mouseX;
    public static int mouseY;

    // sets some DirectX settings
    public static bool init(IntPtr formHwnd, bool windowed)
	{
		int a = 0;
		try {
			// set common resolutions
			sxCommon[0] = 640;
			syCommon[0] = 480;
			sxCommon[1] = 800;
			syCommon[1] = 600;
			sxCommon[2] = 1024;
			syCommon[2] = 768;
			sxCommon[3] = 1280;
			syCommon[3] = 800;
			sxCommon[4] = 1280;
			syCommon[4] = 1024;
			sxCommon[5] = 1440;
			syCommon[5] = 900;
			sxCommon[6] = 1600;
			syCommon[6] = 1200;
			sxCommon[7] = 1680;
			syCommon[7] = 1050;
			sxCommon[8] = 1920;
			syCommon[8] = 1440;
			sxCommon[9] = 2048;
			syCommon[9] = 1536;
            // set d3d object
            dxErr = "making D3D";
            d3d = new Direct3D();
            if (d3d == null) throw new NullReferenceException("d3d is null");
			// get current and possible resolutions
			dxErr = "getting possible resolutions";
			d3dPP = new PresentParameters();
			d3dPP.Windowed = windowed;
			mode = d3d.Adapters[0].CurrentDisplayMode;
			if (windowed == false) {
				foreach (DisplayMode tempMode in d3d.Adapters[0].GetDisplayModes(Format.R5G6B5)) {
					for (a = 0; a <= NResCommon; a++) {
						if (tempMode.Width == sxCommon[a] & tempMode.Height == syCommon[a]) {
							sPossible[a, 0] = true;
							break;
						}
					}
                }
                foreach (DisplayMode tempMode in d3d.Adapters[0].GetDisplayModes(Format.X8B8G8R8))
                {
                    for (a = 0; a <= NResCommon; a++)
                    {
                        if (tempMode.Width == sxCommon[a] & tempMode.Height == syCommon[a])
                        {
                            sPossible[a, 1] = true;
                            break;
                        }
                    }
                }
			}
			// set up keyboard input (don't acquire it yet)
			dxErr = "making DI";
			di = new DirectInput();
			dxErr = "making keyboard device";
			diKeyDevice = new Keyboard(di);
			dxErr = "setting cooperative level of keyboard device";
            diKeyDevice.SetCooperativeLevel(formHwnd, SlimDX.DirectInput.CooperativeLevel.Background | SlimDX.DirectInput.CooperativeLevel.Nonexclusive);
			dxErr = "";
		} catch (Exception ex) {
			dxErr = ex.Message.TrimEnd('.') + " while " + dxErr;
			endX(false);
			return false;
		}
		// set up sound
		try {
			ds = new DirectSound();
			ds.SetCooperativeLevel(formHwnd, SlimDX.DirectSound.CooperativeLevel.Priority);
			dsBuf = new SoundBufferDescription();
			dsBuf.Flags = BufferFlags.Control3D | BufferFlags.ControlVolume;
            dsBuf.AlgorithmFor3D = DirectSound3DAlgorithmGuid.FullHrt3DAlgorithm;
		} catch { // I've got backup in case DS doesn't work, so ignore errors
		}
		return true;
	}

    // sets up Direct3D device
    public static bool init3d(out SlimDX.Direct3D9.Device d3dLinkDevice, IntPtr drawHwnd, int resX, int resY, Format resFormat, Vector3 camSourceVal, Vector3 camTargetVal, float camWidth, double lookDist)
    {
        Material mat = default(Material);
        d3dLinkDevice = null;
        try
        {
            dxErr = "setting basic structures";
            // remember screen resolution
            sx = resX;
            sy = resY;
            // set up some lighting stuff
            mat.Ambient = Color.White;
            mat.Diffuse = Color.White;
            d3dLight.Type = LightType.Directional;
            d3dLight.Direction = new Vector3(0, -1, 0);
            // tell how to load 3d device
            d3dPP.BackBufferWidth = sx;
            d3dPP.BackBufferHeight = sy;
            d3dPP.BackBufferFormat = resFormat;
            d3dPP.BackBufferCount = 1;
            d3dPP.SwapEffect = SwapEffect.Copy;
            d3dPP.Multisample = MultisampleType.None;
            d3dPP.EnableAutoDepthStencil = true;
            // enable z-buffering
            d3dPP.AutoDepthStencilFormat = Format.D16;
            // use 16 bit z-buffering
            camSource = camSourceVal;
            camTarget = camTargetVal;
            // set up camera view matrix
            dxErr = "setting up camera view matrix";
            matView = Matrix.LookAtLH(camSource, camTarget, new Vector3(0, 1, 0));
            // set up projection matrix (pi/2 = radians)
            dxErr = "setting up projection matrix";
            matProj = Matrix.PerspectiveFovLH(camWidth, Convert.ToSingle(sx / sy), Convert.ToSingle(lookDist / 1000), Convert.ToSingle(lookDist));
            // acquire keyboard
            dxErr = "acquiring keyboard";
            mouseX = sx / 2;
            mouseY = sy / 2;
            diKeyDevice.Acquire();
            // make D3D device
            dxErr = "making D3D device";
            d3dDevice = null;
            d3dLinkDevice = new SlimDX.Direct3D9.Device(d3d, 0, SlimDX.Direct3D9.DeviceType.Hardware, drawHwnd, (d3d.GetDeviceCaps(0, SlimDX.Direct3D9.DeviceType.Hardware).DeviceCaps.HasFlag(DeviceCaps.HWTransformAndLight) ? CreateFlags.HardwareVertexProcessing : CreateFlags.SoftwareVertexProcessing), d3dPP);
            d3dDevice = d3dLinkDevice;
            if (d3dDevice == null)
                throw new Exception("D3D device is null");
            dxErr = "enabling z-buffering";
            d3dDevice.SetRenderState(RenderState.ZEnable, 1);
            dxErr = "disabling cull mode";
            d3dDevice.SetRenderState(RenderState.CullMode, Cull.None);
            dxErr = "enabling alpha blending";
            d3dDevice.SetRenderState(RenderState.AlphaBlendEnable, true);
            // set vertex format
            dxErr = "setting vertex format";
            d3dDevice.VertexFormat = VertexFmt;
            // use the camera view for the viewport
            dxErr = "using the camera view for the viewport";
            d3dDevice.SetTransform(TransformState.View, matView);
            // tell device to use projection matrix
            dxErr = "telling device to use projection matrix";
            d3dDevice.SetTransform(TransformState.Projection, matProj);
            // tell device to use lighting
            dxErr = "setting material";
            d3dDevice.Material = mat;
            dxErr = "setting light";
            setLight(0.5F, 0.5F, 0.5F, 0.5F, 0.5F, 0.5F);
            dxErr = "enabling light";
            d3dDevice.EnableLight(0, true);
            // tell device to use linear texture filter
            //dxErr = "setting up linear texture filter"
            //d3dDevice.SamplerState(0).MinFilter = TextureFilter.Linear
            //d3dDevice.SamplerState(0).MagFilter = TextureFilter.Linear
            return true;
        }
        catch (Exception ex)
        {
            dxErr = ex.Message.TrimEnd('.') + " while " + dxErr;
            endX(false);
            return false;
        }
    }

    public static void setLight(float dR, float dG, float dB, float aR, float aG, float aB)
    {
        d3dLight.Diffuse = new Color4(dR, dG, dB);
        d3dLight.Ambient = new Color4(aR, aG, aB);
        d3dDevice.SetLight(0, d3dLight);
    }

    public static void setCamPos(float pX, float pY, float pZ, float tX, float tY, float tZ)
    {
        camSource = new Vector3(pX, pY, pZ);
        camTarget = new Vector3(tX, tY, tZ);
        matView = Matrix.LookAtLH(camSource, camTarget, new Vector3(0, 1, 0));
        d3dDevice.SetTransform(TransformState.View, matView);
    }

    public static void setDefaultRes()
    {
        int a = 0;
        int b = 0;
        // check if current res is common
        sx = -1;
        if (mode.Format == Format.R5G6B5 | mode.Format == Format.X8R8G8B8)
        {
            for (a = 0; a <= NResCommon; a++)
            {
                if (mode.Width == sxCommon[a] & mode.Height == syCommon[a])
                {
                    sx = mode.Width;
                    sy = mode.Height;
                    d3dPP.BackBufferFormat = mode.Format;
                    break;
                }
            }
        }
        // if it isn't choose the highest possible common res
        if (sx == -1)
        {
            for (b = 0; b <= NDepthCommon; b++)
            {
                for (a = 0; a <= NResCommon; a++)
                {
                    if (sPossible[NResCommon - a, NDepthCommon - b] == true)
                    {
                        sx = sxCommon[NResCommon - a];
                        sy = syCommon[NResCommon - a];
                        if (b == 0)
                        {
                            d3dPP.BackBufferFormat = Format.R5G6B5;
                        }
                        else if (b == 1)
                        {
                            d3dPP.BackBufferFormat = Format.X8R8G8B8;
                        }
                        break;
                    }
                }
            }
        }
        // if that doesn't work then set to current res
        if (sx == -1)
        {
            sx = mode.Width;
            sy = mode.Height;
            d3dPP.BackBufferFormat = mode.Format;
        }
    }

    public static void textDraw(SlimDX.Direct3D9.Font fnt, Color4 col, string text, int left, int top)
    {
        fnt.DrawString(null, text, new Rectangle(left, top, sx, sy), DrawTextFormat.Top | DrawTextFormat.Left, col);
    }

    public static Vector3 sndTrans(Vector3 vec)
    {
        // translate point for use with default DirectSound camera
        Vector3 camSourceOrig = default(Vector3);
        Vector3 camTargetOrig = default(Vector3);
        Vector3 ret = default(Vector3);
        // this isn't the fastest way to do this but it works (or at least it should)
        camSourceOrig = camSource;
        // remember original camera
        camTargetOrig = camTarget;
        ret = vec3Project(vec);
        // project with original camera
        DX.setCamPos(0, 0, 0, 0, 0, 1);
        // set to directsound camera
        ret = vec3Unproject(ret);
        // unproject with directsound camera
        DX.setCamPos((camSourceOrig.X), (camSourceOrig.Y), (camSourceOrig.Z), (camTargetOrig.X), (camTargetOrig.Y), (camTargetOrig.Z));
        // restore original camera
        return ret;
    }

    public static Vector3 vec3Project(Vector3 vec)
    {
        // easier to use version of the directx function
        return Vector3.Project(vec, d3dDevice.Viewport.X, d3dDevice.Viewport.Y, d3dDevice.Viewport.Width, d3dDevice.Viewport.Height, d3dDevice.Viewport.MinZ, d3dDevice.Viewport.MaxZ, Matrix.Identity);
    }

    public static Vector3 vec3Unproject(Vector3 vec)
    {
        // easier to use version of the directx function
        return Vector3.Unproject(vec, d3dDevice.Viewport.X, d3dDevice.Viewport.Y, d3dDevice.Viewport.Width, d3dDevice.Viewport.Height, d3dDevice.Viewport.MinZ, d3dDevice.Viewport.MaxZ, Matrix.Identity);
    }

    // mouse tracking procedures (for C# mouse tracking)
    // triggers on mouse up so must be handled on MouseDoubleClick or MouseUp event
    public static void mouseDblClk()
    {
        mouseState[mouseKey] = 2;
    }

    public static void mouseDown(int button, int x, int y)
    {
        mouseX = x;
        mouseY = y;
        mouseDX[button] = x;
        mouseDY[button] = y;
        mouseState[button] = 1;
        mouseKey = button;
    }

    public static void mouseUp(int button, int x, int y)
    {
        mouseX = x;
        mouseY = y;
        mouseUX[button] = x;
        mouseUY[button] = y;
        mouseState[button] = 0;
    }

    public static int mouseMove(int button, int x, int y)
    {
        mouseX = x;
        mouseY = y;
        if (mouseState[button] == 0 & button != 0)
        {
            mouseDown(button, x, y);
            return button;
        }
        else if (mouseState[button] != 0 & button == 0)
        {
            mouseUp(button, x, y);
            return button;
        }
        else
        {
            return -1;
        }
    }

    public static bool keyboardUpdate()
    {
        Key a = default(Key);
        diKeysChanged.Clear();
        if (diKeyDevice == null)
            return false; // just in case
        if (diKeyState != null)
        {
            // keep earlier keystates up to date
            for (a = 0; a <= (Key)255; a++)
            {
                diLastKeyState[(int)a] = diKeyState.IsPressed(a);
            }
        }
        diKeyState = diKeyDevice.GetCurrentState();
        // update new keys
        if (diLastKeyState == null)
            return false; // if this is the 1st time setting DIkeystate
        // check for any changes since last time
        for (a = 0; a <= (Key)255; a++)
        {
            if (diKeyState.IsPressed(a) != diLastKeyState[(int)a])
            {
                diKeysChanged.Add(a);
            }
        }
        return true;
    }

    // frame rate & stuff like that
    // if you need better timing see http://geisswerks.com/ryan/FAQS/timing.html
    public static void doEventsX()
    {
        System.Windows.Forms.Application.DoEvents();
        timeLast = timeNow;
        while (timeNow == timeLast)
        {
            timeNow = Environment.TickCount;
            if (timeNow != timeLast)
            {
                if (timeNow < timeLast)
                {
                    timeLast -= UInt32.MaxValue;
                    timeStart -= UInt32.MaxValue;
                    timeFpsLast -= UInt32.MaxValue;
                }
                if (timeFpsLast + 1000 < timeNow)
                {
                    if (fpsCounter <= 0)
                    {
                        fps = 1000f / (timeNow - timeLast);
                    }
                    else
                    {
                        fps = fpsCounter;
                    }
                    timeFpsLast = timeNow;
                    fpsCounter = 0;
                }
                else
                {
                    fpsCounter += 1;
                }
                // Environment.TickCount really only updates about 60 times per second
            }
            else
            {
                System.Threading.Thread.Sleep(1);
                // be nice to other programs
                // this may actually sleep more than 1 ms but sleeps about as long as Environment.TickCount resolution
                System.Windows.Forms.Application.DoEvents();
            }
        }
    }

    public static void endX(bool really = true)
    {
        try
        {
            if ((diKeyDevice != null))
            {
                diKeyDevice.Unacquire();
                diKeyDevice = null;
            }
            ds.Dispose();
            //d3dX_ = null;
            d3dDevice.Dispose();
            d3d.Dispose();
        }
        finally
        {
            if (really == true)
                Application.Exit();
        }
    }

    public struct Img2D
    {
        // directx objects
        public Sprite spr;
        public Texture tex;
        // to draw sprite
        public Vector3 pos;
        public Vector3 rotCenter;
        public SizeF drawSize;
        public float rot;
        public int srcWidth;
        public int srcHeight;
        public Rectangle srcRect;
        public int transColor;
        public int color;

        public bool open(string path, int transColorVal = 0)
        {
            Image img = default(Image);
            if ((spr != null))
                spr.Dispose();
            // dispose any previously made sprite to avoid crash while program exits
            if (!System.IO.File.Exists(path))
                return false;
            // get width & height of bitmap
            img = Image.FromFile(path);
            srcWidth = img.Width;
            srcHeight = img.Height;
            // set default rect
            srcRect = new Rectangle(0, 0, srcWidth, srcHeight);
            // set sprite and texture objects
            transColor = transColorVal;
            spr = new Sprite(DX.d3dDevice);
            tex = Texture.FromFile(DX.d3dDevice, path, srcWidth, srcHeight, 1, Usage.None, Format.Unknown, Pool.Managed, Filter.None, Filter.None, transColor);
            return true;
        }

        public void init(int col = -1, float scl = 1)
        {
            drawSize.Width = scl;
            drawSize.Height = scl;
            color = col;
        }

        public void draw()
        {
            if ((tex != null))
            {
                spr.Begin(SpriteFlags.AlphaBlend);
                // TODO: set spr.transform
                //.Spr.Draw .Tex, .sprRect, .vecScl, .vecRot, .Rot, .vecPos, .sprColor
                spr.Draw(tex, srcRect, rotCenter, pos, new Color4(color)); // draw
                spr.End();
            }
        }
    }

    public struct Poly2D
    {
        public struct PolyV2D
        {
            public TLVertex[] v;

            public void makeRec(float x1, float x2, float y1, float y2, float z, int col00, int col10, int col01, int col11)
            {
                v = new TLVertex[4];
                v[0] = new TLVertex(x1, y1, z, col00, 0, 0);
                v[1] = new TLVertex(x2, y1, z, col10, 1, 0);
                v[2] = new TLVertex(x1, y2, z, col01, 0, 1);
                v[3] = new TLVertex(x2, y2, z, col11, 1, 1);
            }
        }
        public PolyV2D[] poly; // store vertices
        public int[] nV; // number of vertices
        public Texture tex;
        public PrimitiveType primitive;

        public void setNPoly(int numPoly)
        {
            nV = new int[numPoly + 1];
            poly = new PolyV2D[numPoly + 1];
        }

        public void draw()
        {
            int a = 0;
            DX.d3dDevice.VertexFormat = DX.TLVertexFmt;
            for (a = 0; a <= nV.Length - 1; a++)
            {
                DX.d3dDevice.DrawUserPrimitives(primitive, nV[0], poly[a].v);
            }
            DX.d3dDevice.VertexFormat = DX.VertexFmt;
        }
    }

    public struct Poly3D
    {
        public struct PolyV3D
        {
            public Vertex[] v;
            public void makeRec(float x1, float x2, float y1, float y2, float z1, float z2, byte xyz, int col = DX.ColWhite)
            {
                //Poly.Npoly = 2
                v = new Vertex[4];
                if (xyz == 0)
                {
                    v[0] = new Vertex(x1, y1, z1, 1, 0, col);
                    v[1] = new Vertex(x1, y2, z1, 1, 1, col);
                    v[2] = new Vertex(x1, y1, z2, 0, 0, col);
                    v[3] = new Vertex(x1, y2, z2, 0, 1, col);
                }
                else if (xyz == 1)
                {
                    v[0] = new Vertex(x1, y1, z1, 0, 0, col);
                    v[1] = new Vertex(x2, y1, z1, 1, 0, col);
                    v[2] = new Vertex(x1, y1, z2, 0, 1, col);
                    v[3] = new Vertex(x2, y1, z2, 1, 1, col);
                }
                else if (xyz == 2)
                {
                    v[0] = new Vertex(x1, y1, z1, 0, 0, col);
                    v[1] = new Vertex(x2, y1, z1, 1, 0, col);
                    v[2] = new Vertex(x1, y2, z1, 0, 1, col);
                    v[3] = new Vertex(x2, y2, z1, 1, 1, col);
                }
            }

            public void makeBox(float x1, float x2, float y1, float y2, float z1, float z2, int col = DX.ColWhite)
            {
                //Call P3SetNpoly(Poly, 0)
                //Poly.Npoly(0) = 12
                v = new Vertex[14];
                v[13] = new Vertex(x1, y1, z1, 0, 0, col);
                v[12] = new Vertex(x2, y1, z1, 1, 0, col);
                v[11] = new Vertex(x1, y2, z1, 0, 1, col);
                v[10] = new Vertex(x2, y2, z1, 1, 1, col);
                v[9] = new Vertex(x2, y2, z2, 1, 0, col);
                v[8] = new Vertex(x2, y1, z1, 0, 1, col);
                v[7] = new Vertex(x2, y1, z2, 0, 0, col);
                v[6] = new Vertex(x1, y1, z1, 1, 1, col);
                v[5] = new Vertex(x1, y1, z2, 1, 0, col);
                v[4] = new Vertex(x1, y2, z1, 0, 1, col);
                v[3] = new Vertex(x1, y2, z2, 0, 0, col);
                v[2] = new Vertex(x2, y2, z2, 1, 0, col);
                v[1] = new Vertex(x1, y1, z2, 0, 1, col);
                v[0] = new Vertex(x2, y1, z2, 1, 1, col);
            }

            public void makeCircle(Vector3 vec, float rad, int detail, int col = DX.ColWhite)
            {
                int a = 0;
                double rot = 0;
                v = new Vertex[detail + 1];
                for (a = 0; a <= detail; a++)
                {
                    rot = a / detail * Math.PI;
                    if (a % 2 == 0)
                    {
                        rot = -rot;
                    }
                    v[a] = new Vertex(vec.X + Convert.ToSingle(System.Math.Sin(rot) + System.Math.Cos(rot - 0.5 * Math.PI)) * rad, vec.Y + Convert.ToSingle(System.Math.Cos(rot) + System.Math.Sin(rot + 0.5 * Math.PI)) * rad, vec.Z, 0, 0, col);
                }
            }

            public void makeCone(Vector3 vec, float rad1, float rad2, float height, int detail, int col = DX.ColWhite)
            {
                int a = 0;
                float xRot = 0;
                float yRot = 0;
                float rad = 0;
                v = new Vertex[detail * 2 + 2];
                for (a = 0; a <= detail * 2 + 1; a++)
                {
                    xRot = Convert.ToSingle((a / 2) / detail * 2 * Math.PI);
                    yRot = (a % 2) * height;
                    if (a % 2 == 0)
                    {
                        rad = rad1;
                    }
                    else
                    {
                        rad = rad2;
                    }
                    v[a] = new Vertex(vec.X + Convert.ToSingle(System.Math.Sin(xRot) + System.Math.Cos(xRot - 0.5 * Math.PI)) * rad, vec.Y + yRot * height, vec.Z + Convert.ToSingle(System.Math.Cos(xRot) + System.Math.Sin(xRot + 0.5 * Math.PI)) * rad, 0, 0, col);
                }
            }

            public void makeSphere(Vector3 vec, float rad, int xDetail, int yDetail, int col = DX.ColWhite)
            {
                int a = 0;
                double xRot = 0;
                double yRot = 0;
                v = new Vertex[xDetail * yDetail * 2 + 1];
                for (a = 1; a <= xDetail * yDetail * 2; a++)
                {
                    xRot = (a / 2) / xDetail * 2 * Math.PI + 0.5 * Math.PI;
                    yRot = a / 2;
                    if (a % 2 == 1)
                    {
                        yRot = yRot + xDetail;
                    }
                    if (yRot < xDetail)
                    {
                        yRot = 0;
                    }
                    if (yRot > xDetail * yDetail - 1)
                    {
                        yRot = xDetail * (yDetail + 1);
                    }
                    yRot = yRot / (xDetail * (yDetail + 1)) * Math.PI + 0.5 * Math.PI;
                    v[xDetail * yDetail * 2 + 1 - a] = new Vertex(vec.X + rad * Convert.ToSingle(System.Math.Cos(xRot) * System.Math.Cos(yRot)), vec.Y + rad * Convert.ToSingle(System.Math.Sin(yRot)), vec.Z + rad * Convert.ToSingle(System.Math.Sin(xRot) * System.Math.Cos(yRot)), 0, 0, col);
                }
            }
        }
        public PolyV3D[] poly; // store vertices
        public int[] nV; // number of vertices
        public VertexBuffer[] polyVB; // store vertices for renderer
        public Texture tex;
        public string texPath;
        public PrimitiveType primitive;
        public Matrix mat; // stores both position & rotation
        public Vector3 pos;
        public Vector3 rot;
        public Vector3 scl;
        public Vector3 scl2;
        public Vector3 rotOld; // don't have to set up matrix again if nothing changed
        public Vector3 posOld;
        public Vector3 sclOld;
        public Vector3 scl2Old;

        public void init(int numPoly = -1000)
        {
            primitive = PrimitiveType.TriangleStrip;
            scl = new Vector3(1, 1, 1);
            sclOld = scl;
            scl2 = new Vector3(1, 1, 1);
            scl2Old = scl;
            mat = Matrix.Identity; // if you leave matrices alone object won't draw
            if (numPoly != -1000)
                setNpoly(numPoly);
        }

        public void setNpoly(int numPoly)
        {
            poly = new PolyV3D[numPoly + 1];
            nV = new int[numPoly + 1];
            polyVB = new VertexBuffer[numPoly + 1];
        }

        // like setNPoly but preserves current values
        public void setNpolyP(int numPoly)
        {
            Array.Resize(ref poly, numPoly + 1);
            Array.Resize(ref nV, numPoly + 1);
            Array.Resize(ref polyVB, numPoly + 1);
        }

        public bool setBuf(int rangeMin = 0, int rangeMax = -1)
        {
            int a = 0;
            int b = 0;
            bool ret = false;
            ret = true;
            if (rangeMax < 0)
                rangeMax = nV.Length - 1;
            for (a = rangeMin; a <= rangeMax; a++)
            {
                try
                {
                    if (primitive == PrimitiveType.TriangleStrip | primitive == PrimitiveType.TriangleList)
                    {
                        // figure out triangle normals (for lighting)
                        for (b = 2; b <= Convert.ToInt32((primitive != PrimitiveType.TriangleList ? nV[a] + 1 : nV[a] * 3 - 1)); b++)
                        {
                            if (primitive != PrimitiveType.TriangleList | b % 3 == 2)
                            {
                                poly[a].v[b].norm = Vector3.Normalize(Vector3.Cross(Vector3.Subtract(new Vector3(poly[a].v[b - 2].x, poly[a].v[b - 2].y, poly[a].v[b - 2].z), new Vector3(poly[a].v[b - 1].x, poly[a].v[b - 1].y, poly[a].v[b - 1].z)), Vector3.Subtract(new Vector3(poly[a].v[b - 1].x, poly[a].v[b - 1].y, poly[a].v[b - 1].z), new Vector3(poly[a].v[b].x, poly[a].v[b].y, poly[a].v[b].z))));
                                // the code above only works if triangles are counterclockwise
                                // but triangle strips usually alternate btwn CW and CCW so fix this
                                if (primitive == PrimitiveType.TriangleStrip & b % 2 == 0)
                                    poly[a].v[b].norm = Vector3.Multiply(poly[a].v[b].norm, -1);
                            }
                        }
                        poly[a].v[0].norm = poly[a].v[2].norm;
                        poly[a].v[1].norm = poly[a].v[2].norm;
                        if (primitive == PrimitiveType.TriangleList)
                        {
                            for (b = 1; b <= nV[a] - 1; b++)
                            {
                                poly[a].v[b * 3].norm = poly[a].v[b * 3 + 2].norm;
                                poly[a].v[b * 3 + 1].norm = poly[a].v[b * 3 + 2].norm;
                            }
                        }
                    }
                    // create vertex buffer
                    polyVB[a] = new VertexBuffer(d3dDevice, System.Runtime.InteropServices.Marshal.SizeOf(poly[a]), 0, DX.VertexFmt, Pool.Default);
                    if ((polyVB[a] != null))
                    {
                        DataStream stream = polyVB[a].Lock(0, 0, SlimDX.Direct3D9.LockFlags.None);
                        stream.WriteRange(poly[a].v);
                        polyVB[a].Unlock();
                    }
                }
                catch
                {
                    ret = false;
                    polyVB[a] = null;
                }
            }
            return ret;
        }

        public void draw()
        {
            int a = 0;
            // skip if nothing changed
            if (rot.X != rotOld.X | rot.Y != rotOld.Y | rot.Z != rotOld.Z | pos.X != posOld.X | pos.Y != posOld.Y | pos.Z != posOld.Z | scl.X != sclOld.X | scl.Y != sclOld.Y | scl.Z != sclOld.Z | scl2.X != scl2Old.X | scl2.Y != scl2Old.Y | scl2.Z != scl2Old.Z)
            {
                mat = Matrix.Identity;
                // do matrix scaling
                if (scl.X != 1 | scl.Y != 1 | scl.Z != 1)
                {
                    mat = Matrix.Multiply(mat, Matrix.Scaling(scl));
                    sclOld = scl;
                }
                // do matrix rotation
                if (rot.X != 0)
                    mat = Matrix.Multiply(mat, Matrix.RotationX(rot.X));
                if (rot.Y != 0)
                    mat = Matrix.Multiply(mat, Matrix.RotationY(rot.Y));
                if (rot.Z != 0)
                    mat = Matrix.Multiply(mat, Matrix.RotationZ(rot.Z));
                rotOld = rot;
                // do more matrix scaling
                if (scl2.X != 1 | scl2.Y != 1 | scl2.Z != 1)
                {
                    mat = Matrix.Multiply(mat, Matrix.Scaling(scl2));
                    scl2Old = scl2;
                }
                // do matrix translation
                if (pos.X != 0 | pos.Y != 0 | pos.Z != 0)
                {
                    mat = Matrix.Multiply(mat, Matrix.Translation(pos));
                    posOld = pos;
                }
            }
            // draw
            DX.d3dDevice.SetTransform(TransformState.World, mat);
            for (a = 0; a <= nV.Length - 1; a++)
            {
                DX.d3dDevice.SetStreamSource(0, polyVB[a], 0, System.Runtime.InteropServices.Marshal.SizeOf(polyVB[a]));
                // set source vertex buffer
                DX.d3dDevice.DrawPrimitives(primitive, 0, nV[a]); // draw triangles
                //d3dDevice.DrawUserPrimitives(primitiveType, nV(a), poly(a).v) 'alternate to 2 lines above that doesn't use vertex buffer
            }
        }

        /*public bool open(string path)
		{
			// open my custom model file type
			int fFile = 0;
			float[] tV = new float[6];
			string s = "";
			float si = 0;
			int i = 0;
			int i2 = 0;
			init(0);
			fFile = FileSystem.FreeFile();
			try {
				FileSystem.FileOpen(fFile, path, OpenMode.Input);
				FileSystem.Input(fFile, ref s);
				// keyword for ways to open model, this one only supports cubes
				if (s == "cubes") {
					FileSystem.Input(fFile, ref i);
					FileSystem.Input(fFile, ref si);
					FileSystem.Input(fFile, ref s);
					tex = Texture.FromFile(modDX.d3dDevice, System.IO.Path.GetDirectoryName(path) + "\\" + s);
					texPath = s;
					setNpoly(i - 1);
					// loop through cubes
					for (i = 1; i <= nV.Length; i++) {
						nV[i - 1] = 12;
						FileSystem.Input(fFile, ref tV[0]);
						FileSystem.Input(fFile, ref tV[1]);
						FileSystem.Input(fFile, ref tV[2]);
						FileSystem.Input(fFile, ref tV[3]);
						FileSystem.Input(fFile, ref tV[4]);
						FileSystem.Input(fFile, ref tV[5]);
						// get coordinates
						poly[i - 1].makeBox(tV[0], tV[1], tV[4], tV[5], -tV[3], -tV[2]);
						// make cube
						// rotate cube
						for (i2 = 0; i2 <= nV[0] + 1; i2++) {
							tV[1] = poly[i - 1].v[i2].y;
							poly[i - 1].v[i2].y = -poly[i - 1].v[i2].z;
							poly[i - 1].v[i2].z = tV[1];
							poly[i - 1].v[i2].v = 1 - poly[i - 1].v[i2].y / si;
							// make texture y based on height
						}
					}
				// this mode sets every property of every vertex
				} else if (s == "flex") {
					FileSystem.Input(fFile, ref i);
					FileSystem.Input(fFile, ref s);
					tex = Texture.FromFile(modDX.d3dDevice, System.IO.Path.GetDirectoryName(path) + "\\" + s);
					texPath = s;
					setNpoly(Convert.ToInt16(i));
					for (i = 0; i <= nV.Length - 1; i++) {
						FileSystem.Input(fFile, ref nV[i]);
						poly[i].v = new Vertex[nV[i] + 2];
						for (i2 = 0; i2 <= nV[i] + 1; i2++) {
							FileSystem.Input(fFile, ref poly[i].v[i2].x);
							FileSystem.Input(fFile, ref poly[i].v[i2].y);
							FileSystem.Input(fFile, ref poly[i].v[i2].z);
							FileSystem.Input(fFile, ref poly[i].v[i2].u);
                            FileSystem.Input(fFile, ref poly[i].v[i2].v);
                            poly[i].v[i2].color = Color.White.ToArgb();
						}
					}
				}
				FileSystem.FileClose(fFile);
				setBuf();
				return true;
			} catch {
				try {
					// Debug.Print ("warning: model " & path & " failed to load")
					FileSystem.FileClose(fFile);
				} catch {
				}
				return false;
			}
		}*/
    }

    public struct Sound
    {
        public SecondarySoundBuffer buf;
        public SoundBuffer3D buf3D;
        public string path;

        public Sound(string pathVal)
        {
            path = pathVal;
            // load sound
            // sources:
            // http://code.google.com/p/slimdx/issues/attachmentText?id=481&aid=2971502975668307817&name=DirectSoundWrapper.cs
            // http://code.google.com/p/slimdx/issues/detail?id=416
            // http://www.gamedev.net/topic/520834-slimdx-how-to-initialize-secondarysoundbuffer/
            // http://stackoverflow.com/questions/3877362/playing-sound-with-slimdx-and-directsound-c
            try
            {
                SlimDX.Multimedia.WaveStream wave = new SlimDX.Multimedia.WaveStream(path);
                SoundBufferDescription bufDesc = new SoundBufferDescription();
                bufDesc.Format = wave.Format;
                bufDesc.SizeInBytes = (int)wave.Length;
                bufDesc.Flags = BufferFlags.Control3D;
                buf = new SecondarySoundBuffer(ds, bufDesc);
                byte[] data = new byte[bufDesc.SizeInBytes];
                wave.Read(data, 0, (int)wave.Length);
                buf.Write(data, 0, SlimDX.DirectSound.LockFlags.None);
            }
            catch // load failed, make sure program knows
            {
                buf = null;
                buf3D = null;
                return;
            }
            // tell how to play sound
            buf3D = new SoundBuffer3D(buf);
            //Buf3D.SetConeAngles(DxVBLibA.CONST_DSOUND.DS3D_MINCONEANGLE, 100, DxVBLibA.CONST_DS3DAPPLYFLAGS.DS3D_IMMEDIATE)
            //Buf3D.ConeAngles(DSoundHelper.MinConeAngle) = 100 'TODO: fix this
            buf3D.ConeOutsideVolume = -400;
        }

        public void play(Vector3 sndPos, bool bLoop)
        {
            // don't play sound if you can barely hear it
            if (500 < Math.Pow(sndPos.X, 2) + Math.Pow(sndPos.Y, 2) + Math.Pow(sndPos.Z, 2))
                return;
            try
            {
                if (buf == null)
                    throw new Exception();
                // stop sound if it's playing already
                buf.Stop();
                buf.CurrentPlayPosition = 0;
                // set position & play sound
                buf3D.Position = sndPos;
                if (bLoop)
                {
                    buf.Play(0, PlayFlags.Looping);
                }
                else
                {
                    buf.Play(0, PlayFlags.None);
                }
            }
            catch
            {
                // DS can be picky about files, use .NET API if DS doesn't work
                try
                {
                    System.Media.SoundPlayer sndPlayer = new System.Media.SoundPlayer(path);
                    sndPlayer.Play();
                }
                catch
                {
                }
            }
        }
    }

    public struct SoundCopies // so that copies of a sound can play at the same time
    {
        public Sound[] snd;
        public byte last; // last copy played

        public SoundCopies(string path, byte nCopies)
        {
            byte b = 0;
            snd = new Sound[nCopies + 1];
            for (b = 0; b <= nCopies; b++)
            {
                snd[b] = new Sound(path);
            }
            last = 0;
        }

        public void play(Vector3 sndPos)
        {
            last += Convert.ToByte(1);
            if (last >= snd.Length)
            {
                last = 0;
            }
            snd[last].play(sndPos, false);
        }
    }
}