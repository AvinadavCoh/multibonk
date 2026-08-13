using System;
using System.Collections.Generic;
using System.Text;

namespace Multibonk.Net
{
    /// <summary>
    /// Builds a wire-format PAYLOAD buffer (packet id + fields, no frame header).
    ///
    /// Byte-for-byte compatible with the legacy mod's OutgoingMessage
    /// (Multibonk/Networking/Comms/Base/OutgoingMessage.cs): every multi-byte
    /// primitive is written with BitConverter.GetBytes on the runtime's native
    /// endianness (little-endian on the Windows/x64 targets this mod runs on),
    /// bools are a single 0/1 byte, and strings are UTF8 bytes prefixed with a
    /// 2-byte signed length (max short.MaxValue bytes).
    ///
    /// The 2-byte length-prefix FRAME header is added by <see cref="Connection"/>,
    /// not here - this class only produces the payload that goes after it.
    /// </summary>
    public sealed class NetWriter
    {
        private readonly List<byte> _buffer = new List<byte>();

        public int Length => _buffer.Count;

        public void WriteByte(byte value) => _buffer.Add(value);

        public void WriteBool(bool value) => _buffer.Add(value ? (byte)1 : (byte)0);

        public void WriteShort(short value) => _buffer.AddRange(BitConverter.GetBytes(value));

        public void WriteUShort(ushort value) => _buffer.AddRange(BitConverter.GetBytes(value));

        public void WriteInt(int value) => _buffer.AddRange(BitConverter.GetBytes(value));

        public void WriteLong(long value) => _buffer.AddRange(BitConverter.GetBytes(value));

        public void WriteFloat(float value) => _buffer.AddRange(BitConverter.GetBytes(value));

        public void WriteDouble(double value) => _buffer.AddRange(BitConverter.GetBytes(value));

        /// <summary>UTF8 bytes prefixed with a 2-byte signed length. Null is written as empty.</summary>
        public void WriteString(string value)
        {
            if (value == null) value = string.Empty;
            byte[] data = Encoding.UTF8.GetBytes(value);
            if (data.Length > short.MaxValue)
                throw new ArgumentException("String too long to write");
            WriteShort((short)data.Length);
            _buffer.AddRange(data);
        }

        /// <summary>Appends raw bytes with no length prefix - caller tracks the count.</summary>
        public void WriteBytes(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            _buffer.AddRange(data);
        }

        public byte[] ToArray() => _buffer.ToArray();

        public void Clear() => _buffer.Clear();
    }
}
