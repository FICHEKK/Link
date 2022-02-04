using System.Runtime.InteropServices;

namespace Networking.Transport
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct FourByteStruct
    {
        [FieldOffset(0)]
        public int intValue;

        [FieldOffset(0)]
        public float floatValue;
    }
}
