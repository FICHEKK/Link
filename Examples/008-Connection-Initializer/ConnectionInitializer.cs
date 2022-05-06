using Link.Channels;
using Link.Nodes;

namespace Link.Examples._008_Connection_Initializer;

/// <summary>
/// This example explains and demonstrates the concept of connection initializers.
/// The default connection initialization performed by the library will be enough
/// for most use cases. However, if you need to set extra options, such as custom
/// channels, knowing about connection initializers will make that process simple.
/// </summary>
public static class ConnectionInitializer
{
    /// <summary>
    /// This example does not run any networking logic, rather it simply
    /// demonstrates how to set-up connection initializers.
    /// </summary>
    public static void Main()
    {
        using var server = new Server { ConnectionInitializer = InitializeConnection };
        using var client = new Client { ConnectionInitializer = InitializeConnection };
    }

    /// <summary>
    /// Each time a new connection is created, this method will perform the initialization.
    /// The default connection initialization is still performed by the library, this just
    /// adds extra customization and overrides default settings (if setting was changed).
    /// </summary>
    private static void InitializeConnection(Connection connection)
    {
        // Here we can set connection options:
        connection.PeriodDuration = 3000;
        connection.TimeoutDuration = 30_000;
        connection.SmoothingFactor = 0.25;
        connection.DeviationFactor = 0.35;

        // Or we can add custom channels:
        connection[0] = new UnreliableChannel(connection);
        connection[1] = new SequencedChannel(connection);
        connection[2] = new ReliableChannel(connection);
    }
}
