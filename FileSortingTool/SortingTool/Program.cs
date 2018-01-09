using Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SortingTool
{
	class SortingTool: ConsoleApp
	{
		private const int ArrayChunckSize = 1024000;
		private const int QuickSortTrashold = 1024;
		private LargeFile file;
		private ISort<byte[]> radixSort;
		private ISort<byte[]> mergeSort;
		public SortingTool()
			: base("File sorting tool for Altium test(Sergii Lozytskyi 2018)") { }

		static Tuple<List<byte[]>, int[]> ReadLines(Stream stream, CancellationToken cancel)
		{
			var blockLines = new List<byte[]>();
			var blockBorder = new int[ParallelRadixSort.BorderSizeForStr];
			var reader = new StreamReader(stream);
			while (!reader.EndOfStream && !cancel.IsCancellationRequested)
			{
				var lineBytes = new Line(reader.ReadLine()).ToBytes();
				blockLines.Add(lineBytes);
				++blockBorder[lineBytes[0] - Line.Sep];
			}

			return new Tuple<List<byte[]>, int[]>(blockLines, blockBorder);
		}

		private string Sort(string fullPath)
		{
			file = LargeFile.Open(fullPath);
			string resultFileName = null;
			
			//Console.WriteLine($"File will be sorted in {fileParts.Length} parts. Only one part can be fully loaded into memory.");

			var i = 0;
			foreach(var part in file.Read())
			{
				Console.WriteLine($"Sorting Part({i}) using ParallelRadixSort...");

				var blocks = new ConcurrentBag<Tuple<List<byte[]>, int[]>>();
				part.Process((stream, block, cancel) =>
				{
					blocks.Add(ReadLines(stream, cancel));
				});

				var lines = blocks.SelectMany(p => p.Item1).AsParallel().ToArray();

				var border = new int[ParallelRadixSort.BorderSizeForStr];
				for(var j = 0; j < border.Length; ++j)
					border[j] = blocks.Select(p => p.Item2[j]).Sum();
				blocks = null;

				radixSort = new ParallelRadixSort(border);
				radixSort.Sort(lines);
				radixSort = null;

				if (i == 0)
					Console.WriteLine($"Saving Part({i}) to disk...");
				else
					Console.WriteLine($"Merge Part({i}) to all other using MergeSort and save to disk...");
				resultFileName = Merge(lines, fullPath, resultFileName);
				lines = null;
				i++;
				GC.Collect();
			}
			return resultFileName;
		}

		private string Merge(byte[][] lines, string sourceFilePath, string fileToMarge)
		{
			var sortedFilePath = Utils.GetSortedFileFullPath(sourceFilePath);

			if (File.Exists(fileToMarge))
			{
				mergeSort = new MergeSort(sortedFilePath, fileToMarge);
				mergeSort.Sort(lines);
				File.Delete(fileToMarge);
			}
			else
			{
				//var processors = Utils.GetProcessorCount();
				//var size = lines.Length / processors;
				//var indexes = Enumerable.Range(0, processors).Select(i => i * size).ToList();
				//var fileSize = Line.FitOffsets(lines, indexes, out List<Tuple<int, long>> offsets);
				//var file = LargeFile.Create(sortedFilePath, fileSize);
				//file.WriteLines(lines, offsets);
				File.AppendAllLines(sortedFilePath, lines.Select(l => new Line(l).ToString()));
			}

			return sortedFilePath;
		}

		private string QuickSort(string sourceFile)
		{
			var lines = new Line[ArrayChunckSize];
			var read = -1;
			Parallel.ForEach(File.ReadAllLines(sourceFile), (line, _, i) =>
			{
				lines[Interlocked.Increment(ref read)] = new Line(line);
			});

			Array.Resize(ref lines, read + 1);
			Array.Sort(lines);

			var targetFile = Utils.GetSortedFileFullPath(sourceFile);
			File.AppendAllLines(targetFile, lines.Select(l => l.ToString()));

			return targetFile;
		}

		protected override void OnCancel()
		{
			if (null != file)
				file.Cancel();
			if (null != radixSort)
				radixSort.Cancel();
			if (null != mergeSort)
				mergeSort.Cancel();
		}

		private static void PrintImporatantInfo()
		{
				Console.WriteLine("========================Important========================");
				Console.WriteLine(@"Release notes:
1. Tool use Radix, Quick and Merge sort algorithms in conjunction with parallelization.
2. Tool works only with ASCII encoded human readable symbols (32-127 ASCII codes).
3. Numbers must have positive values of 4 bytes integer
4. Each line must have correct format according to task description.
5. Add progress like it is done in FileGenerator.
");

				Console.WriteLine(@"Can be improved in following places:
1. Introducing parallelizm in part where merge sort algoritnm is used.
2. Introducing more smarter logic for duplicate strings processing.
3. Add error processing for inccorect files/lines.");
			}

		protected override void OnExecute()
		{
			PrintImporatantInfo();
			Console.WriteLine("Sorting file '{0}', please wait...", fileName);

			var sortedFilePath = Utils.GetFileSize(fileName) <= QuickSortTrashold
				? QuickSort(fileName)
				: Sort(fileName);

			Console.WriteLine("Sorted file: {0}", sortedFilePath);
		}

		protected override void PrintHelpText()
		{
			// TODO: Write help text for tool.
			Console.WriteLine("Heare should be tool help text :)!");

			Console.WriteLine("Usage example:");
			Console.WriteLine("SortingTool.exe -FileName c:\temp\test.txt");
		}

		static int Main(string[] args)
		{
			var app = new SortingTool();
			return app.Run(args);
		}
	}
}
