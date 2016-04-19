﻿using System;
using System.Windows;
using System.Windows.Media.Imaging;
using FTD2XX_NET;
using PinDmd.Common;

namespace PinDmd.Output.PinDmd1
{
	/// <summary>
	/// Output target for PinDMD2 devices.
	/// </summary>
	public class PinDmd1 : IFrameDestination
	{
		public bool IsAvailable { get; private set; }

		public int Width { get; } = 128;
		public int Height { get; } = 32;

		private FTDI.FT_DEVICE_INFO_NODE _pinDmd1Device;
		private readonly byte[] _frameBuffer;

		private static readonly FTDI Ftdi = new FTDI();
		private static PinDmd1 _instance;
		
		private PinDmd1()
		{
			// 2 bits per pixel + 4 init pixels
			var size = (Width * Height / 4) * 4;
			_frameBuffer = new byte[size];
			_frameBuffer[0] = 0x81;    // frame sync bytes
			_frameBuffer[1] = 0xC3;
			_frameBuffer[2] = 0xE7;
			_frameBuffer[3] = 0x0;     // command byte
		}

		public void Init()
		{
			// get number of FTDI devices connected to the machine
			uint ftdiDeviceCount = 0;
			var status = Ftdi.GetNumberOfDevices(ref ftdiDeviceCount);
			if (status != FTDI.FT_STATUS.FT_OK) {
				Console.WriteLine("Failed to get number of FTDI devices: {0}", status);
				return;
			}

			// if no FTDI device found, return.
			if (ftdiDeviceCount == 0) {
				return;
			}

			// Allocate storage for device info list
			var ftdiDeviceList = new FTDI.FT_DEVICE_INFO_NODE[ftdiDeviceCount];

			// Populate our device list
			status = Ftdi.GetDeviceList(ftdiDeviceList);
			if (status != FTDI.FT_STATUS.FT_OK) {
				Console.WriteLine("Failed to get FTDI devices: {0}", status);
				return;
			}

			for (uint i = 0; i < ftdiDeviceCount; i++) {
				var serialNumber = ftdiDeviceList[i].SerialNumber;
				if (serialNumber == "DMD1000" || serialNumber == "DMD1001") {
					_pinDmd1Device = ftdiDeviceList[i];
					IsAvailable = true;

					Console.WriteLine("Found PinDMDv1:");
					Console.WriteLine("  Device Index: {0}", i);
					Console.WriteLine("  Flags: {0:x}", _pinDmd1Device.Flags);
					Console.WriteLine("  Type: {0}", _pinDmd1Device.Type);
					Console.WriteLine("  ID: {0:x}", _pinDmd1Device.ID);
					Console.WriteLine("  Location ID: {0}", _pinDmd1Device.LocId);
					Console.WriteLine("  Serial Number: {0}", _pinDmd1Device.SerialNumber);
					Console.WriteLine("  Description: {0}", _pinDmd1Device.Description);
					break;
				}
			}

			if (!IsAvailable) {
				Console.WriteLine("PinDMDv1 device not found.");
				return;
			}

			// open device by serial number
			status = Ftdi.OpenBySerialNumber(_pinDmd1Device.SerialNumber);
			if (status != FTDI.FT_STATUS.FT_OK) {
				Console.WriteLine("Failed to open device: {0}", status);
				IsAvailable = false;
				return;
			}

			// set bit mode
			status = Ftdi.SetBitMode(0xff, 0x1);
			if (status != FTDI.FT_STATUS.FT_OK) {
				Console.WriteLine("Failed to set bit mode: {0}", status);
				IsAvailable = false;
				return;
			}

			// set baud rate
			status = Ftdi.SetBaudRate(12000);
			if (status != FTDI.FT_STATUS.FT_OK) {
				Console.WriteLine("Failed to set baud rate: {0}", status);
				IsAvailable = false;
				return;
			}

			Console.WriteLine("Connected to PinDMDv1.");
		}

		/// <summary>
		/// Returns the current instance of the PinDMD2 API. In any case,
		/// the instance get (re-)initialized.
		/// </summary>
		/// <returns></returns>
		public static PinDmd1 GetInstance()
		{
			if (_instance == null) {
				_instance = new PinDmd1();
			}
			_instance.Init();
			return _instance;
		}

		/// <summary>
		/// Renders an image to the display.
		/// </summary>
		/// <param name="bmp">Any bitmap</param>
		public void Render(BitmapSource bmp)
		{
			if (!IsAvailable) {
				throw new SourceNotAvailableException();
			}
			if (bmp.PixelWidth != Width || bmp.PixelHeight != Height) {
				throw new Exception($"Image must have the same dimensions as the display ({Width}x{Height}).");
			}

			var bytesPerPixel = (bmp.Format.BitsPerPixel + 7) / 8;
			var bytes = new byte[bytesPerPixel];
			var rect = new Int32Rect(0, 0, 1, 1);
			var byteIdx = 4;

			for (var y = 0; y < Height; y++) {
				for (var x = 0; x < Width; x += 8)
				{
					byte bd0 = 0;
					byte bd1 = 0;

					for (var v = 7; v >= 0; v--) {

						rect.X = x + v;
						rect.Y = y;
						bmp.CopyPixels(rect, bytes, bytesPerPixel, 0);

						// convert to HSL
						double hue;
						double saturation;
						double luminosity;
						ColorUtil.RgbToHsl(bytes[2], bytes[1], bytes[0], out hue, out saturation, out luminosity);

						var pixel = (byte)Math.Round(luminosity * 255d);

						bd0 <<= 1;
						bd1 <<= 1;

						if ((pixel & 1) != 0) {
							bd0 |= 1;
						}

						if ((pixel & 2) != 0) {
							bd1 |= 1;
						}
					}

					_frameBuffer[byteIdx + 0] = bd0;
					_frameBuffer[byteIdx + 512] = bd1;
					byteIdx++;
				}
			}

			uint numBytesWritten = 0;
			var status = Ftdi.Write(_frameBuffer, _frameBuffer.Length, ref numBytesWritten);
			if (status != FTDI.FT_STATUS.FT_OK) {
				throw new Exception("Error writing to FTDI device: " + status);
			}
		}

		public void Destroy()
		{
			if (_pinDmd1Device != null) {
				Ftdi.SetBitMode(0x00, 0x0);
				Ftdi.Close();
				_pinDmd1Device = null;
				IsAvailable = false;
			}
		}
	}
}
