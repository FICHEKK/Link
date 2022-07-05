using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Link.Serialization
{
    /// <summary>
    /// Stores and internally handles all of the serialization logic and offers
    /// a simple way register and also access already registered serializers.
    /// </summary>
    public static class Serializers
    {
        /// <summary>
        /// Represents a method that attempts to convert given type to a type of serializer that can
        /// be used to serialize given type. Method must return <c>null</c> if given type cannot be
        /// serialized by this mapper.
        /// </summary>
        public delegate Type? Mapper(Type type);

        /// <summary>
        /// A collection of mappers used to create generic serializer type instances.
        /// </summary>
        private static readonly List<Mapper> Mappers = new()
        {
            type => !type.IsEnum ? null
                : typeof(EnumSerializer<,>).MakeGenericType(type, Enum.GetUnderlyingType(type)),
            
            type => !type.IsArray || type.GetArrayRank() != 1 ? null
                : typeof(ArraySerializer<>).MakeGenericType(type.GetElementType()),

            type => !type.IsGenericType || type.GetGenericTypeDefinition() != typeof(ArraySegment<>) ? null
                : typeof(ArraySegmentSerializer<>).MakeGenericType(type.GetGenericArguments()),
        };
        
        /// <summary>
        /// Extends serialization system by adding a mapper that can be used to generate new generic serializers.
        /// </summary>
        public static void AddMapper(Mapper mapper)
        {
            if (mapper is null)
                throw new InvalidOperationException("Cannot add a null mapper.");
            
            lock (Lock) Mappers.Add(mapper);
        }

        /// <summary>
        /// Registers default serialization strategies.
        /// </summary>
        static Serializers()
        {
            Add(new StringSerializer());
            Add(new ByteArraySerializer());
            
            Add(new ByteSerializer());
            Add(new SByteSerializer());
            Add(new BoolSerializer());
            
            Add(new ShortSerializer());
            Add(new UShortSerializer());
            Add(new CharSerializer());
            
            Add(new IntSerializer());
            Add(new UIntSerializer());
            Add(new FloatSerializer());
            
            Add(new LongSerializer());
            Add(new ULongSerializer());
            Add(new DoubleSerializer());
        }

        /// <summary>
        /// Ensures that only a single thread can be registering a serializer.
        /// </summary>
        private static readonly object Lock = new();

        /// <summary>
        /// Adds a serializer for specific data type. If type already has a serializer, it will be overwritten.
        /// </summary>
        public static void Add<T>(ISerializer<T> serializer)
        {
            if (Cache<T>.Serializer is not null)
                Log.Info($"Overwriting serializer for type '{typeof(T)}'.");
            
            Cache<T>.Serializer = serializer ?? throw new NullReferenceException("Serializer cannot be set to null.");
        }
        
        /// <summary>
        /// Returns <see cref="ISerializer{T}"/> for specified type.
        /// </summary>
        public static ISerializer<T> Get<T>() => Cache<T>.Serializer ?? RegisterOrThrow<T>();

        /// <summary>
        /// Registers serialization strategies for specified type, or throws if type is not supported.
        /// </summary>
        private static ISerializer<T> RegisterOrThrow<T>()
        {
            lock (Lock)
            {
                if (Cache<T>.Serializer is not null)
                    return Cache<T>.Serializer;
                    
                foreach (var mapper in Mappers)
                {
                    var serializerType = mapper(typeof(T));
                    if (serializerType is null) continue;

                    return Cache<T>.Serializer = (ISerializer<T>) Activator.CreateInstance(serializerType);
                }

                throw new InvalidOperationException($"Serializer could not be registered for type '{typeof(T)}'.");
            }
        }

        /// <summary>
        /// Type safe storage of already registered serializers and deserializers.
        /// It is generic in order so each unique type has its own serialization strategies.
        /// </summary>
        private static class Cache<T>
        {
            public static ISerializer<T>? Serializer;
        }
        
        private class ByteArraySerializer : ISerializer<byte[]>
        {
            public void Write(Packet packet, byte[] array)
            {
                packet.Buffer.WriteVarInt(array.Length);
                packet.Buffer.WriteBytes(array);
            }

            public byte[] Read(ReadOnlyPacket packet)
            {
                var length = packet.Buffer.ReadVarInt(out _);
                return packet.Buffer.ReadBytes(length);
            }
        }
        
        private class ByteSerializer : ISerializer<byte>
        {
            public void Write(Packet packet, byte value) => packet.Buffer.Write(value);
            public byte Read(ReadOnlyPacket packet) => packet.Buffer.ReadByte();
        }
        
        private class SByteSerializer : ISerializer<sbyte>
        {
            public void Write(Packet packet, sbyte value) => packet.Buffer.Write((byte) value);
            public sbyte Read(ReadOnlyPacket packet) => (sbyte) packet.Buffer.ReadByte();
        }

        private class BoolSerializer : ISerializer<bool>
        {
            public void Write(Packet packet, bool value) => packet.Buffer.Write((byte) (value ? 1 : 0));
            public bool Read(ReadOnlyPacket packet) => packet.Buffer.ReadByte() != 0;
        }

        private class ShortSerializer : ISerializer<short>
        {
            public void Write(Packet packet, short value) => packet.Buffer.Write(value);
            public short Read(ReadOnlyPacket packet) => packet.Buffer.ReadShort();
        }
        
        private class UShortSerializer : ISerializer<ushort>
        {
            public void Write(Packet packet, ushort value) => packet.Buffer.Write((short) value);
            public ushort Read(ReadOnlyPacket packet) => (ushort) packet.Buffer.ReadShort();
        }
        
        private class CharSerializer : ISerializer<char>
        {
            public void Write(Packet packet, char value) => packet.Buffer.Write((short) value);
            public char Read(ReadOnlyPacket packet) => (char) packet.Buffer.ReadShort();
        }

        private class IntSerializer : ISerializer<int>
        {
            public void Write(Packet packet, int value) => packet.Buffer.Write(value);
            public int Read(ReadOnlyPacket packet) => packet.Buffer.ReadInt();
        }
        
        private class UIntSerializer : ISerializer<uint>
        {
            public void Write(Packet packet, uint value) => packet.Buffer.Write((int) value);
            public uint Read(ReadOnlyPacket packet) => (uint) packet.Buffer.ReadInt();
        }
        
        private class FloatSerializer : ISerializer<float>
        {
            public void Write(Packet packet, float value) => packet.Buffer.Write(new Union { @float = value }.@int);
            public float Read(ReadOnlyPacket packet) => new Union { @int = packet.Buffer.ReadInt() }.@float;
        }

        private class LongSerializer : ISerializer<long>
        {
            public void Write(Packet packet, long value) => packet.Buffer.Write(value);
            public long Read(ReadOnlyPacket packet) => packet.Buffer.ReadLong();
        }
        
        private class ULongSerializer : ISerializer<ulong>
        {
            public void Write(Packet packet, ulong value) => packet.Buffer.Write((long) value);
            public ulong Read(ReadOnlyPacket packet) => (ulong) packet.Buffer.ReadLong();
        }
        
        private class DoubleSerializer : ISerializer<double>
        {
            public void Write(Packet packet, double value) => packet.Buffer.Write(new Union { @double = value }.@long);
            public double Read(ReadOnlyPacket packet) => new Union { @long = packet.Buffer.ReadLong() }.@double;
        }
        
        [StructLayout(LayoutKind.Explicit)]
        private struct Union
        {
            [FieldOffset(0)]
            public int @int;
            
            [FieldOffset(0)]
            public float @float;
            
            [FieldOffset(0)]
            public long @long;
            
            [FieldOffset(0)]
            public double @double;
        }
    }
}
