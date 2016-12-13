﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using LibDmd.Common;
using LibDmd.Converter.Colorize;
using NLog;

namespace LibDmd.Converter
{
	/// <summary>
	/// Tuät viär Bit Graischtuifä-Frames i RGB24-Frames umwandlä.
	/// </summary>
	/// <remarks>
	/// Hiä gits zwe Methodä. I jedem Fau wärdid aui Farbdatä zersch vomänä Feil
	/// gladä.
	/// 
	/// Im erschtä Fau wärdid d Palettäwächsu ibärä Sitäkanau aagäh. Diä Wächsu
	/// chemid diräkt vom ROM, wo midem Pinball Browser abgändered wordä sind.
	/// 
	/// Im zweitä Fau timmer jedes Biud häschä und luägid ob dr Häsch neimä im
	/// Feil vorhandä isch. Faus ja, de wird diä entsprächendi Palettä gladä. S Feil
	/// cha abr ai nu Maskä beinhautä wo dynamischi Elemänt uisbländid, diä wärdid
	/// de ai nu aagwandt bim Häschä.
	/// 
	/// Bim Häschä isch nu wichtig z wissä dass mr uifd Bitplanes seperat häschid,
	/// und nid uifd Originaldatä vo VPM. Bi drii Maskä und viär Bit git das auso
	/// drii mau viär plus viär unghäschti, macht sächzä Häsches zum Vrgliichä.
	/// </remarks>
	public class Gray4Colorizer : IConverter
	{
		public readonly int Width;
		public readonly int Height;
		public readonly Color DefaultColor = Colors.DarkOrchid;

		public RenderBitLength From { get; } = RenderBitLength.Gray4;
		public RenderBitLength To { get; } = RenderBitLength.Rgb24;

		private readonly Coloring _coloring;
		private readonly Animation[] _animation;
		private readonly byte[] _coloredFrame;
		private Color[] _palette;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private Animation _currentAnimation;

		public Gray4Colorizer(int width, int height, string palFile, string fsqFile = null)
		{
			Width = width;
			Height = height;
			Logger.Info("[colorize] Loading palette file at {0}...", palFile);
			_coloring = new Coloring(palFile);
			_coloredFrame = new byte[width * height * 3];
			if (fsqFile != null) {
				Logger.Info("[colorize] Loading animation file at {0}...", fsqFile);
				_animation = Animation.ReadFrameSequence(fsqFile);
			}
			SetPalette(_coloring.DefaultPalette != null ? _coloring.DefaultPalette.Colors : new[] {Colors.Black, DefaultColor});
			Logger.Debug("[colorize] Initialized.");
		}

		public byte[] Convert(byte[] frame)
		{
			// Wennä Animation am laifä isch de nämmer diräkt diä Datä
			var coloredFrame = PopAnimation();
			if (coloredFrame != null) {
				return coloredFrame;
			}

			// Zersch dimmer s Frame i Planes uifteilä
			var planes = FrameUtil.Split4Bit(Width, Height, frame);
			var match = false;

			// Jedi Plane wird einisch duräghäscht
			for (var i = 0; i < 4; i++) {
				var checksum = FrameUtil.Checksum(planes[i]);

				// Wemer dr Häsch hett de luägemr grad obs ächt äs Mäpping drzuäg git
				match = ApplyMapping(checksum, false);

				// Faus ja de grad awändä und guät isch
				if (match) {
					break;
				}
			}
			// Villicht het än Animation aagfangä..
			coloredFrame = PopAnimation();
			if (coloredFrame != null) {
				return coloredFrame;
			}

			// Faus nei de gemmr Maskä fir Maskä durä und luägid ob da eppis passt
			if (!match && _coloring.Masks.Length > 0) {
				var maskedPlane = new byte[512];
				for (var i = 0; i < 4; i++) {
					foreach (var mask in _coloring.Masks) {
						var plane = new BitArray(planes[i]);
						plane.And(new BitArray(mask)).CopyTo(maskedPlane, 0);
						var checksum = FrameUtil.Checksum(maskedPlane);
						if (ApplyMapping(checksum, true)) {
							break;
						}
					}
				}
			}

			// Villicht het ja etz än Animation aagfangä..
			coloredFrame = PopAnimation();
			if (coloredFrame != null) {
				return coloredFrame;
			}

			// Faus nid timmr eifach iifärbä.
			ColorUtil.ColorizeFrame(Width, Height, frame, _palette, _coloredFrame);
			return _coloredFrame;
		}

		private bool ApplyMapping(uint checksum, bool masked)
		{
			var mapping = _coloring.FindMapping(checksum);
			if (mapping == null) {
				return false;
			}
			if (mapping.Mode == 1) {
				var palette = _coloring.GetPalette(mapping.Offset);
				if (palette == null) {
					Logger.Warn("[colorize] No palette found at index {0} for {1} frame.", mapping.Offset, masked ? "masked" : "unmasked");
					return false;
				}
				Logger.Info("[colorize] Setting palette of {0} colors via {1} frame.", palette.Colors.Length, masked ? "masked" : "unmasked");
				SetPalette(palette.Colors);
				return true;
			}
			if (mapping.Mode == 2) {
				if (mapping.Offset >= _animation.Length) {
					Logger.Warn("[colorize] No animation found at index {0} for {1} frame.", mapping.Offset, masked ? "masked" : "unmasked");
					return false;
				}
				Logger.Info("[colorize] Playing animation of {0} frames via {1} frame.", _animation[mapping.Offset].Frames.Length, masked ? "masked" : "unmasked");
				_currentAnimation = _animation[mapping.Offset];
				return true;
			}
			return false;
		}

		private byte[] PopAnimation()
		{
			if (_currentAnimation == null) {
				return null;
			}
			Logger.Trace("[colorize] Playing frame {0} of animation.", _currentAnimation.CurrentFrame);
			var replacementFrame = _currentAnimation.Next();
			if (_currentAnimation.IsFinished) {
				_currentAnimation = null;
			}
			if (replacementFrame.BitLength == 4) {
				ColorUtil.ColorizeFrame(Width, Height, replacementFrame.GetFrame(Width, Height), _palette, _coloredFrame);
				return _coloredFrame;
			}
			return null;
		}

		/// <summary>
		/// Tuät nii Farbä dr Palettä wo grad bruichd wird zuäwiisä.
		/// </summary>
		/// <param name="colors">Diä niiä Farbä vord Palettä</param>
		public void SetPalette(Color[] colors)
		{
			Logger.Debug("[colorize] Setting new palette:");
			Array.ForEach(colors, c => Logger.Trace("   " + c));

			_palette = ColorUtil.GetPalette(colors, 16);
		}

		/// <summary>
		/// Tuät d Palettä wo grad bruichd wird mitärän andärä uiswächslä.
		/// </summary>
		/// <param name="index">Dr Index fo dr niiä Palettä wo vom Palettä-Feil gläsä wordä isch</param>
		public void LoadPalette(uint index)
		{
			var palette = _coloring.GetPalette(index);
			if (palette != null) {
				Logger.Info("[colorize] Setting palette of {0} colors via side channel...", palette.Colors.Length);
				SetPalette(palette.Colors);

			} else {
				Logger.Warn("[colorize] No palette with index {0} found to load through side channel.", index);
			}
		}
	}
}
