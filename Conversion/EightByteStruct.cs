using System.Runtime.InteropServices;

namespace Networking.Transport.Conversion
{
    [StructLayout(LayoutKind.Explicit)]
    public struct EightByteStruct
    {
        [FieldOffset(0)]
        public long longValue;

        [FieldOffset(0)]
        public double doubleValue;
    }
}
