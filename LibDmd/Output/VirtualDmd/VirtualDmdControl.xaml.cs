﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;

namespace LibDmd.Output.VirtualDmd
{
	/// <summary>
	/// Interaction logic for VirtualDmdControl.xaml
	/// </summary>
	public partial class VirtualDmdControl : UserControl, IFrameDestination, IGray4, IGray2, IRgb24
	{
		public bool IsAvailable { get; } = true;
		public bool IsRgb { get; } = true;

		private double _hue;
		private double _sat;
		private double _lum;

		private Color[] _gray2Palette;
		private Color[] _gray4Palette;

		public VirtualDmdControl()
		{
			InitializeComponent();
			SetColor(Color.FromRgb(0xff, 0x30, 0));
		}

		public void Render(BitmapSource bmp)
		{
			Dispatcher.Invoke(() => Dmd.Source = bmp);
		}

		public void RenderGray4(BitmapSource bmp)
		{
			Render(bmp);
		}

		public void RenderGray4(byte[] frame)
		{
			// retrieve dimensions from frame size with AR = 1:4
			var width = 2 * (int) Math.Sqrt(frame.Length);
			var height = width / 4;
			if (_gray4Palette != null) {
				RenderRgb24(ColorUtil.ColorizeFrame(width, height, frame, _gray4Palette));
			} else {
				Render(ImageUtils.ConvertFromGray4(width, height, frame, _hue, _sat, _lum));
			}
		}

		public void RenderGray2(byte[] frame)
		{
			// retrieve dimensions from frame size with AR = 1:4
			var width = 2 * (int) Math.Sqrt(frame.Length);
			var height = width / 4;
			if (_gray2Palette != null) {
				RenderRgb24(ColorUtil.ColorizeFrame(width, height, frame, _gray2Palette));
			} else {
				Render(ImageUtils.ConvertFromGray2(width, height, frame, _hue, _sat, _lum));
			}
		}
		
		public void RenderGray2(BitmapSource bmp)
		{
			throw new NotImplementedException();
		}

		public void RenderRgb24(byte[] frame)
		{
			if (frame.Length % 3 != 0) {
				throw new ArgumentException("RGB24 buffer must be divisible by 3, but " + frame.Length + " isn't.");
			}
			var width = 2 * (int)Math.Sqrt((double)frame.Length / 3);
			var height = width / 4;
			Render(ImageUtils.ConvertFromRgb24(width, height, frame));
		}

		public void SetColor(Color color)
		{
			ColorUtil.RgbToHsl(color.R, color.G, color.B, out _hue, out _sat, out _lum);
		}

		public void SetPalette(Color[] colors)
		{
			_gray2Palette = ColorUtil.GetGrayPalette(colors, 4);
			_gray4Palette = ColorUtil.GetGrayPalette(colors, 16);
		}

		public void Init()
		{
			// nothing to init
		}

		public void Dispose()
		{
			// nothing to dispose
		}
	}
}
