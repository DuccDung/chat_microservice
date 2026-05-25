# SystemChatBoxRealtime

He thong chat realtime theo mo hinh microservice, xay dung bang ASP.NET Core 9. Ung dung ho tro dang ky/dang nhap, tim nguoi dung, tao hoi thoai 1-1, gui tin nhan text/anh/voice, cap nhat realtime bang WebSocket va thu nghiem goi audio/video bang WebRTC.

## Kien truc tong quan

Solution gom 6 project .NET 9:

| Project | Vai tro | Dev port |
| --- | --- | --- |
| `AuthService` | Dang nhap, dang ky | HTTP `5007`, HTTPS `7231` |
| `UserService` | Thong tin nguoi dung, tim user theo email | HTTP `5183` |
| `MessageCallService` | Conversation, thread, message, peer info | HTTP `5226` |
| `NotificationService` | Notification API | HTTP `5206` |
| `SharedKernel` | Shared model/DTO va EF Core model dung chung | - |
| `WebServer` | MVC/Razor UI, cookie auth, upload file, WebSocket `/ws`, WebRTC signaling | HTTP `5296`, HTTPS `7268` |

Luong chinh:

```text
Browser
  -> WebServer (MVC/Razor + Cookie Auth + WebSocket)
  -> AuthService / UserService / MessageCallService / NotificationService
  -> SQL Server
```

Trong Docker, Nginx gateway route public traffic vao `WebServer` va co the proxy cac route `/api/*` den backend service tuong ung.

## Chuc nang chinh

- Dang ky va dang nhap nguoi dung.
- Tim nguoi dung bang email.
- Tao hoac mo conversation 1-1.
- Tai danh sach thread va lich su tin nhan.
- Gui tin nhan text.
- Upload va gui anh trong chat.
- Ghi am va gui voice message.
- Broadcast tin nhan realtime qua WebSocket.
- Signaling goi audio/video bang WebRTC.

## Cau truc thu muc

```text
SystemChatBoxRealtime/
|-- AuthService/
|   |-- Controllers/
|   |-- Dtos/
|   |-- Models/
|   |-- Services/
|   |-- Program.cs
|   `-- Dockerfile
|-- UserService/
|-- MessageCallService/
|-- NotificationService/
|-- SharedKernel/
|-- WebServer/
|   |-- Controllers/
|   |-- Dtos/
|   |-- Interfaces/
|   |-- Services/
|   |-- ViewModels/
|   |-- Views/
|   |-- wwwroot/
|   |-- Program.cs
|   `-- Dockerfile
|-- gateway/
|   `-- nginx.conf
|-- docker-compose.yml
|-- data.sql
|-- PROJECT_CONTEXT.md
|-- Tasks.md
`-- SystemChatBoxRealtime.sln
```

## Service backend

`AuthService` compile rieng controller auth. Cac controller/service cho user, conversation va notification dang nam trong cay `AuthService`, sau do duoc cac project service rieng link source qua `.csproj`:

- `UserService` link `UsersController`, `UserService`, `IUserService`.
- `MessageCallService` link `ConversationsController`, `MessageCallService`, `IMessageCallService`.
- `NotificationService` link `NotificationsController`, `NotificationService`, `INotificationService`.

Tat ca service backend dung chung `SocialNetworkContext` va SQL Server.

## API chinh

Authentication:

- `POST /api/auth/login`
- `POST /api/auth/register`

Users:

- `GET /api/users/{id}`
- `GET /api/users/search?email=...`

Conversations/messages:

- `POST /api/conversations`
- `GET /api/conversations/threads?accountId=...`
- `GET /api/conversations/{conversationId}/messages?me=...&limit=...`
- `POST /api/conversations/{conversationId}/messages`
- `POST /api/conversations/{conversationId}/messages/image`
- `POST /api/conversations/{conversationId}/messages/audio`
- `GET /api/conversations/{conversationId}/peer?meId=...`

Health:

- `GET /health` co trong `UserService`, `MessageCallService`, `NotificationService`.
- `gateway/nginx.conf` co route `/health` tra ve `healthy`.
- `AuthService` va `WebServer` hien chua map endpoint `/health` rieng trong code.

## WebSocket realtime

WebSocket endpoint nam trong `WebServer`:

```text
GET /ws
```

