using System;
using System.Threading.Tasks;

namespace Lib.AspNetCore.WebSocketsCompression.Helpers
{
    internal static class TargetFrameworksHelper
    {
        #region Fields
        private static readonly Task _completedTask;
        #endregion

        #region Properties
        internal static Task CompletedTask => _completedTask;
        #endregion

        #region Constructor
        static TargetFrameworksHelper()
        {
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            tcs.SetResult(null);

            _completedTask = tcs.Task;
        }
        #endregion

        #region Methods
        internal static Task FromException(Exception ex)
        {
#if NET451
            return FromException<object>(ex);
#else
            return Task.FromException(ex);
#endif
        }

        internal static Task<T> FromException<T>(Exception ex)
        {
#if NET451
            var tcs = new TaskCompletionSource<T>();
            tcs.SetException(ex);
            return tcs.Task;
#else
            return Task.FromException<T>(ex);
#endif
        }

        internal static T[] Empty<T>()
        {
#if NET451
            return new T[0];
#else
            return Array.Empty<T>();
#endif
        }
        #endregion
    }
}
