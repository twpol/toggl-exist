using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Toggl_Exist
{
    public static class AsyncExtensions
    {
        public static IEnumerable<TResult> WaitAll<TResult>(this IEnumerable<Task<TResult>> source)
        {
            Task.WaitAll(source.ToArray());
            return source.Select(task => task.Result);
        }
    }
}
