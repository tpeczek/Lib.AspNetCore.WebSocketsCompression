using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using Lib.AspNetCore.WebSocketsCompression.Helpers;

namespace Lib.AspNetCore.WebSocketsCompression.Net.WebSockets
{
    internal class CompressionWebSocket : WebSocket
    {
        #region Fields
        private const int MAX_MESSAGE_HEADER_LENGTH = 14;
        private const int MASK_LENGTH = 4;
        private const int MAX_CONTROL_PAYLOAD_LENGTH = 125;

        private static readonly UTF8Encoding _textEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        private static readonly WebSocketState[] _validReceiveStates = { WebSocketState.Open, WebSocketState.CloseSent };
        private static readonly WebSocketState[] _validSendStates = { WebSocketState.Open, WebSocketState.CloseReceived };
        private static readonly WebSocketState[] _validCloseOutputStates = { WebSocketState.Open, WebSocketState.CloseReceived };

        private readonly Stream _stream;
        private readonly string _subprotocol;

        private readonly Timer _keepAliveTimer;
        private readonly Utf8MessageState _utf8TextState = new Utf8MessageState();
        private readonly CancellationTokenSource _abortSource = new CancellationTokenSource();

        private WebSocketState _state = WebSocketState.Open;
        private bool _disposed;

        private bool _sentCloseFrame;
        private bool _receivedCloseFrame;
        private WebSocketCloseStatus? _closeStatus = null;
        private string _closeStatusDescription = null;        

        private readonly byte[] _receiveBuffer;
        private int _receiveBufferCount = 0;
        private int _receiveBufferOffset = 0;
        private int _receivedMaskOffsetOffset = 0;
        private Task<WebSocketReceiveResult> _lastReceiveTask;
        private CompressionWebSocketMessageHeader _lastReceiveHeader = new CompressionWebSocketMessageHeader { Opcode = WebSocketMessageOpcode.Text, Fin = true };

        private byte[] _sendBuffer;
        private bool _lastSendWasFragment;
        private Task _lastSendTask;
        private readonly SemaphoreSlim _sendFrameAsyncLock = new SemaphoreSlim(1, 1);
        #endregion

        #region Properties
        public override WebSocketCloseStatus? CloseStatus => _closeStatus;

        public override string CloseStatusDescription => _closeStatusDescription;

        public override string SubProtocol => _subprotocol;

        public override WebSocketState State => _state;

        private object StateUpdateLock => _abortSource;

        private object ReceiveAsyncLock => _utf8TextState;
        #endregion

        #region Constructor
        internal CompressionWebSocket(Stream stream, string subprotocol, TimeSpan keepAliveInterval, int receiveBufferSize)
        {
            _stream = stream;
            _subprotocol = subprotocol;
            _receiveBuffer = new byte[Math.Max(receiveBufferSize, MAX_MESSAGE_HEADER_LENGTH)];

            _abortSource.Token.Register(source =>
            {
                CompressionWebSocket webSocket = (CompressionWebSocket)source;

                lock (webSocket.StateUpdateLock)
                {
                    WebSocketState state = webSocket._state;
                    if (state != WebSocketState.Closed && state != WebSocketState.Aborted)
                    {
                        webSocket._state = ((state != WebSocketState.None) && (state != WebSocketState.Connecting)) ? WebSocketState.Aborted : WebSocketState.Closed;
                    }
                }
            }, this);

            if (keepAliveInterval > TimeSpan.Zero)
            {
                _keepAliveTimer = new Timer(source => ((CompressionWebSocket)source).SendKeepAliveFrameAsync(), this, keepAliveInterval, keepAliveInterval);
            }
        }
        #endregion

        #region Methods
        public override void Abort()
        {
            _abortSource.Cancel();

            Dispose();
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            WebSocketValidationHelper.ValidateArraySegment(buffer, nameof(buffer));

            try
            {
                WebSocketValidationHelper.ThrowIfInvalidState(_state, _disposed, _validReceiveStates);

                lock (ReceiveAsyncLock)
                {
                    ThrowIfOperationInProgress(_lastReceiveTask);

                    Task<WebSocketReceiveResult> receiveTask = _lastReceiveTask = InternalReceiveAsync(buffer, cancellationToken);
                    return receiveTask;
                }
            }
            catch (Exception ex)
            {
                return TargetFrameworksHelper.FromException<WebSocketReceiveResult>(ex);
            }
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            return SendAsync(buffer, messageType, false, endOfMessage, cancellationToken);
        }

