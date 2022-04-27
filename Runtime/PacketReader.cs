namespace Link
{
    public ref struct PacketReader
    {
        private readonly Packet _packet;
        private int _readPosition;

        public PacketReader(Packet packet, int readPosition = 0)
        {
            _packet = packet;
            _readPosition = readPosition;
        }
        
        public string ReadString()
        {
            var stringByteCount = Read<int>();
            var stringValue = Packet.Encoding.GetString(_packet.Buffer, _readPosition, stringByteCount);
            _readPosition += stringByteCount;
            return stringValue;
        }

        public unsafe T Read<T>() where T : unmanaged
        {
            var value = _packet.Buffer.Read<T>(_readPosition);
            _readPosition += sizeof(T);
            return value;
        }

        public unsafe T[] ReadArray<T>() where T : unmanaged
        {
            var array = _packet.Buffer.ReadArray<T>(_readPosition);
            _readPosition += sizeof(int) + array.Length * sizeof(T);
            return array;
        }
        
        public unsafe T[] ReadSlice<T>(int length) where T : unmanaged
        {
            var slice = _packet.Buffer.ReadSlice<T>(length, _readPosition);
            _readPosition += length * sizeof(T);
            return slice;
        }
    }
}
