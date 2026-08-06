using Il2Cpp;
using Multibonk.Game;
using Multibonk.Networking.Comms.Base;
using UnityEngine;

namespace Multibonk.Networking.Lobby
{
    public enum LobbyState
    {
        None, Hosting, Connected, AwaitingForHost
    }

    public class LobbyPlayer
    {
        private static int _uuidCounter = 0; // contador interno para IDs únicos

        public Connection Connection { get; private set; }
        public ushort UUID { get; private set; }
        public string Name { get; private set; }
        public string SelectedCharacter { get; set; }
        public int Ping { get; set; }
        public Vector3 SpawnPosition { get; set; }
        public Quaternion SpawnRotation { get; set; }

        // Synced health for the Players HUD (updated by damage packets)
        public float CurrentHealth { get; set; } = 100f;
        public float MaxHealth { get; set; } = 100f;

        // Synced level for the Players HUD (updated by level-up packets)
        public int Level { get; set; } = 1;

        /// <summary>
        /// Set to true when this player's death is confirmed for the current run.
        /// Reset to false at the start of every new run via LobbyContext.ResetDeathFlags().
        /// </summary>
        public bool IsDead { get; set; } = false;

        public LobbyPlayer(
            string name = "Unknown",
            ushort? uuid = null,
            string selectedCharacter = "None",
            Connection connection = null
        )
        {
            Name = name;
            SelectedCharacter = selectedCharacter;
            Ping = 0;
            Connection = connection;

            if (uuid.HasValue)
            {
                UUID = uuid.Value;
            }
            else
            {
                UUID = (ushort)System.Threading.Interlocked.Increment(ref _uuidCounter);
            }
        }
    }

    public static class LobbyPatchFlags
    {
        public static bool IsHosting;
        public static bool InMultiplayer; // True when hosting or connected to a lobby

        /// <summary>
        /// Shared reference so packet handlers that cannot inject LobbyContext via DI
        /// can still reach the current lobby. Set by LobbyService on create / join.
        /// </summary>
        public static LobbyContext CurrentLobby { get; internal set; }
    }

    public class LobbyContext
    {
        private List<LobbyPlayer> players = new List<LobbyPlayer>();
        private readonly object _lock = new object();
        private LobbyPlayer myself;

        public LobbyContext()
        {
            // Reset per-run state whenever the run coordinator signals a restart.
            RunCoordinator.RunReset += ResetDeathFlags;
        }

        public LobbyState State { get; private set; }

        public event Action<LobbyContext> OnLobbyCreated;
        public event Action<LobbyContext> OnLobbyJoin;
        public event Action<string> OnLobbyJoinFailed;
        public event Action<LobbyPlayer, LobbyContext> OnPlayerJoined;
        public event Action<LobbyPlayer, LobbyContext> OnPlayerLeft;
        public event Action<LobbyState, LobbyState, LobbyContext> OnLobbyStateChanged;
        public event Action<LobbyContext> OnLobbyClosed;

        public void SetState(LobbyState state)
        {
            var previous = State;
            State = state;
            OnLobbyStateChanged?.Invoke(previous, State, this);
        }

        public LobbyPlayer GetMyself()
        {
            return myself;
        }

        public void SetMyself(LobbyPlayer player)
        {
            myself = player;
            AddPlayer(player);
        }

        public LobbyPlayer AddPlayer(string name)
        {
            var player = new LobbyPlayer(name);
            lock (_lock)
            {
                players.Add(player);
            }
            OnPlayerJoined?.Invoke(player, this);
            return player;
        }

        public void ClearPlayers()
        {
            lock (_lock)
            {
                players.Clear();
            }
        }

        public void AddPlayer(LobbyPlayer player)
        {
            lock (_lock)
            {
                players.Add(player);
            }
        }

        /// <summary>
        /// Remove a player by UUID.
        /// Took a Guid until now, compared against a ushort UUID via Guid.Equals(object),
        /// which is false for every input - so this silently removed nobody.
        /// </summary>
        public LobbyPlayer RemovePlayer(ushort uuid)
        {
            LobbyPlayer player = null;
            lock (_lock)
            {
                player = players.Find(p => p.UUID == uuid);
                if (player != null)
                    players.Remove(player);
            }
            if (player != null)
                OnPlayerLeft?.Invoke(player, this);
            return player;
        }

        /// <summary>
        /// Removes the player whose Connection matches <paramref name="conn"/>.
        /// Fires OnPlayerLeft and returns the removed player, or null if not found.
        /// Used by the disconnect handler to clean up a dropped client.
        /// </summary>
        public LobbyPlayer RemovePlayer(Connection conn)
        {
            LobbyPlayer player = null;
            lock (_lock)
            {
                player = players.Find(p => conn == p.Connection);
                if (player != null)
                    players.Remove(player);
            }
            if (player != null)
                OnPlayerLeft?.Invoke(player, this);
            return player;
        }

        public LobbyPlayer GetPlayer(ushort uuid)
        {
            var player = players.Find(p => uuid.Equals(p.UUID));

            return player;
        }


        public LobbyPlayer GetPlayer(Connection conn)
        {
            var player = players.Find(p => conn.Equals(p.Connection));

            return player;
        }

        /// <summary>
        /// This method is currently not zero-copy. It always returns a copy of the players list to be thread-safe.
        /// </summary>
        /// <returns></returns>
        public List<LobbyPlayer> GetPlayers()
        {
            lock (_lock)
            {
                return players.ToList();
            }
        }

        public void TriggerLobbyCreated() => OnLobbyCreated?.Invoke(this);
        public void TriggerLobbyJoin() => OnLobbyJoin?.Invoke(this);
        public void TriggerLobbyJoinFailed(string reason) => OnLobbyJoinFailed?.Invoke(reason);
        public void TriggerLobbyClosed() => OnLobbyClosed?.Invoke(this);

        /// <summary>
        /// Resets IsDead on every lobby seat. Called at the start of each new run
        /// and via RunCoordinator.RunReset (which fires on restart / retry).
        /// </summary>
        public void ResetDeathFlags()
        {
            lock (_lock)
            {
                foreach (var p in players)
                    p.IsDead = false;
            }
        }

        /// <summary>
        /// Returns true only when the lobby has at least one player and every
        /// player in it is marked dead.
        /// </summary>
        public bool AreAllPlayersDead()
        {
            lock (_lock)
            {
                if (players.Count == 0) return false;
                foreach (var p in players)
                {
                    if (!p.IsDead) return false;
                }
                return true;
            }
        }
    }
}
