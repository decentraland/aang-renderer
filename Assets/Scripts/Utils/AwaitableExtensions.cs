using System.Threading.Tasks;
using UnityEngine;

namespace Utils
{
    public static class AwaitableExtensions
    {
        public static async Task AsTask(this Awaitable a)
        {
            await a;
        }

        public static async Task<T> AsTask<T>(this Awaitable<T> a)
        {
            return await a;
        }
    }
}