# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SystemChatBoxRealtime is a real-time chat system built with ASP.NET Core 9 microservices. It supports text messaging, image/audio sharing, and WebRTC video/audio calls via WebSocket.

## Architecture

**6-project .NET 9 solution:**

- **AuthService** - ASP.NET Core Web API: authentication, user management, conversations, messages, notifications. Uses Entity Framework Core with SQL Server. Ports: HTTP 5007, HTTPS 7231.
- **UserService** - User information and search service.
- **MessageCallService** - Conversations, threads, messages, and peer info management.
- **NotificationService** - Notification handling.
- **SharedKernel** - Shared models/DTOs. Links source files from AuthService (Dtos/, Models/) via compile includes, avoiding duplication.
- **WebServer** - ASP.NET Core MVC/Razor frontend: renders UI, calls backend APIs via typed HttpClient, manages WebSocket endpoint `/ws` for realtime browser communication, handles WebRTC signaling. Ports: HTTP 5296, HTTPS 7268.

**Key flow:** Browser → WebServer (MVC + WebSocket) → Backend Services (Web API)

## Build & Run Commands

```bash
# Build entire solution
dotnet build SystemChatBoxRealtime.sln

# Run individual services
dotnet run --project AuthService/AuthService.csproj
dotnet run --project WebServer/WebServer.csproj
dotnet run --project UserService/UserService.csproj
dotnet run --project MessageCallService/MessageCallService.csproj
dotnet run --project NotificationService/NotificationService.csproj

# Build specific project
dotnet build AuthService/AuthService.csproj

# Run with specific configuration
dotnet run --project AuthService/AuthService.csproj --configuration Release
```

## Configuration

**AuthService** reads from environment variables (via DotNetEnv):
- `DB_Connection` - SQL Server connection string
- `WebServer_Origin` - CORS origin for WebServer

**WebServer** reads API base URLs from `appsettings.json`:
```json
{
  "ApiClients": {
    "Auth": { "BaseUrl": "https://localhost:7231/" }
  }
}
```

Check `.env`, user secrets, or local config if settings aren't in `appsettings.json`.

## WebSocket Realtime

Endpoint: `GET /ws` in WebServer (not in AuthService)

Client messages: `subscribe`, `unsubscribe`, `ping`, `call.send`
Server events: `hello`, `pong`, `subscribed`, `unsubscribed`, `message-text`, `message-image`, `message-audio`, `call.event`

User identity from cookie auth fallback to query string `userId`.

## Key Files to Read First

1. `AuthService/Program.cs` - service configuration, EF Core, CORS
2. `AuthService/Models/SocialNetworkContext.cs` - DbContext, entity mappings
3. `WebServer/Program.cs` - MVC, cookie auth, HttpClient, WebSocket `/ws`
4. `WebServer/Controllers/HomeController.cs` - chat screens, message sending
5. `WebServer/Services/RealtimeHub.cs` - socket management per user/conversation
6. `WebServer/Services/WebSocketHandler.cs` - WebSocket message handling
7. `SharedKernel/` - shared DTOs and models

## Frontend JavaScript (modular)

- `WebServer/wwwroot/js/core/api.js` - Axios instance
- `WebServer/wwwroot/js/services/ws-client.js` - WebSocket client, dispatches `ws:message`
- `WebServer/wwwroot/js/pages/chat/` - chat_composer.js, chat_realtime.js, threads.js, webrtc-service.js, video-call-ui.js

Layout loads JS modules in `WebServer/Views/Shared/_Layout.cshtml`.

## Known Technical Issues

- Passwords stored/compared as plain text (should hash)
- File uploads saved locally in `WebServer/wwwroot/uploads` (needs shared storage for multi-instance)
- WebSocket in single WebServer instance (needs Redis pub/sub for scale-out)
- `Message.IsRead` is global bool (insufficient for per-user read receipts in group chat)
- WebRTC uses Google STUN only (no TURN server - may fail on restricted networks)
- Some controllers in AuthService are compiled but not hosted (linked in SharedKernel via source includes)

## Docker

Each service has a Dockerfile targeting .NET 9 ASP.NET runtime. Build context is solution root.

```bash
docker build -f AuthService/Dockerfile -t authservice .
docker build -f WebServer/Dockerfile -t webserver .
```
