# Changelog

## [1.0.0] - 2026-02-10

### Added
- Initial release of Unity Connection Management package.
- State-machine-based connection lifecycle (Offline, ClientConnecting, ClientConnected, ClientReconnecting, StartingHost, Hosting).
- Direct IP connection method via Unity Transport.
- Unity Relay connection method via Session integration.
- Connection approval with payload validation, server full check, build type check, and duplicate connection check.
- Automatic client reconnection with configurable attempts.
- Session management (create, join, quick join, reconnect, leave, delete).
- PubSub messaging infrastructure for connection events.
- Profile management with authentication support.
- Client preferences persistence.
