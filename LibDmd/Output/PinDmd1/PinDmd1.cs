﻿using System;
using System.Windows;
using System.Windows.Media.Imaging;
using FTD2XX_NET;
using LibDmd.Common;
using NLog;

namespace LibDmd.Output.PinDmd1
{
	/// <summary>
	/// Output target for PinDMDv1 devices.
	/// </summary>
	/// <see cref="http://pindmd.com/"/>
	public class PinDmd1 : BufferRenderer, IFrameDestination
	{
		public bool IsRgb { get; } = false;

		public override sealed int Width { get; } = 128;
		public override sealed int Height { get; } = 32;

		private FTDI.FT_DEVICE_INFO_NODE _pinDmd1Device;
		private readonly byte[] _frameBuffer;

		private static readonly FTDI Ftdi = new FTDI();
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
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
				Logger.Error("Failed to get number of FTDI devices: {0}", status);
				return;
			}

			// if no FTDI device found, return.
			if (ftdiDeviceCount == 0) {
				Logger.Debug("PinDMDv1 device not found.");
				return;
			}

			// allocate storage for device info list
			var ftdiDeviceList = new FTDI.FT_DEVICE_INFO_NODE[ftdiDeviceCount];

			// populate device list
			status = Ftdi.GetDeviceList(ftdiDeviceList);
			if (status != FTDI.FT_STATUS.FT_OK) {
				Logger.Error("Failed to get FTDI devices: {0}", status);
				return;
			}

			// loop through list and find PinDMDv1
			for (uint i = 0; i < ftdiDeviceCount; i++) {
				var serialNumber = ftdiDeviceList[i].SerialNumber;
				if (serialNumber == "DMD1000" || serialNumber == "DMD1001") {
					_pinDmd1Device = ftdiDeviceList[i];
					IsAvailable = true;

					Logger.Info("Found PinDMDv1 device.");
					Logger.Debug("   Device Index:  {0}", i);
					Logger.Debug("   Flags:         {0:x}", _pinDmd1Device.Flags);
					Logger.Debug("   Type:          {0}", _pinDmd1Device.Type);
					Logger.Debug("   ID:            {0:x}", _pinDmd1Device.ID);
					Logger.Debug("   Location ID:   {0}", _pinDmd1Device.LocId);
					Logger.Debug("   Serial Number: {0}", _pinDmd1Device.SerialNumber);
					Logger.Debug("   Description:   {0}", _pinDmd1Device.Description);
					break;
				}
			}

			if (!IsAvailable) {
				Logger.Debug("PinDMDv1 device not found.");
				return;
			}

			// open device by serial number
			status = Ftdi.OpenBySerialNumber(_pinDmd1Device.SerialNumber);
			if (status != FTDI.FT_STATUS.FT_OK) {
				Logger.Error("Failed to open device: {0}", status);
				IsAvailable = false;
				return;
			}

			// set bit mode
			status = Ftdi.SetBitMode(0xff, 0x1);
			if (status != FTDI.FT_STATUS.FT_OK) {
				Logger.Error("Failed to set bit mode: {0}", status);
				IsAvailable = false;
				return;
			}

			// set baud rate
			status = Ftdi.SetBaudRate(12000);
			if (status != FTDI.FT_STATUS.FT_OK) {
				Logger.Error("Failed to set baud rate: {0}", status);
				IsAvailable = false;
				return;
			}
			Logger.Info("Connected to PinDMDv1.");
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
			// copy bitmap to frame buffer
			RenderGrey2(bmp, _frameBuffer, 4);

			// send frame buffer to device
			uint numBytesWritten = 0;
			var status = Ftdi.Write(_frameBuffer, _frameBuffer.Length, ref numBytesWritten);
			if (status != FTDI.FT_STATUS.FT_OK) {
				throw new Exception("Error writing to FTDI device: " + status);
			}
		}

		public void Dispose()
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
