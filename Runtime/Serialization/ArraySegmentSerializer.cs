using System;

namespace Link.Serialization
{
    public class ArraySegmentSerializer<T> : ISerializer<ArraySegment<T>>
    {
        private static readonly ISerializer<T> ElementSerializer = Serializers.Get<T>();
        
        public void Write(Packet packet, ArraySegment<T> segment)
        {
            var array = segment.Array!;
            var offset = segment.Offset;
            var count = segment.Count;
            
            packet.Buffer.WriteVarInt(count);

            for (var i = 0; i < count; i++)
                ElementSerializer.Write(packet, array[offset + i]);
        }

        public ArraySegment<T> Read(ReadOnlyPacket packet)
        {
            var length = packet.Buffer.ReadVarInt(out _);

            if (length == 0)
                return new ArraySegment<T>(Array.Empty<T>());
                
            if (length < 0)
                throw new InvalidOperationException($"Cannot read array of length {length} as it is negative.");
                
            var array = new T[length];

            for (var i = 0; i < length; i++)
                array[i] = ElementSerializer.Read(packet);

            return new ArraySegment<T>(array);
        }
    }
}
