using System;

namespace Link.Serialization
{
    public class ArraySerializer<T> : ISerializer<T[]>
    {
        private static readonly ISerializer<T> ElementSerializer = Serializers.Get<T>();
        
        public void Write(Packet packet, T[] array)
        {
            packet.Buffer.WriteVarInt(array.Length);

            foreach (var element in array)
                ElementSerializer.Write(packet, element);
        }

        public T[] Read(ReadOnlyPacket packet)
        {
            var length = packet.Buffer.ReadVarInt(out _);

            if (length == 0)
                return Array.Empty<T>();
                
            if (length < 0)
                throw new InvalidOperationException($"Cannot read array of length {length} as it is negative.");
                
            var array = new T[length];

            for (var i = 0; i < length; i++)
                array[i] = ElementSerializer.Read(packet);

            return array;
        } 
    }
}
