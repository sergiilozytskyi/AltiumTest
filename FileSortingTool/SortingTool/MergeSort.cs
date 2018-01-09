using Common;
using System.IO;

namespace SortingTool
{
	internal class MergeSort : ISort<byte[]>
	{
		private string _targetFile;
		private string _sourceFile;
		private bool _isCancelled;
		public MergeSort(string targetFile, string sourceFile)
		{
			_sourceFile = sourceFile;
			_targetFile = targetFile;
		}

		public void Cancel()
		{
			_isCancelled = true;
		}
		public void Sort(byte[][] source)
		{
			using (var src = File.OpenText(_sourceFile))
			using (var target = new StreamWriter(File.OpenWrite(_targetFile)))
			{
				int i = 0;
				Line first = null, second = null;
				while (!src.EndOfStream || i < source.Length)
				{
					if (!src.EndOfStream && i < source.Length)
					{
						first = first ?? new Line(src.ReadLine());
						second = second ?? new Line(source[i]);

						if (first.CompareTo(second) >= 0)
						{
							target.WriteLine(second.ToString());
							source[i++] = null;
							second = null;
						}
						else
						{
							target.WriteLine(first.ToString());
							first = null;
						}
					}
					else if (!src.EndOfStream)
					{
						target.WriteLine(first.ToString());
						while (!src.EndOfStream && !_isCancelled)
						{
							first = new Line(src.ReadLine());
							target.WriteLine(first.ToString());
						};
					}
					else if (i < source.Length)
					{
						target.WriteLine(second.ToString());
						for (i = i + 1; i < source.Length && !_isCancelled; ++i)
						{
							second = new Line(source[i++]);
							target.WriteLine(second.ToString());
						}
					}
				}
			}
		}
	}
}
