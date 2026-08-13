using System;
using System.Text;

namespace Multibonk.Net
{
    /// <summary>
    /// Reads primitives out of a wire-format PAYLOAD buffer, in the order they were
    /// written by <see cref="NetWriter"/>.
    ///
    /// Byte-for-byte compatible with the legacy mod's IncomingMessage
    /// (Multibonk/Networking/Comms/Base/IncomingMessage.cs): same endianness,
    /// same bool/string encoding, same "not enough bytes" failure mode
    /// (throws IndexOutOfRangeException rather than returning a default).
    /// </summary>
    public sealed class NetReader
    {
        private readonly byte[] _buffer;
        private int _offset;

        public NetReader(byte[] data)
        {
            _buffer = data ?? throw new ArgumentNullException(nameof(data));
            _offset = 0;
        }

        public int Remaining => _buffer.Length - _offset;

        public byte ReadByte()
        {
            EnsureAvailable(1);
            return _buffer[_offset++];
        }

        public bool ReadBool() => ReadByte() != 0;

        public short ReadShort()
        {
            EnsureAvailable(2);
            short value = BitConverter.ToInt16(_buffer, _offset);
            _offset += 2;
            return value;
        }

        public ushort ReadUShort()
        {
            EnsureAvailable(2);
            ushort value = BitConverter.ToUInt16(_buffer, _offset);
            _offset += 2;
            return value;
        }

        public int ReadInt()
        {
            EnsureAvailable(4);
            int value = BitConverter.ToInt32(_buffer, _offset);
            _offset += 4;
            return value;
        }

        public long ReadLong()
        {
            EnsureAvailable(8);
            long value = BitConverter.ToInt64(_buffer, _offset);
            _offset += 8;
            return value;
        }

        public float ReadFloat()
        {
            EnsureAvailable(4);
            float value = BitConverter.ToSingle(_buffer, _offset);
            _offset += 4;
            return value;
        }

        public double ReadDouble()
        {
            EnsureAvailable(8);
            double value = BitConverter.ToDouble(_buffer, _offset);
            _offset += 8;
            return value;
        }

        public string ReadString()
        {
            short length = ReadShort();
            EnsureAvailable(length);
            string value = Encoding.UTF8.GetString(_buffer, _offset, length);
            _offset += length;
            return value;
        }

        public byte[] ReadBytes(int count)
        {
            EnsureAvailable(count);
            byte[] data = new byte[count];
            Array.Copy(_buffer, _offset, data, 0, count);
            _offset += count;
            return data;
        }

        private void EnsureAvailable(int length)
        {
            if (length < 0 || _offset + length > _buffer.Length)
                throw new IndexOutOfRangeException($"Not enough bytes to read {length} bytes from packet.");
        }
    }
}
