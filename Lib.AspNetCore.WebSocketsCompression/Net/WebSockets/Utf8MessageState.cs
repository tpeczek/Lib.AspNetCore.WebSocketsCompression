namespace Lib.AspNetCore.WebSocketsCompression.Net.WebSockets
{
    internal sealed class Utf8MessageState
    {
        #region Properties
        internal bool SequenceInProgress { get; set; }

        internal int AdditionalBytesExpected { get; set; }

        internal int ExpectedValueMin { get; set; }

        internal int CurrentDecodeBits { get; set; }
        #endregion
    }
}
