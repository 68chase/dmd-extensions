﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibDmd.Common;
using LibDmd.Input;
using NLog;

namespace LibDmd.Output.VirtualDmd
{
	/// <summary>
	/// Interaction logic for VirtualDmdControl.xaml
	/// </summary>
	public partial class VirtualDmdControl : IColoredGray2Destination, IColoredGray4Destination, IResizableDestination
	// these others are for debugging purpose. basically you can make the virtual dmd 
	// behave like any other display by adding/removing interfaces
	/*
	IGray2Destination, IGray4Destination, 
	IColoredGray2Destination, IColoredGray4Destination*/
	{
		public int DmdWidth { get; private set; } = 128;
		public int DmdHeight { get;private set; } = 32;

		public bool IsAvailable { get; } = true;

		public DmdWindow Host { set; get; }
		public bool IgnoreAspectRatio {
			get { return Dmd.Stretch == Stretch.UniformToFill; }
			set { Dmd.Stretch = value ? Stretch.Fill : Stretch.UniformToFill; }
		}

		private double _hue;
		private double _sat;
		private double _lum;

		private Color[] _gray2Palette;
		private Color[] _gray4Palette;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public VirtualDmdControl()
		{
			InitializeComponent();
			ClearColor();
		}

		public void RenderBitmap(BitmapSource bmp)
		{
			Dispatcher.Invoke(() => Dmd.Source = bmp);
		}

		public void RenderGray2(byte[] frame)
		{
			if (_gray2Palette != null) {
				RenderRgb24(ColorUtil.ColorizeFrame(DmdWidth, DmdHeight, frame, _gray2Palette));
			} else {
				RenderBitmap(ImageUtil.ConvertFromGray2(DmdWidth, DmdHeight, frame, _hue, _sat, _lum));
			}
		}

		public void RenderGray4(byte[] frame)
		{
			if (_gray4Palette != null) {
				RenderRgb24(ColorUtil.ColorizeFrame(DmdWidth, DmdHeight, frame, _gray4Palette));
			} else {
				RenderBitmap(ImageUtil.ConvertFromGray4(DmdWidth, DmdHeight, frame, _hue, _sat, _lum));
			}
		}

		public void RenderRgb24(byte[] frame)
		{
			if (frame.Length % 3 != 0) {
				throw new ArgumentException("RGB24 buffer must be divisible by 3, but " + frame.Length + " isn't.");
			}
			RenderBitmap(ImageUtil.ConvertFromRgb24(DmdWidth, DmdHeight, frame));
		}

		public void RenderColoredGray2(byte[][] planes, Color[] palette)
		{
			SetPalette(palette);
			RenderGray2(FrameUtil.Join(DmdWidth, DmdHeight, planes));
		}


		public void RenderColoredGray4(byte[][] planes, Color[] palette)
		{
			SetPalette(palette);
			RenderGray4(FrameUtil.Join(DmdWidth, DmdHeight, planes));
		}

		public void SetDimensions(int width, int height)
		{
			DmdWidth = width;
			DmdHeight = height;
			Dispatcher.Invoke(() => {
				Effect.AspectRatio = (double)width / height;
				Effect.BlockCount = Math.Max(width, height);
				Effect.Max = Effect.AspectRatio * 0.375;
			});
			Host.SetDimensions(width, height);
		}

		public void SetColor(Color color)
		{
			ColorUtil.RgbToHsl(color.R, color.G, color.B, out _hue, out _sat, out _lum);
		}

		public void SetPalette(Color[] colors)
		{
			_gray2Palette = ColorUtil.GetPalette(colors, 4);
			_gray4Palette = ColorUtil.GetPalette(colors, 16);
		}

		public void ClearPalette()
		{
			_gray2Palette = null;
			_gray4Palette = null;
		}

		public void ClearColor()
		{
			SetColor(RenderGraph.DefaultColor);
		}

		public void Init()
		{
			if (this is IFixedSizeDestination) {
				SetDimensions(DmdWidth, DmdHeight);
			}
			
		}

		public void Dispose()
		{
			// nothing to dispose
		}
	}

	public interface DmdWindow
	{
		void SetDimensions(int width, int height);
	}
}
