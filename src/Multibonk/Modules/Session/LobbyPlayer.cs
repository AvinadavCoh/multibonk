using Multibonk.Net;

namespace Multibonk.Modules.Session
{
    /// <summary>One player in the current session's lobby roster, host or client role.</summary>
    public sealed class LobbyPlayer
    {
        public ushort Uuid { get; set; }

        public string Name { get; set; }

        /// <summary>Selected character name (e.g. "Warrior"), or empty until CHARACTER_SELECTION arrives.</summary>
        public string Character { get; set; }

        /// <summary>
        /// Host-side only: the connection this player is reachable on. Null for the
        /// host's own local entry (matches legacy LobbyPlayer, whose Connection is null
        /// for the host and is checked with <c>?.</c> before sending). Always null on
        /// the client side - clients never hold peer connections.
        /// </summary>
        public Connection Connection { get; set; }

        /// <summary>True for the single entry representing "me" - the local host or the local client.</summary>
        public bool IsLocal { get; set; }
    }
}
