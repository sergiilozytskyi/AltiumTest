using Common;
using System;
using System.IO;
using System.Threading;

namespace FileValidator
{
	class FileValidator: ConsoleApp
	{
		private bool linesOnly;

		static int Main(string[] args)
		{
			var app = new FileValidator();
			return app.Run(args);
		}

		public FileValidator() 
			: base("Text file validator for Altium test (Sergii Lozytskyi 2018)")
		{ }

		protected override void OnCancel()
		{ }

		protected override void PrintHelpText()
		{
			Console.WriteLine("-LinesOnly true: Validate only lines format and does not validate sort order.");
			Console.WriteLine("Usage example:");
			Console.WriteLine("FileValidator.exe -LinesOnly false -FileName c:\temp\test_sorted(1).txt");
		}

		protected override bool ParsePrameters(string[] args)
		{
			if (!base.ParsePrameters(args))
			{
				return false;
			}

			linesOnly = ParseArgument(args, "-LinesOnly", false);

			return true;
		}
		protected override void OnExecute()
		{

			var linesProcessed = 0;
			var file = LargeFile.Open(fileName);
			{
				foreach (var part in file.Read())
				{
					part.Process((stream, block, cancel) =>
					{
						var reader = new StreamReader(stream);
						while (!reader.EndOfStream)
						{
							var line1 = new Line(reader.ReadLine());
							Interlocked.Increment(ref linesProcessed);
							if (!linesOnly && !reader.EndOfStream)
							{
								var line2 = new Line(reader.ReadLine());
								if (line1.CompareTo(line2) > 0)
								{
									throw new Exception("File is not sorted!");
								}

								Interlocked.Increment(ref linesProcessed);
							}
						}
					});
				}
			}

			Console.WriteLine($"Total lines processed: {linesProcessed}");

			Console.WriteLine($"All lines have valid format.");
			if (!linesOnly)
				Console.WriteLine($"File '{fileName}' sorted correctly.");
		}
	}
}
