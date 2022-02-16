using Networking.Serialization;

namespace Networking.Transport
{
    public class PacketReader
    {
        public int ReadPosition { get; set; }
        private readonly Packet _packet;

        public PacketReader(Packet packet) =>
            _packet = packet;

        public string ReadString()
        {
            var stringByteCount = Read<int>();
            var stringValue = Packet.Encoding.GetString(_packet.Buffer, ReadPosition, stringByteCount);
            ReadPosition += stringByteCount;
            return stringValue;
        }

        public T ReadSerializable<T>() where T : INetworkSerializable, new()
        {
            var serializable = new T();
            serializable.Deserialize(this);
            return serializable;
        }

        public T[] ReadSerializableArray<T>() where T : INetworkSerializable, new()
        {
            var length = Read<int>();
            var serializableArray = new T[length];

            for (var i = 0; i < length; i++)
            {
                serializableArray[i] = ReadSerializable<T>();
            }

            return serializableArray;
        }

        public unsafe T Read<T>() where T : unmanaged
        {
            var value = _packet.Buffer.Read<T>(ReadPosition);
            ReadPosition += sizeof(T);
            return value;
        }

        public unsafe T[] ReadArray<T>() where T : unmanaged
        {
            var array = _packet.Buffer.ReadArray<T>(ReadPosition);
            ReadPosition += sizeof(int) + array.Length * sizeof(T);
            return array;
        }
    }
}
