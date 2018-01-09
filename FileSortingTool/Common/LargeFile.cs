using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Common
{
	public class Block
	{
		public long Start { set; get; }
		public long End { set; get; }
		public int Id { set; get; }
	}
	public class Part
	{
		private LargeFile _file;
		private IEnumerable<Block> _blocks;
		public Part(LargeFile file, IEnumerable<Block> blocks)
		{
			_file = file;
			_blocks = blocks;
		}

		public void Process(Action<Stream, Block, CancellationToken> processor)
		{
			_file.ProcessParallel(_blocks, processor);
		}
	}

	public class LargeFile
    {
		private const byte eol = 0x0A; // '/n' symbol in ASCII encoding.
		private const int SearchWindowSize = 1024; // Must be twice greater then max line length
		private const int EolSearchWindowSize = 256;
		private Func<MemoryMappedFile> m_memoryMappedFileFactory;
		private long m_fileSize;
		private CancellationTokenSource m_cancel = new CancellationTokenSource();

		private LargeFile(Func<MemoryMappedFile> memoryMappedFileFactory, long fileSize)
		{
			this.m_memoryMappedFileFactory = memoryMappedFileFactory;
			this.m_fileSize = fileSize;
		}

		private IEnumerable<Block> GetWriteBlocks(int count)
		{
			var blockSize = m_fileSize / count;
			var reminder = m_fileSize % count;
			return Enumerable.Range(0, count).Select(cpu =>
			{
				var start = cpu * blockSize;
				return new Block()
				{
					Id = cpu,
					Start = start,
					End = cpu != count - 1 ? start + blockSize : m_fileSize
				};
			});
		}

		private long NextLineStart(MemoryMappedFile mmf, long offset)
		{
			using (var frame = mmf.CreateViewAccessor(offset, SearchWindowSize))
			{
				var buffer = new byte[SearchWindowSize];
				var read = frame.ReadArray(0, buffer, 0, SearchWindowSize);
				var start = Array.IndexOf(buffer, eol, 0, read);
				return start != -1 ? offset + start + 1 : -1;
			}
		}

		private IEnumerable<Block> CreateBlocksFromOffesets(List<long> offsets)
		{
			offsets.Sort();
			for (var i = 0; i < offsets.Count; ++i)
			{
				yield return new Block()
				{
					Id = i,
					Start = i == 0 ? 0 : offsets[i],
					End = i < offsets.Count - 1 ? offsets[i + 1] : m_fileSize
				};
			};
		}

		private IEnumerable<Block> GetReadBlocks(int count)
		{
			using (var mmf = m_memoryMappedFileFactory())
			{
				var blockSize = m_fileSize / count;
				var offsets = new List<long>();
				Parallel.For(0, count,
				i =>
				{
					var offset = NextLineStart(mmf, i * blockSize);
					if (offset != -1)
					{
						lock (offsets)
							offsets.Add(offset);
					}
				});

				return CreateBlocksFromOffesets(offsets);
			}
		}

		//private IEnumerable<Block> GetReadBlocks(int count)
		//{
		//	using (var mmf = m_memoryMappedFileFactory())
		//	{
		//		var blockSize = m_fileSize / count;
		//		var tasks = Enumerable.Range(1, count - 1)
		//		.Select(cpu => Task.Factory.StartNew(() =>
		//			{
		//				var offset = cpu * blockSize;
		//				while (offset < m_fileSize)
		//				{
		//					var bufferSize = Math.Min(EolSearchWindowSize, Math.Min(blockSize, m_fileSize - offset));
		//					var buffer = new byte[bufferSize];
		//					using (var stream = mmf.CreateViewStream(offset, buffer.Length))
		//					{
		//						var read = stream.Read(buffer, 0, buffer.Length);
		//						var eolIndex = Array.IndexOf(buffer, eol, 0, read);
		//						if (eolIndex == -1)
		//						{
		//							offset += read;
		//						}
		//						else
		//						{
		//							offset += eolIndex + 1;
		//							break;
		//						}
		//					}
		//				}
		//				return offset;
		//			})).ToArray();

		//		Task.WaitAll(tasks);

		//		return Enumerable.Range(0, count)
		//		.Select(id => new Block()
		//		{
		//			Id = id,
		//			Start = id == 0 ? 0 : tasks[id - 1].Result,
		//			End = id == count - 1 ? m_fileSize : tasks[id].Result
		//		});
		//	}
		//}
		public void Cancel()
		{
			m_cancel.Cancel();
		}

		public bool IsCancelled
		{
			get
			{
				return m_cancel.IsCancellationRequested;
			}
		}

		public static LargeFile Create(string fullPath, long fileSize)
		{
			if(!Directory.Exists(Path.GetDirectoryName(fullPath)))
			{
				throw new ArgumentException($"Directory '{fullPath}' does not exist!");
			}

			if (File.Exists(fullPath))
				File.Delete(fullPath);

			using (var mmf = MemoryMappedFile.CreateFromFile(fullPath, FileMode.Create, string.Format("MAP_{0}", DateTime.Now.Ticks), fileSize)) { }

			return new LargeFile(() => MemoryMappedFile.CreateFromFile(fullPath), fileSize);
		}

		public static LargeFile Open(string fullPath)
		{
			if (!File.Exists(fullPath))
			{
				throw new ArgumentException($"File '{fullPath}' does not exist!");
			}

			var fileInfo = new FileInfo(fullPath);

			return new LargeFile(() => MemoryMappedFile.CreateFromFile(fullPath), fileInfo.Length);
		}

		public void WriteLines(byte[][] lines, List<Tuple<int, long>> offsets)
		{
			var blocks = CreateBlocksFromOffesets(offsets.Select( t => t.Item2).ToList());
			//var bb = new[] { new Block() { End = m_fileSize } };
			ProcessParallel(blocks, (s, b, c) =>
			{
				using (var writer = new StreamWriter(s))
				{
					var i = b.Id;
					long written = 0;
					while(written < b.End - b.Start)
					{
						var line = new Line(lines[i]).ToString();
						writer.WriteLine(line);
						written += line.Length + 2;
						lines[i++] = null;
					}
				}
			});
		}

		public IEnumerable<Part> Read()
		{
			var processors = Utils.GetProcessorCount();

			var partsCount = (int)(m_fileSize * 2 / Utils.GetAvailableMemory()) + 1;
			var totalBlocks = GetReadBlocks(partsCount * processors);

			while (totalBlocks.Any())
			{
				yield return new Part(this, totalBlocks.Take(processors));
				totalBlocks = totalBlocks.Skip(processors);
			}
		}

		public IEnumerable<Part> Write()
		{
			var processors = Utils.GetProcessorCount();

			var partsCount = (int)(m_fileSize * 2 / Utils.GetAvailableMemory()) + 1;
			var totalBlocks = GetWriteBlocks(partsCount * processors);

			while (totalBlocks.Any())
			{
				yield return new Part(this, totalBlocks.Take(processors));
				totalBlocks = totalBlocks.Skip(processors);
			}
		}

		public void ProcessParallel(IEnumerable<Block> blocks, Action<Stream, Block, CancellationToken> processor)
		{
			using (var mmf = m_memoryMappedFileFactory())
			{
				var tasks = blocks
				.Select(block => Task.Factory.StartNew(() =>
				{
					using (var stream = mmf.CreateViewStream(block.Start, block.End - block.Start))
					{
						processor(stream, block, m_cancel.Token);
					}
				}, m_cancel.Token));

				Task.WaitAll(tasks.ToArray());
			}
		}
	}
}
