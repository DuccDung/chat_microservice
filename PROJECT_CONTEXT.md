# Project Context - SystemChatBoxRealtime

## 1. Muc dich du an

`SystemChatBoxRealtime` la he thong chat realtime theo mo hinh client-server. Du an tap trung vao cac chuc nang:

- Dang ky, dang nhap nguoi dung.
- Tim nguoi dung bang email.
- Tao va mo cuoc tro chuyen 1-1.
- Tai danh sach doan chat va lich su tin nhan.
- Gui tin nhan text, anh, ghi am.
- Cap nhat tin nhan realtime qua WebSocket.
- Thu nghiem goi video/audio bang WebRTC.

Solution hien tai gom 2 project .NET 9:

- `ApplicationServer`: backend API va tang truy cap database.
- `WebServer`: ung dung web MVC/Razor, giao dien nguoi dung, goi API backend, va quan ly WebSocket realtime cho browser.

## 2. Cau truc tong quan

```text
SystemChatBoxRealtime/
|-- ApplicationServer/
|   |-- Controllers/
|   |-- Dtos/
|   |-- Models/
|   |-- Program.cs
|   |-- appsettings.json
|   `-- ApplicationServer.csproj
|
|-- WebServer/
|   |-- Controllers/
|   |-- Dtos/
|   |-- Interfaces/
|   |-- Services/
|   |-- ViewModels/
|   |-- Views/
|   |-- wwwroot/
|   |-- Program.cs
|   |-- appsettings.json
|   `-- WebServer.csproj
|
|-- SystemChatBoxRealtime.sln
|-- README.md
`-- PROJECT_CONTEXT.md
```

## 3. ApplicationServer

`ApplicationServer` la ASP.NET Core Web API. Project nay chiu trach nhiem xu ly nghiep vu va luu doc du lieu thong qua Entity Framework Core.

Cong nghe chinh:

- ASP.NET Core Web API
- Entity Framework Core 9
- SQL Server
- DotNetEnv de load bien moi truong tu `.env`
- CORS cho phep `WebServer` goi API

File quan trong:

- `ApplicationServer/Program.cs`: cau hinh controller, OpenAPI, EF Core, CORS.
- `ApplicationServer/Models/SocialNetworkContext.cs`: DbContext va mapping cac bang SQL Server.
- `ApplicationServer/Controllers/AuthController.cs`: API dang nhap/dang ky.
- `ApplicationServer/Controllers/UsersController.cs`: API lay user theo id va tim user bang email.
- `ApplicationServer/Controllers/ConversationsController.cs`: API conversation, thread, message, peer info.

### API chinh

Authentication:

- `POST /api/auth/login`
- `POST /api/auth/register`

Users:

- `GET /api/users/{id}`
- `GET /api/users/search?email=...`

Conversations:

- `POST /api/conversations`
- `GET /api/conversations/threads?accountId=...`
- `GET /api/conversations/{conversationId}/messages?me=...&limit=...`
- `POST /api/conversations/{conversationId}/messages`
- `POST /api/conversations/{conversationId}/messages/image`
- `POST /api/conversations/{conversationId}/messages/audio`
- `GET /api/conversations/{conversationId}/peer?meId=...`

### Database models dang co

DbContext dang map cac entity:

- `Account`
- `Conversation`
- `ConversationMember`
- `Message`
- `Friendship`
- `Notification`
- `Post`
- `PostComment`
- `PostLike`
- `PostMedium`
- `PostShare`
- `Profile`

Chat hien tai dung nhieu nhat cac bang `account`, `conversations`, `conversation_members`, va `messages`.

## 4. WebServer

`WebServer` la ASP.NET Core MVC/Razor. Day la app nguoi dung truc tiep truy cap.

Trach nhiem chinh:

- Render man hinh login/register/chat.
- Luu phien dang nhap bang cookie authentication.
- Goi API sang `ApplicationServer` bang typed `HttpClient`.
- Luu file upload anh/voice vao `wwwroot/uploads`.
- Mo WebSocket endpoint `/ws`.
- Broadcast tin nhan realtime toi browser dang subscribe conversation.
- Xu ly signaling cho WebRTC call.

File quan trong:

- `WebServer/Program.cs`: cau hinh MVC, cookie auth, typed HttpClient, WebSocket endpoint `/ws`.
- `WebServer/Controllers/AuthController.cs`: login/register tren web, tao cookie auth.
- `WebServer/Controllers/HomeController.cs`: man hinh chat, partial views, gui message, upload image/audio, call popup.
- `WebServer/Services/AuthService.cs`: goi API auth cua ApplicationServer.
- `WebServer/Services/UserService.cs`: goi API user cua ApplicationServer.
- `WebServer/Services/ConversationService.cs`: goi API conversation/message cua ApplicationServer, dong thoi luu file upload local.
- `WebServer/Services/RealtimeHub.cs`: quan ly socket theo user va subscription theo conversation.
- `WebServer/Services/WebSocketHandler.cs`: doc message WebSocket tu browser va xu ly subscribe/call events.

## 5. Luong dang nhap

1. Browser mo `/Auth/Login`.
2. JavaScript login goi endpoint WebServer `/auth/login`.
3. `WebServer.Controllers.AuthController` goi `IAuthService.LoginAsync`.
4. `AuthService` goi `ApplicationServer` tai `POST /api/auth/login`.
5. Neu login thanh cong, WebServer tao cookie auth voi claim:
   - `ClaimTypes.NameIdentifier`: account id
   - `ClaimTypes.Name`: email
6. User vao duoc trang chat `Home/Main`.

Luu y: password hien tai dang duoc so sanh truc tiep dang plain text trong database.

## 6. Luong chat

### Tai danh sach thread

1. Client goi `/chat/threads`.
2. `HomeController.ThreadsView` lay account id tu cookie.
3. WebServer goi `ApplicationServer` endpoint `/api/conversations/threads?accountId=...`.
4. Ket qua duoc render bang partial `Views/Shared/Partials/_ChatThreads.cshtml`.

### Mo mot conversation

1. User click thread trong danh sach.
2. JS `handle_choice_item_threads.js` goi `/chat/conversation?conversationId=...`.
3. WebServer goi API `/api/conversations/{conversationId}/messages`.
4. Partial `_ConversationMessages.cshtml` duoc inject vao `#messageScroller`.
5. Client gui WebSocket message `subscribe` cho conversation dang mo.

