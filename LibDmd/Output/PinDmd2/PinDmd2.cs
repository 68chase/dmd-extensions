﻿using System;
using System.Windows;
using System.Windows.Media.Imaging;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using NLog;
using LibDmd.Common;

namespace LibDmd.Output.PinDmd2
{
	/// <summary>
	/// Output target for PinDMD2 devices.
	/// </summary>
	/// <see cref="http://pindmd.com/"/>
	public class PinDmd2 : BufferRenderer, IFrameDestination, IGray4
	{
		public string Name { get; } = "PinDMD v2";
		public bool IsRgb { get; } = false;

		public override sealed int Width { get; } = 128;
		public override sealed int Height { get; } = 32;

		private UsbDevice _pinDmd2Device;
		private readonly byte[] _frameBuffer;

		private static PinDmd2 _instance;
		private static readonly UsbDeviceFinder PinDmd2Finder = new UsbDeviceFinder(0x0314, 0xe457);
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private PinDmd2()
		{
			// 4 bits per pixel plus 4 init bytes
			var size = (Width * Height / 2) + 4;
			_frameBuffer = new byte[size];
			_frameBuffer[0] = 0x81;    // frame sync bytes
			_frameBuffer[1] = 0xC3;
			_frameBuffer[2] = 0xE7;    // overridden when rendering frame, here only for reference.
			_frameBuffer[3] = 0x0;
		}

		/// <summary>
		/// Returns the current instance of the PinDMD2 API. In any case,
		/// the instance get (re-)initialized.
		/// </summary>
		/// <returns></returns>
		public static PinDmd2 GetInstance()
		{
			if (_instance == null) {
				_instance = new PinDmd2();
			}
			_instance.Init();
			return _instance;
		}

		public void Init()
		{
			// find and open the usb device.
			var allDevices = UsbDevice.AllDevices;
			foreach (UsbRegistry usbRegistry in allDevices) {
				UsbDevice device;
				if (usbRegistry.Open(out device)) {
					if (device.Info.Descriptor.VendorID == 0x0314 && (device.Info.Descriptor.ProductID & 0xFFFF) == 0xe457) {
						_pinDmd2Device = device;
						break;
					}
				}
			}

			// if the device is open and ready
			if (_pinDmd2Device == null) {
				Logger.Debug("PinDMDv2 device not found.");
				IsAvailable = false;
				return;
			}
			_pinDmd2Device.Open();

			if (_pinDmd2Device.Info.ProductString.Contains("pinDMD V2")) {
				Logger.Info("Found PinDMDv2 device.");
				Logger.Debug("   Manufacturer: {0}", _pinDmd2Device.Info.ManufacturerString);
				Logger.Debug("   Product:      {0}", _pinDmd2Device.Info.ProductString);
				Logger.Debug("   Serial:       {0}", _pinDmd2Device.Info.SerialString);
				Logger.Debug("   Language ID:  {0}", _pinDmd2Device.Info.CurrentCultureLangID);

			} else {
				Logger.Debug("Device found but it's not a PinDMDv2 device ({0}).", _pinDmd2Device.Info.ProductString);
				IsAvailable = false;
				Dispose();
				return;
			}

			var usbDevice = _pinDmd2Device as IUsbDevice;
			if (!ReferenceEquals(usbDevice, null)) {
				usbDevice.SetConfiguration(1);
				usbDevice.ClaimInterface(0);
			}
			IsAvailable = true;
		}

		/// <summary>
		/// Renders an image to the display.
		/// </summary>
		/// <param name="bmp">Any bitmap</param>
		public void Render(BitmapSource bmp)
		{
			// copy bitmap to frame buffer
			RenderGray4(bmp, _frameBuffer, 4);

			// send frame buffer to device
			var writer = _pinDmd2Device.OpenEndpointWriter(WriteEndpointID.Ep01);
			int bytesWritten;
			var error = writer.Write(_frameBuffer, 2000, out bytesWritten);
			if (error != ErrorCode.None) {
				Logger.Error("Error sending data to device: {0}", UsbDevice.LastErrorString);
				throw new Exception(UsbDevice.LastErrorString);
			}
		}

		public void RenderGray4(BitmapSource bmp)
		{
			// that's all PinDMDv2 can do anyway..
			Render(bmp);
		}

		public void Dispose()
		{
			if (_pinDmd2Device != null && _pinDmd2Device.IsOpen) {
				var wholeUsbDevice = _pinDmd2Device as IUsbDevice;
				if (!ReferenceEquals(wholeUsbDevice, null)) {
					wholeUsbDevice.ReleaseInterface(0);
				}
				_pinDmd2Device.Close();
			}
			_pinDmd2Device = null;
			UsbDevice.Exit();
		}
	}
}
