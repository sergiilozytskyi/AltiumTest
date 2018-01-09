using System;
using System.Diagnostics;
using System.IO;

namespace Common
{
	public abstract class ConsoleApp
	{
		protected string fileName;

		public string Name { get; private set; }

		public ConsoleApp(string name)
		{
			Name = name;
			Console.CancelKeyPress += Console_CancelKeyPress;
		}

		protected abstract void OnCancel();

		protected abstract void OnExecute();

		protected abstract void PrintHelpText();

		protected virtual bool ParsePrameters(string[] args)
		{
			fileName = ParseArgument<string>(args, "-FileName", null);
			if (string.IsNullOrEmpty(fileName))
			{
				Error($"Please specify valid file!");
				return false;
			}

			if (string.IsNullOrEmpty(Path.GetDirectoryName(fileName)))
			{
				// use curren directory
				fileName = Path.Combine(Directory.GetCurrentDirectory(), fileName);
			}

			return true;
		}

		private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			e.Cancel = true;
			Console.WriteLine("Cancellation requested, please wait...");
			OnCancel();
		}

		protected static T ParseArgument<T>(string[] args, string name, T deafult)
		{
			try
			{
				var index = Array.IndexOf(args, name);
				if (-1 != index)
				{
					return (T)Convert.ChangeType(args[index + 1], typeof(T));
				}
				else
				{
					return deafult;
				}
			}
			catch (Exception ex)
			{
				return default(T);
			}
		}

		public int Run(string[] args)
		{
			try
			{
				Console.WriteLine($"{Name}.");
				var stopWatch = new Stopwatch();
				stopWatch.Start();

				if (ParsePrameters(args))
				{
					var totalFreeRam = Utils.GetAvailableMemory();
					var processors = Utils.GetProcessorCount();
					Console.WriteLine("====================System parameters====================");
					Console.WriteLine("Available RAM: {0}", Utils.BytesToString(totalFreeRam));
					Console.WriteLine("Processors: {0}", processors);
					Console.WriteLine("Available disk space: {0}", Utils.BytesToString(Utils.GetFreeDiskSpace(fileName)));
					Console.WriteLine("=========================================================");
					Console.WriteLine("Execution started. To interrupt process press 'Ctrl+C'.");

					OnExecute();

					var ts = stopWatch.Elapsed;
					Console.WriteLine(String.Format("Execution time: {0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10));
					Console.WriteLine("Press any key...");
					Console.ReadLine();
				}
				else
				{
					PrintHelpText();
				}
				return 0;
			}
            catch(Exception ex)
            {
				Error("Unexpected error.", ex);
				return 1;
            }
		}

		protected static void Error(string message, Exception ex = null)
		{
			Console.WriteLine("=============== Something went wrong :( ================");
			Console.WriteLine(message);

			if (ex != null)
			{
				Console.WriteLine("================== Error Details ==================");
				Console.WriteLine(ex.ToString());
			}

			Console.WriteLine("==================================================");
		}
	}
}
