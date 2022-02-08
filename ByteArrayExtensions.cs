using System.Runtime.InteropServices;

namespace Networking.Transport
{
    /// <summary>
    /// Component that extends byte array with the ability to read and write primitive values.
    /// </summary>
    public static class ByteArrayExtensions
    {
        public static sbyte ReadSignedByte(this byte[] bytes, int offset) =>
            (sbyte) bytes.ReadByte(offset);

        public static bool ReadBool(this byte[] bytes, int offset) =>
            bytes.ReadByte(offset) != 0;

        public static ushort ReadUnsignedShort(this byte[] bytes, int offset) =>
            (ushort) bytes.ReadShort(offset);

        public static char ReadChar(this byte[] bytes, int offset) =>
            (char) bytes.ReadShort(offset);

        public static uint ReadUnsignedInt(this byte[] bytes, int offset) =>
            (uint) bytes.ReadInt(offset);

        public static float ReadFloat(this byte[] bytes, int offset) =>
            new FourByteStruct {intValue = bytes.ReadInt(offset)}.floatValue;

        public static ulong ReadUnsignedLong(this byte[] bytes, int offset) =>
            (ulong) bytes.ReadLong(offset);

        public static double ReadDouble(this byte[] bytes, int offset) =>
            new EightByteStruct {longValue = bytes.ReadLong(offset)}.doubleValue;

        public static byte ReadByte(this byte[] bytes, int offset) =>
            bytes[offset];

        public static short ReadShort(this byte[] bytes, int offset)
        {
#if BIGENDIAN
            var b0 = (int) bytes[offset + 0];
            var b1 = (int) bytes[offset + 1];
            return (short) (b0 << 8 | b1);
#else
            var b0 = (int) bytes[offset + 0];
            var b1 = (int) bytes[offset + 1];
            return (short) (b0 | b1 << 8);
#endif
        }

        public static int ReadInt(this byte[] bytes, int offset)
        {
#if BIGENDIAN
            var b0 = (int) bytes[offset + 0];
            var b1 = (int) bytes[offset + 1];
            var b2 = (int) bytes[offset + 2];
            var b3 = (int) bytes[offset + 3];
            return b0 << 24 | b1 << 16 | b2 << 8 | b3;
#else
            var b0 = (int) bytes[offset + 0];
            var b1 = (int) bytes[offset + 1];
            var b2 = (int) bytes[offset + 2];
            var b3 = (int) bytes[offset + 3];
            return b0 | b1 << 8 | b2 << 16 | b3 << 24;
#endif
        }

        public static long ReadLong(this byte[] bytes, int offset)
        {
#if BIGENDIAN
            var b0 = (long) bytes[offset + 0];
            var b1 = (long) bytes[offset + 1];
            var b2 = (long) bytes[offset + 2];
            var b3 = (long) bytes[offset + 3];
            var b4 = (long) bytes[offset + 4];
            var b5 = (long) bytes[offset + 5];
            var b6 = (long) bytes[offset + 6];
            var b7 = (long) bytes[offset + 7];
            return b0 << 56 | b1 << 48 | b2 << 40 | b3 << 32 | b4 << 24 | b5 << 16 | b6 << 8 | b7;
#else
            var b0 = (long) bytes[offset + 0];
            var b1 = (long) bytes[offset + 1];
            var b2 = (long) bytes[offset + 2];
            var b3 = (long) bytes[offset + 3];
            var b4 = (long) bytes[offset + 4];
            var b5 = (long) bytes[offset + 5];
            var b6 = (long) bytes[offset + 6];
            var b7 = (long) bytes[offset + 7];
            return b0 | b1 << 8 | b2 << 16 | b3 << 24 | b4 << 32 | b5 << 40 | b6 << 48 | b7 << 56;
#endif
        }

        public static void WriteSignedByte(this byte[] bytes, sbyte value, int offset) =>
            bytes.WriteByte((byte) value, offset);

        public static void WriteBool(this byte[] bytes, bool value, int offset) =>
            bytes.WriteByte((byte) (value ? 1 : 0), offset);

        public static void WriteUnsignedShort(this byte[] bytes, ushort value, int offset) =>
            bytes.WriteShort((short) value, offset);

        public static void WriteChar(this byte[] bytes, char value, int offset) =>
            bytes.WriteShort((short) value, offset);

        public static void WriteUnsignedInt(this byte[] bytes, uint value, int offset) =>
            bytes.WriteInt((int) value, offset);

        public static void WriteFloat(this byte[] bytes, float value, int offset) =>
            bytes.WriteInt(new FourByteStruct {floatValue = value}.intValue, offset);

        public static void WriteUnsignedLong(this byte[] bytes, ulong value, int offset) =>
            bytes.WriteLong((long) value, offset);

        public static void WriteDouble(this byte[] bytes, double value, int offset) =>
            bytes.WriteLong(new EightByteStruct {doubleValue = value}.longValue, offset);

        public static void WriteByte(this byte[] bytes, byte value, int offset) =>
            bytes[offset] = value;

        public static void WriteShort(this byte[] bytes, short value, int offset)
        {
#if BIGENDIAN
            bytes[offset + 0] = (byte) (value >> 8);
            bytes[offset + 1] = (byte) (value >> 0);
#else
            bytes[offset + 0] = (byte) (value >> 0);
            bytes[offset + 1] = (byte) (value >> 8);
#endif
        }

        public static void WriteInt(this byte[] bytes, int value, int offset)
        {
#if BIGENDIAN
            bytes[offset + 0] = (byte) (value >> 24);
            bytes[offset + 1] = (byte) (value >> 16);
            bytes[offset + 2] = (byte) (value >> 8);
            bytes[offset + 3] = (byte) (value >> 0);
#else
            bytes[offset + 0] = (byte) (value >> 0);
            bytes[offset + 1] = (byte) (value >> 8);
            bytes[offset + 2] = (byte) (value >> 16);
            bytes[offset + 3] = (byte) (value >> 24);
#endif
        }

        public static void WriteLong(this byte[] bytes, long value, int offset)
        {
#if BIGENDIAN
            bytes[offset + 0] = (byte) (value >> 56);
            bytes[offset + 1] = (byte) (value >> 48);
            bytes[offset + 2] = (byte) (value >> 40);
            bytes[offset + 3] = (byte) (value >> 32);
            bytes[offset + 4] = (byte) (value >> 24);
            bytes[offset + 5] = (byte) (value >> 16);
            bytes[offset + 6] = (byte) (value >> 8);
            bytes[offset + 7] = (byte) (value >> 0);
#else
            bytes[offset + 0] = (byte) (value >> 0);
            bytes[offset + 1] = (byte) (value >> 8);
            bytes[offset + 2] = (byte) (value >> 16);
            bytes[offset + 3] = (byte) (value >> 24);
            bytes[offset + 4] = (byte) (value >> 32);
            bytes[offset + 5] = (byte) (value >> 40);
            bytes[offset + 6] = (byte) (value >> 48);
            bytes[offset + 7] = (byte) (value >> 56);
#endif
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct FourByteStruct
        {
            [FieldOffset(0)]
            public int intValue;

            [FieldOffset(0)]
            public float floatValue;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct EightByteStruct
        {
            [FieldOffset(0)]
            public long longValue;

            [FieldOffset(0)]
            public double doubleValue;
        }
    }
}
