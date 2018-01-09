namespace SortingTool
{
	interface ISort<T>
	{
		void Sort(T[] source);
		void Cancel();
	}
}
