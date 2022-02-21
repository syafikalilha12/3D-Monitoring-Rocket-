/*
Direct3DControl (version 11)
Copyright (C) 2015 by Jeremy Spiller.  All rights reserved.

Redistribution and use in source and binary forms, with or without 
modification, are permitted provided that the following conditions are met:

   1. Redistributions of source code must retain the above copyright 
      notice, this list of conditions and the following disclaimer.
   2. Redistributions in binary form must reproduce the above copyright 
      notice, this list of conditions and the following disclaimer in 
      the documentation and/or other materials provided with the distribution.
 
THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE 
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE 
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE 
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR 
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN 
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) 
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE 
POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;


namespace Gosub
{
	/// <summary>
	/// DirectX in a control, by Jeremy Spiller
	/// </summary>
	public class Direct3d : System.Windows.Forms.UserControl
	{
		// Control and DirectX stuff
		private IContainer components = null; // Windows designer code
		Device mDx;
		D3DEnumeration mEnumerationSettings = new D3DEnumeration();
		D3DSettings mGraphicsSettings = new D3DSettings();

		// Form and control info
		bool mControlVisible;
		Rectangle mControlRectangle = new Rectangle();
		bool mFormVisible;
		Rectangle mFormRectangle = new Rectangle();
		FormBorderStyle mFormBorderStyle;
		MainMenu mFormMainMenu;
		Form[] mFormOwnedForms;

		// Debug info
		public int DxDebugNumAutoVertexBuffers;
		public int DxDebugNumAutoIndexBuffers;
		public int DxDebugNumAutoTextures;
		public int DxDebugNumAutoMeshes;

		// Thread stuff
		Thread mThread;
		DrawState mDrawState;
		volatile int mFps;
		bool mDirectXSetup;

		// Mouse and screen info
		Vector2 mScreenPixel;
		Vector2 mInverseScreenPixel;
		Vector2 mViewportSize;
		Point mDxMouseLastPosition;
		bool mMouseHovering;

		// Flags
		bool mControlLoaded;
		bool mAutoResize;
		bool mFullScreenCurrent;
		bool mFullScreenRequest;
		bool mForceDeviceUpdate;
		bool mInRender;
		bool mEverHibernated;
		bool mDxSimulateFullScreen;
		int  mDxBackBufferCount = 2;

		// Thread/UI draw state
		enum DrawState
		{
			Disabled,
			ReadyToDraw,
			ReadyToPresent,
			DeviceLost,
			Exit
		}

		/// <summary>
		/// DirectX events.
		/// </summary>
		public delegate void DxDirect3dDelegate(Direct3d d3d, Device dx);
		public delegate void DxDirect2dDelegate(Direct3d d3d, Device dx, Surface surface, Graphics graphics);

		/// <summary>
		/// Returns the DirectX device.  Do not store this, as it can
		/// change.  NOTE: The DirectX device is NULL until the
		/// control is loaded, and is NULL if there was an error.
		/// </summary>
		public Device Dx { get { return mDx; } }

		/// <summary>
		/// Occurs once after DirectX has been initialized for the first time.  
		/// Setup AutoMesh's, AutoVertexBuffer's, and AutoTexture's here.
		/// </summary>
		public event DxDirect3dDelegate DxLoaded;

		/// <summary>
		/// Occurs when a new DirectX device has been initialized.
		/// Setup lights and restore DX objects here.
		/// NOTE: We don't have control over when this may happen.
		/// </summary>
		public event DxDirect3dDelegate DxRestore;

		/// <summary>
		/// This event is called whenever DirectX decides to toss our surfaces.
		/// Delete DX objects here (but not AutoMesh, etc.)
		/// </summary>
		public event DxDirect3dDelegate DxLost;

		/// <summary>
		/// Occurs when the surface is resized.
		/// </summary>
		public event DxDirect3dDelegate DxResizing;

		/// <summary>
		/// Occurs before 3d rendering.  When this event is used, you
		/// must manually clear the screen (use dx.Clear).
		/// </summary>
		public event DxDirect3dDelegate DxRenderPre;

		/// <summary>
		/// Occurs when it is time to render 3d objects.  Place all 3d
		/// drawing code in this event.
		/// </summary>
		public event DxDirect3dDelegate DxRender3d;

		/// <summary>
		/// Occurs after Render3d, to draw 2d graphics over the 3d scene.
		/// There is a speed penalty when using this event.
		/// </summary>
		public event DxDirect2dDelegate DxRender2d;


		/// <summary>
		/// When true, the control autoresizes to fill the whole form.
		/// NOTE: Setting this back to false leaves the control the scale
		/// of the whole form.
		/// </summary>
		public bool DxAutoResize
		{
			get { return mAutoResize; }
			set { mAutoResize = value; }
		}

		/// <summary>
		/// Get/Set full screen mode.  In full screen mode, the form that 
		/// this control is on is expanded to the full screen, and the Direct3d
		/// control is also expanded.  The menu is removed.  When returning to
		/// windowed mode, the form, control, and menu is restored.  Changing
		/// this property causes the device to recreated (see DxForceDeviceUpdate).
		/// WARNING: Any forms owned by the parent form of this control are unlinked
		/// (ChildForm.Owner = null) when switched to full screen mode.  This is done
		/// to prevent a child form from blocking the user input since the form
		/// wouldn't normally be visible.  
		/// </summary>
		public bool DxFullScreen
		{
			get	{ return mFullScreenRequest; }
			set 
			{ 
				if (mFullScreenRequest == value)
					return;
				mFullScreenRequest = value;
				DxForceDeviceUpdate();
			}
		}

		/// <summary>
		/// Delete and recreate the DirectX device.  If called from within
		/// the draw event, the device will be recreated on the next render pass.
		/// </summary>
		public void DxForceDeviceUpdate()
		{
			if (mDirectXSetup && ParentForm != null && !mInRender)
				SetupDirectX();
			else
				mForceDeviceUpdate = true;
		}

		/// <summary>
		/// When true, full screen mode is simulated in a window that is expanded
		/// to fit the whole screen.  This allows dialogs to be displayed on top
		/// of the Direct3D device.  This property is automatically set to true if
		/// the video card runs out of video memory when the DxBackBuffer count is 1.
		/// Changing this property does not automatically recreate the device.
		/// Call DxForceDeviceUpdate() to force the setting to take effect.
		/// WARNING: Any forms owned by the parent form of this control are unlinked
		/// (ChildForm.Owner = null) when switched to full screen mode.  This is done
		/// to prevent a child form from blocking the user input since the form
		/// wouldn't normally be visible.  
		/// </summary>
		public bool DxSimulateFullScreen
		{
			get { return mDxSimulateFullScreen; }
			set { mDxSimulateFullScreen = value; }
		}

		/// <summary>
		/// This property may be set to 1 or 2, and should usually be 2 (which is the default).
		/// If the video card runs out of video memory, the back buffer count is automatically
		/// set to 1.  
		/// Chaning this property does not automatically recreate the device.
		/// Call DxForceDeviceUpdate() to force the setting to take effect.
		/// </summary>
		public int DxBackBufferCount
		{
			get { return mDxBackBufferCount; }
			set { mDxBackBufferCount = Math.Min(2, Math.Max(1, value)); }
		}

		/// <summary>
		/// Frames per second
		/// </summary>
		public int DxFPS { get { return mFps; } }

		/// <summary>
		/// Convert view port coordinates (ie. mouse) to screen coordinates (-1..1)
		/// </summary>
		public Vector3 DxViewToScreen(Vector3 point)
		{
			if (mDx != null)
				return new Vector3(point.X / mDx.Viewport.Width * 2 - 1,
							-(point.Y / mDx.Viewport.Height * 2 - 1), 0);
			return new Vector3();
		}

		/// <summary>
		/// Convert unit coordinates (-1..1) to view port coordinates (ie. mouse)
		/// </summary>
		public Vector3 DxScreenToView(Vector3 point)
		{
			if (mDx != null)
				return new Vector3((point.X + 1) * mDx.Viewport.Width / 2,
								(-point.Y + 1) * mDx.Viewport.Height / 2, 0);
			return new Vector3();
		}

		/// <summary>
		/// DirectX in a control, by Jeremy Spiller
		/// </summary>
		public Direct3d()
		{
			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();
			
			// Create graphics sample
			mEnumerationSettings.ConfirmDeviceCallback = delegate { return true; };
			mEnumerationSettings.Enumerate();

			// Choose the initial settings for the application
			bool foundFullscreenMode = mGraphicsSettings.FindBestFullscreenMode(false, false, mEnumerationSettings);
			bool foundWindowedMode = mGraphicsSettings.FindBestWindowedMode(false, false, mEnumerationSettings, ClientRectangle);

			// Window or Full Screen not found
			if (!foundFullscreenMode && !foundWindowedMode)
				throw new Direct3dException("No compatible devices found");
		}

		/// <summary>
		/// Load event to setup the control
		/// </summary>
		private void Direct3d_Load(object sender, System.EventArgs e)
		{
			// Don't setup the control when inside the forms designer or inside another control
			if (ParentForm == null)
				return;

			// Initialize the 3D environment for this control
			mDrawState = DrawState.Disabled;
			mThread = new Thread((ThreadStart)delegate { ThreadPresentLoop(); });
			mThread.Start();
			mDxMouseLastPosition = new Point(Size.Width/2, Size.Height/2);
			
			// Detect when the computer goes in to hibernation
			// Unfortunately, when it comes out of hibernation, the
			// VSYNC (PresentInterval of one) is ruined in windowed mode.
			Microsoft.Win32.SystemEvents.PowerModeChanged +=
				delegate(object powerSender, Microsoft.Win32.PowerModeChangedEventArgs powerArgs)
				{
					if (powerArgs.Mode == Microsoft.Win32.PowerModes.Suspend)
						mEverHibernated = true;
				};

			// Force DirectX to initialize the window
			mForceDeviceUpdate = true;
			TryRenderControl();
		}

		/// <summary>
		/// Disable threading before allowing the handle to be destroyed
		/// </summary>
		protected override void OnHandleDestroyed(EventArgs e)
		{
			SetDrawStateAfterPresenting(DrawState.Exit);
			if (mThread != null)
			{
				mThread.Join();
				mThread = null;
			}
			DeleteDirectxDevice();
			base.OnHandleDestroyed(e);
		}

		/// <summary> 
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose(bool disposing)
		{
			SetDrawStateAfterPresenting(DrawState.Exit);
			if (mThread != null)
			{
				mThread.Join();
				mThread = null;
			}
			DeleteDirectxDevice();
			
			if (disposing && components != null)
				components.Dispose();
			
			base.Dispose(disposing);
		}

        /// <summary>
        /// Prevent control from becoming too small
        /// </summary>
        protected override void OnSizeChanged(EventArgs e)
        {
            if (Width < 8)
                Width = 8;
            if (Height < 8)
                Height = 8;
            base.OnSizeChanged(e);
        }

		/// <summary>
		/// Set our variables to not active and not ready.
		/// NOTE: mDrawState must be Disabled (or Exit) before calling this function
		/// </summary>
		void DeleteDirectxDevice()
		{
			Debug.Assert(mDrawState == DrawState.Disabled || mDrawState == DrawState.Exit);

			if (mDx != null)
			{
				DxLostInternal(null, null);
				mDx.Dispose();
				mDx = null;
			}
		}

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.SuspendLayout();
			// 
			// Direct3d
			// 
			this.Name = "Direct3d";
			this.Load += new System.EventHandler(this.Direct3d_Load);
			this.ResumeLayout(false);

		}

		/// <summary>
		/// Safely set DrawState after presenting the frame.
		/// </summary>
		void SetDrawStateAfterPresenting(DrawState drawState)
		{
			Debug.Assert(drawState != DrawState.ReadyToPresent);
			if (mThread == null)
				return;
			bool assigned = false;
			while (!assigned)
			{
				// Assign new state (if not presenting)
				lock (mThread)
				{
					if (mDrawState != DrawState.ReadyToPresent)
					{
						mDrawState = drawState;
						assigned = true;
					}
				}
				// Wait for present to complete
				if (!assigned)
					Thread.Sleep(1);
			}
		}

		/// <summary>
		/// Try to create the DirectX device.  Returns the device, or NULL if it failed.
		/// </summary>
		/// <returns></returns>
		Device CreateDevice()
		{
			// --------------------------------------------------------------
			// Set up presentation parameters from current settings
			// --------------------------------------------------------------
			PresentParameters presentParams = new PresentParameters();
			presentParams.Windowed = !mFullScreenRequest || mDxSimulateFullScreen;
			presentParams.MultiSample = mGraphicsSettings.MultisampleType;
			presentParams.MultiSampleQuality = mGraphicsSettings.MultisampleQuality;
			presentParams.SwapEffect = SwapEffect.Discard;
			presentParams.EnableAutoDepthStencil = mEnumerationSettings.AppUsesDepthBuffer;
			presentParams.AutoDepthStencilFormat = mGraphicsSettings.DepthStencilBufferFormat;
			presentParams.FullScreenRefreshRateInHz = 0;
			presentParams.BackBufferFormat = mGraphicsSettings.DeviceCombo.BackBufferFormat;
			presentParams.BackBufferCount = mDxBackBufferCount;

			// If doing 2D graphics, allow a lockable back buffer.
			presentParams.PresentFlag = DxRender2d == null ? PresentFlag.None : PresentFlag.LockableBackBuffer;

			if (presentParams.Windowed)
			{
				// Windowed mode parameters
				presentParams.BackBufferWidth = ClientRectangle.Right - ClientRectangle.Left;
				presentParams.BackBufferHeight = ClientRectangle.Bottom - ClientRectangle.Top;
				presentParams.DeviceWindow = this;

				// If Windows goes in to hibernation, VSYNC (PresentInterval of one)
				// no longer works.  I don't know why, but this works better than nothing.
				presentParams.PresentationInterval 
						= mEverHibernated ? PresentInterval.Immediate : PresentInterval.One;
			}
			else
			{
				// Full screen mode parameters
				presentParams.BackBufferWidth = mGraphicsSettings.DisplayMode.Width;
				presentParams.BackBufferHeight = mGraphicsSettings.DisplayMode.Height;
				presentParams.DeviceWindow = this.Parent;
				presentParams.PresentationInterval = PresentInterval.One;
			}

			// --------------------------------------------------------------
			// Create flags
			// --------------------------------------------------------------
			CreateFlags createFlags = new CreateFlags();
			if (mGraphicsSettings.VertexProcessingType == VertexProcessingType.Software)
				createFlags = CreateFlags.SoftwareVertexProcessing;
			else if (mGraphicsSettings.VertexProcessingType == VertexProcessingType.Mixed)
				createFlags = CreateFlags.MixedVertexProcessing;
			else if (mGraphicsSettings.VertexProcessingType == VertexProcessingType.Hardware)
				createFlags = CreateFlags.HardwareVertexProcessing;
			else if (mGraphicsSettings.VertexProcessingType == VertexProcessingType.PureHardware)
				createFlags = CreateFlags.HardwareVertexProcessing | CreateFlags.PureDevice;
			else
				throw new Direct3dException("Incorrect VertexProcessingType");

			// This application can be multithreaded
			createFlags |= CreateFlags.MultiThreaded;

			// --------------------------------------------------------------
			// Create the device
			// --------------------------------------------------------------
            Device device = new Device(mGraphicsSettings.AdapterOrdinal,
                                    mGraphicsSettings.DevType, this, createFlags, presentParams);

			// Sometimes Device.Clear fails with an exception, so we
			// want to catch that early (for the retry in SetupDirectX)
			try
			{
				device.Clear(ClearFlags.Target | ClearFlags.ZBuffer, BackColor.ToArgb(), 1.0f, 0);
			}
			catch
			{
				try { device.Dispose(); } catch { }
				throw;
			}
			return device;
		}

		/// <summary>
		/// Setup the form for full screen or windowed mode
		/// based on mFullScreenRequest.
		/// </summary>
		void SetFormScreenParameters()
		{
			// Save normal form settings (or initial settings when starting up minimized/maximized)
			if (!mFullScreenCurrent)
			{
				// Save form setting only when window state is normal
				if (ParentForm.WindowState == FormWindowState.Normal
							|| mFormRectangle.Size == new Size())
				{
					mFormBorderStyle = ParentForm.FormBorderStyle;
					mFormRectangle.Location = ParentForm.Location;
					mFormRectangle.Size = ParentForm.Size;
					mFormMainMenu = ParentForm.Menu;
					mFormVisible = ParentForm.Visible;
				}
				// Save control settings before entering full screen mode
				mControlRectangle.Location = this.Location;
				mControlRectangle.Size = this.Size;
				mControlVisible = this.Visible;
			}

			// Switch from windowed mode to full screen mode
			if (!mFullScreenCurrent && mFullScreenRequest)
			{
				// Save owned forms, and get rid of them so they can't overlap the full screen
				mFormOwnedForms = ParentForm.OwnedForms;
				for (int i = 0; i < mFormOwnedForms.Length; i++)
					mFormOwnedForms[i].Owner = null;

				// Setup form to be full screen mode
				if (ParentForm.WindowState == FormWindowState.Minimized)
					ParentForm.WindowState = FormWindowState.Normal;
				ParentForm.FormBorderStyle = FormBorderStyle.None;
				ParentForm.Menu = null;
				ParentForm.Visible = true;
				Location = new Point(0, 0);
				Size = new Size(mGraphicsSettings.DisplayMode.Width, mGraphicsSettings.DisplayMode.Height);
				Visible = true;
				ParentForm.BringToFront();  // This form must be on top
				BringToFront(); // This control must be on top
			}

			// Switch from full screen to windowed mode
			if (mFullScreenCurrent && !mFullScreenRequest)
			{
				// Restore form parameters
				ParentForm.FormBorderStyle = mFormBorderStyle;
				ParentForm.Location = mFormRectangle.Location;
				ParentForm.Size = mFormRectangle.Size;
				if (mFormMainMenu != null)
					ParentForm.Menu = mFormMainMenu;

				// Restore control parameters
				Location = mControlRectangle.Location;
				Size = mControlRectangle.Size;
				Visible = mControlVisible;

				// Display this form
				ParentForm.Visible = mFormVisible;
				ParentForm.BringToFront();
				ParentForm.Focus();

				// Restore owned forms
				Form[] newOwnedForms = ParentForm.OwnedForms;
				if (mFormOwnedForms != null)
					for (int i = 0; i < mFormOwnedForms.Length; i++)
					{
						mFormOwnedForms[i].Owner = ParentForm;
						mFormOwnedForms[i].BringToFront();
						mFormOwnedForms[i].Focus();
					}
				mFormOwnedForms = null;

				// Presumably if a new form popped up, it should have the focus
				for (int i = 0; i < newOwnedForms.Length; i++)
				{
					newOwnedForms[i].BringToFront();
					newOwnedForms[i].Focus();
				}
			}

			// Set new full screen mode
			mFullScreenCurrent = mFullScreenRequest;
		}

		/// <summary>
		/// Setup the DirectX window.
		/// </summary>
		void SetupDirectX()
		{
			mDirectXSetup = true;
			SetDrawStateAfterPresenting(DrawState.Disabled);

			// If in full screen (or going to full screen), display a black 
			// form to conver the screen and prevent flickering
			Form blackForm = null;
			if (mFullScreenCurrent || mFullScreenRequest)
			{
				// We need to force a redraw so the form is the correct size,
				// and is not hidden by the task bar.  Display a black form
				// while doing this, so there is not as much annoying flicker.
				blackForm = new Form();
				blackForm.WindowState = FormWindowState.Maximized;
				blackForm.ControlBox = false;
				blackForm.MinimizeBox = false;
				blackForm.MaximizeBox = false;
				blackForm.ShowInTaskbar = false;
				blackForm.BackColor = Color.Black;
				blackForm.Show();
			}
						
			// Delete old Dx object and setup the form for full screen or windowed mode
			DeleteDirectxDevice();
			SetFormScreenParameters();

			// Setup the DirectX device
			mGraphicsSettings.IsWindowed = !mFullScreenRequest;
			mDx = null;
			bool outOfVideoMemory = false;
			try 
				{ mDx = CreateDevice(); } 
			catch (OutOfVideoMemoryException)
				{ outOfVideoMemory = true; }
			catch (Exception)
				{ /* Once in a while DirectX fails for no reason (so retry below) */ }

            // Retry creating device on failure (when not out of video memory)
            if (mDx == null && !outOfVideoMemory)
            {
                Thread.Sleep(50);
				try 
					{ mDx = CreateDevice(); } 
				catch (OutOfVideoMemoryException)
					{ outOfVideoMemory = true; }
				catch (Exception)
					{ /* dead - one more try below */ }
            }

			// When out of video memory, retry with a back buffer count of 1
			if (outOfVideoMemory && mDxBackBufferCount >= 2)
			{
				mDxBackBufferCount = 1;
				outOfVideoMemory = false;
				try 
					{ mDx = CreateDevice(); } 
				catch (OutOfVideoMemoryException)
					{ outOfVideoMemory = true; }
				catch (Exception)
					{ /* Once in a while DirectX fails for no reason (so retry below) */ }
			}

            // When still out of video memory, retry in simulated window mode
			if (outOfVideoMemory && !mDxSimulateFullScreen)
			{
				mDxSimulateFullScreen = true;
				outOfVideoMemory = false;
				try 
					{ mDx = CreateDevice(); } 
				catch (OutOfVideoMemoryException)
					{ outOfVideoMemory = true; }
				catch (Exception)
					{ /* Once in a while DirectX fails for no reason (so retry below) */ }
			}
            
            // One last retry
            if (mDx == null)
            {
				outOfVideoMemory = false;
                Thread.Sleep(50);
				try 
					{ mDx = CreateDevice(); } 
				catch (OutOfVideoMemoryException)
					{ outOfVideoMemory = true; }
				catch (Exception)
					{ /* dead! */ }
            }

			if (blackForm != null)
				blackForm.Close();

			// If the DirectX device can't be setup, restore and throw an error
			if (mDx == null)
			{
				mFullScreenRequest = false;
				SetFormScreenParameters();
				if (outOfVideoMemory)
					throw new Direct3dException("Out of video memory");
				throw new Direct3dException("Error creating DirectX device");
			}

			// Warn user about null ref device that can't render anything
			if (mGraphicsSettings.DeviceInfo.Caps.PrimitiveMiscCaps.IsNullReference)
			{
				mFullScreenRequest = false;
				SetFormScreenParameters();
				throw new Direct3dException("Null rendering device can't render anything");
			}

			// Setup the event handlers
			mDx.DeviceReset += new System.EventHandler(this.DxRestoreInternal);
			mDx.DeviceLost += new System.EventHandler(this.DxLostInternal);
			mDx.DeviceResizing += new System.ComponentModel.CancelEventHandler(this.DxResizeInternal);

			// Initialize device-dependent objects
			DxResizeInternal(null, null);
			if (!mControlLoaded && DxLoaded != null)
				DxLoaded(this, mDx);
			mControlLoaded = true;
			DxRestoreInternal(null, null);

			lock (mThread)
				mDrawState = DrawState.ReadyToDraw;
		}

		/// <summary>
		/// Draws the scene for this instance of the control.  This is
		/// called in the UI thread, so Windows Forms can be used inside
		/// the DxRender3d function.
		/// </summary>
		void RenderControl()
		{
			// Get current draw state
			DrawState drawState;
			lock (mThread)
				drawState = mDrawState;

			// Need a device update for FullScreen
			if (mForceDeviceUpdate)
			{
				mForceDeviceUpdate = false;
				SetupDirectX();
				lock (mThread)
					drawState = mDrawState;
			}

			// Check for lost device
			if (drawState == DrawState.DeviceLost)
			{
				try
				{
					// Test the cooperative level to see if it's okay to render
					mDx.TestCooperativeLevel();
				}
				catch (DeviceLostException)
				{
					// If the device was lost, do not render until we get it back
					return;
				}
				catch (DeviceNotResetException)
				{
					// Reset the device and resize it
					SetupDirectX();
					lock (mThread)
						drawState = mDrawState;
				}
			}

			// --------------------------------------------------------------
			// Exit if not ready to draw
			// --------------------------------------------------------------
			if (drawState != DrawState.ReadyToDraw)
				return;
			
			// Optionally autoresize this control to fit the form
			if (mAutoResize && !mFullScreenCurrent
				&& (Location != new Point(0, 0) || Size != ParentForm.ClientSize))
			{
				Location = new Point(0, 0);
				if (ParentForm.ClientSize.Width > 0 && ParentForm.ClientSize.Height > 0)
					Size = ParentForm.ClientSize;
			}

			// Ensure this control has the focus in full screen mode
			if (mFullScreenCurrent && !Focused)
				Focus();

			// Draw scene
			if (DxRenderPre == null)
				mDx.Clear(ClearFlags.Target | ClearFlags.ZBuffer,
							BackColor.ToArgb(), 1.0f, 0);
			else
				DxRenderPre(this, mDx);

			// RenderDelegate the scene
			mDx.BeginScene();
			try
			{
				if (DxRender3d != null)
					DxRender3d(this, mDx);
			}
			finally
			{
				mDx.EndScene();

				if (DxRender2d != null)
				{
					using (Surface surface = mDx.GetBackBuffer(0, 0, BackBufferType.Mono))
					using (Graphics graphics = surface.GetGraphics())
					{
						DxRender2d(this, mDx, surface, graphics);
					}
				}

				// Scene is presented in other thread
				lock (mThread)
					mDrawState = DrawState.ReadyToPresent;			
			}
		}

		/// <summary>
		/// Draws the scene for this instance of the control.  This is
		/// called in the UI thread, so Windows Forms can be used inside
		/// the DxRender3d function.
		/// </summary>
		void TryRenderControl()
		{
			// This can happen when Application.DoEvents is called in
			// user event code, or in the initialize function.
			if (mInRender)
				return; 
			mInRender = true;
			try
			{
				RenderControl();
			}
			finally
			{
				mInRender = false;
			}
		}

		/// <summary>
		/// This runs in a separate thread.  The control is rendered in
		/// the UI thread, and presented in this thread.
		/// </summary>
		void ThreadPresentLoop()
		{
			int frame = 0;
			int seconds = 0;
			IAsyncResult asyncResult = null;

			while (true)
			{
				// Get draw state - exit, render, or do nothing
				bool render =  false;
				lock (mThread)
				{
					if (mDrawState == DrawState.Exit)
						return;
					if (mDrawState == DrawState.ReadyToDraw || mDrawState == DrawState.DeviceLost)
						if (asyncResult == null	|| asyncResult.IsCompleted)
						{
							asyncResult = null;
							render = true;
						}
				}
				// Ask control to render itself when it's ready to draw
				if (render)
				{
					try 
					{ 
						asyncResult = BeginInvoke((MethodInvoker)delegate() { TryRenderControl(); }); 
					}
					catch { };
				}

				// Maintain frame count
				int s = DateTime.Now.Second;
				if (s != seconds)
				{
					mFps = frame;
					frame = 0;
					seconds = s;
				}

				// Always sleep
				Thread.Sleep(5);

				// Present the frame if it's ready to be presented
				DrawState drawState;
				lock (mThread)
					drawState = mDrawState;
				if (drawState == DrawState.ReadyToPresent)
				{
					try
					{
						mDx.Present();
						frame++;
						lock (mThread)
							mDrawState = DrawState.ReadyToDraw;
					}
					catch
					{
						lock (mThread)
							mDrawState = DrawState.DeviceLost;
					}
				}
			}
		}

		/// <summary>
		/// Called when our environment was resized
		/// </summary>
		void DxResizeInternal(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (mDx != null)
			{
				mScreenPixel = new Vector2(2f / mDx.Viewport.Width, 2f / mDx.Viewport.Height);
				mInverseScreenPixel = new Vector2(1 / mScreenPixel.X, 1 / mScreenPixel.Y);
				mViewportSize = new Vector2(mDx.Viewport.Width, mDx.Viewport.Height);

				if (DxResizing != null)
					DxResizing(this, mDx);
			}
		}

		/// <summary>
		/// Called when a device needs to be restored.
		/// </summary>
		void DxRestoreInternal(System.Object sender, System.EventArgs e)
		{
			if (DxRestore != null)
				DxRestore(this, mDx);
		}

		/// <summary>
		/// Called when DirectX tosses the surface
		/// </summary>
		void DxLostInternal(System.Object sender, System.EventArgs e)
		{
			if (DxLost != null)
				DxLost(this, mDx);
		}

		/// <summary>
		/// Displays a dialog so the user can select a new adapter, device, or
		/// display mode, and then recreates the 3D environment if needed
		/// </summary>
		public void DxShowSelectDeviceDialog(Form owner)
		{
			// Can't display dialogs in fullscreen mode
			this.DxFullScreen = false;

			// Make sure the main form is in the background
			this.SendToBack();

			// --- Display settings dialog ---
			D3DSettingsForm settingsForm = new D3DSettingsForm(mEnumerationSettings, mGraphicsSettings);
			System.Windows.Forms.DialogResult result = settingsForm.ShowDialog(owner);
			if (result != System.Windows.Forms.DialogResult.OK)
				return; // User hit cancel

			mGraphicsSettings = settingsForm.settings;

			// Setup to change modes when next frame is rendered
			DxFullScreen = !mGraphicsSettings.IsWindowed;
			mForceDeviceUpdate = true;
		}

		/// <summary>
		/// Gets the current device settings.  NOTE: This is an internal
		/// reference, so be careful.
		/// </summary>
		public D3DSettings DxGetDeviceSettings()
		{
			return mGraphicsSettings;
		}

		/// <summary>
		/// Sets the current device settings.  NOTE: This is an internal
		/// reference, so be careful.
		/// </summary>
		public void DxSetDeviceSettings(D3DSettings settings)
		{
			mGraphicsSettings = settings;
			DxFullScreen = !mGraphicsSettings.IsWindowed;
			mForceDeviceUpdate = true;
		}

		/// <summary>
		/// Returns the scale of the view port
		/// </summary>
		public Vector2 DxViewportSize { get { return mViewportSize; } }

		/// <summary>
		/// Returns the scale of a screen pixel in view port units
		/// </summary>
		public Vector2 DxScreenPixel { get { return mScreenPixel; } }

		/// <summary>
		/// Convert a screen coordinate (-1 to 1) to viewport coordinate (ie. mouse Position).
		/// </summary>
		public Vector2 DxScreenToView(Vector2 screen)
		{
			return new Vector2((screen.X + 1) * mInverseScreenPixel.X,
								(-screen.Y + 1) * mInverseScreenPixel.Y);
		}

		/// <summary>
		/// Returns TRUE if the mouse is hovering over the view port
		/// </summary>
		public bool DxMouseHovering
		{
			get { return mMouseHovering; }
		}

		/// <summary>
		/// Returns a ray that represents the mouse in world coordinates.  The
		/// viewProj parameter should be Transform.View * Transform.Projection.
		/// </summary>
		public void DxMouseRay(Matrix viewProj, out Vector3 start, out Vector3 direction, float x, float y)
		{
			Matrix worldToScreen = viewProj;
			Matrix screenToWorld = Matrix.Invert(worldToScreen);
						
			// Ray starts at the camera near clipping plane
			Vector3 mouseFront = Vector3.TransformCoordinate(
									new Vector3(x, y, 0), screenToWorld);
			
			// Ray ends at far clipping plane
			Vector3 mouseBack = Vector3.TransformCoordinate(
									new Vector3(x, y, 1), screenToWorld);
			
			start = mouseFront;
			direction = mouseBack - mouseFront;
		}

		/// <summary>
		/// Returns a ray that represents the mouse in world coordinates.  The
		/// viewProj parameter should be Transform.View * Transform.Projection.
		/// </summary>
		public void DxMouseRay(Matrix viewProj, out Vector3 start, out Vector3 direction)
		{
			Vector2 screen = DxMouseLastScreenPosition;
			DxMouseRay(viewProj, out start, out direction, screen.X, screen.Y);
		}

		/// <summary>
		/// Convert a viewport coordinate (ie. mouse Position) to a screen coordinate (-1 to 1)
		/// </summary>
		public Vector2 DxViewToScreen(Point view)
		{
			return new Vector2(view.X * mScreenPixel.X - 1,
							   -(view.Y * mScreenPixel.Y - 1));
		}

		/// <summary>
		/// Returns the last mouse Position in screen coordinates 
		/// (center of screen if mouse not DxMouseHovering)
		/// </summary>
		public Vector2 DxMouseLastScreenPosition
		{
			get { return DxViewToScreen(mDxMouseLastPosition); }
		}

		/// <summary>
		/// Returns the last mouse Position in view coordinates 
		/// (center of screen if mouse not DxMouseHovering)
		/// </summary>
		public Point DxMouseLastPosition
		{
			get { return mDxMouseLastPosition; }
		}

		/// <summary>
		/// Mouse enters control
		/// </summary>
		protected override void OnMouseEnter(EventArgs e)
		{
			mMouseHovering = true;
			base.OnMouseEnter(e);
		}

		/// <summary>
		/// Mouse moves (update last mouse Position, and set DxD3d cursor)
		/// </summary>
		protected override void OnMouseMove(MouseEventArgs e)
		{
			mDxMouseLastPosition.X = e.X;
			mDxMouseLastPosition.Y = e.Y;
			base.OnMouseMove(e);
		}

		/// <summary>
		/// Mouse leaves control
		/// </summary>
		protected override void OnMouseLeave(EventArgs e)
		{
			mMouseHovering = false;
			mDxMouseLastPosition = new Point(Size.Width/2, Size.Height/2);
			base.OnMouseLeave(e);
		}
	}

	//-----------------------------------------------------------------------
	// Exception thrown by this class
	//-----------------------------------------------------------------------
	public class Direct3dException : Exception
	{
		public Direct3dException(string m) : base(m) { }
	}

}
