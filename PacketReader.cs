namespace Networking.Transport
{
    public class PacketReader
    {
        public int ReadPosition { get; set; }
        private readonly Packet _packet;

        public PacketReader(Packet packet) => _packet = packet;

        public unsafe T Read<T>() where T : unmanaged
        {
            var value = _packet.Buffer.Read<T>(ReadPosition);
            ReadPosition += sizeof(T);
            return value;
        }

        public string ReadString()
        {
            var stringLength = Read<int>();
            var stringValue = Packet.Encoding.GetString(_packet.Buffer, ReadPosition, stringLength);
            ReadPosition += stringLength;
            return stringValue;
        }
    }
}
