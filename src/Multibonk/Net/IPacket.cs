namespace Multibonk.Net
{
    /// <summary>
    /// A packet that can be sent over a <see cref="Connection"/>. Concrete packets
    /// live in feature modules (players, enemies, ...), not here - this layer is
    /// game-agnostic pure .NET.
    /// </summary>
    public interface IPacket
    {
        /// <summary>
        /// Wire packet id. <see cref="Connection.Send"/> writes this as the first
        /// byte of the payload, before calling <see cref="Write"/> - implementations
        /// must NOT write it themselves.
        /// </summary>
        byte Id { get; }

        /// <summary>Writes this packet's fields (everything after the id byte).</summary>
        void Write(NetWriter w);
    }
}