        public Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool compressed, bool endOfMessage, CancellationToken cancellationToken)
        {
            if (messageType != WebSocketMessageType.Text && messageType != WebSocketMessageType.Binary)
            {
                throw new ArgumentException("Invalid message type", nameof(messageType));
            }

            WebSocketValidationHelper.ValidateArraySegment(buffer, nameof(buffer));

            try
            {
                WebSocketValidationHelper.ThrowIfInvalidState(_state, _disposed, _validSendStates);
                ThrowIfOperationInProgress(_lastSendTask);
            }
            catch (Exception ex)
            {
                return TargetFrameworksHelper.FromException(ex);
            }

            WebSocketMessageOpcode opcode = _lastSendWasFragment ? WebSocketMessageOpcode.Continuation : (messageType == WebSocketMessageType.Binary ? WebSocketMessageOpcode.Binary : WebSocketMessageOpcode.Text);

            Task sendTask = SendFrameAsync(opcode, compressed, endOfMessage, buffer, cancellationToken);
            _lastSendWasFragment = !endOfMessage;
            _lastSendTask = sendTask;

            return sendTask;
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            WebSocketValidationHelper.ValidateCloseStatus(closeStatus, statusDescription);

            try
            {
                WebSocketValidationHelper.ThrowIfInvalidState(_state, _disposed, _validCloseOutputStates);
            }
            catch (Exception ex)
            {
                return TargetFrameworksHelper.FromException(ex);
            }

            return InternalCloseAsync(closeStatus, statusDescription, cancellationToken);
        }

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            WebSocketValidationHelper.ValidateCloseStatus(closeStatus, statusDescription);

            try
            {
                WebSocketValidationHelper.ThrowIfInvalidState(_state, _disposed, _validCloseOutputStates);
            }
            catch (Exception ex)
            {
                return TargetFrameworksHelper.FromException(ex);
            }