User identity duoc lay tu cookie auth claim `ClaimTypes.NameIdentifier`; neu khong co cookie thi fallback bang query string `userId`.

Client co the gui:

```json
{ "type": "subscribe", "conversationId": 3 }
```

```json
{ "type": "unsubscribe", "conversationId": 3 }
```

```json
{ "type": "ping" }
```

```json
{
  "type": "call.send",
  "toUserId": "2",
  "payload": {}
}
```

Server co the gui ve:

- `hello`
- `pong`
- `subscribed`
- `unsubscribed`
- `message-text`
- `message-image`
- `message-audio`
- `call.event`

## Frontend

Man hinh chinh:

- `WebServer/Views/Auth/Login.cshtml`
- `WebServer/Views/Auth/Register.cshtml`
- `WebServer/Views/Home/Main.cshtml`

JavaScript quan trong:

- `WebServer/wwwroot/js/core/api.js`
- `WebServer/wwwroot/js/services/chatService.js`
- `WebServer/wwwroot/js/services/ws-client.js`
- `WebServer/wwwroot/js/pages/chat/main.js`
- `WebServer/wwwroot/js/pages/chat/threads.js`
- `WebServer/wwwroot/js/pages/chat/chat_composer.js`
- `WebServer/wwwroot/js/pages/chat/chat_realtime.js`
- `WebServer/wwwroot/js/pages/chat/webrtc-service.js`
- `WebServer/wwwroot/js/pages/chat/video-call-ui.js`

## Cau hinh

Backend services doc connection string tu bien moi truong:

```text
DB_Connection
```

`AuthService` doc CORS origin tu:

```text
WebServer_Origin
```

`WebServer` doc base URL backend tu `appsettings.json` hoac environment variables:

```json
{
  "ApiClients": {
    "Auth": { "BaseUrl": "http://localhost:5007" },
    "Users": { "BaseUrl": "http://localhost:5183" },
    "Conversations": { "BaseUrl": "http://localhost:5226" },
    "Notifications": { "BaseUrl": "http://localhost:5206" }
  }
}
```

Trong Docker compose, cac URL duoc override bang service name:

```text
ApiClients__Auth__BaseUrl=http://authservice:8080
ApiClients__Users__BaseUrl=http://userservice:8080
ApiClients__Conversations__BaseUrl=http://messagecallservice:8080
ApiClients__Notifications__BaseUrl=http://notificationservice:8080
```

## Chay local bang dotnet

Can co SQL Server va database `social_network`. Neu dung file `data.sql`, import vao SQL Server truoc khi chay service.

```bash
dotnet build SystemChatBoxRealtime.sln

dotnet run --project AuthService/AuthService.csproj
dotnet run --project UserService/UserService.csproj
dotnet run --project MessageCallService/MessageCallService.csproj
dotnet run --project NotificationService/NotificationService.csproj
dotnet run --project WebServer/WebServer.csproj
```

Mo web:

```text
http://localhost:5296
```

## Chay bang Docker

Build image:

```bash
docker build -f AuthService/Dockerfile -t authservice:test .
docker build -f UserService/Dockerfile -t userservice:test .
docker build -f MessageCallService/Dockerfile -t messagecallservice:test .
docker build -f NotificationService/Dockerfile -t notificationservice:test .
docker build -f WebServer/Dockerfile -t webserver:test .
```

Chay compose:

```bash
docker compose up -d
```

Gateway public:

```text
http://localhost
```

SQL Server trong compose:

```text
localhost:1433
Database: social_network
User: sa
Password: Strong!Pass123
```

Luu y: `docker-compose.yml` hien dung image da build san (`*:test`), khong dung `build:` truc tiep.

## Luu y ky thuat

- Password hien dang luu/so sanh plain text; can hash password truoc khi dung production.
- Upload anh/voice dang luu local trong `WebServer/wwwroot/uploads`; neu scale nhieu instance can shared volume hoac object storage.
- WebSocket state dang nam trong memory cua tung `WebServer`; neu scale out can Redis pub/sub hoac backplane tuong duong.
- WebRTC hien dung STUN, chua co TURN; co the loi tren mang NAT/firewall han che.
- `Message.IsRead` la bool global, chua du cho read receipt theo tung user.
- Worktree co the phat sinh file `bin/obj` va upload runtime; khong nen commit cac file build/runtime.
