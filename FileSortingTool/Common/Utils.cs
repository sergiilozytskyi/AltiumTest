using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace Common
{
    public static partial class Utils
    {
		// System utils
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetDiskFreeSpaceEx(string dirictoryName, out ulong freeBytesAvailable, out ulong totalNumberOfBytes, out ulong totalNumberOfFreeBytes);

        public static long GetFreeDiskSpace(string path)
        {
            var directoryName = Path.GetDirectoryName(path);

            if(!Directory.Exists(directoryName))
            {
                throw new ArgumentException("Directory does not exist {0}", directoryName);
            }

            ulong freeBytesAvailable;
            ulong totalNumberOfBytes;
            ulong totalNumberOfFreeBytes;
            if (!GetDiskFreeSpaceEx(directoryName, out freeBytesAvailable, out totalNumberOfBytes, out totalNumberOfFreeBytes))
            {

            }
            // Be tolerant and assume that we can usef only half of available disk space for our application.
            return (long)Math.Min(freeBytesAvailable / 2, long.MaxValue);
        }
        
        private static bool Is64BitProcess
        {
            get
            {
                return sizeof(int) == 8;
            }
        }

        public static long GetAvailableMemory()
        {
            var freeRAM = new Microsoft.VisualBasic.Devices.ComputerInfo().AvailablePhysicalMemory;
			return (long)(freeRAM);
        }

        public static int GetProcessorCount()
        {
            return System.Environment.ProcessorCount;
        }

		public static String BytesToString(long byteCount)
		{
			string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
			if (byteCount == 0)
				return "0" + suf[0];
			long bytes = Math.Abs(byteCount);
			int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
			double num = Math.Round(bytes / Math.Pow(1024, place), 1);
			return (Math.Sign(byteCount) * num).ToString() + suf[place];
		}

		public static long GetFileSize(string fullPath)
		{
			return new FileInfo(fullPath).Length;
		}

		// Linq extensions
		public static T[] Concat<T>(this T[] x, T[] y)
		{
			if (x == null) throw new ArgumentNullException("x");
			if (y == null) throw new ArgumentNullException("y");
			int oldLen = x.Length;
			Array.Resize(ref x, x.Length + y.Length);
			Array.Copy(y, 0, x, oldLen, y.Length);
			return x;
		}

		public static void ForEach<T>(this IEnumerable<T> enumeration, Action<T> action)
		{
			foreach (T item in enumeration)
			{
				action(item);
			}
		}


		// Other utils
		public static string GetSortedFileFullPath(string unsortedFileFullPath)
		{
			int count = 1;
			var fileNameOnly = Path.GetFileNameWithoutExtension(unsortedFileFullPath) + "_sorted";
			var extension = Path.GetExtension(unsortedFileFullPath);
			var path = Path.GetDirectoryName(unsortedFileFullPath);
			var newFullPath = Path.Combine(path, fileNameOnly + extension);

			while (File.Exists(newFullPath))
			{
				string tempFileName = string.Format("{0}({1})", fileNameOnly, count++);
				newFullPath = Path.Combine(path, tempFileName + extension);
			}
			return newFullPath;
		}
	}
}
