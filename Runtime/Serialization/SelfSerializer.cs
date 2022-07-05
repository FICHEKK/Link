namespace Link.Serialization
{
    public class SelfSerializer<T> : ISerializer<T> where T : ISerializer<T>, new()
    {
        public void Write(Packet packet, T value) =>
            value.Write(packet, value);

        public T Read(ReadOnlyPacket packet) =>
            new T().Read(packet);
    }
}
