﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using App;
using Console.Common;
using LibDmd;
using LibDmd.Input.PBFX2Grabber;
using LibDmd.Input.ScreenGrabber;
using LibDmd.Output;
using LibDmd.Output.Pin2Dmd;
using LibDmd.Output.PinDmd1;
using LibDmd.Output.PinDmd2;
using LibDmd.Output.PinDmd3;
using LibDmd.Processor;
using NLog;

namespace Console.Test
{
	class TestCommand : BaseCommand
	{
		private readonly TestOptions _options;
		private RenderGraph _graph;

		public TestCommand(TestOptions options)
		{
			_options = options;
		}

		public override void Execute(Action onCompleted)
		{
			// define renderers
			var renderers = GetRenderers(_options);

			// chain them up
			_graph = new RenderGraph {
				Destinations = renderers,
				RenderAsGray4 = _options.RenderAsGray4
			};

			// retrieve image
			var bmp = new BitmapImage();
			bmp.BeginInit();
			bmp.UriSource = new Uri("pack://application:,,,/dmdext;component/Test/TestImage.png");
			bmp.EndInit();

			// render image
			_graph.Render(bmp, onCompleted);
		}

		public override void Dispose()
		{
			_graph?.Dispose();
		}
	}
}
