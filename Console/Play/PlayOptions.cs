﻿using CommandLine;
using Console.Common;

namespace Console.Play
{
	class PlayOptions : BaseOptions
	{
		[Option('f', "file", Required = true, HelpText = "Path to the file to play.")]
		public string File { get; set; }

		[ParserState]
		public IParserState LastParserState { get; set; }
	}
}