### Gui text message

1. User nhap tin va bam send.
2. JS `chat_composer.js` goi `/chat/send_message`.
3. `HomeController.SendMessage` lay sender id tu cookie.
4. WebServer goi `ApplicationServer` endpoint `POST /api/conversations/{id}/messages`.
5. Sau khi API tao message thanh cong, WebServer goi `RealtimeHub.BroadcastToConversationAsync`.
6. Cac browser dang subscribe conversation nhan event `message-text`.

### Gui image/audio

Image:

- Browser gui multipart form toi `/chat/send_image`.
- `ConversationService.SendImageMessageAsync` luu file vao `WebServer/wwwroot/uploads/chat`.
- Sau do WebServer goi ApplicationServer de tao message type `image`.
- WebSocket broadcast event `message-image`.

Audio:

- Browser ghi am bang `MediaRecorder`.
- File gui toi `/chat/send_audio`.
- `ConversationService.SendAudioMessageAsync` luu file vao `WebServer/wwwroot/uploads/voice`.
- Sau do WebServer goi ApplicationServer de tao message type `audio`.
- WebSocket broadcast event `message-audio`.

## 7. Realtime WebSocket

WebSocket khong nam o `ApplicationServer`; hien tai no nam trong `WebServer`.

Endpoint:

- `GET /ws`

Khi browser connect:

- WebServer lay `userId` tu cookie auth.
- Neu khong co cookie, fallback lay query string `userId`.
- `WebSocketHandler` them socket vao `RealtimeHub`.

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

## 8. JavaScript frontend

Thu muc chinh:

- `WebServer/wwwroot/js/core/api.js`: tao Axios instance.
- `WebServer/wwwroot/js/services/chatService.js`: API client cho chat/call.
- `WebServer/wwwroot/js/services/ws-client.js`: ket noi WebSocket va dispatch event `ws:message`.
- `WebServer/wwwroot/js/pages/chat/main.js`: khoi tao trang chat, modal tim ban, load threads.
- `WebServer/wwwroot/js/pages/chat/threads.js`: load danh sach thread.
- `WebServer/wwwroot/js/pages/chat/handle_choice_item_threads.js`: click thread, load messages, subscribe conversation.
- `WebServer/wwwroot/js/pages/chat/chat_composer.js`: gui text/image/audio.
- `WebServer/wwwroot/js/pages/chat/chat_realtime.js`: nhan WebSocket message va append UI.
- `WebServer/wwwroot/js/pages/chat/webrtc-service.js`: WebRTC peer connection.
- `WebServer/wwwroot/js/pages/chat/video-call-ui.js`: UI va signaling video call.

