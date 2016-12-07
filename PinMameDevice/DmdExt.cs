﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Media;
using System.Windows.Threading;
using DmdExt.Common;
using LibDmd;
using LibDmd.Output;
using Mindscape.Raygun4Net;
using NLog;
using static System.Windows.Threading.Dispatcher;

namespace PinMameDevice
{
	public class DmdExt
	{
		private readonly PinMameSource _source = new PinMameSource();
		private readonly List<RenderGraph> _graphs = new List<RenderGraph>();
		private readonly List<IDisposable> _renderers = new List<IDisposable>();
		private VirtualDmd _dmd;
		private Color _color = Colors.OrangeRed;
		private Color[] _palette;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly RaygunClient Raygun = new RaygunClient("J2WB5XK0jrP4K0yjhUxq5Q==");

		public DmdExt()
		{
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		}

		public void Open()
		{
			if (_dmd == null) {
				Logger.Info("Opening virtual DMD...");
				SetupVirtualDmd();

			} else {
				_dmd.Dispatcher.Invoke(() => {
					SetupGraphs();
					_dmd.Show();
				});
			}
		}

		public void Close()
		{
			Logger.Info("Closing up.");
			_renderers.ForEach(r => r.Dispose());
			_graphs.ForEach(graph => graph.Dispose());
			_graphs.RemoveAll(g => true);
			_dmd?.Dispatcher.Invoke(() => {
				_dmd.Hide();
			});

			_color = Colors.OrangeRed;
			_palette = null;
		}

		public void SetColor(Color color)
		{
			Logger.Info("Setting color: {0}", color);
			_color = color;
		}
		public void SetPalette(Color[] colors) {
			Logger.Info("Setting palette to {0} colors...", colors.Length);
			_palette = colors;
		}

		public void RenderGray2(int width, int height, byte[] frame)
		{
			_source.FramesGray2.OnNext(frame);
		}

		public void RenderGray4(int width, int height, byte[] frame)
		{
			_source.FramesGray4.OnNext(frame);
		}

		public void RenderRgb24(int width, int height, byte[] frame)
		{
			_source.FramesRgb24.OnNext(frame);
		}

		private void SetupVirtualDmd()
		{
			var thread = new Thread(() => {

				_dmd = new VirtualDmd();
				SetupGraphs();

				// Create our context, and install it:
				SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(CurrentDispatcher));

				// When the window closes, shut down the dispatcher
				_dmd.Closed += (s, e) => CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
				_dmd.Dispatcher.Invoke(() => {
					_dmd.Show();
				});

				// Start the Dispatcher Processing
				Run();
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
		}

		private void SetupGraphs()
		{

			var dest = new List<IFrameDestination> { _dmd.Dmd };

			// create a graph for each bit length.
			_graphs.Add(new RenderGraph {
				Source = _source,
				Destinations = dest,
				RenderAs = RenderBitLength.Gray2
			});
			_graphs.Add(new RenderGraph {
				Source = _source,
				Destinations = dest,
				RenderAs = RenderBitLength.Gray4
			});
			_graphs.Add(new RenderGraph {
				Source = _source,
				Destinations = dest,
				RenderAs = RenderBitLength.Rgb24
			});
			
			if (_palette != null) {
				Logger.Info("Applying palette to DMD...");
				_dmd.Dmd.ClearColor();
				_dmd.Dmd.SetPalette(_palette);
			} else {
				Logger.Info("Applying color to DMD...");
				_dmd.Dmd.ClearPalette();
				_dmd.Dmd.SetColor(_color);	
			}

			_graphs.ForEach(graph => _renderers.Add(graph.StartRendering()));
		}

		private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			var ex = e.ExceptionObject as Exception;
			if (ex != null) {
				Logger.Error(ex.Message);
				Logger.Error(ex.StackTrace);
			}
			Raygun.Send(ex);
		}
	}
}