            return SendCloseFrameAsync(closeStatus, statusDescription, cancellationToken);
        }

        public override void Dispose()
        {
            lock (StateUpdateLock)
            {
                DisposeCompressionWebSocket();
            }
        }

        private void DisposeCompressionWebSocket()
        {
            if (!_disposed)
            {
                _disposed = true;

                _keepAliveTimer?.Dispose();
                _stream?.Dispose();

                if (_state < WebSocketState.Aborted)
                {
                    _state = WebSocketState.Closed;
                }
            }
        }

        private async Task<WebSocketReceiveResult> InternalReceiveAsync(ArraySegment<byte> payloadBuffer, CancellationToken cancellationToken)
        {
            CancellationTokenRegistration registration = cancellationToken.Register(s => ((CompressionWebSocket)s).Abort(), this);
            try
            {
                while (true)
                {
                    CompressionWebSocketMessageHeader header = _lastReceiveHeader;
                    if (header.PayloadLength == 0)
                    {
                        if (_receiveBufferCount < (MAX_MESSAGE_HEADER_LENGTH - MASK_LENGTH))
                        {
                            if (_receiveBufferCount < 2)
                            {
                                await EnsureBufferContainsAsync(2, cancellationToken, throwOnPrematureClosure: true).ConfigureAwait(false);
                            }

                            long payloadLength = _receiveBuffer[_receiveBufferOffset + 1] & 0x7F;
                            int minNeeded = 2 + MASK_LENGTH + (payloadLength <= 125 ? 0 : payloadLength == 126 ? sizeof(ushort) : sizeof(ulong));

                            await EnsureBufferContainsAsync(minNeeded, cancellationToken).ConfigureAwait(false);
                        }

                        if (!TryParseMessageHeaderFromReceiveBuffer(out header))
                        {
                            await CloseWithReceiveErrorAndThrowAsync(WebSocketCloseStatus.ProtocolError, WebSocketError.Faulted, cancellationToken).ConfigureAwait(false);
                        }
                        _receivedMaskOffsetOffset = 0;
                    }

                    if (header.Opcode == WebSocketMessageOpcode.Ping || header.Opcode == WebSocketMessageOpcode.Pong)
                    {
                        await HandleReceivedPingPongAsync(header, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    else if (header.Opcode == WebSocketMessageOpcode.Close)
                    {
                        return await HandleReceivedCloseAsync(header, cancellationToken).ConfigureAwait(false);
                    }

                    if (header.Opcode == WebSocketMessageOpcode.Continuation)
                    {
                        header.Compressed = _lastReceiveHeader.Compressed;
                        header.Opcode = _lastReceiveHeader.Opcode;
                    }

                    int bytesToRead = (int)Math.Min(payloadBuffer.Count, header.PayloadLength);
                    if (bytesToRead == 0)
                    {
                        _lastReceiveHeader = header;
                        return new WebSocketReceiveResult(
                            0,
                            header.Opcode == WebSocketMessageOpcode.Text ? WebSocketMessageType.Text : WebSocketMessageType.Binary,
                            header.PayloadLength == 0 ? header.Fin : false);
                    }

                    if (_receiveBufferCount == 0)
                    {
                        await EnsureBufferContainsAsync(1, cancellationToken, throwOnPrematureClosure: false).ConfigureAwait(false);
                    }

                    int bytesToCopy = Math.Min(bytesToRead, _receiveBufferCount);
                    _receivedMaskOffsetOffset = ApplyMask(_receiveBuffer, _receiveBufferOffset, header.Mask, _receivedMaskOffsetOffset, bytesToCopy);

                    Buffer.BlockCopy(_receiveBuffer, _receiveBufferOffset, payloadBuffer.Array, payloadBuffer.Offset, bytesToCopy);
                    ConsumeFromBuffer(bytesToCopy);
                    header.PayloadLength -= bytesToCopy;

                    if ((header.Opcode == WebSocketMessageOpcode.Text) && !header.Compressed && !TryValidateUtf8(new ArraySegment<byte>(payloadBuffer.Array, payloadBuffer.Offset, bytesToCopy), header.Fin, _utf8TextState))
                    {
                        await CloseWithReceiveErrorAndThrowAsync(WebSocketCloseStatus.InvalidPayloadData, WebSocketError.Faulted, cancellationToken).ConfigureAwait(false);
                    }

                    _lastReceiveHeader = header;
                    return new CompressionWebSocketReceiveResult(
                        bytesToCopy,
                        header.Opcode == WebSocketMessageOpcode.Text ? WebSocketMessageType.Text : WebSocketMessageType.Binary,
                        header.Compressed,
                        bytesToCopy == 0 || (header.Fin && header.PayloadLength == 0));
                }
            }
            catch (Exception ex)
            {
                throw _state == WebSocketState.Aborted ? new WebSocketException(WebSocketError.InvalidState, "Invalid state.", ex) : new WebSocketException(WebSocketError.ConnectionClosedPrematurely, ex);
            }
            finally
            {
                registration.Dispose();
            }
        }

        private async Task CloseWithReceiveErrorAndThrowAsync(WebSocketCloseStatus closeStatus, WebSocketError error, CancellationToken cancellationToken, Exception innerException = null)
        {
            if (!_sentCloseFrame)
            {
                await CloseOutputAsync(closeStatus, string.Empty, cancellationToken).ConfigureAwait(false);
            }

            _receiveBufferCount = 0;

            throw new WebSocketException(error, innerException);
        }

        private async Task InternalCloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            if (!_sentCloseFrame)
            {
                await SendCloseFrameAsync(closeStatus, statusDescription, cancellationToken).ConfigureAwait(false);
            }

            byte[] closeBuffer = new byte[MAX_MESSAGE_HEADER_LENGTH + MAX_CONTROL_PAYLOAD_LENGTH];
            while (!_receivedCloseFrame)
            {
                Task<WebSocketReceiveResult> receiveTask;
                lock (ReceiveAsyncLock)
                {
                    if (_receivedCloseFrame)
                    {
                        break;
                    }

                    receiveTask = _lastReceiveTask;
                    if (receiveTask == null ||
                        (receiveTask.Status == TaskStatus.RanToCompletion && receiveTask.Result.MessageType != WebSocketMessageType.Close))
                    {
                        _lastReceiveTask = receiveTask = InternalReceiveAsync(new ArraySegment<byte>(closeBuffer), cancellationToken);
                    }
                }

                await receiveTask.ConfigureAwait(false);
            }

            lock (StateUpdateLock)
            {
                DisposeCompressionWebSocket();
                if (_state < WebSocketState.Closed)
                {
                    _state = WebSocketState.Closed;
                }
            }
        }

        private bool TryParseMessageHeaderFromReceiveBuffer(out CompressionWebSocketMessageHeader resultHeader)
        {
            var header = new CompressionWebSocketMessageHeader();

            header.Fin = (_receiveBuffer[_receiveBufferOffset] & 0x80) != 0;
            header.Compressed = (_receiveBuffer[_receiveBufferOffset] & 0x40) != 0;
            bool reservedSet = (_receiveBuffer[_receiveBufferOffset] & 0x70) != 0;
            bool reservedExceptCompressedSet = (_receiveBuffer[_receiveBufferOffset] & 0x30) != 0;
            header.Opcode = (WebSocketMessageOpcode)(_receiveBuffer[_receiveBufferOffset] & 0xF);

            bool masked = (_receiveBuffer[_receiveBufferOffset + 1] & 0x80) != 0;
            header.PayloadLength = _receiveBuffer[_receiveBufferOffset + 1] & 0x7F;

            ConsumeFromBuffer(2);

            if (header.PayloadLength == 126)
            {
                header.PayloadLength = (_receiveBuffer[_receiveBufferOffset] << 8) | _receiveBuffer[_receiveBufferOffset + 1];
                ConsumeFromBuffer(2);
            }
            else if (header.PayloadLength == 127)
            {
                header.PayloadLength = 0;
                for (int i = 0; i < 8; i++)
                {
                    header.PayloadLength = (header.PayloadLength << 8) | _receiveBuffer[_receiveBufferOffset + i];
                }
                ConsumeFromBuffer(8);
            }

            bool shouldFail = (!header.Compressed && reservedSet) || reservedExceptCompressedSet;
            if (masked)
            {
                header.Mask = BitConverter.ToInt32(_receiveBuffer, _receiveBufferOffset);
                ConsumeFromBuffer(4);
            }

            switch (header.Opcode)
            {
                case WebSocketMessageOpcode.Continuation:
                    if (_lastReceiveHeader.Fin)
                    {
                        shouldFail = true;
                    }
                    break;

                case WebSocketMessageOpcode.Binary:
                case WebSocketMessageOpcode.Text:
                    if (!_lastReceiveHeader.Fin)
                    {
                        shouldFail = true;
                    }
                    break;

                case WebSocketMessageOpcode.Close:
                case WebSocketMessageOpcode.Ping:
                case WebSocketMessageOpcode.Pong:
                    if (header.PayloadLength > MAX_CONTROL_PAYLOAD_LENGTH || !header.Fin)
                    {
                        shouldFail = true;
                    }
                    break;

                default:
                    shouldFail = true;
                    break;
            }

            resultHeader = header;

            return !shouldFail;
        }

        private static unsafe int ApplyMask(byte[] toMask, int toMaskOffset, int mask, int maskIndex, long count)
        {
            byte* maskPtr = (byte*)&mask;

            fixed (byte* toMaskPtr = toMask)
            {
                byte* p = toMaskPtr + toMaskOffset;
                byte* end = p + count;
                while (p < end)
                {
                    *p++ ^= maskPtr[maskIndex];
                    maskIndex = (maskIndex + 1) & 3;
                }
                return maskIndex;
            }
        }

        private async Task HandleReceivedPingPongAsync(CompressionWebSocketMessageHeader header, CancellationToken cancellationToken)
        {
            if (header.PayloadLength > 0 && _receiveBufferCount < header.PayloadLength)
            {
                await EnsureBufferContainsAsync((int)header.PayloadLength, cancellationToken).ConfigureAwait(false);
            }

            if (header.Opcode == WebSocketMessageOpcode.Ping)
            {
                ApplyMask(_receiveBuffer, _receiveBufferOffset, header.Mask, 0, header.PayloadLength);

                await SendFrameAsync(WebSocketMessageOpcode.Pong, false, true, new ArraySegment<byte>(_receiveBuffer, _receiveBufferOffset, (int)header.PayloadLength), cancellationToken).ConfigureAwait(false);
            }

            if (header.PayloadLength > 0)
            {
                ConsumeFromBuffer((int)header.PayloadLength);
            }
        }

        private async Task<WebSocketReceiveResult> HandleReceivedCloseAsync(CompressionWebSocketMessageHeader header, CancellationToken cancellationToken)
        {
            lock (StateUpdateLock)
            {
                _receivedCloseFrame = true;
                if (_state < WebSocketState.CloseReceived)
                {
                    _state = WebSocketState.CloseReceived;
                }
            }

            WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure;
            string closeStatusDescription = string.Empty;

            if (header.PayloadLength == 1)
            {
                await CloseWithReceiveErrorAndThrowAsync(WebSocketCloseStatus.ProtocolError, WebSocketError.Faulted, cancellationToken).ConfigureAwait(false);
            }
            else if (header.PayloadLength >= 2)
            {
                if (_receiveBufferCount < header.PayloadLength)
                {
                    await EnsureBufferContainsAsync((int)header.PayloadLength, cancellationToken).ConfigureAwait(false);
                }

                ApplyMask(_receiveBuffer, _receiveBufferOffset, header.Mask, 0, header.PayloadLength);

                closeStatus = (WebSocketCloseStatus)(_receiveBuffer[_receiveBufferOffset] << 8 | _receiveBuffer[_receiveBufferOffset + 1]);
                if (!IsValidCloseStatus(closeStatus))
                {
                    await CloseWithReceiveErrorAndThrowAsync(WebSocketCloseStatus.ProtocolError, WebSocketError.Faulted, cancellationToken).ConfigureAwait(false);
                }

                if (header.PayloadLength > 2)
                {
                    try
                    {
                        closeStatusDescription = _textEncoding.GetString(_receiveBuffer, _receiveBufferOffset + 2, (int)header.PayloadLength - 2);
                    }
                    catch (DecoderFallbackException exc)
                    {
                        await CloseWithReceiveErrorAndThrowAsync(WebSocketCloseStatus.ProtocolError, WebSocketError.Faulted, cancellationToken, exc).ConfigureAwait(false);
                    }
                }
                ConsumeFromBuffer((int)header.PayloadLength);
            }

            _closeStatus = closeStatus;
            _closeStatusDescription = closeStatusDescription;

            return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true, closeStatus, closeStatusDescription);
        }

        private static bool IsValidCloseStatus(WebSocketCloseStatus closeStatus)
        {
            // 0-999: "not used"
            // 1000-2999: reserved for the protocol; we need to check individual codes manually
            // 3000-3999: reserved for use by higher-level code
            // 4000-4999: reserved for private use
            // 5000-: not mentioned in RFC

            if (closeStatus < (WebSocketCloseStatus)1000 || closeStatus >= (WebSocketCloseStatus)5000)
            {
                return false;
            }

            if (closeStatus >= (WebSocketCloseStatus)3000)
            {
                return true;
            }

            // Check for the 1000-2999 range known codes
            switch (closeStatus)
            {
                case WebSocketCloseStatus.EndpointUnavailable:
                case WebSocketCloseStatus.InternalServerError:
                case WebSocketCloseStatus.InvalidMessageType:
                case WebSocketCloseStatus.InvalidPayloadData:
                case WebSocketCloseStatus.MandatoryExtension:
                case WebSocketCloseStatus.MessageTooBig:
                case WebSocketCloseStatus.NormalClosure:
                case WebSocketCloseStatus.PolicyViolation:
                case WebSocketCloseStatus.ProtocolError:
                    return true;
                default:
                    return false;
            }
        }

        private void SendKeepAliveFrameAsync()
        {
            bool acquiredLock = _sendFrameAsyncLock.Wait(0);
            if (acquiredLock)
            {
                SendFrameLockAcquiredNonCancelableAsync(WebSocketMessageOpcode.Ping, false, true, new ArraySegment<byte>(TargetFrameworksHelper.Empty<byte>()));
            }
        }

        private async Task SendCloseFrameAsync(WebSocketCloseStatus closeStatus, string closeStatusDescription, CancellationToken cancellationToken)
        {
            byte[] buffer;
            if (string.IsNullOrEmpty(closeStatusDescription))
            {
                buffer = new byte[2];
            }
            else
            {
                buffer = new byte[2 + _textEncoding.GetByteCount(closeStatusDescription)];
                int encodedLength = _textEncoding.GetBytes(closeStatusDescription, 0, closeStatusDescription.Length, buffer, 2);
            }

            ushort closeStatusValue = (ushort)closeStatus;
            buffer[0] = (byte)(closeStatusValue >> 8);
            buffer[1] = (byte)(closeStatusValue & 0xFF);

            await SendFrameAsync(WebSocketMessageOpcode.Close, false, true, new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);

            lock (StateUpdateLock)
            {
                _sentCloseFrame = true;
                if (_state <= WebSocketState.CloseReceived)
                {
                    _state = WebSocketState.CloseSent;
                }
            }
        }

        private Task SendFrameAsync(WebSocketMessageOpcode opcode, bool compressed, bool endOfMessage, ArraySegment<byte> payloadBuffer, CancellationToken cancellationToken)
        {
            return cancellationToken.CanBeCanceled || !_sendFrameAsyncLock.Wait(0) ?
                SendFrameFallbackAsync(opcode, compressed, endOfMessage, payloadBuffer, cancellationToken) :
                SendFrameLockAcquiredNonCancelableAsync(opcode, compressed, endOfMessage, payloadBuffer);
        }

        private Task SendFrameLockAcquiredNonCancelableAsync(WebSocketMessageOpcode opcode, bool compressed, bool endOfMessage, ArraySegment<byte> payloadBuffer)
        {
            Task writeTask = null;
            bool releaseSemaphore = true;

            try
            {
                int sendBytes = WriteFrameToSendBuffer(opcode, compressed, endOfMessage, payloadBuffer);

                writeTask = _stream.WriteAsync(_sendBuffer, 0, sendBytes, CancellationToken.None);

                if (writeTask.IsCompleted)
                {
                    writeTask.GetAwaiter().GetResult();

                    return TargetFrameworksHelper.CompletedTask;
                }

                releaseSemaphore = false;
            }
            catch (Exception ex)
            {
                return TargetFrameworksHelper.FromException(_state == WebSocketState.Aborted ? CreateOperationCanceledException(ex) : new WebSocketException(WebSocketError.ConnectionClosedPrematurely, ex));
            }
            finally
            {
                if (releaseSemaphore)
                {
                    _sendFrameAsyncLock.Release();
                }
            }

            return writeTask.ContinueWith((task, source) =>
            {
                CompressionWebSocket webSocket = (CompressionWebSocket)source;
                webSocket._sendFrameAsyncLock.Release();

                try
                {
                    task.GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    throw webSocket._state == WebSocketState.Aborted ? CreateOperationCanceledException(ex) : new WebSocketException(WebSocketError.ConnectionClosedPrematurely, ex);
                }
            }, this, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private async Task SendFrameFallbackAsync(WebSocketMessageOpcode opcode, bool compressed, bool endOfMessage, ArraySegment<byte> payloadBuffer, CancellationToken cancellationToken)
        {
            await _sendFrameAsyncLock.WaitAsync().ConfigureAwait(false);

            try
            {
                int sendBytes = WriteFrameToSendBuffer(opcode, compressed, endOfMessage, payloadBuffer);
                using (cancellationToken.Register(source => ((CompressionWebSocket)source).Abort(), this))
                {
                    await _stream.WriteAsync(_sendBuffer, 0, sendBytes, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception exc)
            {
                throw _state == WebSocketState.Aborted ? CreateOperationCanceledException(exc, cancellationToken) : new WebSocketException(WebSocketError.ConnectionClosedPrematurely, exc);
            }
            finally
            {
                _sendFrameAsyncLock.Release();
            }
        }
        
        private int WriteFrameToSendBuffer(WebSocketMessageOpcode opcode, bool compressed, bool endOfMessage, ArraySegment<byte> payloadBuffer)
        {
            EnsureBufferLength(ref _sendBuffer, payloadBuffer.Count + MAX_MESSAGE_HEADER_LENGTH);

            int headerLength = WriteHeader(opcode, _sendBuffer, payloadBuffer, compressed, endOfMessage);

            if (payloadBuffer.Count > 0)
            {
                Buffer.BlockCopy(payloadBuffer.Array, payloadBuffer.Offset, _sendBuffer, headerLength, payloadBuffer.Count);
            }

            return headerLength + payloadBuffer.Count;
        }

        private static int WriteHeader(WebSocketMessageOpcode opcode, byte[] sendBuffer, ArraySegment<byte> payload, bool compressed, bool endOfMessage)
        {
            // Client header format:
            // 1 bit - FIN - 1 if this is the final fragment in the message (it could be the only fragment), otherwise 0
            // 1 bit - RSV1 - Per-Message Compressed - 1 if this is the first fragment of compressed message, otherwise 0
            // 1 bit - RSV2 - Reserved - 0
            // 1 bit - RSV3 - Reserved - 0
            // 4 bits - Opcode - How to interpret the payload
            //     - 0x0 - continuation
            //     - 0x1 - text
            //     - 0x2 - binary
            //     - 0x8 - connection close
            //     - 0x9 - ping
            //     - 0xA - pong
            //     - (0x3 to 0x7, 0xB-0xF - reserved)
            // 1 bit - Masked - 1 if the payload is masked, 0 if it's not. Must be 1 for the client. This is only server and we use always 0
            // 7 bits, 7+16 bits, or 7+64 bits - Payload length
            //     - For length 0 through 125, 7 bits storing the length
            //     - For lengths 126 through 2^16, 7 bits storing the value 126, followed by 16 bits storing the length
            //     - For lengths 2^16+1 through 2^64, 7 bits storing the value 127, followed by 64 bytes storing the length
            // 0 or 4 bytes - Mask, if Masked is 1 - random value XOR'd with each 4 bytes of the payload, round-robin
            // Length bytes - Payload data

            sendBuffer[0] = (byte)opcode;

            if (compressed && (opcode != WebSocketMessageOpcode.Continuation))
            {
                sendBuffer[0] |= 0x40;
            }

            if (endOfMessage)
            {
                sendBuffer[0] |= 0x80;
            }

            int headerLength;
            if (payload.Count <= 125)
            {
                sendBuffer[1] = (byte)payload.Count;
                headerLength = 2;
            }
            else if (payload.Count <= ushort.MaxValue)
            {
                sendBuffer[1] = 126;
                sendBuffer[2] = (byte)(payload.Count / 256);
                sendBuffer[3] = (byte)payload.Count;
                headerLength = 2 + sizeof(ushort);
            }
            else
            {
                sendBuffer[1] = 127;
                int length = payload.Count;
                for (int i = 9; i >= 2; i--)
                {
                    sendBuffer[i] = (byte)length;
                    length = length / 256;
                }
                headerLength = 2 + sizeof(ulong);
            }

            return headerLength;
        }

        private static void EnsureBufferLength(ref byte[] buffer, int minLength)
        {
            if ((buffer == null) || (buffer.Length < minLength))
            {
                buffer = new byte[minLength];
            }
        }

        private async Task EnsureBufferContainsAsync(int minimumRequiredBytes, CancellationToken cancellationToken, bool throwOnPrematureClosure = true)
        {
            if (_receiveBufferCount < minimumRequiredBytes)
            {
                if (_receiveBufferCount > 0)
                {
                    Buffer.BlockCopy(_receiveBuffer, _receiveBufferOffset, _receiveBuffer, 0, _receiveBufferCount);
                }
                _receiveBufferOffset = 0;

                while (_receiveBufferCount < minimumRequiredBytes)
                {
                    int numRead = await _stream.ReadAsync(_receiveBuffer, _receiveBufferCount, _receiveBuffer.Length - _receiveBufferCount, cancellationToken).ConfigureAwait(false);
                    _receiveBufferCount += numRead;

                    if (numRead == 0)
                    {
                        if (_disposed)
                        {
                            throw new ObjectDisposedException(nameof(CompressionWebSocket));
                        }
                        else if (throwOnPrematureClosure)
                        {
                            throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely);
                        }
                        break;
                    }
                }
            }
        }

        private void ConsumeFromBuffer(int count)
        {
            _receiveBufferCount -= count;
            _receiveBufferOffset += count;
        }

        private static bool TryValidateUtf8(ArraySegment<byte> arraySegment, bool endOfMessage, Utf8MessageState state)
        {
            for (int i = arraySegment.Offset; i < arraySegment.Offset + arraySegment.Count;)
            {
                // Have we started a character sequence yet?
                if (!state.SequenceInProgress)
                {
                    // The first byte tells us how many bytes are in the sequence.
                    state.SequenceInProgress = true;
                    byte b = arraySegment.Array[i];
                    i++;
                    if ((b & 0x80) == 0) // 0bbbbbbb, single byte
                    {
                        state.AdditionalBytesExpected = 0;
                        state.CurrentDecodeBits = b & 0x7F;
                        state.ExpectedValueMin = 0;
                    }
                    else if ((b & 0xC0) == 0x80)
                    {
                        // Misplaced 10bbbbbb continuation byte. This cannot be the first byte.
                        return false;
                    }
                    else if ((b & 0xE0) == 0xC0) // 110bbbbb 10bbbbbb
                    {
                        state.AdditionalBytesExpected = 1;
                        state.CurrentDecodeBits = b & 0x1F;
                        state.ExpectedValueMin = 0x80;
                    }
                    else if ((b & 0xF0) == 0xE0) // 1110bbbb 10bbbbbb 10bbbbbb
                    {
                        state.AdditionalBytesExpected = 2;
                        state.CurrentDecodeBits = b & 0xF;
                        state.ExpectedValueMin = 0x800;
                    }
                    else if ((b & 0xF8) == 0xF0) // 11110bbb 10bbbbbb 10bbbbbb 10bbbbbb
                    {
                        state.AdditionalBytesExpected = 3;
                        state.CurrentDecodeBits = b & 0x7;
                        state.ExpectedValueMin = 0x10000;
                    }
                    else // 111110bb & 1111110b & 11111110 && 11111111 are not valid
                    {
                        return false;
                    }
                }
                while (state.AdditionalBytesExpected > 0 && i < arraySegment.Offset + arraySegment.Count)
                {
                    byte b = arraySegment.Array[i];
                    if ((b & 0xC0) != 0x80)
                    {
                        return false;
                    }

                    i++;
                    state.AdditionalBytesExpected--;

                    // Each continuation byte carries 6 bits of data 0x10bbbbbb.
                    state.CurrentDecodeBits = (state.CurrentDecodeBits << 6) | (b & 0x3F);

                    if (state.AdditionalBytesExpected == 1 && state.CurrentDecodeBits >= 0x360 && state.CurrentDecodeBits <= 0x37F)
                    {
                        // This is going to end up in the range of 0xD800-0xDFFF UTF-16 surrogates that are not allowed in UTF-8;
                        return false;
                    }
                    if (state.AdditionalBytesExpected == 2 && state.CurrentDecodeBits >= 0x110)
                    {
                        // This is going to be out of the upper Unicode bound 0x10FFFF.
                        return false;
                    }
                }
                if (state.AdditionalBytesExpected == 0)
                {
                    state.SequenceInProgress = false;
                    if (state.CurrentDecodeBits < state.ExpectedValueMin)
                    {
                        // Overlong encoding (e.g. using 2 bytes to encode something that only needed 1).
                        return false;
                    }
                }
            }
            if (endOfMessage && state.SequenceInProgress)
            {
                return false;
            }
            return true;
        }

        private void ThrowIfOperationInProgress(Task operationTask, [CallerMemberName] string methodName = null)
        {
            if (operationTask != null && !operationTask.IsCompleted)
            {
                Abort();

                throw new InvalidOperationException("Already one outstanding operation");
            }
        }

        private static Exception CreateOperationCanceledException(Exception innerException, CancellationToken cancellationToken = default(CancellationToken))
        {
            return new OperationCanceledException(new OperationCanceledException().Message, innerException, cancellationToken);
        }
        #endregion
    }
}
