//directx engine
//Copyright (c) 2007-2013 Andrew Downing
//Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

//last updated 1/18/2013
//look into comments beginning with "problem: " or "problem?: "

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using Drawing = System.Drawing;
using DirectX = Microsoft.DirectX;
using Vector3 = Microsoft.DirectX.Vector3;
using Direct3D = Microsoft.DirectX.Direct3D;
using DirectSound = Microsoft.DirectX.DirectSound;
using DirectInput = Microsoft.DirectX.DirectInput;

static class modDX
{
    //constants
    //# of usual screen resolutions - 1
    public const short NResCommon = 9;
    //# of usual screen bit depths - 1
    public const short NDepthCommon = 1;

    //color constants
    public const int ColWhite = 0xffffff;
    public const int ColGray = 0x111111;
    public const int ColLightGray = 0x969696;
    public const int ColRed = 0xff0000;
    public const int ColGreen = 0x1ff00;
    public const int ColBlue = 0x1ff;

    public const int ColBlack = 0x0;
    //to start directx
    public static string dxErr;
    //makes various DX objects
    public static Direct3D.D3DX d3dX_;
    public static Direct3D.Manager d3d;
    public static Direct3D.Device d3dDevice;
    //current moniter settings
    public static Direct3D.DisplayMode mode;
    //tells how to load 3d device
    public static Direct3D.PresentParameters d3dPP;
    //tells about vertex types to D3D
    public static Direct3D.VertexFormats fvfVertex;
    public static Direct3D.VertexFormats fvfTL;
    public static DirectSound.Device ds;
    public static DirectSound.BufferDescription dsBuf;
    //Public di As DirectInput.Manager 'problem?: this might cause problems later
    public static DirectInput.Device diKeyDevice;
    public static int[] sxCommon = new int[NResCommon + 1];
    public static int[] syCommon = new int[NResCommon + 1];
    //possible usual moniter settings
    public static bool[,] sPossible = new bool[NResCommon + 1, NDepthCommon + 1];
    //screen width
    public static int sx;
    //screen height
    public static int sy;

    //to make objects
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
            z = zVal;
            //this is for z-buffer
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
        public Vertex(float xVal, float yVal, float zVal, float uVal, float vVal, int colorVal = modDX.ColWhite)
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

    //for running loop
    public static Vector3 camSource;
    public static Vector3 camTarget;
    public static DirectX.Matrix matView;
    //projection matrix
    public static DirectX.Matrix matProj;
    public static DirectInput.KeyboardState diKeyState;
    public static bool[] diLastKeyState = new bool[256];
    public static DirectInput.Key[] diKeysChanged;
    public static long timeNow;
    public static long timeLast;
    public static long timeStart;
    public static long timeFpsLast;
    public static float fps;

    public static int fpsCounter;
    //mouse tracking variables (for VB mouse tracking)
    //0 = not down, 1 = down, 2 = double click
    public static byte[] mouseState = new byte[9];
    //last mouse button pressed (for use with double clicking)
    public static int mouseKey;
    //down x
    public static int[] mouseDX = new int[9];
    //down y
    public static int[] mouseDY = new int[9];
    //up x
    public static int[] mouseUX = new int[9];
    //up y
    public static int[] mouseUY = new int[9];
    public static int mouseX;

