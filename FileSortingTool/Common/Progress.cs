namespace Common
{
	using System;
	using System.Text;

	public class Progress : IProgress<double>
	{
		private const int blockCount = 10;
		private const string animation = @"|/-\";

		private double currentProgress = 0;
		private string currentText = string.Empty;
		private int animationIndex = 0;

		public void Report(double value)
		{
			// make sure value is in [0..1] range
			value = Math.Max(0, Math.Min(1, value));

			// since we dont have free thread for Timer lets do simple rendering each only 0.01 part passed.
			// In more solid solution for progress separate thread can be dedicated for Timer which will be print text to console.
			if(value - currentProgress < 0.01 && value != 1) return;

			currentProgress = value;
			int progressBlockCount = (int)(value * blockCount);
			int percent = (int)(currentProgress * 100);
			var text = string.Format("[{0}{1}] {2,3}% {3}",
				new string('#', progressBlockCount), new string('-', blockCount - progressBlockCount),
				percent,
				animation[animationIndex++ % animation.Length]);
			UpdateText(text);
		}

		private void UpdateText(string text)
		{
			// get length of common portion
			int commonPrefixLength = 0;
			int commonLength = Math.Min(currentText.Length, text.Length);
			while (commonPrefixLength < commonLength && text[commonPrefixLength] == currentText[commonPrefixLength])
			{
				commonPrefixLength++;
			}

			// backtrack to the first differing character
			StringBuilder outputBuilder = new StringBuilder();
			outputBuilder.Append('\b', currentText.Length - commonPrefixLength);

			// output new suffix
			outputBuilder.Append(text.Substring(commonPrefixLength));

			// if the new text is shorter than the old one: delete overlapping characters
			int overlapCount = currentText.Length - text.Length;
			if (overlapCount > 0)
			{
				outputBuilder.Append(' ', overlapCount);
				outputBuilder.Append('\b', overlapCount);
			}

			Console.Write(outputBuilder);
			currentText = text;
		}
	}
}
