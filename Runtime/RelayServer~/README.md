# Relay Server

WebSocket relay server powered by [Bun](https://bun.sh/).

## Running

- **Windows:** `LaunchRelayServer-Windows.bat`
- **Linux:** `LaunchRelayServer-Linux.sh`
- **macOS:** `LaunchRelayServer-MacOS.sh`

Default port **80** (`ws://localhost`) when no TLS certificates are present. Place `ssl.crt` and `ssl.key` one folder above the server directory to enable **WSS** on port **443** (`wss://localhost`).

Export the server via **Tools → WebSocket Relay → Export Server** before running these scripts.