    public static int mouseY;
    //sets some DirectX settings
    public static bool init(IntPtr formHwnd, bool windowed)
	{
		int a = 0;
		try {
			//set common resolutions
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
			//set vertex formats
			fvfVertex = (Direct3D.VertexFormats.Position | Direct3D.VertexFormats.Normal | Direct3D.VertexFormats.Texture1 | Direct3D.VertexFormats.Diffuse);
			//tells about vertex types to D3D
			fvfTL = (Direct3D.VertexFormats.Transformed | Direct3D.VertexFormats.Diffuse | Direct3D.VertexFormats.Texture1);
			//get current and possible resolutions
			dxErr = "getting possible resolutions";
			d3dPP = new Direct3D.PresentParameters();
			d3dPP.Windowed = windowed;
			mode = Direct3D.Manager.Adapters[0].CurrentDisplayMode;
			if (windowed == false) {
				foreach (Direct3D.DisplayMode tempMode in Direct3D.Manager.Adapters[0].SupportedDisplayModes) {
					for (a = 0; a <= NResCommon; a++) {
						if (tempMode.Width == sxCommon[a] & tempMode.Height == syCommon[a]) {
							if (tempMode.Format == Direct3D.Format.R5G6B5) {
								sPossible[a, 0] = true;
							} else if (tempMode.Format == Direct3D.Format.X8R8G8B8) {
								sPossible[a, 1] = true;
							}
							break;
						}
					}
				}
			}
			//set up keyboard input (don't acquire it yet)
			//DXerr = "making DI" 'problem?: this might cause problems later
			//DI = DX.DirectInputCreate
			dxErr = "making keyboard device";
			diKeyDevice = new DirectInput.Device(DirectInput.SystemGuid.Keyboard);
			dxErr = "telling device to be a keyboard device";
			diKeyDevice.SetDataFormat(DirectInput.DeviceDataFormat.Keyboard);
			dxErr = "setting cooperative level of keyboard device";
			diKeyDevice.SetCooperativeLevel(formHwnd, DirectInput.CooperativeLevelFlags.Background | DirectInput.CooperativeLevelFlags.NonExclusive);
			dxErr = "";
		} catch (Exception ex) {
			dxErr = ex.Message.TrimEnd('.') + " while " + dxErr;
			endX(false);
			return false;
		}
		//set up sound
		try {
			ds = new DirectSound.Device();
			ds.SetCooperativeLevel(formHwnd, DirectSound.CooperativeLevel.Priority);
			dsBuf = new DirectSound.BufferDescription();
			dsBuf.Control3D = true;
			dsBuf.ControlVolume = true;
			dsBuf.Guid3DAlgorithm = DirectSound.DSoundHelper.Guid3DAlgorithmHrtfLight;
		//I've got backup in case DS doesn't work, so ignore errors
		} catch {
		}
		return true;
	}

