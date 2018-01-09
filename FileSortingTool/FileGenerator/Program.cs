using Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace FileGenerator
{
	public class FileGeneratoApp: ConsoleApp
	{
		private const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
		private const int MinLineLength = 5; // 1 digit + '.' + ' '.+ '\n\r';
		private const int MaxDigitsConunt = 5; // assume that we have to deel only with posotive Int32 numbers.

		private LargeFile file;
		private long fileSize;
		private int duplicatePatternsCount;
		private int maxLineLength;
		private int duplicatesProbabilityPercents;
		private bool useSystemRandom;

		private static char[] GenerateLine(Common.Random random, int length)
		{
			var buffer = new char[length];

			var dotIndex = random.Next(1, Math.Min(length - MinLineLength + 1, MaxDigitsConunt));

			for (var i = 0; i < buffer.Length - 2; i++)
			{
				if (i == dotIndex || i == dotIndex + 1) continue;
				buffer[i] = chars[
					i < dotIndex
					? random.Next(i == 0 ? 1 : 0, 10)
					: random.Next(chars.Length)];
			}

			buffer[dotIndex] = '.';
			buffer[dotIndex + 1] = ' ';
			buffer[buffer.Length - 2] = '\r';
			buffer[buffer.Length - 1] = '\n';
			return buffer;
		}

		private char[] GetNextLine(Common.Random random, long capacity, ref List<char[]> duplicates)
		{
			char[] chars = null;
			if (duplicates.Count > 0 && random.Next(100) < duplicatesProbabilityPercents)
			{
				//get duplicate line
				chars = duplicates[random.Next(duplicates.Count)];

				var remainingBytes = capacity - chars.Length;
				if (remainingBytes <= MinLineLength)
				{
					chars = null;
				}
			}

			// need to generate new line
			if (null == chars)
			{
				// generate new line
				var rnd = random.Next(MinLineLength, maxLineLength);
				var length = Math.Min(rnd, capacity);

				var remainingBytes = capacity - length;
				if (remainingBytes <= MinLineLength)
				{
					length += remainingBytes;
				}

				chars = GenerateLine(random, (int)length);

				// store duplicates to future use
				if (duplicatePatternsCount > 0)
				{
					lock (duplicates)
					{
						--duplicatePatternsCount;
						duplicates.Add(chars);
					}
				}
			}

			return chars;
		}

		public FileGeneratoApp() 
			: base("Text file generator for Altium test (Sergii Lozytskyi 2018)")
		{ }

		protected override void OnCancel()
		{
			if(file != null)
			{
				file.Cancel();
			}
		}

		protected override void OnExecute()
		{
			Console.WriteLine("=========================================================");
			Console.WriteLine("System.Security.Cryptography.RNGCryptoServiceProvider takes much more time but ensures pure random!");
			Console.WriteLine("To increase performance please provide 'UseSystemRandom = true' in command but it can cause unpredictable duplicates!!!");

			// avarage duplicates probabilty. 
			var duplicates = new List<char[]>();
			var totalLinesCount = 0;
			file = LargeFile.Create(fileName, fileSize);
			var parts = file.Write().ToArray();

			if (parts.Length > 1)
				Console.WriteLine($"File generation is devided onto {parts.Length} parts.");

			var random = new Common.Random(useSystemRandom);
			for (var i = 0; i < parts.Length; ++i)
			{
				if (parts.Length > 1)
					Console.WriteLine($"Generating Part({i})...");

				var progress = new Progress();
				parts[i].Process((stream, block, cancel) =>
				{
					var bytesWritten = 0;
					while (!cancel.IsCancellationRequested && bytesWritten < stream.Length)
					{
						var chars = GetNextLine(random, stream.Length - bytesWritten, ref duplicates);

						// use ASCII encoding for simplification
						var buffer = Encoding.ASCII.GetBytes(chars);
						stream.Write(buffer, 0, buffer.Length);
						bytesWritten += buffer.Length;

						progress.Report((double)bytesWritten / stream.Length);

						Interlocked.Increment(ref totalLinesCount);
					}

					if (block.Id == 0)
						Console.WriteLine("Flushing streams, please wait...");
				});
			}

			Console.WriteLine($"Total lines count: {totalLinesCount}");

			if (file.IsCancelled)
			{
				// delete file due to process was cacelled
				if (File.Exists(fileName))
				{
					File.Delete(fileName);
					Console.WriteLine("Process terminated");
					Console.WriteLine("File system has been cleaned.");
					Console.WriteLine("Press any key...");
					Console.ReadLine();
				}
			}
		}
		protected override bool ParsePrameters(string[] args)
		{
			if(!base.ParsePrameters(args))
			{
				return false;
			}

			fileSize = ParseArgument<long>(args, "-FileSize", -1); // must be greater then MaxLineLength.

			if (-1 == fileSize)
			{
				Error($"Please specify valid file size!");
				return false;
			}

			maxLineLength = ParseArgument(args, "-MaxLineLength", 300); // symbols count,  greater or equal MinLineLength = 5.
			duplicatesProbabilityPercents = ParseArgument(args, "-DuplicatesProbabilityPercents", 10);
			duplicatePatternsCount = ParseArgument(args, "-DuplicatePatternsCount", 10);
			useSystemRandom = ParseArgument(args, "-UseSystemRandom", false);


			var availableDiskSpace = Utils.GetFreeDiskSpace(fileName);
			if (availableDiskSpace < fileSize)
			{
				var hrAvailableSpace = Utils.BytesToString(availableDiskSpace);
				Error($"The is not enaugh disk space to create file (available disk space {hrAvailableSpace} bytes).");
				return false;
			}

			if (fileSize < maxLineLength)
			{
				Error($"FileSize must be greter then {MinLineLength}.");
				return false;
			}

			if (100 < duplicatesProbabilityPercents && 0 > duplicatesProbabilityPercents)
			{
				Error("DuplicatesProbability must be in range 0% - 100%.");
				return false;
			}

			Console.WriteLine("====================Input parameters=====================");
			Console.WriteLine($"Full file path: {fileName}");
			Console.WriteLine($"File size: {Utils.BytesToString(fileSize)}");
			Console.WriteLine($"Maximum line length: {maxLineLength}");
			Console.WriteLine($"Duplicate lines probability: {duplicatesProbabilityPercents}");
			Console.WriteLine($"Maximum duplicate patterns: {duplicatePatternsCount}");
			Console.WriteLine($"Random generator: {(useSystemRandom ? "System.Random" : "System.Security.Cryptography.RNGCryptoServiceProvider")}");

			return true;
		}
		protected override void PrintHelpText()
		{
			// TODO: Write help text for tool.
			Console.WriteLine("Heare should be tool help text :)!");
			Console.WriteLine("Usage example:");
			Console.WriteLine("FileGenerator.exe -FileSize 10000000000 -DuplicatesProbabilityPercents 10 -FileName c:\temp\test.txt");
		}
		static int Main(string[] args)
		{
			var app = new FileGeneratoApp();
			return app.Run(args);
		}
	}
}
