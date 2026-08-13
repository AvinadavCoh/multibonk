using System.Linq;
using Multibonk.Networking.Comms.Base;

namespace MultibonkTestKit.Protocol
{
    /// <summary>
    /// Hand-written host -> client packet bodies for MultibonkTestKit's `host` mode
    /// (the tool acts as a fake HOST driving a real game CLIENT - the mirror of
    /// ClientPackets.cs, which acts as a fake client driving a real host).
    ///
    /// Field order/types copied by hand from the real Send*Packet classes in
    /// Multibonk/Networking/Comms/Base/Packet/*.cs (not linked directly - several
    /// depend on Il2Cpp/UnityEngine/ECharacter types that only exist in the modded
    /// game process). Source file noted per packet. Built on the REAL linked
    /// OutgoingPacket/OutgoingMessage/PacketId types so ids/encodings can't drift.
    /// </summary>
    public sealed class SendLobbyPlayerListPacket : OutgoingPacket
    {
        // LobbyPlayerListPacket.cs: WriteByte(id); WriteByte(count); per player: UUID(ushort), Name(string), Character(string)
        public SendLobbyPlayerListPacket(IEnumerable<(ushort uuid, string name, string character)> players)
        {
            var list = players.ToList();
            Message.WriteByte((byte)ServerSentPacketId.LOBBY_PLAYER_LIST_PACKET);
            Message.WriteByte((byte)list.Count);
            foreach (var p in list)
            {
                Message.WriteUShort(p.uuid);
                Message.WriteString(p.name);
                Message.WriteString(p.character);
            }
        }
    }

    public sealed class SendPlayerSelectedCharacterPacket : OutgoingPacket
    {
        // PlayerSelectedCharacterPacket.cs: WriteByte(id); WriteUShort(playerId); WriteString(characterName)
        public SendPlayerSelectedCharacterPacket(ushort playerId, string characterName)
        {
            Message.WriteByte((byte)ServerSentPacketId.PLAYER_SELECTED_CHARACTER);
            Message.WriteUShort(playerId);
            Message.WriteString(characterName);
        }
    }

    public sealed class SendStartGamePacket : OutgoingPacket
    {
        // StartGamePacket.cs: WriteByte(id); WriteInt(seed)
        public SendStartGamePacket(int seed)
        {
            Message.WriteByte((byte)ServerSentPacketId.START_GAME);
            Message.WriteInt(seed);
        }
    }

    public sealed class SendSpawnPlayerPacket : OutgoingPacket
    {
        // SpawnPlayerPacket.cs: WriteByte(id); WriteByte(characterByte); WriteUShort(playerId); pos.x,y,z; rot.x,y,z,w (all float)
        public SendSpawnPlayerPacket(byte characterByte, ushort playerId,
            float posX, float posY, float posZ, float rotX, float rotY, float rotZ, float rotW)
        {
            Message.WriteByte((byte)ServerSentPacketId.SPAWN_PLAYER_PACKET);
            Message.WriteByte(characterByte);
            Message.WriteUShort(playerId);
            Message.WriteFloat(posX);
            Message.WriteFloat(posY);
            Message.WriteFloat(posZ);
            Message.WriteFloat(rotX);
            Message.WriteFloat(rotY);
            Message.WriteFloat(rotZ);
            Message.WriteFloat(rotW);
        }
    }

    public sealed class SendTimeSyncPacket : OutgoingPacket
    {
        // TimeSyncPacket.cs: WriteByte(id); stageTime, runTime (float); paused (bool)
        public SendTimeSyncPacket(float stageTime, float runTime, bool paused)
        {
            Message.WriteByte((byte)ServerSentPacketId.TIME_SYNC);
            Message.WriteFloat(stageTime);
            Message.WriteFloat(runTime);
            Message.WriteBool(paused);
        }
    }

