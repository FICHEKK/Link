using System.Runtime.InteropServices;

namespace Networking.Transport.Conversion
{
    [StructLayout(LayoutKind.Explicit)]
    public struct FourByteStruct
    {
        [FieldOffset(0)]
        public int intValue;

        [FieldOffset(0)]
        public float floatValue;
    }
}
