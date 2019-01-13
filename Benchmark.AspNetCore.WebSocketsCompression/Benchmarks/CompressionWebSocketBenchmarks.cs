using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Lib.AspNetCore.WebSocketsCompression.Net.WebSockets;

namespace Benchmark.AspNetCore.WebSocketsCompression.Benchmarks
{
    [MemoryDiagnoser]
    public class CompressionWebSocketBenchmarks
    {
        #region Fields
        private const int MULTIPLE_CLIENTS_COUNT = 10000;
        private static readonly TimeSpan KEEP_ALIVE_INTERVAL = TimeSpan.FromMinutes(2);
        private const int RECEIVE_BUFFER_SIZE = 4 * 1024;

        private readonly CompressionWebSocket _singleWebSocket;
        private readonly IList<CompressionWebSocket> _multipleWebSockets;
        private readonly byte[] _message = Encoding.UTF8.GetBytes("Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur.Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.");
        #endregion

        #region Constructor
        public CompressionWebSocketBenchmarks()
        {
            _singleWebSocket = new CompressionWebSocket(Stream.Null, null, KEEP_ALIVE_INTERVAL, RECEIVE_BUFFER_SIZE);

            _multipleWebSockets = new List<CompressionWebSocket>();
            for (int i = 0; i < MULTIPLE_CLIENTS_COUNT; i++)
            {
                _multipleWebSockets.Add(new CompressionWebSocket(Stream.Null, null, KEEP_ALIVE_INTERVAL, RECEIVE_BUFFER_SIZE));
            }
        }
        #endregion

        #region Benchmarks
        [Benchmark]
        public Task SendAsync_SingleMessage_SingleWebSocket()
        {
            return _singleWebSocket.SendAsync(_message, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        [Benchmark]
        public Task SendAsync_SingleMessage_MultipleWebSockets()
        {
            int webSocketTaskIndex = 0;
            Task[] webSocketsTasks = new Task[_multipleWebSockets.Count];

            foreach (CompressionWebSocket webSocket in _multipleWebSockets)
            {
                webSocketsTasks[webSocketTaskIndex++] = webSocket.SendAsync(_message, WebSocketMessageType.Text, true, CancellationToken.None);
            }

            return Task.WhenAll(webSocketsTasks);
        }
        #endregion
    }
}
