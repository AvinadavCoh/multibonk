using Multibonk.Net;
using UnityEngine;

namespace Multibonk.Modules.Players
{
    // Field order/types below were copied by hand from the legacy Send*Packet classes in
    // Multibonk/Networking/Comms/Base/Packet/{PlayerMovePacket,PlayerMovedPacket,
    // PlayerRotatePacket,PlayerRotatedPacket}.cs, cross-checked against
    // tools/MultibonkTestKit's Protocol/{ClientPackets,HostSendPackets}.cs (which already
    // sends/decodes these exact layouts), so the wire format matches byte-for-byte.

    /// <summary>Client -&gt; host. Matches PlayerMovePacket.cs: position.x,y,z (float, uncompressed).</summary>
    public sealed class PlayerMovePacket : IPacket
    {
        public byte Id => (byte)ClientSentPacketId.PLAYER_MOVE_PACKET;

        private readonly Vector3 _position;

        public PlayerMovePacket(Vector3 position) => _position = position;

        public void Write(NetWriter w)
        {
            w.WriteFloat(_position.x);
            w.WriteFloat(_position.y);
            w.WriteFloat(_position.z);
        }
    }

    /// <summary>
    /// Client -&gt; host. Matches PlayerRotatePacket.cs / MultibonkTestKit's
    /// SendPlayerRotatePacket: euler angles x,y,z (float) - NOT byte-compressed on this
    /// channel (only the host-&gt;client PLAYER_ROTATED_PACKET compresses to bytes).
    /// </summary>
    public sealed class PlayerRotatePacket : IPacket
    {
        public byte Id => (byte)ClientSentPacketId.PLAYER_ROTATE_PACKET;

        private readonly Vector3 _eulerAngles;

        public PlayerRotatePacket(Vector3 eulerAngles) => _eulerAngles = eulerAngles;

        public void Write(NetWriter w)
        {
            w.WriteFloat(_eulerAngles.x);
            w.WriteFloat(_eulerAngles.y);
            w.WriteFloat(_eulerAngles.z);
        }
    }

    /// <summary>Host -&gt; clients. Matches PlayerMovedPacket.cs: WriteUShort(playerId); position.x,y,z (float).</summary>
    public sealed class PlayerMovedPacket : IPacket
    {
        public byte Id => (byte)ServerSentPacketId.PLAYER_MOVED_PACKET;

        private readonly ushort _playerId;
        private readonly Vector3 _position;

        public PlayerMovedPacket(ushort playerId, Vector3 position)
        {
            _playerId = playerId;
            _position = position;
        }

        public void Write(NetWriter w)
        {
            w.WriteUShort(_playerId);
            w.WriteFloat(_position.x);
            w.WriteFloat(_position.y);
            w.WriteFloat(_position.z);
        }
    }

    /// <summary>
    /// Host -&gt; clients. Matches PlayerRotatedPacket.cs: WriteUShort(playerId); euler
    /// angles x,y,z byte-compressed as (angle / 360 * 255) - precision-lossy by design,
    /// matching legacy exactly (do not "fix" to floats, it would break wire compat).
    /// </summary>
    public sealed class PlayerRotatedPacket : IPacket
    {
        public byte Id => (byte)ServerSentPacketId.PLAYER_ROTATED_PACKET;

        private readonly ushort _playerId;
        private readonly Vector3 _eulerAngles;

        public PlayerRotatedPacket(ushort playerId, Vector3 eulerAngles)
        {
            _playerId = playerId;
            _eulerAngles = eulerAngles;
        }

        public void Write(NetWriter w)
        {
            w.WriteUShort(_playerId);
            w.WriteByte((byte)(_eulerAngles.x / 360f * 255f));
            w.WriteByte((byte)(_eulerAngles.y / 360f * 255f));
            w.WriteByte((byte)(_eulerAngles.z / 360f * 255f));
        }
    }
}
