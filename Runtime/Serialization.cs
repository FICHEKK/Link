using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Link
{
    /// <summary>
    /// Stores and internally handles all of the serialization logic and offers
    /// a simple way register and also access already registered serializers.
    /// </summary>
    public static class Serialization
    {
        /// <summary>
        /// Represents a method that can write a value of specific type to the packet.
        /// </summary>
        public delegate void Writer<in T>(Packet packet, T value);

        /// <summary>
        /// Represents a method that can read a value of specific type from the packet.
        /// </summary>
        public delegate T Reader<out T>(ReadOnlyPacket packet);
        
        /// <summary>
        /// Reference to the generic method used for registering array serializers and deserializers.
        /// </summary>
        private static readonly MethodInfo ArrayFactory = typeof(Serialization).GetMethod(nameof(RegisterArray), BindingFlags.NonPublic | BindingFlags.Static)!;
        
        /// <summary>
        /// Reference to the generic method used for registering array serializers and deserializers.
        /// </summary>
        private static readonly MethodInfo ArraySegmentFactory = typeof(Serialization).GetMethod(nameof(RegisterArraySegment), BindingFlags.NonPublic | BindingFlags.Static)!;

        /// <summary>
        /// Reference to the generic method used for registering enum serializers and deserializers.
        /// </summary>
        private static readonly MethodInfo EnumFactory = typeof(Serialization).GetMethod(nameof(RegisterEnum), BindingFlags.NonPublic | BindingFlags.Static)!;

        /// <summary>
        /// Registers default serialization strategies.
        /// </summary>
        static Serialization()
        {
            Register(StringWriter, StringReader);
            Register(ByteArrayWriter, ByteArrayReader);
            
            Register((packet, value) => packet.Buffer.Write(value), packet => packet.Buffer.ReadByte());
            Register((packet, value) => packet.Buffer.Write((byte) value), packet => (sbyte) packet.Buffer.ReadByte());
            Register((packet, value) => packet.Buffer.Write((byte) (value ? 1 : 0)), packet => packet.Buffer.ReadByte() != 0);
            
            Register((packet, value) => packet.Buffer.Write(value), packet => packet.Buffer.ReadShort());
            Register((packet, value) => packet.Buffer.Write((short) value), packet => (ushort) packet.Buffer.ReadShort());
            Register((packet, value) => packet.Buffer.Write((short) value), packet => (char) packet.Buffer.ReadShort());
            
            Register((packet, value) => packet.Buffer.Write(value), packet => packet.Buffer.ReadInt());
            Register((packet, value) => packet.Buffer.Write((int) value), packet => (uint) packet.Buffer.ReadInt());
            Register((packet, value) => packet.Buffer.Write(new Union { @float = value }.@int), packet => new Union { @int = packet.Buffer.ReadInt() }.@float);
            
            Register((packet, value) => packet.Buffer.Write(value), packet => packet.Buffer.ReadLong());
            Register((packet, value) => packet.Buffer.Write((long) value), packet => (ulong) packet.Buffer.ReadLong());
            Register((packet, value) => packet.Buffer.Write(new Union { @double = value }.@long), packet => new Union { @long = packet.Buffer.ReadLong() }.@double);
        }

        /// <summary>
        /// <see cref="Writer{T}"/> responsible for writing <see cref="string"/> values to a packet.
        /// </summary>
        private static readonly Writer<string> StringWriter = (packet, @string) =>
        {
            if (@string is null)
                throw new InvalidOperationException("Cannot write null string to a packet.");

            if (@string.Length == 0)
            {
                packet.Write((byte) 0);
            }
            else
            {
                packet.Write(Packet.Encoding.GetBytes(@string));
            }
        };

        /// <summary>
        /// <see cref="Reader{T}"/> responsible for reading <see cref="string"/> values from a packet.
        /// </summary>
        private static readonly Reader<string> StringReader = packet =>
        {
            var stringByteCount = packet.Buffer.ReadVarInt(out _);

            if (stringByteCount == 0)
                return string.Empty;

            if (packet.UnreadBytes < stringByteCount)
                throw new InvalidOperationException("Could not read string (out-of-bounds bytes).");

            var @string = Packet.Encoding.GetString(packet.Buffer.Bytes, packet.Buffer.ReadPosition, stringByteCount);
            packet.Buffer.ReadPosition += stringByteCount;
            return @string;
        };
        
        /// <summary>
        /// <see cref="Writer{T}"/> responsible for writing <see cref="byte"/> arrays to a packet.
        /// </summary>
        private static readonly Writer<byte[]> ByteArrayWriter = (packet, array) =>
        {
            packet.Buffer.WriteVarInt(array.Length);
            packet.Buffer.WriteBytes(array);
        };

        /// <summary>
        /// <see cref="Reader{T}"/> responsible for reading <see cref="byte"/> arrays from a packet.
        /// </summary>
        private static readonly Reader<byte[]> ByteArrayReader = packet =>
        {
            var length = packet.Buffer.ReadVarInt(out _);
            return packet.Buffer.ReadBytes(length);
        };

        /// <summary>
        /// Registers write and read strategies for a specific data type.
        /// </summary>
        /// <typeparam name="T">Type of data to write/read.</typeparam>
        public static void Register<T>(Writer<T> writer, Reader<T> reader)
        {
            Cache<T>.Writer = writer ?? throw new NullReferenceException("Write strategy cannot be set to null.");
            Cache<T>.Reader = reader ?? throw new NullReferenceException("Read strategy cannot be set to null.");
        }
        
        /// <summary>
        /// Returns a <see cref="Writer{T}"/> that is responsible for writing value of specified type.
        /// </summary>
        public static Writer<T> GetWriter<T>() => Cache<T>.Writer ?? RegisterOrThrow<T>().writer;

        /// <summary>
        /// Returns a <see cref="Reader{T}"/> that is responsible for reading value of specified type.
        /// </summary>
        public static Reader<T> GetReader<T>() => Cache<T>.Reader ?? RegisterOrThrow<T>().reader;

        /// <summary>
        /// Registers serialization strategies for specified type, or throws if type is not supported.
        /// </summary>
        private static (Writer<T> writer, Reader<T> reader) RegisterOrThrow<T>()
        {
            var type = typeof(T);

            if (type.IsArray && type.GetArrayRank() == 1)
            {
                ArrayFactory.MakeGenericMethod(type.GetElementType()).Invoke(null, Array.Empty<object>());
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ArraySegment<>))
            {
                ArraySegmentFactory.MakeGenericMethod(type.GetGenericArguments()[0]).Invoke(null, Array.Empty<object>());
            }
            else if (type.IsEnum)
            {
                EnumFactory.MakeGenericMethod(type, Enum.GetUnderlyingType(type)).Invoke(null, Array.Empty<object>());
            }

            if (Cache<T>.Writer is null || Cache<T>.Reader is null)
                throw new InvalidOperationException($"Serialization strategy could not be registered for type '{type}'.");
            
            return (Cache<T>.Writer, Cache<T>.Reader);
        }

        private static void RegisterArray<TElement>()
        {
            Cache<TElement[]>.Writer = (packet, array) => WriteArraySegment(packet, new ArraySegment<TElement>(array));
            Cache<TElement[]>.Reader = packet => ReadArraySegment<TElement>(packet).Array;
        }
        
        private static void RegisterArraySegment<TElement>()
        {
            Cache<ArraySegment<TElement>>.Writer = WriteArraySegment;
            Cache<ArraySegment<TElement>>.Reader = ReadArraySegment<TElement>;
        }

        private static void WriteArraySegment<T>(Packet packet, ArraySegment<T> segment)
        {
            var array = segment.Array!;
            var offset = segment.Offset;
            var count = segment.Count;
            
            packet.Buffer.WriteVarInt(count);
            var writer = GetWriter<T>();

            for (var i = 0; i < count; i++)
                writer(packet, array[offset + i]);
        }

        private static ArraySegment<T> ReadArraySegment<T>(ReadOnlyPacket packet)
        {
            var length = packet.Buffer.ReadVarInt(out _);

            if (length == 0)
                return new ArraySegment<T>(Array.Empty<T>());
                
            if (length < 0)
                throw new InvalidOperationException($"Cannot read array of length {length} as it is negative.");
                
            var array = new T[length];
            var reader = GetReader<T>();
            
            for (var i = 0; i < length; i++)
                array[i] = reader(packet);

            return new ArraySegment<T>(array);
        }
        
        private static void RegisterEnum<TEnum, TNumeric>()
        {
            Cache<TEnum>.Writer = (packet, @enum) => packet.Write(Cast<TEnum, TNumeric>.Execute(@enum));
            Cache<TEnum>.Reader = packet => Cast<TNumeric, TEnum>.Execute(packet.Read<TNumeric>());
        }

        /// <summary>
        /// Helper class that performs casting without boxing.
        /// </summary>
        private static class Cast<TSource, TResult>
        {
            /// <summary>
            /// Compiled method that performs casting from <see cref="TSource"/>
            /// value to <see cref="TResult"/> value.
            /// </summary>
            private static readonly Func<TSource, TResult> Caster = Create();

            /// <summary>
            /// Executes the cast, converting <typeparamref name="TSource"/> to
            /// <typeparamref name="TResult"/>.
            /// </summary>
            public static TResult Execute(TSource source) => Caster(source);

            private static Func<TSource, TResult> Create()
            {
                var parameter = Expression.Parameter(typeof(TSource));
                var converter = Expression.Convert(parameter, typeof(TResult));
                return Expression.Lambda<Func<TSource, TResult>>(converter, parameter).Compile();
            }
        }
        
        /// <summary>
        /// Type safe storage of already registered serializers and deserializers.
        /// It is generic in order so each unique type has its own serialization strategies.
        /// </summary>
        private static class Cache<T>
        {
            public static Writer<T>? Writer;
            public static Reader<T>? Reader;
        }
        
        /// <summary>
        /// Helper structure used for reinterpreting between integer and floating point values.
        /// </summary>
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
