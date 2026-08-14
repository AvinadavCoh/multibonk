using System.Collections.Generic;
using Multibonk.Net;

namespace Multibonk.Modules.Session
{
    // Field order/types below were copied by hand from the legacy Send*Packet classes
    // in Multibonk/Networking/Comms/Base/Packet/*.cs so the wire format matches exactly.
    // Decoding of the matching incoming payloads is done inline in SessionModule's
    // handlers (the PacketRegistry hands handlers a NetReader already positioned past
    // the id byte), not via separate decoder classes here.

    /// <summary>Client -&gt; host. Matches JoinLobbyPacket.cs: WriteInt(version); WriteString(name).</summary>
    public sealed class JoinLobbyPacket : IPacket
    {
        public byte Id => (byte)ClientSentPacketId.JOIN_LOBBY_PACKET;

        private readonly int _modVersion;
        private readonly string _playerName;

        public JoinLobbyPacket(int modVersion, string playerName)
        {
            _modVersion = modVersion;
            _playerName = playerName;
        }

        public void Write(NetWriter w)
        {
            w.WriteInt(_modVersion);
            w.WriteString(_playerName);
        }
    }

    /// <summary>Client -&gt; host. Matches SelectCharacterPacket.cs: WriteString(characterName).</summary>
    public sealed class SelectCharacterPacket : IPacket
    {
        public byte Id => (byte)ClientSentPacketId.CHARACTER_SELECTION;

        private readonly string _characterName;

        public SelectCharacterPacket(string characterName) => _characterName = characterName;

        public void Write(NetWriter w) => w.WriteString(_characterName);
    }

    /// <summary>Client -&gt; host. Matches GameLoadedPacket.cs: pos.x,y,z ; rot.x,y,z,w (all float).</summary>
    public sealed class GameLoadedPacket : IPacket
    {
        public byte Id => (byte)ClientSentPacketId.GAME_LOADED_PACKET;

        private readonly float _px, _py, _pz, _rx, _ry, _rz, _rw;

        public GameLoadedPacket(float px, float py, float pz, float rx, float ry, float rz, float rw)
        {
            _px = px; _py = py; _pz = pz;
            _rx = rx; _ry = ry; _rz = rz; _rw = rw;
        }

        public void Write(NetWriter w)
        {
            w.WriteFloat(_px); w.WriteFloat(_py); w.WriteFloat(_pz);
            w.WriteFloat(_rx); w.WriteFloat(_ry); w.WriteFloat(_rz); w.WriteFloat(_rw);
        }
    }

    /// <summary>
    /// Host -&gt; client. Matches LobbyPlayerListPacket.cs: WriteByte(count); per player
    /// UUID(ushort), Name(string), SelectedCharacter(string). The recipient always
    /// identifies itself as entry [0] (see SessionModule.HandleJoinLobby).
    /// </summary>
    public sealed class LobbyPlayerListPacket : IPacket
    {
        public byte Id => (byte)ServerSentPacketId.LOBBY_PLAYER_LIST_PACKET;

        private readonly IReadOnlyList<LobbyPlayer> _players;

        public LobbyPlayerListPacket(IReadOnlyList<LobbyPlayer> players) => _players = players;

        public void Write(NetWriter w)
        {
            w.WriteByte((byte)_players.Count);
            foreach (var p in _players)
            {
                w.WriteUShort(p.Uuid);
                w.WriteString(p.Name ?? string.Empty);
                w.WriteString(p.Character ?? string.Empty);
            }
        }
    }

    /// <summary>Host -&gt; client. Matches PlayerSelectedCharacterPacket.cs: WriteUShort(playerId); WriteString(characterName).</summary>
    public sealed class PlayerSelectedCharacterPacket : IPacket
    {
        public byte Id => (byte)ServerSentPacketId.PLAYER_SELECTED_CHARACTER;

        private readonly ushort _playerId;
        private readonly string _characterName;

        public PlayerSelectedCharacterPacket(ushort playerId, string characterName)
        {
            _playerId = playerId;
            _characterName = characterName;
        }

        public void Write(NetWriter w)
        {
            w.WriteUShort(_playerId);
            w.WriteString(_characterName);
        }
    }

    /// <summary>Host -&gt; client. Matches StartGamePacket.cs: WriteInt(seed).</summary>
    public sealed class StartGamePacket : IPacket
    {
        public byte Id => (byte)ServerSentPacketId.START_GAME;

        private readonly int _seed;

        public StartGamePacket(int seed) => _seed = seed;

        public void Write(NetWriter w) => w.WriteInt(_seed);
    }

    /// <summary>
    /// Host -&gt; client. Matches SpawnPlayerPacket.cs: WriteByte(characterByte);
    /// WriteUShort(playerId); pos.x,y,z; rot.x,y,z,w (all float). Legacy writes a cast
    /// Il2Cpp-only ECharacter enum for the character byte; this module has no player
    /// module / typed character lookup yet, so callers pass a raw placeholder byte
    /// (see SessionModule.HandleGameLoaded).
    /// </summary>
    public sealed class SpawnPlayerPacket : IPacket
    {
        public byte Id => (byte)ServerSentPacketId.SPAWN_PLAYER_PACKET;

        private readonly byte _characterByte;
        private readonly ushort _playerId;
        private readonly float _px, _py, _pz, _rx, _ry, _rz, _rw;

        public SpawnPlayerPacket(byte characterByte, ushort playerId,
            float px, float py, float pz, float rx, float ry, float rz, float rw)
        {
            _characterByte = characterByte;
            _playerId = playerId;
            _px = px; _py = py; _pz = pz;
            _rx = rx; _ry = ry; _rz = rz; _rw = rw;
        }

        public void Write(NetWriter w)
        {
            w.WriteByte(_characterByte);
            w.WriteUShort(_playerId);
            w.WriteFloat(_px); w.WriteFloat(_py); w.WriteFloat(_pz);
            w.WriteFloat(_rx); w.WriteFloat(_ry); w.WriteFloat(_rz); w.WriteFloat(_rw);
        }
    }
}
