namespace Link.Tests.Integration;

public static class Config
{
    /// <summary>
    /// IP address on which client will connect to.
    /// </summary>
    public const string IpAddress = "127.0.0.1";
    
    /// <summary>
    /// Port on which server will listen on.
    /// </summary>
    public const ushort Port = 7777;
    
    /// <summary>
    /// Method will pause for this long (in ms) to let
    /// network operation complete due to network delay.
    /// </summary>
    public const int NetworkDelay = 10;
}
