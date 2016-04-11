﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PinDmd.Input;

namespace PinDmd.Output.VirtualDmd
{
	/// <summary>
	/// Interaction logic for VirtualDmdControl.xaml
	/// </summary>
	public partial class VirtualDmdControl : UserControl, IFrameDestination
	{
		private IDisposable _currentFrameSequence;

		public VirtualDmdControl()
		{
			InitializeComponent();
		}

		public void StartRendering(IFrameSource source)
		{
			if (_currentFrameSequence != null) {
				throw new RenderingInProgressException("Sequence already in progress, stop first.");
			}
			_currentFrameSequence = source.GetFrames().Subscribe(bmp => {
				Dispatcher.Invoke(() => {
					var imageSource = Convert(bmp);
					Dmd.Source = imageSource;
				});
			});
		}

		public void StopRendering()
		{
			_currentFrameSequence.Dispose();
			_currentFrameSequence = null;
		}

		public void RenderBitmap(Bitmap bmp)
		{
			Dispatcher.Invoke(() => {
				var imageSource = Convert(bmp);
				Dmd.Source = imageSource;
			});
		}

		public BitmapSource Convert(Bitmap bitmap)
		{
			var bitmapData = bitmap.LockBits(
				new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
				System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);

			var bitmapSource = BitmapSource.Create(
				bitmapData.Width, bitmapData.Height, 96, 96, PixelFormats.Bgr32, null,
				bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

			bitmap.UnlockBits(bitmapData);
			return bitmapSource;
		}
	}
}
