using System;
using System.Threading.Tasks;

namespace Hangfire.Correlate.Extensions
{
	public static class TaskExtensions
	{
		public static async Task<T> WithTimeout<T>(this Task<T> task, int millisecondsTimeout = 5000)
		{
			await Task.WhenAny(task, Task.Delay(millisecondsTimeout));
			if (task.IsCompleted)
			{
				return await task;
			}

			throw new TimeoutException();
		}
	}
}
