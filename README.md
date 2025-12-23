# Encrypted Messaging

End-to-end encrypted messaging system with a terminal UI client and REST API server.

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![SQLite](https://img.shields.io/badge/SQLite-3-003B57?logo=sqlite)
![Docker](https://img.shields.io/badge/Docker-Ready-2496ED?logo=docker)

## Features

- **End-to-End Encryption**: Messages are encrypted client-side using AES-256. The server only stores ciphertext.
- **Deterministic Key Derivation**: Keys derived from username + password using PBKDF2 (100k iterations). No key exchange needed.
- **Real-time Notifications**: WebSocket-based message notifications when in active chat.
- **Session Persistence**: Stay logged in between app restarts.
- **Interactive Chat**: WhatsApp-style conversation view with message history navigation.
- **Cross-Platform**: Client runs on Windows, Linux, and macOS.

## Architecture

```
┌─────────────────┐         HTTPS/WSS          ┌─────────────────┐
│                 │ ◄──────────────────────────► │                 │
│  TUI Client     │    Encrypted Messages       │  REST API       │
│  (Spectre.Con)  │    (AES-256 ciphertext)     │  (ASP.NET)      │
│                 │                              │                 │
└─────────────────┘                              └────────┬────────┘
                                                          │
                                                          ▼
                                                 ┌─────────────────┐
                                                 │    SQLite DB    │
                                                 │  (Encrypted     │
                                                 │   messages)     │
                                                 └─────────────────┘
```

## Quick Start

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Docker (for server deployment)

### Run the Client

```bash
cd client
dotnet run
```

The client connects to `http://localhost:5000` by default. You can change the server URL from the main menu.

### Run the Server Locally

```bash
cd server
dotnet run
```

Server starts on `http://localhost:5000` by default.

### Deploy Server with Docker

```bash
cd docker
docker compose up -d
```

Server runs on port 8080 inside the container, mapped to host port 8080.

## Project Structure

```
EncryptedMessaging/
├── client/                    # TUI client application
│   ├── models/               # DTOs for API communication
│   ├── security/             # Key derivation and encryption
│   ├── services/             # API client, WebSocket, session manager
│   └── ui/                   # Spectre.Console UI
├── server/                    # REST API server
│   ├── data/                 # SQLite repositories
│   ├── models/               # Domain models and DTOs
│   └── services/             # JWT and WebSocket services
├── docker/                    # Docker deployment files
├── scripts/                   # Build scripts
│   └── build-client.sh       # Build standalone binaries
└── tests/                     # Unit tests
```

## Client Commands

### In Chat Mode

| Command | Description |
|---------|-------------|
| `/up` | Load 20 older messages |
| `/latest` | Jump to most recent messages |
| `/search` | Search through conversation history |
| `/back` | Exit chat and return to menu |

### Navigation

- Arrow keys to navigate menus
- Type to search/filter in lists
- Enter to select

## Building Standalone Binaries

```bash
./scripts/build-client.sh
```

Creates self-contained executables:
- `dist/linux-x64/EncryptedMessaging`
- `dist/win-x64/EncryptedMessaging.exe`

## Security Model

### Key Derivation

```
Password + Username
        │
        ▼
    PBKDF2 (100k iterations, SHA256)
        │
        ▼
    64-byte seed
        │
        ├──► SHA256 ──► Private Key (32 bytes)
        │
        └──► SHA256(Private Key) ──► Public Key (stored on server)
```

### Shared Secret (for message encryption)

```
Both parties compute:
    sorted(PublicKeyA, PublicKeyB)
            │
            ▼
        SHA256 ──► Shared Secret (32 bytes)
```

This ensures both sender and receiver derive the same key without exchanging secrets.

### Message Encryption

- Algorithm: AES-256-CBC
- IV: Random 16 bytes (prepended to ciphertext)
- Padding: PKCS7

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/register` | Register new user |
| POST | `/api/auth/login` | Login and get JWT |
| GET | `/api/users` | List all users |
| GET | `/api/users/{username}` | Get user's public key |
| POST | `/api/messages` | Send encrypted message |
| GET | `/api/messages/received` | Get received messages |
| GET | `/api/messages/sent` | Get sent messages |
| PATCH | `/api/messages/{id}/read` | Mark message as read |
| GET | `/health` | Health check |

## Configuration

### Server Environment Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `JWT_SECRET_KEY` | **Yes (Production)** | Dev fallback | JWT signing key (min 32 chars). Generate with: `openssl rand -base64 32` |
| `DATABASE_PATH` | No | `data/messages.db` | SQLite database path |
| `Jwt:Issuer` | No | `EncryptedMessaging` | JWT issuer claim |
| `Jwt:Audience` | No | `EncryptedMessagingUsers` | JWT audience claim |
| `Jwt:AccessTokenExpirationMinutes` | No | `60` | Token lifetime in minutes |

**⚠️ Security Note**: In Production, the server will **fail to start** if `JWT_SECRET_KEY` is not set. In Development, a warning is logged and a dev-only fallback key is used.

### Client Configuration

Server URL can be changed at runtime from the login menu or by modifying `Config.cs`.

## Development

### Run Tests

```bash
dotnet test
```

### Build All Projects

```bash
dotnet build EncryptedMessaging.slnx
```

## License

GPL-3.0
