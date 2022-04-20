using System;

namespace Link
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

        private static Logger _info;
        private static Logger _warning;
        private static Logger _error;

        /// <summary>
        /// Logs informative messages that are useful during development.
        /// </summary>
        public static Logger Info
        {
            get => _info ?? throw new NullReferenceException($"{nameof(Info)} logger hasn't been initialized.");
            set => _info = value ?? throw new NullReferenceException($"{nameof(Info)} logger cannot be set to null.");
        }

        /// <summary>
        /// Logs unexpected events that might indicate a problem in application logic.
        /// </summary>
        public static Logger Warning
        {
            get => _warning ?? throw new NullReferenceException($"{nameof(Warning)} logger hasn't been initialized.");
            set => _warning = value ?? throw new NullReferenceException($"{nameof(Warning)} logger cannot be set to null.");
        }

        /// <summary>
        /// Logs serious problems that should never occur during application runtime.
        /// </summary>
        public static Logger Error
        {
            get => _error ?? throw new NullReferenceException($"{nameof(Error)} logger hasn't been initialized.");
            set => _error = value ?? throw new NullReferenceException($"{nameof(Error)} logger cannot be set to null.");
        }
    }
}
