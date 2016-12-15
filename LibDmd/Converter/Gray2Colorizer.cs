﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
	/// <summary>
	/// Tuät viär Bit Graischtuifä-Frames i RGB24-Frames umwandlä.
	/// </summary>
	/// 
	/// <remarks>
	/// Je nach <see cref="Mapping.Mode"/> wird d Animazion komplett
	/// abgschpiut oder numä mit Graidatä ergänzt.
	/// </remarks>
	public class Gray2Colorizer : AbstractColorizer, IConverter, IFrameSourceRgb24
	{

		public RenderBitLength From { get; } = RenderBitLength.Gray2;
		public RenderBitLength To { get; } = RenderBitLength.Rgb24;

		public Gray2Colorizer(int width, int height, Coloring coloring, Animation[] animations = null) : base(width, height, coloring, animations)
		{
		}

		public byte[] Convert(byte[] frame)
		{
			// Zersch dimmer s Frame i Planes uifteilä
			var planes = FrameUtil.Split4Bit(Width, Height, frame);
			var match = false;

			// Jedi Plane wird einisch duräghäscht
			for (var i = 0; i < 2; i++) {
				var checksum = FrameUtil.Checksum(planes[i]);

				// Wemer dr Häsch hett de luägemr grad obs ächt äs Mäpping drzuäg git
				match = ApplyMapping(checksum, "unmasked");

				// Faus ja de grad awändä und guät isch
				if (match) {
					break;
				}
			}

			// Faus nei de gemmr Maskä fir Maskä durä und luägid ob da eppis passt
			if (!match && Coloring.Masks.Length > 0) {
				var maskedPlane = new byte[512];
				for (var i = 0; i < 2; i++) {
					foreach (var mask in Coloring.Masks) {
						var plane = new BitArray(planes[i]);
						plane.And(new BitArray(mask)).CopyTo(maskedPlane, 0);
						var checksum = FrameUtil.Checksum(maskedPlane);
						if (ApplyMapping(checksum, "masked")) {
							break;
						}
					}
				}
			}

			// Wenn än Animazion am laifä nisch de wird niid zrugg gäh
			if (IsAnimationRunning) {
				return null;
			}

			// Wenns Biud muäss mit zwe Bytes ergänzt wärdä, de go!
			if (IsEnhancerRunning) {
				var data = CurrentEnhancer.Next();
				if (data.BitLength == 2) {
					planes[2] = data.Planes[0];
					planes[3] = data.Planes[1];
					frame = FrameUtil.Join(Width, Height, planes);

				} else {
					Logger.Warn("Got a bit enhancer that gave us a {0}-bit frame. Duh, ignoring.", data.BitLength);
				}
			}

			// Faus nid timmr eifach iifärbä.
			ColorUtil.ColorizeFrame(Width, Height, frame, Palette.Value, ColoredFrame);
			return ColoredFrame;
		}
	}
}
