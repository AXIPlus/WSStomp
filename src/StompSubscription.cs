using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WSStomp
{
    public class StompSubscription
    {
        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>();
        private readonly WsStomp _stomp;
        private Func<StompMessage, Task> _messageHandler;

        internal StompSubscription(WsStomp stomp, string id, string destination)
        {
            _stomp = stomp;
            _headers.Add("id", id);
            _headers.Add("destination", destination);
        }

        internal Task Subscribe(Func<StompMessage, Task> messageHandler)
        {
            _messageHandler = messageHandler;
            return _stomp.SendStompCommand(StompCommand.Subscribe(), _headers, null);
        }

        internal async Task OnMessage(StompMessage message)
        {
            await _messageHandler(message);
        }

        public Task Unsubscribe()
        {
            var headers = new Dictionary<string, string> { { "id", _headers["id"] }, { "destination", _headers["destination"] } };
            return _stomp.SendStompCommand(StompCommand.Unsubscribe(), headers, null);
        }

        public string GetId()
        {
            return _headers["id"];
        }

        public string GetDestination()
        {
            return _headers["destination"];
        }
    }
}