Layout chat load nhieu file JS module trong:

- `WebServer/Views/Shared/_Layout.cshtml`

## 9. Views va UI

Man hinh chinh:

- `WebServer/Views/Auth/Login.cshtml`
- `WebServer/Views/Auth/Register.cshtml`
- `WebServer/Views/Home/Main.cshtml`

Partial quan trong:

- `_ChatThreads.cshtml`: render danh sach doan chat.
- `_ConversationMessages.cshtml`: render tin nhan.
- `_FormFriends.cshtml`: modal tim ban.
- `_FriendSearchResults.cshtml`: ket qua tim user.
- `_FormPersonal.cshtml`: thong tin user de tao/mo chat.
- `_CallPopup.cshtml`: popup cuoc goi.
- `_IncomingCallPopup.cshtml`: popup cuoc goi den.
- `_Topbar.cshtml`: thanh tren.

CSS chinh:

- `WebServer/wwwroot/css/pages/home/main.css`
- `WebServer/wwwroot/css/pages/login/main.css`
- `WebServer/wwwroot/css/pages/register/main.css`
- `WebServer/wwwroot/css/site.css`

## 10. Cau hinh va cach chay

Launch settings:

ApplicationServer:

- HTTP: `http://localhost:5007`
- HTTPS: `https://localhost:7231`

WebServer:

- HTTP: `http://localhost:5296`
- HTTPS: `https://localhost:7268`

`ApplicationServer/Program.cs` doc connection string tu bien moi truong:

```text
DB_Connection
```

CORS doc origin cua WebServer tu:

```text
WebServer_Origin
```

`WebServer` doc base URL API tu cau hinh:

```json
{
  "ApiClients": {
    "Auth": {
      "BaseUrl": "https://localhost:7231/"
    }
  }
}
```

Neu config nay khong nam trong `appsettings.json`, hay kiem tra `.env`, user secrets, hoac cau hinh local cua nguoi phat trien.

## 11. Luu y ky thuat va rui ro

- Password hien tai dang luu/so sanh plain text. Nen hash password truoc khi dung that.
- `ApplicationServer/appsettings.json` co connection string SQL Server mau. Nen chuyen secret sang `.env`, user secrets, hoac secret manager.
- WebSocket realtime hien nam trong WebServer, khong phai ApplicationServer. Neu scale nhieu instance WebServer, can Redis pub/sub hoac backplane tuong duong.
- Upload file luu local trong `WebServer/wwwroot/uploads`. Neu deploy container hoac multi-instance, can volume/shared storage/object storage.
- `Message.IsRead` la bool global, chua du de read receipt theo tung user trong group chat.
- README hien co ve noi dung tong quat nhung bi loi encoding khi doc trong terminal.
- Git worktree co the co file build `bin/obj` bi modified va file upload phat sinh. Can can than khong commit nham cac file runtime/build.
- Mot so JS co logging/debug va TODO, dac biet menu thread, delete/archive, profile, voice call.
- WebRTC hien dung STUN Google, chua co TURN. Goi video co the loi o mot so mang NAT/firewall.

## 12. Nen doc file nao dau tien khi vao du an

Thu tu goi y:

1. `SystemChatBoxRealtime.sln`
2. `ApplicationServer/Program.cs`
3. `ApplicationServer/Controllers/ConversationsController.cs`
4. `ApplicationServer/Models/SocialNetworkContext.cs`
5. `WebServer/Program.cs`
6. `WebServer/Controllers/HomeController.cs`
7. `WebServer/Services/RealtimeHub.cs`
8. `WebServer/Services/WebSocketHandler.cs`
9. `WebServer/wwwroot/js/services/ws-client.js`
10. `WebServer/wwwroot/js/pages/chat/chat_composer.js`
11. `WebServer/wwwroot/js/pages/chat/chat_realtime.js`
12. `WebServer/wwwroot/js/pages/chat/video-call-ui.js`

