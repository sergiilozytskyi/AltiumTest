using Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SortingTool
{
	internal class ParallelRadixSort : ISort<byte[]>
	{
		private struct Job
		{
			public int left;
			public int right;
			public int depth;
			public short borderSize;
			public int numberByteIndex;
		}

		public const byte BorderSizeForStr = 127 - Line.Sep; // assume that line contains only human readable chars including digits.
		private const short BorderSizeForNumber = 256;
		private const int QuickSortJobTrashold = 5;

		private ConcurrentQueue<Job> queue = new ConcurrentQueue<Job>();

		private int[] _border;
		private CancellationTokenSource _cancel = new CancellationTokenSource();

		private int[] Border<T>(T[] a, int start, int end, int borderSize, Func<T, byte> key)
		{
			var border = new int[borderSize];
			Parallel.For(start, end,
			new ParallelOptions()
			{
				MaxDegreeOfParallelism = Utils.GetProcessorCount(),
				CancellationToken = _cancel.Token
			},
			i => Interlocked.Increment(ref border[key(a[i])]));
			return border;
		}

		static int[] PrefixSum(int[] border, int prefix)
		{
			var prefixSum = new int[border.Length];
			for (int i = 0; i < border.Length; ++i)
			{
				prefixSum[i] = prefix;
				prefix += border[i];
			}
			return prefixSum;
		}

		private void Arrange<T>(T[] a, int left, int right, int[] prefixSum, Func<T, byte> key)
		{
			var dest = new T[right - left];
			Parallel.For(left, right, new ParallelOptions()
			{
				MaxDegreeOfParallelism = Utils.GetProcessorCount(),
				CancellationToken = _cancel.Token
			},
			i =>
			{
				var j = Interlocked.Increment(ref prefixSum[key(a[i])]);
				dest[j - 1 - left] = a[i];
			});

			Array.Copy(dest, 0, a, left, dest.Length);
		}

		private IEnumerable<Job> RadixSort(byte[][] a, int[] border, Job job)
		{
			var left = job.left;
			var right = job.right;
			bool isNumberJob = job.borderSize == BorderSizeForNumber;

			if (right - left > 1 && job.numberByteIndex < sizeof(int))
			{
				Func<byte[], byte> key = l => isNumberJob ? l[job.depth] : (byte)(l[job.depth] - Line.Sep);

				border = border ?? Border(a, left, right, job.borderSize, key);

				var prefixSum = PrefixSum(border, left);
				Arrange(a, left, right, prefixSum, key);

				for (int j = 0; j < border.Length && !_cancel.Token.IsCancellationRequested; ++j)
				{
					if (border[j] == 0) continue;

					yield return new Job()
					{
						left = prefixSum[j] - border[j],
						right = prefixSum[j],
						depth = job.depth + 1,
						borderSize = isNumberJob || j == 0 ? BorderSizeForNumber : BorderSizeForStr,
						numberByteIndex = isNumberJob ? job.numberByteIndex + 1 : 0
					};
				}
			}
		}

		private void ParallelSort(byte[][] lines, int[] border, int workersCount)
		{
			var o = RadixSort(lines, border, new Job() { right = lines.Length, borderSize = BorderSizeForStr });
			o.ForEach(job => queue.Enqueue(job));

			var waitingThreads = 0;
			var mutex = new object();
			var signal = new AutoResetEvent(false);

			var tasks = Enumerable.Range(0, workersCount)
			.Select(cpu =>
			{
				return Task.Factory.StartNew(new Action(() =>
				{
					while (true)
					{
						if(_cancel.Token.IsCancellationRequested)
						{
							signal.Set();
							return;
						}

						if (queue.TryDequeue(out Job job))
						{
							if (job.right - job.left <= QuickSortJobTrashold)
							{
								Array.Sort(lines, job.left, job.right - job.left, new BytesComparer());
							}
							else
							{
								var jobs = RadixSort(lines, null, job);
								jobs.ForEach(j => queue.Enqueue(j));
								signal.Set();
							}
						}

						if (queue.IsEmpty)
						{
							Interlocked.Increment(ref waitingThreads);
							if (waitingThreads == workersCount)
							{
								signal.Set();
								return;
							}
							else
							{
								signal.WaitOne();
								Interlocked.Decrement(ref waitingThreads);
							}
						}
					}
				}), _cancel.Token);
			});

			Task.WaitAll(tasks.ToArray());
		}


		public void Cancel()
		{
			_cancel.Cancel();
		}

		public void Sort(byte[][] source)
		{
			ParallelSort(source, _border, Utils.GetProcessorCount());

			// clear memory
			_border = null;
		}

		public ParallelRadixSort(int[] border)
		{
			_border = border;
		}
	}
}
