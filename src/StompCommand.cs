namespace WSStomp
{
    public class StompCommand
    {
        private readonly string _value;

        private StompCommand(string value)
        {
            _value = value;
        }

        public string GetValue()
        {
            return _value;
        }

        public static StompCommand Connect()
        {
            return new StompCommand("CONNECT");
        }

        public static StompCommand Disconnect()
        {
            return new StompCommand("DISCONNECT");
        }

        public static StompCommand Subscribe()
        {
            return new StompCommand("SUBSCRIBE");
        }

        public static StompCommand Unsubscribe()
        {
            return new StompCommand("UNSUBSCRIBE");
        }

        public static StompCommand Send()
        {
            return new StompCommand("SEND");
        }
    }
}