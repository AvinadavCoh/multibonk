using System.Globalization;
using System.Text;
using Multibonk.Networking.Comms.Base;
using Multibonk.Networking.Comms.Packet.Base.Multibonk.Networking.Comms;

namespace MultibonkTestKit.Protocol
{
    /// <summary>
    /// Hand-written host -> client packet decoders for MultibonkTestKit.
    ///
    /// Field order/types were copied by hand from reading the real
    /// Multibonk/Networking/Comms/Base/Packet/*.cs source files (the "Send*Packet"
    /// classes are the ground truth for what the host writes). Not linked directly
    /// because several of those files reference Il2Cpp/UnityEngine/LobbyPlayer types.
    ///
    /// Decoding uses the REAL linked IncomingMessage type, so ReadInt/ReadFloat/
    /// ReadString/etc. can never drift from the mod's encoding.
    /// </summary>
    public static class ServerPacketDecoder
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// Decodes a full packet payload (id byte + fields) into a human readable string.
        /// Returns null if this packet id has no hand-written decoder (caller should fall
        /// back to logging id + name + length + hex preview - never guess a wrong decode).
        /// </summary>
        public static string TryDecode(ServerSentPacketId id, byte[] raw, out int? levelupResumeCycle)
        {
            levelupResumeCycle = null;
            var msg = new IncomingMessage(raw);
            msg.ReadByte(); // packet id, already known from dispatch

            try
            {
                switch (id)
                {
                    case ServerSentPacketId.LOBBY_PLAYER_LIST_PACKET:
                    {
                        int count = msg.ReadByte();
                        var sb = new StringBuilder();
                        sb.Append($"players={count}");
                        for (int i = 0; i < count; i++)
                        {
                            ushort uuid = msg.ReadUShort();
                            string name = msg.ReadString();
                            string character = msg.ReadString();
                            sb.Append($" | [{i}] uuid={uuid} name=\"{name}\" character=\"{character}\"");
                        }
                        return sb.ToString();
                    }

                    case ServerSentPacketId.PLAYER_SELECTED_CHARACTER:
                    {
                        ushort playerId = msg.ReadUShort();
                        string character = msg.ReadString();
                        return $"playerId={playerId} character=\"{character}\"";
                    }

                    case ServerSentPacketId.START_GAME:
                    {
                        int seed = msg.ReadInt();
                        return $"seed={seed}";
                    }

                    case ServerSentPacketId.PAUSE_GAME:
                    case ServerSentPacketId.UNPAUSE_GAME:
                    case ServerSentPacketId.MAP_FINISHED_LOADING:
                    case ServerSentPacketId.STAGE_TRANSITION:
                    case ServerSentPacketId.RUN_OVER:
                        return "(no fields)";

                    case ServerSentPacketId.SPAWN_PLAYER_PACKET:
                    {
                        byte character = msg.ReadByte(); // ECharacter enum value (Il2Cpp-only enum, logged as raw byte)
                        ushort playerId = msg.ReadUShort();
                        float px = msg.ReadFloat(), py = msg.ReadFloat(), pz = msg.ReadFloat();
                        float rx = msg.ReadFloat(), ry = msg.ReadFloat(), rz = msg.ReadFloat(), rw = msg.ReadFloat();
                        return $"playerId={playerId} characterByte={character} pos=({F(px)},{F(py)},{F(pz)}) rot=({F(rx)},{F(ry)},{F(rz)},{F(rw)})";
                    }

                    case ServerSentPacketId.PLAYER_MOVED_PACKET:
                    {
                        ushort playerId = msg.ReadUShort();
                        float x = msg.ReadFloat(), y = msg.ReadFloat(), z = msg.ReadFloat();
                        return $"playerId={playerId} pos=({F(x)},{F(y)},{F(z)})";
                    }

                    case ServerSentPacketId.PLAYER_ROTATED_PACKET:
                    {
                        ushort playerId = msg.ReadUShort();
                        // byte-compressed euler angles: byte/255*360
                        float ex = msg.ReadByte() / 255f * 360f;
                        float ey = msg.ReadByte() / 255f * 360f;
                        float ez = msg.ReadByte() / 255f * 360f;
                        return $"playerId={playerId} euler=({F(ex)},{F(ey)},{F(ez)})";
                    }

                    case ServerSentPacketId.PLAYER_XP_GAINED_PACKET:
                    {
                        ushort playerId = msg.ReadUShort();
                        int xp = msg.ReadInt();
                        return $"playerId={playerId} xpAmount={xp}";
                    }

                    case ServerSentPacketId.PLAYER_LEVEL_UP_PACKET:
                    {
                        ushort playerId = msg.ReadUShort();
                        int newLevel = msg.ReadInt();
                        return $"playerId={playerId} newLevel={newLevel}";
                    }

                    case ServerSentPacketId.ITEM_DROPPED_PACKET:
                    {
                        string itemId = msg.ReadString();
                        float x = msg.ReadFloat(), y = msg.ReadFloat(), z = msg.ReadFloat();
                        int itemType = msg.ReadInt();
                        int value = msg.ReadInt();
                        return $"itemId=\"{itemId}\" pos=({F(x)},{F(y)},{F(z)}) itemType={itemType} value={value}";
                    }

                    case ServerSentPacketId.ITEM_PICKED_UP_PACKET:
                    {
                        string itemId = msg.ReadString();
                        ushort playerId = msg.ReadUShort();
                        return $"itemId=\"{itemId}\" playerId={playerId}";
                    }

                    case ServerSentPacketId.ENEMY_DEATH_PACKET:
                    {
                        string enemyId = msg.ReadString();
                        return $"enemyId=\"{enemyId}\"";
                    }

                    case ServerSentPacketId.ENEMY_HEALTH_UPDATE_PACKET:
                    {
                        string enemyId = msg.ReadString();
                        float cur = msg.ReadFloat(), max = msg.ReadFloat();
                        return $"enemyId=\"{enemyId}\" currentHealth={F(cur)} maxHealth={F(max)}";
                    }

                    case ServerSentPacketId.ENEMY_SPAWN_PACKET:
                    {
                        int enemyId = msg.ReadInt();
                        int enemyType = msg.ReadInt();
                        float x = msg.ReadFloat(), y = msg.ReadFloat(), z = msg.ReadFloat();
                        int level = msg.ReadInt();
                        bool isBoss = msg.ReadBool();
                        int flag = msg.ReadInt();
                        return $"enemyId={enemyId} enemyType={enemyType} pos=({F(x)},{F(y)},{F(z)}) level={level} isBoss={isBoss} flag={flag}";
                    }

                    case ServerSentPacketId.MAP_REVEAL:
                    {
                        int tileX = msg.ReadInt();
                        int tileY = msg.ReadInt();
                        return $"tileX={tileX} tileY={tileY}";
                    }

                    case ServerSentPacketId.MAP_REVEAL_BULK:
                    {
                        int count = msg.ReadInt();
                        var sb = new StringBuilder();
                        sb.Append($"tiles={count}");
                        int shown = Math.Min(count, 8);
                        for (int i = 0; i < shown; i++)
                            sb.Append($" ({msg.ReadInt()},{msg.ReadInt()})");
                        if (count > shown) sb.Append(" ...");
                        return sb.ToString();
                    }

                    case ServerSentPacketId.CHEST_OPEN:
                    {
                        string chestId = msg.ReadString();
                        ushort playerId = msg.ReadUShort();
                        return $"chestId=\"{chestId}\" playerId={playerId}";
                    }

                    case ServerSentPacketId.SHRINE_USE:
                    {
                        string shrineId = msg.ReadString();
                        ushort playerId = msg.ReadUShort();
                        int shrineType = msg.ReadInt();
                        return $"shrineId=\"{shrineId}\" playerId={playerId} shrineType={shrineType}";
                    }

                    case ServerSentPacketId.PLAYER_DAMAGE:
                    {
                        ushort playerId = msg.ReadUShort();
                        float cur = msg.ReadFloat(), max = msg.ReadFloat(), dmg = msg.ReadFloat();
                        return $"playerId={playerId} currentHealth={F(cur)} maxHealth={F(max)} damage={F(dmg)}";
                    }

                    case ServerSentPacketId.PLAYER_DEATH:
                    {
                        ushort playerId = msg.ReadUShort();
                        float x = msg.ReadFloat(), y = msg.ReadFloat(), z = msg.ReadFloat();
                        return $"playerId={playerId} deathPos=({F(x)},{F(y)},{F(z)})";
                    }

                    case ServerSentPacketId.PLAYER_GOLD_GAINED:
                    {
                        int gold = msg.ReadInt();
                        return $"goldAmount={gold}";
                    }

                    case ServerSentPacketId.WAVE_START:
                    {
                        int wave = msg.ReadInt();
                        return $"waveNumber={wave}";
                    }

                    case ServerSentPacketId.WAVE_COMPLETE:
                    {
                        int wave = msg.ReadInt();
                        return $"waveNumber={wave}";
                    }

                    case ServerSentPacketId.BOSS_SPAWNER_ACTIVATE:
                    {
                        float x = msg.ReadFloat(), y = msg.ReadFloat(), z = msg.ReadFloat();
                        byte spawnerType = msg.ReadByte();
                        return $"pos=({F(x)},{F(y)},{F(z)}) spawnerType={spawnerType}";
                    }

                    case ServerSentPacketId.TIME_SYNC:
                    {
                        float stageTime = msg.ReadFloat();
                        float runTime = msg.ReadFloat();
                        bool paused = msg.ReadBool();
                        return $"stageTime={F(stageTime)} runTime={F(runTime)} paused={paused}";
                    }

                    case ServerSentPacketId.STATE_DIGEST:
                    {
                        ushort sequence = msg.ReadUShort();
                        float stageTime = msg.ReadFloat();
                        float runTime = msg.ReadFloat();
                        int gold = msg.ReadInt();
                        int level = msg.ReadInt();
                        int liveEnemies = msg.ReadInt();
                        int livePickups = msg.ReadInt();
                        int counterCount = msg.ReadByte();
                        var counters = new int[counterCount];
                        for (int i = 0; i < counterCount; i++) counters[i] = msg.ReadInt();
                        int engineEnemyCount = msg.ReadInt(); // appended AFTER the counter array

                        var sb = new StringBuilder();
                        sb.AppendLine();
                        sb.AppendLine($"        sequence        = {sequence}");
                        sb.AppendLine($"        stageTime       = {F(stageTime)}");
                        sb.AppendLine($"        runTime         = {F(runTime)}");
                        sb.AppendLine($"        gold            = {gold}");
                        sb.AppendLine($"        level           = {level}");
                        sb.AppendLine($"        liveEnemies     = {liveEnemies}");
                        sb.AppendLine($"        livePickups     = {livePickups}");
                        sb.AppendLine($"        sentCounters[{counterCount}] = [{string.Join(", ", counters)}]");
                        sb.Append($"        engineEnemyCount = {engineEnemyCount}{(engineEnemyCount < 0 ? "  (-1 = host EnemyManager unavailable when digest built)" : "")}");
                        return sb.ToString();
                    }

                    case ServerSentPacketId.LEVELUP_RESUME:
                    {
                        int cycle = msg.ReadInt();
                        levelupResumeCycle = cycle;
                        return $"cycleNumber={cycle}";
                    }

                    // BOSS_HEALTH (30) has no packet body defined anywhere in the mod source
                    // as of this writing - flagged as truly unknown, not a missing decoder.
                    case ServerSentPacketId.BOSS_HEALTH:
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
