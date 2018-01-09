using Common;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

		private static IEnumerable<T> Merge<T>(IEnumerable<T> firstEnumerable, IEnumerable<T> secondEnumerable, IComparer<T> comparer)
		{
			using (var firstEnumerator = firstEnumerable.GetEnumerator())
			using (var secondEnumerator = secondEnumerable.GetEnumerator())
			{
				bool first = firstEnumerator.MoveNext();
				bool second = secondEnumerator.MoveNext();

				while (first && second)
				{
					if (comparer.Compare(firstEnumerator.Current, secondEnumerator.Current) < 0)
					{
						yield return firstEnumerator.Current;
						first = firstEnumerator.MoveNext();
					}
					else
					{
						yield return secondEnumerator.Current;
						second = secondEnumerator.MoveNext();
					}
				}

				while (first)
				{
					yield return firstEnumerator.Current;
					first = firstEnumerator.MoveNext();
				}

				while (second)
				{
					yield return secondEnumerator.Current;
					second = secondEnumerator.MoveNext();
				}
			}
		}

		public void Sort(byte[][] source)
		{
			using (var target = new StreamWriter(File.OpenWrite(_targetFile)))
			{
				foreach(var line in Merge(source.Select(b => new Line(b)), File.ReadLines(_sourceFile).Select(l => new Line(l)), new LineComparer()))
				{
					target.WriteLine(line.ToString());
				}
			}
		}
	}
}
