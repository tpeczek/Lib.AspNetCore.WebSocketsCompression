using System;
using System.Text;
using System.Net.WebSockets;

namespace Lib.AspNetCore.WebSocketsCompression.Net.WebSockets
{
    internal static class WebSocketValidationHelper
    {
        #region Fields
        private const int MAX_CONTROL_FRAME_PAYLOAD_LENGTH = 123;
        private const int CLOSE_STATUS_CODE_ABORT = 1006;
        private const int CLOSE_STATUS_CODE_FAILED_TLS_HANDSHAKE = 1015;
        private const int INVALID_CLOSE_STATUS_CODES_FROM = 0;
        private const int INVALID_CLOSE_STATUS_CODES_TO = 999;
        #endregion

        #region Methods
        internal static void ValidateArraySegment(ArraySegment<byte> arraySegment, string parameterName)
        {
            if (arraySegment.Array == null)
            {
                throw new ArgumentNullException(parameterName + ".Array");
            }
        }

        internal static void ValidateCloseStatus(WebSocketCloseStatus closeStatus, string statusDescription)
        {
            if (closeStatus == WebSocketCloseStatus.Empty && !String.IsNullOrEmpty(statusDescription))
            {
                throw new ArgumentException("Reason not null", nameof(statusDescription));
            }

            int closeStatusCode = (int)closeStatus;
            if (((closeStatusCode >= INVALID_CLOSE_STATUS_CODES_FROM) && (closeStatusCode <= INVALID_CLOSE_STATUS_CODES_TO)) || (closeStatusCode == CLOSE_STATUS_CODE_ABORT) || (closeStatusCode == CLOSE_STATUS_CODE_FAILED_TLS_HANDSHAKE))
            {
                throw new ArgumentException("Invalid close status code.", nameof(closeStatus));
            }

            int length = 0;
            if (!String.IsNullOrEmpty(statusDescription))
            {
                length = Encoding.UTF8.GetByteCount(statusDescription);
            }

            if (length > MAX_CONTROL_FRAME_PAYLOAD_LENGTH)
            {
                throw new ArgumentException("Invalid close status description", nameof(statusDescription));
            }
        }

        internal static void ThrowIfInvalidState(WebSocketState currentState, bool isDisposed, WebSocketState[] validStates)
        {
            string validStatesText = string.Empty;

            if ((validStates != null) && (validStates.Length > 0))
            {
                foreach (WebSocketState validState in validStates)
                {
                    if (currentState == validState)
                    {
                        if (isDisposed)
                        {
                            throw new ObjectDisposedException(nameof(CompressionWebSocket));
                        }

                        return;
                    }
                }

                validStatesText = string.Join(", ", validStates);
            }

            throw new WebSocketException(WebSocketError.InvalidState, "Invalid state");
        }
        #endregion
    }
}
