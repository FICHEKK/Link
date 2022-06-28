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
        /// Registers default serialization strategies.
        /// </summary>
        static Serialization()
        {
            Register((packet, value) => packet.Write(value), packet => packet.ReadString());
            
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
        /// Reference to the generic method used for registering enum serializers and deserializers.
        /// </summary>
        private static readonly MethodInfo EnumFactory = typeof(Serialization).GetMethod(nameof(RegisterEnum), BindingFlags.NonPublic | BindingFlags.Static)!;

        /// <summary>
        /// Registers write and read strategies for a specific data type.
        /// </summary>
        /// <typeparam name="T">Type of data to write/read.</typeparam>
        public static void Register<T>(Writer<T> writer, Reader<T> reader)
        {
            Cache<T>.Writer = writer ?? throw new NullReferenceException("Write strategy cannot be set to null.");
            Cache<T>.Reader = reader ?? throw new NullReferenceException("Read strategy cannot be set to null.");
        }
        
        public static Writer<T> GetWriter<T>() => Cache<T>.Writer ?? TryRegisterType<T>().writer;

        public static Reader<T> GetReader<T>() => Cache<T>.Reader ?? TryRegisterType<T>().reader;

        private static (Writer<T> writer, Reader<T> reader) TryRegisterType<T>()
        {
            var type = typeof(T);

            if (type.IsEnum)
            {
                EnumFactory.MakeGenericMethod(type, Enum.GetUnderlyingType(type)).Invoke(null, Array.Empty<object>());
                return (Cache<T>.Writer!, Cache<T>.Reader!);
            }

            throw new InvalidOperationException($"Serialization strategy could not be registered for type '{type}'.");
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
