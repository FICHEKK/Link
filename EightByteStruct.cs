using System.Runtime.InteropServices;

namespace Networking.Transport
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct EightByteStruct
    {
        [FieldOffset(0)]
        public long longValue;

        [FieldOffset(0)]
        public double doubleValue;
    }
}
