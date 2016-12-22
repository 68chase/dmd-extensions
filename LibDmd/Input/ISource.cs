﻿using System;
using System.Reactive;
using System.Reactive.Subjects;
using System.Security.RightsManagement;
using System.Windows.Media.Imaging;
using LibDmd.Output;

namespace LibDmd.Input
{
	/// <summary>
	/// Acts as source for any frames ending up on the DMD.
	/// </summary>
	/// <remarks>
	/// Since we want a contineous flow of frames, the method to override
	/// returns an observable. Note that the producer decides on the frequency
	/// in which frames are delivered to the consumer.
	/// </remarks>
	public interface ISource
	{
		/// <summary>
		/// A display name for the source
		/// </summary>
		string Name { get; }

		/// <summary>
		/// The size of the source. Can change any time.
		/// </summary>
		BehaviorSubject<Dimensions> Dimensions { get; }

		/// <summary>
		/// An observable that triggers when the source starts providing frames.
		/// </summary>
		IObservable<Unit> OnResume { get; }

		/// <summary>
		/// An observable that triggers when the source is interrupted, e.g. a game is stopped.
		/// </summary>
		IObservable<Unit> OnPause { get; }
	}

	public struct Dimensions
	{
		public int Width { get; set; }
		public int Height { get; set; }
	}

	public enum ResizeMode
	{
		/// <summary>
		/// Stretch to fit dimensions. Aspect ratio is not kept.
		/// </summary>
		Stretch,

		/// <summary>
		/// Smaller dimensions fits while larger dimension gets cropped.
		/// </summary>
		Fill,

		/// <summary>
		/// Larger dimensions fits and smaller dimension stays black.
		/// </summary>
		Fit
	}
}
