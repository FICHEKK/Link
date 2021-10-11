using System;

namespace Networking.Transport
{
    public partial class Packet
    {
        public void Write(byte value) => _buffer[_writePosition++] = value;
        public void Write(bool value) => Write(BitConverter.GetBytes(value));
        public void Write(char value) => Write(BitConverter.GetBytes(value));
        public void Write(short value) => Write(BitConverter.GetBytes(value));
        public void Write(int value) => Write(BitConverter.GetBytes(value));
        public void Write(long value) => Write(BitConverter.GetBytes(value));
        public void Write(float value) => Write(BitConverter.GetBytes(value));
        public void Write(double value) => Write(BitConverter.GetBytes(value));

        public void Write(string value)
        {
            Write(value.Length);
            Write(Encoding.GetBytes(value));
        }

        public void Write(int[] array)
        {
            var bytes = new byte[array.Length * sizeof(int)];
            Buffer.BlockCopy(array, 0, bytes, 0, bytes.Length);
            Write(bytes);
        }

        private void Write(byte[] bytes)
        {
            foreach (var b in bytes)
            {
                _buffer[_writePosition++] = b;
            }
        }

        private void Write(byte[] bytes, int length)
        {
            for (var i = 0; i < length; i++)
            {
                _buffer[_writePosition++] = bytes[i];
            }
        }
    }
}
