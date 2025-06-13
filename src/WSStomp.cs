using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace WSStomp
{
    public class WsStomp
    {
        private readonly StringBuilder _buffer = new StringBuilder(); // Shared buffer
        private readonly object _bufferLock = new object(); // Lock object for thread safety
        private readonly ClientWebSocket _client = new ClientWebSocket();

        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>
        {
            { "accept-version", "1.2" },
            { "heart-beat", "10000,10000" }
        };

        private readonly Dictionary<string, StompSubscription> _subscriptions = new Dictionary<string, StompSubscription>();
        private readonly CancellationToken _cancellationToken = CancellationToken.None;

        private TaskCompletionSource<bool> _connectCompletionSource;
        private int _nextSubscriptionId;

        public void SetHttpHeader([NotNull] string headerName, [NotNull] string headerValue)
        {
            _client.Options.SetRequestHeader(headerName, headerValue);
        }

        public void SetStompHeader([NotNull] string headerName, [NotNull] string headerValue)
        {
            _headers.Add(headerName, headerValue);
        }

        [NotNull]
        public async Task ConnectAsync([NotNull] Uri uri, CancellationToken cancellationToken)
        {
            if (_connectCompletionSource != null) throw new InvalidOperationException("ConnectAsync is already in progress.");

            _connectCompletionSource = new TaskCompletionSource<bool>();

            try
            {
                await _client.ConnectAsync(uri, cancellationToken);
                _ = StartReceivingAsync();
                await SendStompCommand(StompCommand.Connect(), _headers, null);
                await _connectCompletionSource.Task;
            }
            finally
            {
                _connectCompletionSource = null;
            }
        }

        [NotNull]
        public async Task CloseAsync(WebSocketCloseStatus closeStatus, [NotNull] string statusDescription, CancellationToken cancellationToken)
        {
            // cancellationToken. checkAL
            await SendStompCommand(StompCommand.Disconnect(), null, null);
            await _client.CloseAsync(closeStatus, statusDescription, cancellationToken);
        }

        [NotNull]
        public async Task<StompSubscription> SubscribeAsync([NotNull] string destination, Func<StompMessage, Task> messageHandler)
        {
            var id = "sub-" + Interlocked.Increment(ref _nextSubscriptionId);
            var subscription = new StompSubscription(this, id, destination);
            await subscription.Subscribe(messageHandler);
            _subscriptions.Add(id, subscription);
            return subscription;
        }

        public async Task SendAsync([NotNull] string destination, [CanBeNull] string body)
        {
            var headers = new Dictionary<string, string>
            {
                { "destination", destination }
            };
            await SendStompCommand(StompCommand.Send(), headers, body);
        }

        internal async Task SendStompCommand([NotNull] StompCommand command, [CanBeNull] Dictionary<string, string> headers, [CanBeNull] string body)
        {
            if (headers == null) headers = new Dictionary<string, string>();
            if (body == null) body = "";

            if (body.Length > 0)
            {
                headers.Add("content-length", body.Length.ToString());
                headers.Add("content-type", "text/plain");
            }

            var hdrs = "";
            foreach (var header in headers) hdrs += header.Key + ":" + header.Value + "\n";

            var frame = command.GetValue() + "\n" + hdrs + "\n" + body + "\0";
            var buffer = Encoding.UTF8.GetBytes(frame);

            Console.WriteLine(frame);
            await _client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task StartReceivingAsync()
        {
            var buffer = new byte[8192];
            while (_client.State == WebSocketState.Open)
            {
                var segment = new ArraySegment<byte>(buffer);
                WebSocketReceiveResult result;

                try
                {
                    result = await _client.ReceiveAsync(segment, _cancellationToken);
                }
                catch (WebSocketException)
                {
                    break; // Handle as needed
                }

                if (result.MessageType == WebSocketMessageType.Close) break;


                var data = Encoding.UTF8.GetString(buffer, 0, result.Count);
                lock (_bufferLock)
                {
                    _buffer.Append(data);

                    int nullCharIndex;
                    while ((nullCharIndex = _buffer.ToString().IndexOf('\0')) != -1)
                    {
                        // Extract the portion before the null character
                        var message = _buffer.ToString(0, nullCharIndex);
                        _buffer.Remove(0, nullCharIndex + 1); // Remove the processed data (including \0)

                        // Process the extracted message
                        var frame = ProcessFrame(message);
                        _ = UseFrame(frame);
                    }
                }
            }
        }

        private StompFrame ProcessFrame(string rawMessage)
        {
            if (string.IsNullOrWhiteSpace(rawMessage)) throw new ArgumentException("Message cannot be null or empty", nameof(rawMessage));

            // Split the message into lines and parse:
            var lines = rawMessage.Split(new[] { '\n' }, StringSplitOptions.None);
            var command = lines[0].Trim(); // First line is the command
            var headers = new Dictionary<string, string>();
            var body = new StringBuilder();

            // Parse headers (lines until a blank line)
            var i = 1;
            for (; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) // Blank line indicates the start of the body
                {
                    i++; // Move to the next line (start of the body)
                    break;
                }

                var headerParts = line.Split(new[] { ':' }, 2); // Key and Value
                if (headerParts.Length == 2) headers[headerParts[0].Trim()] = headerParts[1].Trim();
            }

            // Read the body (remaining lines)
            for (; i < lines.Length; i++) body.AppendLine(lines[i]);

            // Decide the StompMessage type based on the command
            switch (command)
            {
                case "CONNECTED":
                {
                    return new StompConnected
                    {
                        Command = command,
                        Headers = headers,
                        Version = headers.TryGetValue("version", out var version) ? version : null,
                        Session = headers.TryGetValue("session", out var session) ? session : null,
                        Server = headers.TryGetValue("server", out var server) ? server : null,
                        HeartBeat = headers.TryGetValue("heart-beat", out var heartBeat) ? heartBeat : null
                    };
                }
                case "MESSAGE":
                {
                    return new StompMessage
                    {
                        Command = command,
                        Headers = headers,
                        Body = body.ToString(),

                        Subscription = _subscriptions[headers["subscription"]],
                        MessageId = headers.TryGetValue("message-id", out var messageId) ? messageId : null,
                        Destination = headers.TryGetValue("destination", out var destination) ? destination : null,
                        ContentType = headers.TryGetValue("content-type", out var contentType) ? contentType : null
                    };
                }
                case "RECEIPT":
                {
                    return new StompReceipt
                    {
                        Command = command,
                        Headers = headers,
                        ReceiptId = headers.TryGetValue("receipt-id", out var receiptId) ? receiptId : null
                    };
                }
                case "ERROR":
                {
                    return new StompError
                    {
                        Command = command,
                        Headers = headers,
                        Body = body.ToString(),
                        ReceiptId = headers.TryGetValue("receipt-id", out var receiptId) ? receiptId : null,
                        ContentType = headers.TryGetValue("content-type", out var contentType) ? contentType : null,
                        Message = headers.TryGetValue("message", out var message) ? message : null
                    };
                }
                default:
                    return new StompFrame
                    {
                        Command = command,
                        Headers = headers,
                        Body = body.ToString()
                    };
            }
        }

        private async Task UseFrame(StompFrame frame)
        {
            switch (frame)
            {
                case StompConnected connected:
                {
                    _connectCompletionSource?.TrySetResult(true);
                }
                    break;

                case StompMessage message:
                {
                    await message.Subscription.OnMessage(message);
                }
                    break;

                case StompReceipt receipt:
                {
                }
                    break;

                case StompError error:
                {
                }
                    break;
            }
        }
    }
}