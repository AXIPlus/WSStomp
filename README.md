# WSStomp

STOMP over WebSockets in C#

## Intention

WSStomp was originally developed as a library for using STOMP over WebSockets in C#.

The main intent was to be used with the SpringBoot SimpleBroker, but STOMP 1.2 functionality was further enhanced.

## Installation

Install WSStomp via NuGet.

## Usage

```aiignore
static async Task Main(string[] args)
{
    var uri = new Uri("wss://your_hostname/ws_path");
    Console.WriteLine("Connecting to " + uri);

    var stomp = new WsStomp();
    stomp.SetStompHeader("Authorization", "your token");    // optional
    await stomp.ConnectAsync(uri, CancellationToken.None);
   
    Console.WriteLine("Connected");
    
    await stomp.SubscribeAsync("/topic/auth/token", TestCallback);
    var serviceSubscription = await stomp.SubscribeAsync("/topic/test-service", TestCallback2);
    Console.WriteLine("Subscribed");
    
    Console.ReadLine();

    Console.WriteLine("Getting token");
    await stomp.SendAsync("/auth", "");
    
    Console.ReadLine();
    await serviceSubscription.Unsubscribe();

    Console.ReadLine();
}

static Task TestCallback(StompMessage message)
{
    Console.WriteLine($"Message received on subscription {message.Subscription.GetId()} / {message.Subscription.GetDestination()}:");
    Console.WriteLine(message.Body);
    return Task.CompletedTask;
}

static Task TestCallback2(StompMessage message)
{
    Console.WriteLine($"Just another callback function on subscription {message.Subscription.GetId()} / {message.Subscription.GetDestination()}:");
    Console.WriteLine(message.Body);
    return Task.CompletedTask;
}
```