using Link.Nodes;

namespace Link.Examples._015_Automatic_Vs_Manual;

/// <summary>
/// This example explains what automatic and manual modes are, how those modes are used
/// and how to select the mode.
/// <br/><br/>
/// Automatic mode is a mode in which all of the received packets are automatically and
/// immediately processed as they are received. The thread on which this processing is
/// performed is not controlled by the user. Since this might be a problem in certain
/// cases, there also exists a manual mode.
/// <br/><br/>
/// Manual mode allows you to process received packets in a controlled manner. Manual
/// mode works by enqueuing all of the received packets, so they can be processed later
/// on the required thread. This way, you are in control on which thread processing of
/// packets should be performed. Execution is done by calling <see cref="Node.Tick"/>.
/// <br/><br/>
/// All <see cref="Node"/> instances, by default, operate using automatic mode. Mode
/// is changed by setting the <see cref="Node.IsAutomatic"/> property.
/// </summary>
public static class AutomaticVsManual
{
    private const string IpAddress = "127.0.0.1";
    private const ushort Port = 7777;
    private const int DelayBetweenTicks = 3000;
    private const string ExitKeyword = "exit";

    private static bool _exitWasRequested;
    
    public static void Main()
    {
        // Create server that operates in manual mode.
        using var server = new Server { IsAutomatic = false };
        server.AddHandler(args => ProcessClientMessage(args.Packet));
        server.Start(Port);

        // Create client that operates in automatic mode (which is the default).
        using var client = new Client { IsAutomatic = true };
        client.Connect(IpAddress, Port);
        
        // Processing of packets is done on a thread under out control.
        StartManualProcessingThread(server);

        // Send some text messages from client to server.
        SendMessagesFromClient(client);
    }

    private static void StartManualProcessingThread(Server server) => new Thread(() =>
    {
        while (!_exitWasRequested)
        {
            // Print for each tick call.
            Console.WriteLine($"[{DateTime.Now}] Server is processing received packets...");
            
            // All of the received packets are processed with this call.
            server.Tick();
            
            // Wait for some time for new packets to arrive.
            Thread.Sleep(millisecondsTimeout: DelayBetweenTicks);
        }
    }) {IsBackground = true}.Start();

    private static void SendMessagesFromClient(Client client)
    {
        Console.WriteLine("INSTRUCTIONS");
        Console.WriteLine("Enter message and press enter to send.");
        Console.WriteLine("Try to spam messages and see what happens.");
        Console.WriteLine($"Enter '{ExitKeyword}' to end the process.");
        Console.WriteLine();

        while (true)
        {
            var message = Console.ReadLine();

            if (message!.ToLower() == ExitKeyword)
            {
                _exitWasRequested = true;
                break;
            }
            
            client.Send(Packet.Get(Delivery.Reliable).Write(message));
        }
    }

    private static void ProcessClientMessage(ReadOnlyPacket packet)
    {
        var clientMessage = packet.ReadString();
        Console.WriteLine($"Server received: '{clientMessage}'");
    }
}
