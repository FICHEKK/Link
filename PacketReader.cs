using System.Text;

namespace Networking.Transport
{
    public class PacketReader
    {
        private static readonly Encoding Encoding = Encoding.UTF8;

        public int Position { get; set; }
        private readonly Packet _packet;

        public PacketReader(Packet packet) =>
            _packet = packet;

        public string ReadString()
        {
            var stringByteCount = Read<int>();
            var stringValue = Encoding.GetString(_packet.Buffer, Position, stringByteCount);
            Position += stringByteCount;
            return stringValue;
        }

        public unsafe T Read<T>() where T : unmanaged
        {
            var value = _packet.Buffer.Read<T>(Position);
            Position += sizeof(T);
            return value;
        }

        public unsafe T[] ReadArray<T>() where T : unmanaged
        {
            var array = _packet.Buffer.ReadArray<T>(Position);
            Position += sizeof(int) + array.Length * sizeof(T);
            return array;
        }
    }
}
