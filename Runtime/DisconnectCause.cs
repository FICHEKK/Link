namespace Link
{
    /// <summary>
    /// Cause of the connection termination.
    /// </summary>
    public enum DisconnectCause
    {
        /// <summary>
        /// Connection has been deliberately closed by the client.
        /// </summary>
        ClientLogic,
        
        /// <summary>
        /// Connection has been deliberately closed by the server.
        /// </summary>
        ServerLogic,
        
        /// <summary>
        /// Connection has timed-out. 
        /// </summary>
        Timeout,
    }
}