    public sealed class SendStateDigestPacket : OutgoingPacket
    {
        // StateDigestPacket.cs: WriteByte(id); sequence(ushort); stageTime,runTime(float); gold,level,liveEnemies,livePickups(int);
        // WriteByte(sentCounters.Length); sentCounters[](int); THEN engineEnemyCount(int) appended AFTER the counter array.
        public SendStateDigestPacket(ushort sequence, float stageTime, float runTime,
            int gold, int level, int liveEnemies, int livePickups, int[] sentCounters, int engineEnemyCount)
        {
            Message.WriteByte((byte)ServerSentPacketId.STATE_DIGEST);
            Message.WriteUShort(sequence);
            Message.WriteFloat(stageTime);
            Message.WriteFloat(runTime);
            Message.WriteInt(gold);
            Message.WriteInt(level);
            Message.WriteInt(liveEnemies);
            Message.WriteInt(livePickups);

            Message.WriteByte((byte)sentCounters.Length);
            for (int i = 0; i < sentCounters.Length; i++)
                Message.WriteInt(sentCounters[i]);

            Message.WriteInt(engineEnemyCount); // MUST come after the counter array
        }
    }

    public sealed class SendEnemySpawnPacket : OutgoingPacket
    {
        // EnemySpawnPacket.cs: WriteByte(id); enemyId,enemyType(int); pos.x,y,z(float); level(int); isBoss(bool); flag(int)
        public SendEnemySpawnPacket(int enemyId, int enemyType, float posX, float posY, float posZ, int level, bool isBoss, int flag)
        {
            Message.WriteByte((byte)ServerSentPacketId.ENEMY_SPAWN_PACKET);
            Message.WriteInt(enemyId);
            Message.WriteInt(enemyType);
            Message.WriteFloat(posX);
            Message.WriteFloat(posY);
            Message.WriteFloat(posZ);
            Message.WriteInt(level);
            Message.WriteBool(isBoss);
            Message.WriteInt(flag);
        }
    }

    public sealed class SendEnemyDeathPacket : OutgoingPacket
    {
        // EnemyDeathPacket.cs: WriteByte(id); WriteString(enemyId)
        public SendEnemyDeathPacket(string enemyId)
        {
            Message.WriteByte((byte)ServerSentPacketId.ENEMY_DEATH_PACKET);
            Message.WriteString(enemyId);
        }
    }

    public sealed class SendEnemyHealthUpdatePacket : OutgoingPacket
    {
        // EnemyHealthUpdatePacket.cs: WriteByte(id); WriteString(enemyId); currentHealth,maxHealth(float)
        public SendEnemyHealthUpdatePacket(string enemyId, float currentHealth, float maxHealth)
        {
            Message.WriteByte((byte)ServerSentPacketId.ENEMY_HEALTH_UPDATE_PACKET);
            Message.WriteString(enemyId);
            Message.WriteFloat(currentHealth);
            Message.WriteFloat(maxHealth);
        }
    }

    public sealed class SendItemDroppedPacket : OutgoingPacket
    {
        // ItemDroppedPacket.cs: WriteByte(id); WriteString(itemId); pos.x,y,z(float); itemType,value(int)
        public SendItemDroppedPacket(string itemId, float posX, float posY, float posZ, int itemType, int value)
        {
            Message.WriteByte((byte)ServerSentPacketId.ITEM_DROPPED_PACKET);
            Message.WriteString(itemId);
            Message.WriteFloat(posX);
            Message.WriteFloat(posY);
            Message.WriteFloat(posZ);
            Message.WriteInt(itemType);
            Message.WriteInt(value);
        }
    }

    public sealed class SendItemPickedUpPacket : OutgoingPacket
    {
        // ItemPickedUpPacket.cs: WriteByte(id); WriteString(itemId); WriteUShort(playerId)
        public SendItemPickedUpPacket(string itemId, ushort playerId)
        {
            Message.WriteByte((byte)ServerSentPacketId.ITEM_PICKED_UP_PACKET);
            Message.WriteString(itemId);
            Message.WriteUShort(playerId);
        }
    }

    public sealed class SendMapRevealPacket : OutgoingPacket
    {
        // MapRevealPacket.cs: WriteByte(id); tileX,tileY(int) - actually rounded world x/z, see ClientMapRevealPacket.cs
        public SendMapRevealPacket(int tileX, int tileY)
        {
            Message.WriteByte((byte)ServerSentPacketId.MAP_REVEAL);
            Message.WriteInt(tileX);
            Message.WriteInt(tileY);
        }
    }

