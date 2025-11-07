using Il2Cpp;
using Il2CppAssets.Scripts.Actors.Player;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using Multibonk.Game.Handlers;
using Multibonk.Networking.Comms.Base.Packet;
using Multibonk.Networking.Comms.Multibonk.Networking.Comms;
using Multibonk.Networking.Lobby;
using System.Linq;
using UnityEngine;
using static Il2Cpp.AnimatedMeshScriptableObject;
using static MelonLoader.MelonLaunchOptions;

namespace Multibonk.Game.Handlers.NetworkNotify
{
    public class GameLoadedEventHandler : GameEventHandler
    {
        private readonly NetworkService networkService;

        public GameLoadedEventHandler(LobbyContext lobbyContext, NetworkService networkService)
        {
            this.networkService = networkService;

            GameEvents.GameLoadedEvent += () =>
            {
                // Clean up any existing network players from previous games
                DebugLogger.Log("=== GAME LOADED - CLEANING UP OLD PLAYERS ===");
                GameFunctions.CleanupAllNetworkPlayers();
                
                // CLIENT: Send position to host
                if (!LobbyPatchFlags.IsHosting && lobbyContext.State == LobbyState.Connected)
                {
                    if (MyPlayer.Instance != null)
                    {
                        var position = MyPlayer.Instance.transform.position;
                        var rotation = MyPlayer.Instance.transform.rotation;
                        var packet = new SendGameLoadedPacket(position, rotation);
                        networkService.GetClientService().Enqueue(packet);
                        DebugLogger.LogSpawn($"Client sent spawn position: ({position.x}, {position.y}, {position.z})");
                    }
                    return;
                }

                // HOST: Wait a moment for clients to send their positions, then spawn everyone
                if (!LobbyPatchFlags.IsHosting)
                    return;

                DebugLogger.LogSpawn("=== GAME LOADED EVENT (HOST) ===");
                DebugLogger.LogSpawn($"MyPlayer exists: {MyPlayer.Instance != null}");
                if (MyPlayer.Instance != null)
                {
                    var pos = MyPlayer.Instance.transform.position;
                    DebugLogger.LogSpawn($"MyPlayer position: ({pos.x}, {pos.y}, {pos.z})");
                    DebugLogger.LogSpawn($"MyPlayer active: {MyPlayer.Instance.gameObject.activeSelf}");
                }

                // Wait a short delay for client positions to arrive
                System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
                {
                    try
                    {
                        GameDispatcher.Enqueue(() => SpawnAllPlayers(lobbyContext));
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Error($"Error scheduling SpawnAllPlayers: {ex}");
                    }
                });
            };
        }

        private void SpawnAllPlayers(LobbyContext lobbyContext)
        {
            try
            {
                DebugLogger.LogSpawn("Host is spawning players for all clients");

            var allPlayers = lobbyContext.GetPlayers().Where(p => p.Connection != null).ToList();
            DebugLogger.LogSpawn($"Total players to spawn: {allPlayers.Count}");

            // For each connected client, send them spawn packets for ALL other players (including host)
            foreach (var client in allPlayers)
            {
                DebugLogger.LogSpawn($"Processing client: {client.Name} (UUID: {client.UUID})");
                DebugLogger.LogSpawn($"Client selected character: {client.SelectedCharacter}");

                // Skip players who haven't selected a character yet
                if (client.SelectedCharacter == null || client.SelectedCharacter.Length == 0 || client.SelectedCharacter == "None")
                {
                    DebugLogger.Warning($"Client {client.Name} hasn't selected a character yet, skipping spawn");
                    continue;
                }

                var cPos = client.SpawnPosition;
                DebugLogger.LogSpawn($"Client spawn position: ({cPos.x}, {cPos.y}, {cPos.z})");

                // If client position is not set (Vector3.zero), use host position as fallback
                var spawnPos = client.SpawnPosition;
                var spawnRot = client.SpawnRotation;
                
                if (spawnPos == Vector3.zero)
                {
                    DebugLogger.Warning($"Client {client.Name} position not received yet, using host position as fallback");
                    if (MyPlayer.Instance != null)
                    {
                        spawnPos = MyPlayer.Instance.transform.position;
                        spawnRot = MyPlayer.Instance.transform.rotation;
                    }
                }

                // Spawn this client's player on the host using THEIR position (or fallback)
                var clientCharacter = Enum.Parse<ECharacter>(client.SelectedCharacter);
                try
                {
                    GameFunctions.SpawnNetworkPlayer(client.UUID, clientCharacter, spawnPos, spawnRot);
                    DebugLogger.LogSpawn($"Spawned {client.Name} locally on host at ({spawnPos.x}, {spawnPos.y}, {spawnPos.z})");
                }
                catch (Exception spawnEx)
                {
                    DebugLogger.Error($"Failed to spawn {client.Name}: {spawnEx.Message}");
                    DebugLogger.Error($"Stack: {spawnEx.StackTrace}");
                    continue; // Skip sending spawn packets for this player if spawn failed
                }

                // Send spawn packets to this client for ALL other players
                foreach (var otherPlayer in allPlayers)
                {
                    if (otherPlayer.UUID == client.UUID)
                        continue; // Don't send spawn packet for themselves

                    // Skip players who haven't selected a character
                    if (otherPlayer.SelectedCharacter == null || otherPlayer.SelectedCharacter.Length == 0 || otherPlayer.SelectedCharacter == "None")
                        continue;

                    var otherPos = otherPlayer.SpawnPosition;
                    var otherRot = otherPlayer.SpawnRotation;
                    
                    // Use fallback if position not received
                    if (otherPos == Vector3.zero && MyPlayer.Instance != null)
                    {
                        otherPos = MyPlayer.Instance.transform.position;
                        otherRot = MyPlayer.Instance.transform.rotation;
                    }

                    var otherCharacter = Enum.Parse<ECharacter>(otherPlayer.SelectedCharacter);
                    var spawnPacket = new SendSpawnPlayerPacket(otherCharacter, otherPlayer.UUID, otherPos, otherRot);
                    client.Connection.EnqueuePacket(spawnPacket);
                    DebugLogger.LogSpawn($"Sent spawn packet to {client.Name} for player {otherPlayer.Name} at ({otherPos.x}, {otherPos.y}, {otherPos.z})");
                }

                // Also send HOST player spawn to clients
                var myUuid = lobbyContext.GetMyself().UUID;
                var myCharacter = Enum.Parse<ECharacter>(lobbyContext.GetMyself().SelectedCharacter);
                var myPosition = MyPlayer.Instance.transform.position;
                var myRotation = MyPlayer.Instance.transform.rotation;
                var hostSpawnPacket = new SendSpawnPlayerPacket(myCharacter, myUuid, myPosition, myRotation);
                client.Connection.EnqueuePacket(hostSpawnPacket);
                DebugLogger.LogSpawn($"Sent HOST spawn packet to {client.Name} at ({myPosition.x}, {myPosition.y}, {myPosition.z})");
            }

            DebugLogger.LogSpawn("Finished spawning all players");
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error in SpawnAllPlayers: {ex.Message}");
                DebugLogger.Error($"Stack trace: {ex.StackTrace}");
            }
        }

    }
}
