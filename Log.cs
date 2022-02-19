using UnityEngine;

namespace Networking.Transport
{
    /// <summary>
    /// A very simple and customizable logging component.
    /// </summary>
    public static class Log
    {
        /// <summary>
        /// Represents a method that can write given message to the log.
        /// </summary>
        public delegate void Logger(string message);

        /// <summary>
        /// Logs informative messages that are useful during development.
        /// </summary>
        public static Logger Info = Debug.Log;

        /// <summary>
        /// Logs unexpected events that might indicate a problem in application logic.
        /// </summary>
        public static Logger Warning = Debug.LogWarning;

        /// <summary>
        /// Logs serious problems that should never occur during application runtime.
        /// </summary>
        public static Logger Error = Debug.LogError;
    }
}
