﻿using System;
using System.Drawing;
using System.Windows;
using System.Windows.Media.Imaging;
using NLog;

namespace LibDmd.Output.PinDmd3
{
	/// <summary>
	/// Output target for PinDMD2 devices.
	/// </summary>
	/// <see cref="http://pindmd.com/"/>
	public class PinDmd3 : BufferRenderer, IFrameDestination
	{
		public bool IsRgb { get; } = true;

		/// <summary>
		/// Firmware string read from the device if connected
		/// </summary>
		public string Firmware { get; private set; }

		/// <summary>
		/// Width in pixels of the display, 128 for PinDMD3
		/// </summary>
		public override sealed int Width { get; } = 128;

		/// <summary>
		/// Height in pixels of the display, 32 for PinDMD3
		/// </summary>
		public override sealed int Height { get; } = 32;

		private static PinDmd3 _instance;
		private readonly PixelRgb24[] _frameBuffer;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Returns the current instance of the PinDMD API.
		/// </summary>
		/// <returns></returns>
		public static PinDmd3 GetInstance()
		{
			if (_instance == null) {
				_instance = new PinDmd3();
			} 
			_instance.Init();
			return _instance;
		}

		/// <summary>
		/// Constructor, initializes the DMD.
		/// </summary>
		private PinDmd3()
		{
			_frameBuffer = new PixelRgb24[Width * Height];
		}

		public void Init()
		{
			var port = Interop.Init(new Options() {
				DmdRed = 255,
				DmdGreen = 0,
				DmdBlue = 0,
				DmdColorize = 0
			});
			IsAvailable = port != 0;
			if (IsAvailable) {
				var info = GetInfo();
				Firmware = info.Firmware;
				Logger.Info("Found PinDMDv3 device.");
				Logger.Debug("   Firmware:    {0}", Firmware);
				Logger.Debug("   Resolution:  {0}x{1}", Width, Height);

				if (info.Width != Width || info.Height != Height) {
					throw new UnsupportedResolutionException("Should be " + Width + "x" + Height + " but is " + info.Width + "x" + info.Height + ".");
				}
			} else {
				Logger.Debug("PinDMDv3 device not found.");
			}
		}

		/// <summary>
		/// Returns width, height and firmware version of the connected DMD.
		/// 
		/// </summary>
		/// <remarks>Device must be connected, otherwise <seealso cref="SourceNotAvailableException"/> is thrown.</remarks>
		/// <returns>DMD info</returns>
		public DmdInfo GetInfo()
		{
			if (!IsAvailable) {
				throw new SourceNotAvailableException();
			}

			var info = new DeviceInfo();
			Interop.GetDeviceInfo(ref info);

			return new DmdInfo() {
				Width = info.Width,
				Height = info.Height,
				Firmware = info.Firmware
			};
		}

		/// <summary>
		/// Renders an image to the display.
		/// </summary>
		/// <param name="bmp">Any bitmap</param>
		public void Render(BitmapSource bmp)
		{
			// make sure we can render
			AssertRenderReady(bmp);

			var bytesPerPixel = (bmp.Format.BitsPerPixel + 7) / 8;
			var bytes = new byte[bytesPerPixel];
			var rect = new Int32Rect(0, 0, 1, 1);

			for (var y = 0; y < Height; y++) {
				for (var x = 0; x < Width; x++) {
					rect.X = x;
					rect.Y = y;
					bmp.CopyPixels(rect, bytes, bytesPerPixel, 0);
					_frameBuffer[(y * Width) + x].Red = bytes[2];
					_frameBuffer[(y * Width) + x].Green = bytes[1];
					_frameBuffer[(y * Width) + x].Blue = bytes[0];
				}
			}

			// send frame buffer to device
			Interop.RenderRgb24Frame(_frameBuffer);
		}

		public void Dispose()
		{
			Interop.DeInit();
		}
	}

	/// <summary>
	/// Defines width, height and firmware of the DMD.
	/// </summary>
	public class DmdInfo
	{
		public byte Width;
		public byte Height;
		public string Firmware;
	}
}