    public sealed class SendChestOpenPacket : OutgoingPacket
    {
        // ChestOpenPacket.cs: WriteByte(id); WriteString(chestId); WriteUShort(playerId)
        // chestId is a quantized "x_y_z" world-position key (Math.Round per axis) - see
        // ChestOpenPacketHandler.cs client-side resolution.
        public SendChestOpenPacket(string chestId, ushort playerId)
        {
            Message.WriteByte((byte)ServerSentPacketId.CHEST_OPEN);
            Message.WriteString(chestId);
            Message.WriteUShort(playerId);
        }
    }

    public sealed class SendShrineUsePacket : OutgoingPacket
    {
        // ShrineUsePacket.cs: WriteByte(id); WriteString(shrineId); WriteUShort(playerId); WriteInt(shrineType)
        // shrineId is a quantized "x_y_z" world-position key, shrineType 0-5 - see ShrineUsePacketHandler.cs.
        public SendShrineUsePacket(string shrineId, ushort playerId, int shrineType)
        {
            Message.WriteByte((byte)ServerSentPacketId.SHRINE_USE);
            Message.WriteString(shrineId);
            Message.WriteUShort(playerId);
            Message.WriteInt(shrineType);
        }
    }

    public sealed class SendServerPlayerXpGainedPacket : OutgoingPacket
    {
        // PlayerXpGainedPacket.cs (server variant): WriteByte(id); WriteUShort(playerId); WriteInt(xpAmount)
        public SendServerPlayerXpGainedPacket(ushort playerId, int xpAmount)
        {
            Message.WriteByte((byte)ServerSentPacketId.PLAYER_XP_GAINED_PACKET);
            Message.WriteUShort(playerId);
            Message.WriteInt(xpAmount);
        }
    }

    public sealed class SendServerPlayerGoldGainedPacket : OutgoingPacket
    {
        // PlayerGoldGainedPacket.cs (server variant): WriteByte(id); WriteInt(goldAmount)
        public SendServerPlayerGoldGainedPacket(int goldAmount)
        {
            Message.WriteByte((byte)ServerSentPacketId.PLAYER_GOLD_GAINED);
            Message.WriteInt(goldAmount);
        }
    }

    public sealed class SendWaveStartPacket : OutgoingPacket
    {
        // WaveStartPacket.cs: WriteByte(id); WriteInt(waveNumber)
        public SendWaveStartPacket(int waveNumber)
        {
            Message.WriteByte((byte)ServerSentPacketId.WAVE_START);
            Message.WriteInt(waveNumber);
        }
    }

    public sealed class SendWaveCompletePacket : OutgoingPacket
    {
        // WaveCompletePacket.cs: WriteByte(id); WriteInt(waveNumber)
        public SendWaveCompletePacket(int waveNumber)
        {
            Message.WriteByte((byte)ServerSentPacketId.WAVE_COMPLETE);
            Message.WriteInt(waveNumber);
        }
    }

    public sealed class SendPauseGamePacket : OutgoingPacket
    {
        // PauseGamePacket.cs: WriteByte(id) only
        public SendPauseGamePacket()
        {
            Message.WriteByte((byte)ServerSentPacketId.PAUSE_GAME);
        }
    }

    public sealed class SendUnpauseGamePacket : OutgoingPacket
    {
        // UnpauseGamePacket.cs: WriteByte(id) only
        public SendUnpauseGamePacket()
        {
            Message.WriteByte((byte)ServerSentPacketId.UNPAUSE_GAME);
        }
    }

    public sealed class SendLevelupResumePacket : OutgoingPacket
    {
        // LevelupCoordPackets.cs: WriteByte(id); WriteInt(cycleNumber)
        public SendLevelupResumePacket(int cycleNumber)
        {
            Message.WriteByte((byte)ServerSentPacketId.LEVELUP_RESUME);
            Message.WriteInt(cycleNumber);
        }
    }

    public sealed class SendRunOverPacket : OutgoingPacket
    {
        // RunOverPacket.cs: WriteByte(id) only
        public SendRunOverPacket()
        {
            Message.WriteByte((byte)ServerSentPacketId.RUN_OVER);
        }
    }

    public sealed class SendStageTransitionPacket : OutgoingPacket
    {
        // StageTransitionPacket.cs: WriteByte(id) only
        public SendStageTransitionPacket()
        {
            Message.WriteByte((byte)ServerSentPacketId.STAGE_TRANSITION);
        }
    }
}