    //sets up Direct3D device
    public static bool init3d(out Direct3D.Device d3dLinkDevice, IntPtr drawHwnd, int resX, int resY, Direct3D.Format resFormat, Vector3 camSourceVal, Vector3 camTargetVal, float camWidth, double lookDist)
    {
        Direct3D.Material mat = default(Direct3D.Material);
        d3dLinkDevice = null;
        try
        {
            dxErr = "setting basic structures";
            //remember screen resolution
            sx = resX;
            sy = resY;
            mat.Ambient = Drawing.Color.White;
            mat.Diffuse = Drawing.Color.White;
            //tell how to load 3d device
            d3dPP.BackBufferWidth = sx;
            d3dPP.BackBufferHeight = sy;
            d3dPP.BackBufferFormat = resFormat;
            d3dPP.BackBufferCount = 1;
            d3dPP.SwapEffect = Direct3D.SwapEffect.Copy;
            d3dPP.MultiSample = Direct3D.MultiSampleType.None;
            d3dPP.EnableAutoDepthStencil = true;
            //enable z-buffering
            d3dPP.AutoDepthStencilFormat = Direct3D.DepthFormat.D16;
            //use 16 bit z-buffering
            camSource = camSourceVal;
            camTarget = camTargetVal;
            //set up camera view matrix
            dxErr = "setting up camera view matrix";
            matView = DirectX.Matrix.LookAtLH(camSource, camTarget, new Vector3(0, 1, 0));
            //set up projection matrix (pi/2 = radians)
            dxErr = "setting up projection matrix";
            matProj = DirectX.Matrix.PerspectiveFovLH(camWidth, Convert.ToSingle(sx / sy), Convert.ToSingle(lookDist / 1000), Convert.ToSingle(lookDist));
            //acquire keyboard
            dxErr = "acquiring keyboard";
            mouseX = sx / 2;
            mouseY = sy / 2;
            diKeyDevice.Acquire();
            //make D3D device
            dxErr = "making D3D device";
            d3dDevice = null;
            d3dLinkDevice = new Direct3D.Device(0, Direct3D.DeviceType.Hardware, drawHwnd, (Direct3D.CreateFlags)(Direct3D.Manager.GetDeviceCaps(0, Direct3D.DeviceType.Hardware).DeviceCaps.SupportsHardwareTransformAndLight ? Direct3D.CreateFlags.HardwareVertexProcessing : Direct3D.CreateFlags.SoftwareVertexProcessing), d3dPP);
            d3dDevice = d3dLinkDevice;
            if (d3dDevice == null)
                throw new Exception("D3D device is null");
            dxErr = "enabling z-buffering";
            d3dDevice.RenderState.ZBufferEnable = true;
            dxErr = "disabling cull mode";
            d3dDevice.RenderState.CullMode = Direct3D.Cull.None;
            dxErr = "enabling alpha blending";
            d3dDevice.RenderState.AlphaBlendEnable = true;
            //set vertex format
            dxErr = "setting vertex format";
            d3dDevice.VertexFormat = fvfVertex;
            //use the camera view for the viewport
            dxErr = "using the camera view for the viewport";
            d3dDevice.SetTransform(Direct3D.TransformType.View, matView);
            //tell device to use projection matrix
            dxErr = "telling device to use projection matrix";
            d3dDevice.SetTransform(Direct3D.TransformType.Projection, matProj);
            //tell device to use lighting
            dxErr = "setting material";
            d3dDevice.Material = mat;
            dxErr = "setting light";
            d3dDevice.Lights[0].Type = Direct3D.LightType.Directional;
            d3dDevice.Lights[0].Direction = new Vector3(0, -1, 0);
            d3dDevice.Lights[0].Diffuse = Drawing.Color.Gray;
            d3dDevice.Lights[0].Ambient = Drawing.Color.Gray;
            d3dDevice.Lights[0].Enabled = true;
            //tell device to use linear texture filter
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

    public static void setCamPos(float pX, float pY, float pZ, float tX, float tY, float tZ)
    {
        camSource = new Vector3(pX, pY, pZ);
        camTarget = new Vector3(tX, tY, tZ);
        matView = DirectX.Matrix.LookAtLH(camSource, camTarget, new Vector3(0, 1, 0));
        d3dDevice.SetTransform(Direct3D.TransformType.View, matView);
    }

    public static void setDefaultRes()
    {
        int a = 0;
        int b = 0;
        //check if current res is common
        sx = -1;
        if (mode.Format == Direct3D.Format.R5G6B5 | mode.Format == Direct3D.Format.X8R8G8B8)
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
        //if it isn't choose the highest possible common res
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
                            d3dPP.BackBufferFormat = Direct3D.Format.R5G6B5;
                        }
                        else if (b == 1)
                        {
                            d3dPP.BackBufferFormat = Direct3D.Format.X8R8G8B8;
                        }
                        break;
                    }
                }
            }
        }
        //if that doesn't work then set to current res
        if (sx == -1)
        {
            sx = mode.Width;
            sy = mode.Height;
            d3dPP.BackBufferFormat = mode.Format;
        }
    }

    public static void textDraw(Direct3D.Font fnt, int col, string text, int left, int top)
    {
        fnt.DrawText(null, text, new Drawing.Rectangle(left, top, sx, sy), Direct3D.DrawTextFormat.Top | Direct3D.DrawTextFormat.Left, col);
    }

    public static Vector3 sndTrans(Vector3 vec)
    {
        Vector3 ret = default(Vector3);
        //translate point for use with default DirectSound camera
        Vector3 camSourceOrig = default(Vector3);
        Vector3 camTargetOrig = default(Vector3);
        //this isn't the fastest way to do this but it works (or at least it should)
        camSourceOrig = camSource;
        //remember original camera
        camTargetOrig = camTarget;
        ret = vec3Project(vec);
        //project with original camera
        modDX.setCamPos(0, 0, 0, 0, 0, 1);
        //set to directsound camera
        ret = vec3Unproject(ret);
        //unproject with directsound camera
        modDX.setCamPos((camSourceOrig.X), (camSourceOrig.Y), (camSourceOrig.Z), (camTargetOrig.X), (camTargetOrig.Y), (camTargetOrig.Z));
        return ret;
        //restore original camera
    }

    public static Vector3 vec3Project(Vector3 vec)
    {
        //easier to use version of the directx function
        return Vector3.Project(vec, d3dDevice.Viewport, matProj, matView, DirectX.Matrix.Identity);
    }

    public static Vector3 vec3Unproject(Vector3 vec)
    {
        //easier to use version of the directx function
        return Vector3.Unproject(vec, d3dDevice.Viewport, matProj, matView, DirectX.Matrix.Identity);
    }

    //mouse tracking procedures (for VB mouse tracking)
    //triggers on mouse up so must be handled on MouseDoubleClick or MouseUp event
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
        DirectInput.Key a = default(DirectInput.Key);
        diKeysChanged = new DirectInput.Key[1];
        if (diKeyDevice == null)
            return false;
        //just in case
        if ((diKeyState != null))
        {
            //keep earlier keystates up to date
            for (a = 0; a <= (DirectInput.Key)255; a++)
            {
                diLastKeyState[(int)a] = diKeyState[a];
            }
        }
        diKeyState = diKeyDevice.GetCurrentKeyboardState();
        //update new keys
        if (diLastKeyState == null)
            return false;
        //if this is the 1st time setting DIkeystate
        //check for any changes since last time
        for (a = 0; a <= (DirectInput.Key)255; a++)
        {
            if (diKeyState[a] != diLastKeyState[(int)a])
            {
                Array.Resize(ref diKeysChanged, diKeysChanged.Length + 1);
                diKeysChanged[diKeysChanged.Length - 1] = a;
            }
        }
        return true;
    }

    //frame rate & stuff like that
    //if you need better timing see http://geisswerks.com/ryan/FAQS/timing.html
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
                //Environment.TickCount really only updates about 60 times per second
            }
            else
            {
                System.Threading.Thread.Sleep(1);
                //be nice to other programs
                //this may actually sleep more than 1 ms but sleeps about as long as Environment.TickCount resolution
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
            ds = null;
            d3dX_ = null;
            d3dDevice = null;
            d3d = null;
        }
        finally
        {
            if (really == true)
                System.Windows.Forms.Application.Exit();
        }
    }

    public struct Img2D
    {
        //directx objects
        public Direct3D.Sprite spr;
        public Direct3D.Texture tex;
        //to draw sprite
        public Drawing.PointF pos;
        public Drawing.PointF rotCenter;
        public Drawing.SizeF drawSize;
        public float rot;
        public int srcWidth;
        public int srcHeight;
        public Drawing.Rectangle srcRect;
        public int transColor;

        public int color;
        public bool open(string path, int transColorVal = 0)
        {
            Drawing.Image img = default(Drawing.Image);
            if ((spr != null))
                spr.Dispose();
            //dispose any previously made sprite to avoid crash while program exits
            if (!System.IO.File.Exists(path))
                return false;
            //get width & height of bitmap
            img = Drawing.Image.FromFile(path);
            srcWidth = img.Width;
            srcHeight = img.Height;
            //set default rect
            srcRect = new Drawing.Rectangle(0, 0, srcWidth, srcHeight);
            //set sprite and texture objects
            transColor = transColorVal;
            spr = new Direct3D.Sprite(modDX.d3dDevice);
            tex = Direct3D.TextureLoader.FromFile(modDX.d3dDevice, path, srcWidth, srcHeight, 1, Direct3D.Usage.None, Direct3D.Format.Unknown, Direct3D.Pool.Managed, Direct3D.Filter.None, Direct3D.Filter.None,
            transColor);
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
                spr.Begin(Direct3D.SpriteFlags.AlphaBlend);
                spr.Draw2D(tex, srcRect, drawSize, rotCenter, rot, pos, color);
                //draw
                spr.End();
            }
        }
    }

    public struct Poly2D
    {
        public struct PolyV2D
        {
            //for some reason 1000 can't be variable
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
        //store vertices
        public PolyV2D[] poly;
        //number of vertices
        public int[] nV;
        public Direct3D.Texture tex;

        public Direct3D.PrimitiveType primitiveType;
        public void setNPoly(int numPoly)
        {
            nV = new int[numPoly + 1];
            poly = new PolyV2D[numPoly + 1];
        }

        public void draw()
        {
            int a = 0;
            modDX.d3dDevice.VertexFormat = modDX.fvfTL;
            for (a = 0; a < nV.Length; a++)
            {
                modDX.d3dDevice.DrawUserPrimitives(primitiveType, nV[0], poly[a].v);
            }
            modDX.d3dDevice.VertexFormat = modDX.fvfVertex;
        }
    }

    public struct Poly3D
    {
        public struct PolyV3D
        {

            public Vertex[] v;
            public void makeRec(float x1, float x2, float y1, float y2, float z1, float z2, byte xyz, int col = modDX.ColWhite)
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

            public void makeBox(float x1, float x2, float y1, float y2, float z1, float z2, int col = modDX.ColWhite)
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

            public void makeCircle(Vector3 vec, float rad, int detail, int col = modDX.ColWhite)
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

            public void makeCone(Vector3 vec, float rad1, float rad2, float height, int detail, int col = modDX.ColWhite)
            {
                int a = 0;
                float xRot = 0;
                float yRot = 0;
                float rad = 0;
                v = new Vertex[detail * 2 + 2];
                for (a = 0; a <= detail * 2 + 1; a++)
                {
                    xRot = Convert.ToSingle(a / 2 / detail * 2 * Math.PI);
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

            public void makeSphere(Vector3 vec, float rad, int xDetail, int yDetail, int col = modDX.ColWhite)
            {
                int a = 0;
                double xRot = 0;
                double yRot = 0;
                v = new Vertex[xDetail * yDetail * 2 + 1];
                for (a = 1; a <= xDetail * yDetail * 2; a++)
                {
                    xRot = a / 2 / xDetail * 2 * Math.PI + 0.5 * Math.PI;
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
        //store vertices
        public PolyV3D[] poly;
        //number of vertices
        public int[] nV;
        //store vertices for renderer
        public Direct3D.VertexBuffer[] polyVB;
        public Direct3D.Texture tex;
        public string texPath;
        public Direct3D.PrimitiveType primitiveType;
        //stores both position & rotation
        public DirectX.Matrix mat;
        public Vector3 pos;
        public Vector3 rot;
        public Vector3 scl;
        public Vector3 scl2;
        //don't have to set up matrix again if nothing changed
        public Vector3 rotOld;
        public Vector3 posOld;
        public Vector3 sclOld;

        public Vector3 scl2Old;
        public void init(int numPoly = -1000)
        {
            primitiveType = Direct3D.PrimitiveType.TriangleStrip;
            scl = new Vector3(1, 1, 1);
            sclOld = scl;
            scl2 = new Vector3(1, 1, 1);
            scl2Old = scl;
            mat = DirectX.Matrix.Identity;
            //if you leave matrices alone object won't draw
            if (numPoly != -1000)
                setNpoly(numPoly);
        }

        public void setNpoly(int numPoly)
        {
            poly = new PolyV3D[numPoly + 1];
            nV = new int[numPoly + 1];
            polyVB = new Direct3D.VertexBuffer[numPoly + 1];
        }

        //like setNPoly but uses redim preserve
        public void setNpolyP(int numPoly)
        {
            Array.Resize(ref poly, numPoly + 1);
            Array.Resize(ref nV, numPoly + 1);
            Array.Resize(ref polyVB, numPoly + 1);
        }

        public bool setBuf(int rangeMin = 0, int rangeMax = -1)
        {
            bool ret = false;
            int a = 0;
            int b = 0;
            ret = true;
            if (rangeMax < 0)
                rangeMax = nV.Length - 1;
            for (a = rangeMin; a <= rangeMax; a++)
            {
                try
                {
                    if (primitiveType == Direct3D.PrimitiveType.TriangleStrip | primitiveType == Direct3D.PrimitiveType.TriangleList)
                    {
                        //figure out triangle normals (for lighting)
                        for (b = 2; b <= Convert.ToInt32((primitiveType != Direct3D.PrimitiveType.TriangleList ? nV[a] + 1 : nV[a] * 3 - 1)); b++)
                        {
                            if (primitiveType != Direct3D.PrimitiveType.TriangleList | b % 3 == 2)
                            {
                                poly[a].v[b].norm = Vector3.Normalize(Vector3.Cross(Vector3.Subtract(new Vector3(poly[a].v[b - 2].x, poly[a].v[b - 2].y, poly[a].v[b - 2].z), new Vector3(poly[a].v[b - 1].x, poly[a].v[b - 1].y, poly[a].v[b - 1].z)), Vector3.Subtract(new Vector3(poly[a].v[b - 1].x, poly[a].v[b - 1].y, poly[a].v[b - 1].z), new Vector3(poly[a].v[b].x, poly[a].v[b].y, poly[a].v[b].z))));
                                //the code above only works if triangles are counterclockwise
                                //but triangle strips usually alternate btwn CW and CCW so fix this
                                if (primitiveType == Direct3D.PrimitiveType.TriangleStrip & b % 2 == 0)
                                    poly[a].v[b].norm = Vector3.Scale(poly[a].v[b].norm, -1);
                            }
                        }
                        poly[a].v[0].norm = poly[a].v[2].norm;
                        poly[a].v[1].norm = poly[a].v[2].norm;
                        if (primitiveType == Direct3D.PrimitiveType.TriangleList)
                        {
                            for (b = 1; b <= nV[a] - 1; b++)
                            {
                                poly[a].v[b * 3].norm = poly[a].v[b * 3 + 2].norm;
                                poly[a].v[b * 3 + 1].norm = poly[a].v[b * 3 + 2].norm;
                            }
                        }
                    }
                    //create vertex buffer
                    polyVB[a] = new Direct3D.VertexBuffer(typeof(Vertex), poly[a].v.Length, modDX.d3dDevice, 0, modDX.fvfVertex, Direct3D.Pool.Default);
                    if ((polyVB[a] != null))
                        polyVB[a].SetData(poly[a].v, 0, Direct3D.LockFlags.None);
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
            //skip if nothing changed
            if (rot.X != rotOld.X | rot.Y != rotOld.Y | rot.Z != rotOld.Z | pos.X != posOld.X | pos.Y != posOld.Y | pos.Z != posOld.Z | scl.X != sclOld.X | scl.Y != sclOld.Y | scl.Z != sclOld.Z | scl2.X != scl2Old.X | scl2.Y != scl2Old.Y | scl2.Z != scl2Old.Z)
            {
                mat = DirectX.Matrix.Identity;
                //do matrix scaling
                if (scl.X != 1 | scl.Y != 1 | scl.Z != 1)
                {
                    mat = DirectX.Matrix.Multiply(mat, DirectX.Matrix.Scaling(scl));
                    sclOld = scl;
                }
                //do matrix rotation
                if (rot.X != 0)
                    mat = DirectX.Matrix.Multiply(mat, DirectX.Matrix.RotationX(rot.X));
                if (rot.Y != 0)
                    mat = DirectX.Matrix.Multiply(mat, DirectX.Matrix.RotationY(rot.Y));
                if (rot.Z != 0)
                    mat = DirectX.Matrix.Multiply(mat, DirectX.Matrix.RotationZ(rot.Z));
                rotOld = rot;
                //do more matrix scaling
                if (scl2.X != 1 | scl2.Y != 1 | scl2.Z != 1)
                {
                    mat = DirectX.Matrix.Multiply(mat, DirectX.Matrix.Scaling(scl2));
                    scl2Old = scl2;
                }
                //do matrix translation
                if (pos.X != 0 | pos.Y != 0 | pos.Z != 0)
                {
                    mat = DirectX.Matrix.Multiply(mat, DirectX.Matrix.Translation(pos));
                    posOld = pos;
                }
            }
            //draw
            modDX.d3dDevice.SetTransform(Direct3D.TransformType.World, mat);
            for (a = 0; a < nV.Length; a++)
            {
                modDX.d3dDevice.SetStreamSource(0, polyVB[a], 0);
                //set source vertex buffer
                modDX.d3dDevice.DrawPrimitives(primitiveType, 0, nV[a]);
                //draw triangles
                //d3dDevice.DrawUserPrimitives(primitiveType, nV(a), poly(a).v) 'alternate to 2 lines above that doesn't use vertex buffer
            }
        }

        /*public bool open(string path)
		{
			//open my custom model file type
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
				//keyword for ways to open model, this one only supports cubes
				if (s == "cubes") {
					FileSystem.Input(fFile, ref i);
					FileSystem.Input(fFile, ref si);
					FileSystem.Input(fFile, ref s);
					tex = Direct3D.TextureLoader.FromFile(modDX.d3dDevice, System.IO.Path.GetDirectoryName(path) + "\\" + s);
					texPath = s;
					setNpoly(i - 1);
					//loop through cubes
					for (i = 1; i <= nV.Length; i++) {
						nV[i - 1] = 12;
						FileSystem.Input(fFile, ref tV[0]);
						FileSystem.Input(fFile, ref tV[1]);
						FileSystem.Input(fFile, ref tV[2]);
						FileSystem.Input(fFile, ref tV[3]);
						FileSystem.Input(fFile, ref tV[4]);
						FileSystem.Input(fFile, ref tV[5]);
						//get coordinates
						poly[i - 1].makeBox(tV[0], tV[1], tV[4], tV[5], -tV[3], -tV[2]);
						//make cube
						//rotate cube
						for (i2 = 0; i2 <= nV[0] + 1; i2++) {
							tV[1] = poly[i - 1].v[i2].y;
							poly[i - 1].v[i2].y = -poly[i - 1].v[i2].z;
							poly[i - 1].v[i2].z = tV[1];
							poly[i - 1].v[i2].v = 1 - poly[i - 1].v[i2].y / si;
							//make texture y based on height
						}
					}
				//this mode sets every property of every vertex
				} else if (s == "flex") {
					FileSystem.Input(fFile, ref i);
					FileSystem.Input(fFile, ref s);
                    tex = Direct3D.TextureLoader.FromFile(modDX.d3dDevice, System.IO.Path.GetDirectoryName(path) + "\\" + s);
					texPath = s;
					setNpoly(Convert.ToInt16(i));
					for (i = 0; i < nV.Length; i++) {
						FileSystem.Input(fFile, ref nV[i]);
						poly[i].v = new Vertex[nV[i] + 2];
						for (i2 = 0; i2 <= nV[i] + 1; i2++) {
							FileSystem.Input(fFile, ref poly[i].v[i2].x);
							FileSystem.Input(fFile, ref poly[i].v[i2].y);
							FileSystem.Input(fFile, ref poly[i].v[i2].z);
							FileSystem.Input(fFile, ref poly[i].v[i2].u);
                            FileSystem.Input(fFile, ref poly[i].v[i2].v);
                            poly[i].v[i2].color = Drawing.Color.White.ToArgb();
						}
					}
				}
				FileSystem.FileClose(fFile);
				setBuf();
				return true;
			} catch {
				try {
					//Debug.Print ("warning: model " & path & " failed to load")
					FileSystem.FileClose(fFile);
				} catch {
				}
				return false;
			}
		}*/
    }

    public struct Sound
    {
        public DirectSound.SecondaryBuffer buf;
        public DirectSound.Buffer3D buf3D;

        public string path;
        public Sound(string pathVal)
        {
            path = pathVal;
            //load sound
            try
            {
                buf = new DirectSound.SecondaryBuffer(path, modDX.dsBuf, modDX.ds);
                //load failed, make sure program knows
            }
            catch
            {
                buf = null;
                buf3D = null;
                return;
            }
            //tell how to play sound
            buf3D = new DirectSound.Buffer3D(buf);
            //Buf3D.SetConeAngles(DxVBLibA.CONST_DSOUND.DS3D_MINCONEANGLE, 100, DxVBLibA.CONST_DS3DAPPLYFLAGS.DS3D_IMMEDIATE)
            //Buf3D.ConeAngles(DSoundHelper.MinConeAngle) = 100 'problem: fix this
            buf3D.ConeOutsideVolume = -400;
        }

        public void play(Vector3 sndPos, bool bLoop)
        {
            //don't play sound if you can barely hear it
            if (500 < Math.Pow(sndPos.X, 2) + Math.Pow(sndPos.Y, 2) + Math.Pow(sndPos.Z, 2))
                return;
            try
            {
                if (buf == null)
                    throw new Exception();
                //stop sound if it's playing already
                buf.Stop();
                buf.SetCurrentPosition(0);
                //set position & play sound
                buf3D.Position = sndPos;
                if (bLoop)
                {
                    buf.Play(0, DirectSound.BufferPlayFlags.Looping);
                }
                else
                {
                    buf.Play(0, DirectSound.BufferPlayFlags.Default);
                }
            }
            catch
            {
                //DS can be picky about files, use .NET API if DS doesn't work
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

    public struct SoundCopies
    {
        //so that copies of a sound can play at the same time
        public Sound[] snd;
        //last copy played
        public byte last;

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