using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Common
{
	public class BytesComparer : IComparer<byte[]>
	{
		int IComparer<byte[]>.Compare(byte[] x, byte[] y)
		{
			return new Line(x).CompareTo(new Line(y));
		}
	}

	public class LineComparer : IComparer<Line>
	{
		int IComparer<Line>.Compare(Line x, Line y)
		{
			return x.CompareTo(y);
		}
	}
	public class Line : IComparable<Line>
	{
		public const byte Sep = 0x1F; // Unit separator char
		public const string DotAndSpace = ". "; // Dot and Sapce chars

		private string _str;
		private int _number;

		public Line(byte[] bytes)
		{
			_number = BitConverter.ToInt32(bytes.Reverse().Take(sizeof(int)).ToArray(), 0);
			_str = Encoding.ASCII.GetString(bytes, 0, bytes.Length - sizeof(int) - 1);
		}

		public Line(string line)
		{
			var dotIndex = line.IndexOf(DotAndSpace);
			if (dotIndex == -1)
				throw new ArgumentException($"Incorrect input stirng '{line}'.");

			var numStr = line.Substring(0, dotIndex);
			if (!Int32.TryParse(numStr, out _number))
				throw new ArgumentException($"Incorrect number line {numStr}");

			_str = line.Substring(dotIndex + DotAndSpace.Length, line.Length - dotIndex - DotAndSpace.Length);
		}

		public override string ToString()
		{
			return $"{_number}. {_str}";
		}

		public byte[] ToBytes()
		{
			var reverseNumBytes = new[] { Sep }.Concat(BitConverter.GetBytes(_number).Reverse().ToArray());
			return !string.IsNullOrEmpty(_str)
			? Encoding.ASCII.GetBytes(_str).Concat(reverseNumBytes)
			: reverseNumBytes;
		}

		public int CompareTo(Line line)
		{
			if (line == null) return 1;

			var result = string.Compare(_str, line._str, StringComparison.Ordinal);

			if (result != 0)
				return result;

			if (_number > line._number)
				return 1;
			else if (_number < line._number)
				return -1;
			return 0;
		}

		public static long FitOffsets(byte[][] lines, List<int> indexes, out List<Tuple<int, long>> offsets)
		{
			long total = 0;
			offsets = new List<Tuple<int, long>>(indexes.Count);
			for (var i = 0; i < lines.Length; ++i)
			{
				if(indexes.Contains(i))
				{
					offsets.Add(new Tuple<int, long>(i, total));
				}
				total += new Line(lines[i]).ToString().Length + 2;
			}
			return total;
		}
	}
}
