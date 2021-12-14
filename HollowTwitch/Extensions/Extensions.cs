using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HollowTwitch.Commands;
using UnityEngine;

namespace HollowTwitch.Extensions
{
    public static class Extensions
    {
        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> self, out TKey key, out TValue value) 
            => (key, value) = (self.Key, self.Value);

        public static bool HasValue(this CameraEffects @enum, CameraEffects toCheck)
            => (@enum & toCheck) != 0;
        
        public static GameObject GetChild(this GameObject go, string child)
        {
            return go.transform.Find(child).gameObject;
        }

        public static async Task<bool> WaitOneAsync(this WaitHandle handle, int millisecondsTimeout, CancellationToken cancellationToken)
        {
            RegisteredWaitHandle registeredHandle = null;
            CancellationTokenRegistration tokenRegistration = default(CancellationTokenRegistration);
            try
            {
                var tcs = new TaskCompletionSource<bool>();
                registeredHandle = ThreadPool.RegisterWaitForSingleObject(
                    handle,
                    (state, timedOut) => ((TaskCompletionSource<bool>)state).TrySetResult(!timedOut),
                    tcs,
                    millisecondsTimeout,
                    true);
                tokenRegistration = cancellationToken.Register(
                    state => ((TaskCompletionSource<bool>)state).TrySetCanceled(),
                    tcs);
                return await tcs.Task;
            }
            finally
            {
                if (registeredHandle != null)
                    registeredHandle.Unregister(null);
                tokenRegistration.Dispose();
            }
        }

        public static Task<bool> WaitOneAsync(this WaitHandle handle, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return handle.WaitOneAsync((int)timeout.TotalMilliseconds, cancellationToken);
        }

        public static Task<bool> WaitOneAsync(this WaitHandle handle, CancellationToken cancellationToken)
        {
            return handle.WaitOneAsync(Timeout.Infinite, cancellationToken);
        }
    }
}
