# Multibonk Multiplayer Mod - Installation Guide

## Requirements
- **Megabonk** game (Steam version)
- **MelonLoader** (will be installed automatically)

## Installation Steps

### 1. Install MelonLoader
1. Download MelonLoader from: https://github.com/LavaGang/MelonLoader/releases/latest
2. Download `MelonLoader.x64.zip` (for 64-bit games)
3. Extract the zip file
4. Run `MelonLoader.Installer.exe`
5. Click "Select" and browse to your Megabonk installation folder:
   - Default: `C:\Program Files (x86)\Steam\steamapps\common\Megabonk\Megabonk.exe`
   - Or: `D:\SteamLibrary\steamapps\common\Megabonk\Megabonk.exe`
6. Click "Install" and wait for completion
7. Click "OK" when done

### 2. Install Multibonk Mod
1. Locate your Megabonk installation folder (same as above)
2. Open the `Mods` folder (created by MelonLoader)
   - If it doesn't exist, run the game once and it will be created
3. Copy **ALL DLL files** from the mod package into the `Mods` folder:
   - `Multibonk.dll`
   - `Microsoft.Extensions.DependencyInjection.dll`
   - `Microsoft.Extensions.DependencyInjection.Abstractions.dll`
4. That's it!

### 3. Verify Installation
1. Launch Megabonk
2. Wait for the game to fully load
3. Press **F5** to toggle the multiplayer menu
4. You should see the connection window!

## How to Use

### Hosting a Game
1. Press **F5** in the main menu
2. Enter your player name
3. Click "🖥️ Start Server (Host)"
4. Choose your character
5. Share your IP address with friends (they need to connect to your IP:25565)
6. Click "Start Game" when everyone is ready

### Joining a Game
1. Press **F5** in the main menu
2. Enter your player name
3. Enter the host's IP address and port (format: `192.168.1.100:25565`)
4. Click "🔌 Connect to Server"
5. Choose your character
6. Wait for the host to start the game

### Controls
- **F5**: Toggle multiplayer menu (hide/show)
- **F6**: Spawn test player (debug, when hosting solo)
- **Tab**: Switch between Name/IP fields in connection window
- **Enter/Escape**: Deactivate text field

## Network Setup

### Playing Over Internet
Both players need port forwarding or use **Radmin VPN**:
1. Download Radmin VPN: https://www.radmin-vpn.com/
2. Both players create accounts and join the same network
3. Use the Radmin IP address to connect (format: `26.x.x.x:25565`)

### Playing on LAN
- Host uses their local IP (find with `ipconfig` in CMD)
- Default port: 25565
- Format: `192.168.1.100:25565`

## Features
✅ Full multiplayer synchronization
✅ Character selection
✅ Movement and animation sync
✅ Enemy spawning sync
✅ Player interaction sync
✅ Chest and shrine sync
✅ Player damage and death sync
✅ Customizable gameplay rules

## Troubleshooting

### "Could not find MelonLoader"
- Make sure MelonLoader is installed correctly
- Run the game once before installing the mod

### "Connection refused"
- Check firewall settings
- Verify port forwarding (if playing over internet)
- Make sure host started the server first

### "Text fields not working"
- Click on the field to activate it
- Type normally
- Press Tab to switch fields
- Press Enter or Escape to finish editing

### Game crashes when joining
- Make sure both players have the SAME version of the mod
- Check logs in: `Megabonk/MelonLoader/Latest.log`
- Send logs to mod developer for help

## Version Info
- **Mod Version**: 1.0.0
- **Author**: guilhermeljs
- **Built**: November 7, 2025

## Support
If you encounter issues, check the logs at:
`<Megabonk Install>\MelonLoader\Latest.log`

Happy bonking! 🎮
