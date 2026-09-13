using System;

namespace TikChoco.MistLib
{
    public sealed class MistNativeException : Exception
    {
        public MistNativeException(string message) : base(message)
        {
        }

        public MistNativeException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
