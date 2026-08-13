using System.Globalization;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace MultibonkTestKit.Protocol
{
    /// <summary>
    /// Decoded fields pulled out of a client-&gt;host packet, for the caller (HostMode)
    /// to act on (assign UUIDs, track last known position, etc.) without re-parsing.
    /// Only the fields relevant to at least one packet type are populated; check
    /// <see cref="ClientPacketDecoder.TryDecode"/>'s <c>id</c> to know which apply.
    /// </summary>
    public sealed class ClientDecodedData
    {
        public int IntVersion;
        public string StringA;
        public float X, Y, Z;
        public float RotX, RotY, RotZ, RotW;
        public int IntValue;
        public float FloatA, FloatB, FloatC;
    }

    /// <summary>
    /// Hand-written client -> host packet decoders for MultibonkTestKit's `host` mode.
    ///
    /// Field order/types copied by hand from the real Multibonk/Networking/Comms/Base/Packet/*.cs
    /// source files (not linked directly - see HostSendPackets.cs header for why). Decoding
    /// uses the REAL linked IncomingMessage type so read primitives can't drift.
    /// </summary>
    public static class ClientPacketDecoder
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string TryDecode(ClientSentPacketId id, byte[] raw, out ClientDecodedData data)
        {
            data = new ClientDecodedData();
            var msg = new IncomingMessage(raw);
            msg.ReadByte(); // packet id, already known from dispatch

            try
            {
                switch (id)
                {
                    case ClientSentPacketId.JOIN_LOBBY_PACKET:
                    {
                        // JoinLobbyPacket.cs: version(int), playerName(string)
                        data.IntVersion = msg.ReadInt();
                        data.StringA = msg.ReadString();
                        return $"version={data.IntVersion} name=\"{data.StringA}\"";
                    }

                    case ClientSentPacketId.CHARACTER_SELECTION:
                    {
                        // SelectCharacterPacket.cs: characterName(string)
                        data.StringA = msg.ReadString();
                        return $"character=\"{data.StringA}\"";
                    }

                    case ClientSentPacketId.GAME_LOADED_PACKET:
                    {
                        // GameLoadedPacket.cs: pos.x,y,z ; rot.x,y,z,w (all float)
                        data.X = msg.ReadFloat(); data.Y = msg.ReadFloat(); data.Z = msg.ReadFloat();
                        data.RotX = msg.ReadFloat(); data.RotY = msg.ReadFloat(); data.RotZ = msg.ReadFloat(); data.RotW = msg.ReadFloat();
                        return $"pos=({F(data.X)},{F(data.Y)},{F(data.Z)}) rot=({F(data.RotX)},{F(data.RotY)},{F(data.RotZ)},{F(data.RotW)})";
                    }

                    case ClientSentPacketId.PLAYER_MOVE_PACKET:
                    {
                        // PlayerMovePacket.cs: position.x,y,z (float)
                        data.X = msg.ReadFloat(); data.Y = msg.ReadFloat(); data.Z = msg.ReadFloat();
                        return $"pos=({F(data.X)},{F(data.Y)},{F(data.Z)})";
                    }

                    case ClientSentPacketId.PLAYER_ROTATE_PACKET:
                    {
                        // PlayerRotatePacket.cs: euler.x,y,z (float) - not byte-compressed on this channel
                        data.RotX = msg.ReadFloat(); data.RotY = msg.ReadFloat(); data.RotZ = msg.ReadFloat();
                        return $"euler=({F(data.RotX)},{F(data.RotY)},{F(data.RotZ)})";
                    }

                    case ClientSentPacketId.PLAYER_XP_GAINED_PACKET:
                    {
                        // ClientLootPackets.cs: xpAmount(int)
                        data.IntValue = msg.ReadInt();
                        return $"xpAmount={data.IntValue}";
                    }

                    case ClientSentPacketId.PLAYER_GOLD_GAINED_PACKET:
                    {
                        // ClientLootPackets.cs: goldAmount(int)
                        data.IntValue = msg.ReadInt();
                        return $"goldAmount={data.IntValue}";
                    }

                    case ClientSentPacketId.PLAYER_HEALTH_PACKET:
                    {
                        // ClientHealthPacket.cs: currentHealth,maxHealth,damageAmount (float)
                        data.FloatA = msg.ReadFloat(); data.FloatB = msg.ReadFloat(); data.FloatC = msg.ReadFloat();
                        return $"cur={F(data.FloatA)} max={F(data.FloatB)} dmg={F(data.FloatC)}";
                    }

                    case ClientSentPacketId.MAP_REVEAL_PACKET:
                    {
                        // ClientMapRevealPacket.cs: tileX,tileY(int) - actually rounded world x/z
                        data.IntValue = msg.ReadInt();
                        int tileY = msg.ReadInt();
                        data.FloatA = data.IntValue;
                        data.FloatB = tileY;
                        return $"tileX={data.IntValue} tileY={tileY}";
                    }

                    case ClientSentPacketId.PICKUP_CONSUMED_PACKET:
                    {
                        // ClientPickupConsumedPacket.cs: hostId(string)
                        data.StringA = msg.ReadString();
                        return $"hostId=\"{data.StringA}\"";
                    }

                    case ClientSentPacketId.PLAYER_DIED_PACKET:
                        return "(no fields - sender identified by connection)";

                    case ClientSentPacketId.LEVELUP_DONE_PACKET:
                        return "(no fields - sender identified by connection)";

                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                return $"<decode error: {ex.Message}>";
            }
        }

        private static string F(float v) => v.ToString("0.###", Inv);
    }
}
