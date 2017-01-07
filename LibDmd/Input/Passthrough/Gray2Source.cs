﻿using System;
using System.Reactive;
using System.Reactive.Subjects;

namespace LibDmd.Input.Passthrough
{
	/// <summary>
	/// An input source that just contains a 2-bit observable.
	/// </summary>
	public class Gray2Source : AbstractSource, IGray2Source
	{
		public override string Name { get; }

		public IObservable<Unit> OnResume => _onResume;
		public IObservable<Unit> OnPause => _onPause;

		private readonly ISubject<Unit> _onResume = new Subject<Unit>();
		private readonly ISubject<Unit> _onPause = new Subject<Unit>();

		public readonly Subject<byte[]> FramesGray2 = new Subject<byte[]>();

		public Gray2Source(string name)
		{
			Name = name;
		}

		public IObservable<byte[]> GetGray2Frames()
		{
			return FramesGray2;
		}
	}
}
