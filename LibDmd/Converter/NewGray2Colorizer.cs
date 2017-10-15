﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Converter.Colorize;
using LibDmd.Input;
using NLog;

namespace LibDmd.Converter
{
	public class NewGray2Colorizer : AbstractSource, IConverter
	{
		public override string Name { get; } = "NEW 2-Bit Colorizer";
		public FrameFormat From { get; } = FrameFormat.Gray2;
		public IObservable<Unit> OnResume { get; }
		public IObservable<Unit> OnPause { get; }

		protected readonly Subject<Tuple<byte[][], Color[]>> ColoredGray2AnimationFrames = new Subject<Tuple<byte[][], Color[]>>();
		protected readonly Subject<Tuple<byte[][], Color[]>> ColoredGray4AnimationFrames = new Subject<Tuple<byte[][], Color[]>>();

		protected byte[] ColoredFrame;

		private readonly Coloring _coloring;
		private readonly AnimationSet _animations;

		private Animation _activeAnimation = null;
		private Palette _defaultPalette;
		private Palette _palette;
		private IDisposable _paletteReset;

		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public NewGray2Colorizer(Coloring coloring, AnimationSet animations)
		{
			_coloring = coloring;
			_animations = animations;

			SetPalette(coloring.DefaultPalette, true);
		}

		public void Init()
		{
			Dimensions.Subscribe(dim => ColoredFrame = new byte[dim.Width * dim.Height * 3]);
		}

		public void Convert(byte[] frame)
		{
			var planes = FrameUtil.Split(Dimensions.Value.Width, Dimensions.Value.Height, 2, frame);
			TriggerAnimation(planes);
			Render(Convert(planes));
		}

		private void TriggerAnimation(byte[][] planes)
		{
			var mapping = FindMapping(planes);

			// Faus niid gfundä hemmr fertig
			if (mapping == null) {
				return;
			}

			// Wird ignoriärt ("irrelevant")
			if (mapping.Mode == SwitchMode.Event) {
				return;
			}

			// Faus scho eppis am laifä isch, ahautä
			_activeAnimation?.Reset();

			// Palettä ladä
			var palette = _coloring.GetPalette(mapping.PaletteIndex);
			if (palette == null) {
				Logger.Warn("[colorize] No palette found at index {0}.", mapping.PaletteIndex);
				return;
			}
			Logger.Info("[colorize] Setting palette {0} of {1} colors.", mapping.PaletteIndex, palette.Colors.Length);
			_paletteReset?.Dispose();
			_paletteReset = null;
			SetPalette(palette);

			// Palettä risettä wenn ä Lengi gäh isch
			if (!mapping.IsAnimation && mapping.Duration > 0) {
				_paletteReset = Observable
					.Never<Unit>()
					.StartWith(Unit.Default)
					.Delay(TimeSpan.FromMilliseconds(mapping.Duration)).Subscribe(_ => {
						if (_defaultPalette != null) {
							Logger.Info("[colorize] Resetting to default palette after {0} ms.", mapping.Duration);
							SetPalette(_defaultPalette);
						}
						_paletteReset = null;
					});
			}

			// Animazionä
			if (mapping.IsAnimation) {
				if (_animations == null) {
					Logger.Warn("[colorize] Tried to load animation but no animation file loaded.");
					return;
				}
				_activeAnimation = _animations.Find(mapping.Offset);

				if (_activeAnimation == null) {
					Logger.Warn("[colorize] Cannot find animation at position {0}.", mapping.Offset);
					return;
				}

				_activeAnimation.Start(mapping.Mode, planes, ColoredGray2AnimationFrames, ColoredGray4AnimationFrames, _palette, AnimationFinished);
			}
		}

		private byte[][] Convert(byte[][] planes)
		{
			if (_activeAnimation == null) {
				return planes;
			}
			_activeAnimation.NextFrame(planes);

			return null;
		}

		private void Render(byte[][] planes)
		{
			if (planes == null) {
				return;
			}

			// Wenns kä Erwiiterig gä hett, de gäbemer eifach d Planes mit dr Palettä zrugg
			if (planes.Length == 2) {
				ColoredGray2AnimationFrames.OnNext(new Tuple<byte[][], Color[]>(planes, _palette.GetColors(planes.Length)));
			}

			// Faus scho, de schickermr s Frame uifd entsprächendi Uisgab faus diä gsetzt isch
			if (planes.Length == 4) {
				ColoredGray4AnimationFrames.OnNext(new Tuple<byte[][], Color[]>(planes, _palette.GetColors(planes.Length)));
			}
		}

		/// <summary>
		/// Tuät Bitplane fir Bitplane häschä unds erschtä Mäpping wo gfundä
		/// wordä isch zrugg gäh.
		/// </summary>
		/// <param name="planes">Bitplanes vom Biud</param>
		/// <returns>Mäpping odr null wenn nid gfundä</returns>
		private Mapping FindMapping(byte[][] planes)
		{
			var maskSize = Dimensions.Value.Width * Dimensions.Value.Height / 8;

			// Jedi Plane wird einisch duräghäscht
			for (var i = 0; i < 2; i++)
			{
				var checksum = FrameUtil.Checksum(planes[i]);

				var mapping = _coloring.FindMapping(checksum);
				if (mapping != null) {
					return mapping;
				}

				// Wenn kä Maskä definiert, de nächschti Bitplane
				if (_coloring.Masks == null || _coloring.Masks.Length <= 0) {
					continue;
				}

				// Sisch gemmr Maskä fir Maskä durä und luägid ob da eppis passt
				var maskedPlane = new byte[maskSize];
				foreach (var mask in _coloring.Masks) {
					var plane = new BitArray(planes[i]);
					plane.And(new BitArray(mask)).CopyTo(maskedPlane, 0);
					checksum = FrameUtil.Checksum(maskedPlane);
					mapping = _coloring.FindMapping(checksum);
					if (mapping != null) {
						return mapping;
					}
				}
			}
			return null;
		}

		/// <summary>
		/// Tuät nii Farbä dr Palettä wo grad bruichd wird zuäwiisä.
		/// </summary>
		/// <param name="palette">Diä nii Palettä</param>
		/// <param name="isDefault"></param>
		public void SetPalette(Palette palette, bool isDefault = false)
		{
			if (palette == null) {
				Logger.Warn("[colorize] Ignoring null palette.");
				return;
			}
			if (isDefault) {
				_defaultPalette = palette;
			}
			Logger.Debug("[colorize] Setting new palette: [ {0} ]", string.Join(" ", palette.Colors.Select(c => c.ToString())));
			_palette = palette;
		}

		/// <summary>
		/// Wird uisgfiährt wenn än Animazion fertig isch, cha irgend ä Modus si.
		/// </summary>
		protected void AnimationFinished()
		{
			//Logger.Trace("[timing] Animation finished.");
			//LastChecksum = 0x0;
			SetPalette(_defaultPalette);
			_activeAnimation = null;
		}

		public IObservable<Tuple<byte[][], Color[]>> GetColoredGray2Frames()
		{
			return ColoredGray2AnimationFrames;
		}

		public IObservable<Tuple<byte[][], Color[]>> GetColoredGray4Frames()
		{
			return ColoredGray4AnimationFrames;
		}
	}
}
