using System.Collections.Generic;

namespace WSStomp
{
    public class StompFrame
    {
        public string Command { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public string Body { get; set; }
    }

    public class StompConnected : StompFrame
    {
        public string Version { get; set; }
        public string Session { get; set; }
        public string Server { get; set; }
        public string HeartBeat { get; set; }
    }

    public class StompMessage : StompFrame
    {
        public StompSubscription Subscription { get; set; }
        public string MessageId { get; set; }
        public string Destination { get; set; }
        public string ContentType { get; set; }
    }

    public class StompReceipt : StompFrame
    {
        public string ReceiptId { get; set; }
    }

    public class StompError : StompFrame
    {
        public string ReceiptId { get; set; }
        public string ContentType { get; set; }
        public string Message { get; set; }
    }
}