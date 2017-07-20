using System.Runtime.InteropServices;

namespace Lib.AspNetCore.WebSocketsCompression.Net.WebSockets
{
    [StructLayout(LayoutKind.Auto)]
    internal struct CompressionWebSocketMessageHeader
    {
        #region Properties
        internal WebSocketMessageOpcode Opcode { get; set; }

        internal bool Compressed { get; set; }

        internal bool Fin { get; set; }

        internal long PayloadLength { get; set; }

        internal int Mask { get; set; }
        #endregion
    }
}
