# Tasks - Docker, Gateway va Scaling

Muc tieu: cap nhat trang thai trien khai Docker cho he thong `SystemChatBoxRealtime`, ghi ro viec da co trong repo va cac viec con lai truoc khi dua len moi truong production.

## Trang thai hien tai

| Hang muc | Trang thai | Ghi chu |
| --- | --- | --- |
| Dockerfile cho `AuthService` | Done | Expose `8080`, build tu solution root |
| Dockerfile cho `WebServer` | Done | Expose `8080`, build tu solution root |
| Dockerfile cho `UserService` | Done | Co link source tu `AuthService` |
| Dockerfile cho `MessageCallService` | Done | Co link source tu `AuthService` |
| Dockerfile cho `NotificationService` | Done | Co link source tu `AuthService` |
| `docker-compose.yml` | Done co ban | Dung SQL Server, gateway, 5 app services |
| Nginx gateway | Done co ban | Route public vao `WebServer`, proxy `/api/*` va `/ws` |
| SQL Server container | Done co ban | Co volume va healthcheck |
| Health endpoint | Partial | Co o 3 backend service moi; thieu `AuthService` va `WebServer` |
| Redis backplane | Not started | Can cho WebSocket scale-out |
| Shared/object storage cho uploads | Not started | Can cho scale nhieu `WebServer` |
| CI/CD | Not started | Chua co pipeline build/push image |

## Checklist tiep theo

### Phase 1 - Xac minh Docker hien co

- [x] Tao Dockerfile cho `UserService`
- [x] Tao Dockerfile cho `MessageCallService`
- [x] Tao Dockerfile cho `NotificationService`
- [x] Cap nhat Dockerfile `AuthService` expose port container `8080`
- [x] Cap nhat Dockerfile `WebServer` expose port container `8080`
- [x] Tao `docker-compose.yml`
- [x] Tao `gateway/nginx.conf`
- [ ] Build lai tat ca image bang tag dung trong compose:

```bash
docker build -f AuthService/Dockerfile -t authservice:test .
docker build -f UserService/Dockerfile -t userservice:test .
docker build -f MessageCallService/Dockerfile -t messagecallservice:test .
docker build -f NotificationService/Dockerfile -t notificationservice:test .
docker build -f WebServer/Dockerfile -t webserver:test .
```

- [ ] Chay `docker compose up -d`
- [ ] Kiem tra gateway tai `http://localhost`
- [ ] Kiem tra log cua tung service neu gateway tra loi loi
- [ ] Import/restore database `social_network` vao SQL Server container neu database rong

### Phase 2 - Database

- [x] Them SQL Server service bang image `mcr.microsoft.com/mssql/server:2022-latest`
- [x] Them `ACCEPT_EULA=Y`
- [x] Them `MSSQL_PID=Developer`
- [x] Map port `1433:1433`
- [x] Them volume `sqlserver_data`
- [x] Them healthcheck SQL Server
- [ ] Chon va chuan hoa cach khoi tao database:
  - Import `data.sql`
  - EF migration
  - DbInitializer trong code
- [ ] Khong hard-code password SQL Server trong compose production

### Phase 3 - API Gateway

- [x] Chon Nginx lam gateway co ban
- [x] Proxy route `/` ve `WebServer`
- [x] Proxy route `/ws` ve `WebServer` voi header WebSocket
- [x] Proxy `/api/auth/` ve `AuthService`
- [x] Proxy `/api/users/` ve `UserService`
- [x] Proxy `/api/conversations/` ve `MessageCallService`
- [x] Proxy `/api/notifications/` ve `NotificationService`
- [x] Them gateway `/health`
- [ ] Kiem tra lai `proxy_pass` cho cac route `/api/*` de dam bao path sau khi rewrite dung voi controller route
- [ ] Them SSL/TLS cho production
- [ ] Can nhac sticky session hoac Redis/session store neu scale `WebServer`

### Phase 4 - Health checks

- [x] `UserService`: `GET /health`
- [x] `MessageCallService`: `GET /health`
- [x] `NotificationService`: `GET /health`
- [ ] `AuthService`: them `GET /health`
- [ ] `WebServer`: them `GET /health`
- [ ] Them Docker healthcheck cho tung app service
- [ ] Doi `depends_on` sang health condition neu can dam bao startup order tot hon

### Phase 5 - File uploads

Van de hien tai: `WebServer` luu file upload vao `wwwroot/uploads`, khong phu hop khi scale nhieu instance.

- [ ] Chon giai phap storage:
  - Shared Docker volume cho dev/single host
  - MinIO/S3-compatible object storage cho production
  - Azure Blob/AWS S3 neu deploy cloud
- [ ] Dua duong dan upload vao configuration
- [ ] Dam bao static file URL van truy cap duoc qua gateway

### Phase 6 - WebSocket scaling

Van de hien tai: subscription va connection WebSocket nam trong memory cua tung `WebServer`.

- [ ] Them Redis vao compose neu scale `WebServer`
- [ ] Publish event message vao Redis channel theo conversation
- [ ] Moi `WebServer` subscribe Redis va forward event toi local sockets
- [ ] Can nhac sticky session trong gateway cho WebSocket
- [ ] Them TURN server cho WebRTC neu can goi on dinh tren moi truong that

### Phase 7 - Bao mat va production readiness

- [ ] Hash password bang BCrypt/Argon2 thay vi plain text
- [ ] Dua secret ra khoi repo/compose plain text
- [ ] Cau hinh HTTPS/SSL termination tai gateway
- [ ] Cau hinh CORS theo domain that
- [ ] Gioi han dung luong/kieu file upload
- [ ] Them logging tap trung va monitoring
- [ ] Them CI/CD build, test, push image

## Cau hinh Docker hien tai

`docker-compose.yml` dang dung cac image:

```text
authservice:test
userservice:test
messagecallservice:test
notificationservice:test
webserver:test
```

Bien moi truong quan trong:

```text
DB_Connection=Server=sqlserver,1433;Database=social_network;User Id=sa;Password=Strong!Pass123;TrustServerCertificate=true
WebServer_Origin=http://gateway
ApiClients__Auth__BaseUrl=http://authservice:8080
ApiClients__Users__BaseUrl=http://userservice:8080
ApiClients__Conversations__BaseUrl=http://messagecallservice:8080
ApiClients__Notifications__BaseUrl=http://notificationservice:8080
```

Gateway public:

```text
http://localhost
```

## Rui ro can uu tien

1. Password plain text.
2. Upload local khong scale duoc.
3. WebSocket in-memory khong scale duoc.
4. Chua co health endpoint cho `AuthService` va `WebServer`.
5. Chua co quy trinh khoi tao database ro rang cho container moi.
6. Secret SQL Server dang hard-code trong compose dev.

Cap nhat gan nhat: 2026-05-25
