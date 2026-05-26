# Báo cáo chi tiết dự án SystemChatBoxRealtime

Ngày lập báo cáo: 26/05/2026

## 1. Tổng quan dự án

SystemChatBoxRealtime là hệ thống mạng xã hội/chat realtime được xây dựng theo mô hình microservice bằng ASP.NET Core 9. Hệ thống tập trung vào các nghiệp vụ chính: đăng ký tài khoản, đăng nhập, quản lý hồ sơ cá nhân, tạo bài viết cá nhân, tìm người dùng theo email, tạo cuộc trò chuyện riêng, tạo nhóm chat, gửi tin nhắn văn bản, gửi ảnh, gửi ghi âm, cập nhật tin nhắn realtime qua WebSocket và thử nghiệm gọi thoại/gọi video bằng WebRTC.

Dự án hiện có cả phần giao diện người dùng và các service backend. Người dùng truy cập vào WebServer, còn WebServer đóng vai trò render giao diện, giữ phiên đăng nhập bằng cookie, xử lý upload file cục bộ, mở WebSocket cho trình duyệt và gọi sang các service nghiệp vụ thông qua HTTP API.

## 2. Mục tiêu nghiệp vụ

Mục tiêu chính của hệ thống là cung cấp một ứng dụng chat giống Messenger ở mức cơ bản đến trung bình, có khả năng:

- Cho phép người dùng tạo tài khoản bằng email.
- Xác thực đăng ký bằng OTP gửi qua email.
- Cho phép người dùng đăng nhập và duy trì phiên bằng cookie.
- Hiển thị danh sách cuộc trò chuyện của người dùng.
- Tìm người dùng khác bằng email để bắt đầu nhắn tin.
- Tạo hoặc mở lại cuộc trò chuyện 1-1 đã có.
- Tạo nhóm chat với nhiều thành viên.
- Quản lý nhóm chat gồm đổi tên, đổi ảnh, copy link tham gia, xóa thành viên, rời nhóm và chuyển quyền trưởng nhóm.
- Gửi và nhận tin nhắn realtime.
- Hỗ trợ các loại tin nhắn: text, image, audio.
- Cho phép gọi thoại/gọi video 1-1 và gọi nhóm thử nghiệm bằng WebRTC.
- Cho phép người dùng quản lý trang cá nhân, avatar, ảnh bìa và bài viết cá nhân.

## 3. Kiến trúc tổng thể

Solution gồm 6 project .NET chính và 1 gateway Nginx:

| Thành phần | Vai trò |
| --- | --- |
| AuthService | Xử lý xác thực backend: kiểm tra email tồn tại, đăng nhập, đăng ký tài khoản |
| UserService | Cung cấp thông tin người dùng, tìm user theo email, quản lý profile và bài viết |
| MessageCallService | Quản lý conversation, thread, message, group chat và thông tin peer cho cuộc gọi |
| NotificationService | Quản lý thông báo backend |
| SharedKernel | Chia sẻ model, DTO, DbContext và cache helper cho các service |
| WebServer | MVC/Razor UI, cookie auth, upload file, WebSocket realtime, WebRTC signaling |
| gateway | Nginx reverse proxy cho WebServer, WebSocket và các route API |

Luồng tổng thể:

- Browser truy cập WebServer.
- WebServer render Razor View và phục vụ file tĩnh.
- Khi cần dữ liệu, WebServer gọi AuthService, UserService, MessageCallService hoặc NotificationService bằng HttpClient.
- Các backend service dùng chung SQL Server database social_network.
- Một số service dùng Redis để cache, nếu không có Redis thì fallback sang cache giả Noop.
- WebSocket realtime nằm tại WebServer, không nằm ở các service backend.

## 4. Công nghệ sử dụng

| Nhóm | Công nghệ |
| --- | --- |
| Backend | ASP.NET Core 9, MVC, Web API |
| ORM | Entity Framework Core 9 |
| Database | SQL Server |
| Realtime | WebSocket tự quản lý trong WebServer |
| Gọi audio/video | WebRTC, STUN Google |
| Cache | Redis qua IDistributedCache, có fallback NoopDistributedCache |
| Frontend | Razor View, JavaScript module, Axios CDN |
| Triển khai | Dockerfile cho từng service, docker-compose, Nginx gateway |
| Email | SMTP qua SmtpClient để gửi OTP |

## 5. Cấu trúc thư mục nghiệp vụ

| Thư mục | Nội dung chính |
| --- | --- |
| AuthService/Controllers | AuthController, UsersController, ConversationsController, NotificationsController |
| AuthService/Services | Service nghiệp vụ dùng lại cho UserService, MessageCallService, NotificationService |
| AuthService/Models | Entity Framework model và SocialNetworkContext |
| UserService | Host UsersController và ProfileController |
| MessageCallService | Host ConversationsController |
| NotificationService | Host NotificationsController |
| SharedKernel/Caching | Cache key, cache service, Redis/Noop cache |
| WebServer/Controllers | Controller giao diện cho auth, chat, profile |
| WebServer/Services | Client gọi backend API, WebSocket hub, group call registry, email sender |
| WebServer/Views | Razor View và partial cho login, register, chat, profile, call popup |
| WebServer/wwwroot/js | JavaScript xử lý UI, chat, WebSocket, WebRTC, profile |
| WebServer/wwwroot/uploads | Lưu file upload ảnh, voice, avatar, ảnh nhóm, ảnh bài viết |
| gateway | Cấu hình Nginx |

## 6. Mô hình dữ liệu chính

### 6.1. Bảng account

Bảng account lưu tài khoản người dùng. Các trường nghiệp vụ chính:

- account_id: khóa chính của tài khoản.
- account_name: tên hiển thị.
- email: email đăng nhập, có unique index.
- password: mật khẩu hiện đang lưu dạng plain text.
- photo_path: ảnh đại diện.
- photo_background: ảnh bìa.
- date_of_birth: ngày sinh.
- gender: giới tính, lưu dạng số.
- bio: tiểu sử ngắn.

### 6.2. Bảng conversations

Bảng conversations lưu cuộc trò chuyện:

- conversation_id: khóa chính.
- is_group: phân biệt chat 1-1 và nhóm chat.
- title: tên nhóm hoặc tiêu đề cuộc trò chuyện.
- avatar_url: ảnh đại diện nhóm, được bổ sung qua database_update_group_avatar.sql.
- created_at: ngày tạo.

### 6.3. Bảng conversation_members

Bảng conversation_members thể hiện thành viên trong conversation:

- conversation_member_id: khóa chính.
- conversation_id: cuộc trò chuyện.
- account_id: thành viên.
- joined_at: thời điểm tham gia.
- created_at: thời điểm tạo bản ghi.
- title: đang được dùng để lưu vai trò trong nhóm, ví dụ owner hoặc member.

### 6.4. Bảng messages

Bảng messages lưu tin nhắn:

- message_id: khóa chính.
- conversation_id: cuộc trò chuyện chứa tin nhắn.
- sender_id: người gửi.
- content: nội dung tin nhắn hoặc URL file.
- message_type: loại tin nhắn, hiện dùng text, image, audio.
- created_at: thời điểm gửi.
- parent_message_id: dùng cho nghiệp vụ reply nhưng UI hiện chưa thể hiện rõ.
- is_read: trạng thái đọc, hiện là bool global.
- is_remove: trạng thái xóa mềm.

### 6.5. Bảng Notifications

Bảng Notifications lưu thông báo:

- Id: khóa chính.
- Type: loại thông báo.
- Content: nội dung thông báo.
- SenderId: người gửi/thực hiện hành động.
- ConsumerId: người nhận thông báo.
- Date: ngày tạo.
- IsRead: trạng thái đã đọc.

### 6.6. Nhóm bảng bài viết

Các bảng posts, post_media, post_comments, post_like, post_shares phục vụ nghiệp vụ mạng xã hội:

- posts: bài viết của tài khoản.
- post_media: ảnh hoặc media gắn với bài viết.
- post_comments: bình luận bài viết.
- post_like: lượt thích.
- post_shares: chia sẻ bài viết.

Trong mã hiện tại, WebServer và UserService đã triển khai phần tạo/sửa/xóa bài viết cá nhân và media ảnh. Các nút thích, bình luận, chia sẻ có hiển thị ở UI nhưng chưa thấy luồng xử lý backend tương ứng.

### 6.7. Bảng friendships

Bảng friendships có cấu trúc để lưu quan hệ bạn bè:

- requester_id: người gửi lời mời.
- addressee_id: người nhận lời mời.
- status: trạng thái, mặc định pending.

Tuy nhiên trong nghiệp vụ hiện tại, luồng kết bạn chưa được triển khai rõ trên WebServer. Hệ thống đang ưu tiên tìm người dùng bằng email và tạo cuộc trò chuyện trực tiếp.

## 7. Nghiệp vụ đăng ký tài khoản

### 7.1. Mục tiêu

Cho phép người dùng tạo tài khoản mới bằng họ tên, email và mật khẩu. Trước khi tạo tài khoản, hệ thống yêu cầu xác thực OTP qua email.

### 7.2. Tác nhân

- Người dùng chưa có tài khoản.
- WebServer.
- AuthService.
- SMTP server.
- SQL Server.

### 7.3. Luồng chính

1. Người dùng mở màn hình đăng ký.
2. Người dùng nhập họ tên, email, mật khẩu và xác nhận mật khẩu.
3. Frontend kiểm tra dữ liệu cơ bản:
   - Họ tên không được rỗng.
   - Email phải đúng định dạng.
   - Mật khẩu tối thiểu 8 ký tự.
   - Mật khẩu xác nhận phải trùng mật khẩu.
4. WebServer nhận yêu cầu gửi OTP tại /auth/register/send-otp.
5. WebServer chuẩn hóa email về chữ thường và kiểm tra dữ liệu lần nữa.
6. WebServer gọi AuthService kiểm tra email đã tồn tại chưa.
7. Nếu email chưa tồn tại, WebServer sinh OTP gồm 6 chữ số.
8. OTP được lưu vào MemoryCache với thời hạn 5 phút.
9. WebServer gửi OTP qua SMTP đến email người dùng.
10. Người dùng nhập OTP trên giao diện.
11. WebServer kiểm tra OTP tại /auth/register/verify-otp.
12. Nếu OTP đúng và còn hạn, WebServer gọi AuthService để tạo tài khoản.
13. AuthService kiểm tra lại email trùng, sau đó thêm account mới vào database.
14. Người dùng được chuyển về màn hình đăng nhập.

### 7.4. Quy tắc nghiệp vụ

- OTP có thời hạn 5 phút.
- OTP gồm 6 chữ số.
- Nếu nhập sai OTP quá 5 lần, OTP bị xóa và người dùng phải gửi lại mã mới.
- Không được đăng ký nếu email đã tồn tại.
- Endpoint /auth/register trực tiếp trên WebServer bị chặn và yêu cầu xác thực OTP trước.
- AuthService chỉ tạo account với accountName, email, password; không tự tạo profile riêng.

### 7.5. Ngoại lệ

- Email rỗng hoặc sai định dạng: trả lỗi yêu cầu nhập email hợp lệ.
- Mật khẩu dưới 8 ký tự: trả lỗi mật khẩu quá ngắn.
- Email đã tồn tại: trả lỗi email đã được đăng ký.
- OTP hết hạn: yêu cầu gửi lại mã mới.
- OTP sai quá nhiều lần: xóa OTP và yêu cầu gửi lại.
- SMTP lỗi: không thể gửi OTP.

## 8. Nghiệp vụ đăng nhập

### 8.1. Mục tiêu

Cho phép người dùng đã có tài khoản đăng nhập vào hệ thống và truy cập màn hình chat.

### 8.2. Luồng chính

1. Người dùng nhập email và mật khẩu trên màn hình đăng nhập.
2. Frontend kiểm tra email và mật khẩu không rỗng.
3. WebServer nhận yêu cầu /auth/login.
4. WebServer gọi AuthService tại /api/auth/login.
5. AuthService tìm account có email và password trùng khớp.
6. Nếu đăng nhập đúng, AuthService trả về thông tin account.
7. WebServer tạo cookie authentication với claim:
   - NameIdentifier: accountId.
   - Name: email.
8. Nếu người dùng chọn "Nhớ tôi", cookie được lưu persistent trong 7 ngày.
9. Nếu không chọn, cookie dùng theo phiên với cấu hình mặc định 8 giờ và sliding expiration.
10. Frontend chuyển người dùng vào /home/main.

### 8.3. Quy tắc nghiệp vụ

- Người dùng phải có account trong bảng account.
- Email và password hiện được so sánh trực tiếp trong database.
- Các trang chat và profile yêu cầu đăng nhập.
- WebSocket cũng lấy userId từ cookie claim NameIdentifier.

### 8.4. Điểm cần lưu ý

- Mật khẩu đang lưu và so sánh dạng plain text, chưa hash.
- Response đăng nhập backend còn trả cả trường password, không phù hợp cho môi trường thật.
- Chưa thấy endpoint đăng xuất hoàn chỉnh. Topbar hiện có nút đăng xuất nhưng JavaScript chỉ hiển thị alert demo.

## 9. Nghiệp vụ trang chat chính

### 9.1. Mục tiêu

Trang chat chính là nơi người dùng xem danh sách cuộc trò chuyện, mở một thread, gửi tin nhắn, tạo chat mới, tạo nhóm và gọi audio/video.

### 9.2. Thành phần giao diện

- Sidebar trái: danh sách thread, ô tìm kiếm, tab Tất cả/Chưa đọc/Nhóm.
- Khu vực chat phải: header người đang chat, nút gọi thoại, nút gọi video, danh sách tin nhắn, composer gửi tin.
- Modal tạo tin nhắn mới: tìm user bằng email, chọn chế độ chat riêng hoặc tạo nhóm.
- Modal quản lý nhóm.
- Popup cuộc gọi đến/đang gọi.

### 9.3. Luồng vào màn hình

1. Người dùng đã đăng nhập truy cập /home/main.
2. WebServer lấy accountId từ cookie.
3. WebServer gọi UserService để lấy thông tin user hiện tại.
4. WebServer render view Main cùng topbar.
5. JavaScript tải danh sách thread qua /chat/threads.
6. Nếu có thread, frontend tự mở thread đầu tiên sau khi danh sách load xong.

## 10. Nghiệp vụ tìm người dùng

### 10.1. Mục tiêu

Cho phép người dùng tìm tài khoản khác bằng email để xem thông tin hoặc bắt đầu nhắn tin.

### 10.2. Luồng chính

1. Người dùng bấm nút tạo tin nhắn mới.
2. WebServer trả về modal tìm bạn.
3. Người dùng nhập email.
4. Frontend gọi /chat/search_user hoặc /chat/users/search.
5. WebServer gọi UserService /api/users/search.
6. UserService tìm account theo email đã chuẩn hóa.
7. Nếu tìm thấy user khác tài khoản hiện tại, UI hiển thị kết quả.
8. Với chat riêng, người dùng bấm vào kết quả để xem modal thông tin cá nhân.
9. Với tạo nhóm, người dùng bấm "Thêm" để đưa user vào danh sách thành viên nhóm.

### 10.3. Quy tắc nghiệp vụ

- Không cho chọn chính tài khoản hiện tại làm đối tượng chat.
- Tìm kiếm hiện là tìm theo email chính xác, không phải tìm gần đúng theo tên.
- Tham số limit có xuất hiện ở frontend nhưng backend UserService hiện chỉ trả một user theo email.

## 11. Nghiệp vụ chat 1-1

### 11.1. Mục tiêu

Cho phép hai người dùng mở cuộc trò chuyện riêng và nhắn tin với nhau.

### 11.2. Luồng tạo hoặc mở lại conversation

1. Người dùng tìm người khác bằng email.
2. Người dùng chọn nhắn tin.
3. WebServer gửi yêu cầu tạo conversation 1-1 với accountId hiện tại và friendId.
4. MessageCallService kiểm tra:
   - accountId và friendId hợp lệ.
   - Hai id phải khác nhau.
   - Cả hai user phải tồn tại.
5. MessageCallService tìm conversation 1-1 đã có giữa hai tài khoản.
6. Nếu đã có, trả về conversation cũ.
7. Nếu chưa có, tạo mới conversation is_group = false.
8. Thêm hai bản ghi conversation_members.
9. Xóa cache danh sách thread của hai user.
10. Frontend tải lại danh sách thread và mở conversation vừa tạo.

### 11.3. Quy tắc nghiệp vụ

- Không thể tạo chat 1-1 với chính mình.
- Conversation 1-1 được tái sử dụng nếu đã tồn tại.
- Chỉ thành viên của conversation mới được xem lịch sử tin nhắn hoặc gửi tin nhắn.

## 12. Nghiệp vụ nhóm chat

### 12.1. Mục tiêu

Cho phép người dùng tạo và quản lý nhóm chat nhiều thành viên.

### 12.2. Tạo nhóm

Luồng tạo nhóm:

1. Người dùng mở modal tin nhắn mới.
2. Chọn chế độ "Tạo nhóm".
3. Nhập tên nhóm, có thể để trống.
4. Tìm từng thành viên bằng email.
5. Thêm thành viên vào danh sách đã chọn.
6. Bấm tạo nhóm.
7. WebServer lấy ownerId từ cookie.
8. MessageCallService kiểm tra owner tồn tại và có ít nhất một thành viên khác owner.
9. Hệ thống kiểm tra tất cả memberIds tồn tại trong account.
10. Nếu title rỗng, hệ thống tự tạo tên nhóm từ tên các thành viên.
11. Tạo conversation is_group = true.
12. Thêm owner với role owner.
13. Thêm các thành viên còn lại với role member.
14. Xóa cache thread của tất cả thành viên.
15. Frontend tải lại danh sách thread và mở nhóm mới.

Quy tắc:

- OwnerId bắt buộc.
- Nhóm phải có ít nhất một thành viên khác người tạo.
- MemberIds được lọc trùng và loại bỏ chính owner.
- Role nhóm được lưu trong conversation_members.title.

### 12.3. Xem và quản lý thông tin nhóm

Người dùng bấm menu ba chấm của thread nhóm và chọn quản lý nhóm. Hệ thống hiển thị:

- Tên nhóm.
- Ảnh nhóm.
- Số thành viên.
- Link tham gia nhóm.
- Danh sách thành viên.
- Vai trò trưởng nhóm/thành viên.
- Nút đổi tên, đổi ảnh nếu người xem là owner.
- Nút xóa thành viên nếu người xem là owner.
- Nút rời nhóm.

### 12.4. Đổi tên và đổi ảnh nhóm

Luồng:

1. Owner mở modal quản lý nhóm.
2. Owner sửa tên hoặc chọn ảnh nhóm mới.
3. WebServer lưu ảnh nhóm vào wwwroot/uploads/groups nếu có upload.
4. WebServer gọi MessageCallService update group.
5. MessageCallService kiểm tra người yêu cầu là owner.
6. Cập nhật title và avatar_url.
7. Xóa cache group info và thread của các thành viên.
8. Frontend tải lại thread và modal nhóm.

Quy tắc:

- Chỉ owner được đổi tên/ảnh nhóm.
- Tên nhóm không được rỗng nếu gửi lên.
- Ảnh nhóm chỉ hỗ trợ jpg, jpeg, png, webp, gif.
- Đường dẫn ảnh nhóm được lưu dạng URL tương đối trong WebServer.

### 12.5. Link tham gia nhóm

Hệ thống tạo link dạng /chat/groups/{conversationId}/join cho từng nhóm. Người dùng đã đăng nhập có thể truy cập link để tham gia nhóm.

Luồng:

1. Người dùng mở link tham gia nhóm.
2. Nếu chưa đăng nhập, hệ thống chuyển về màn hình login.
3. Nếu đã đăng nhập, WebServer gọi MessageCallService join group.
4. Nếu user chưa là thành viên, thêm vào conversation_members với role member.
5. Nếu đã là thành viên, trả về conversation hiện tại.
6. Frontend chuyển người dùng về màn hình chat chính.

### 12.6. Xóa thành viên khỏi nhóm

Luồng:

1. Owner mở quản lý nhóm.
2. Chọn xóa một thành viên.
3. MessageCallService kiểm tra group tồn tại.
4. Kiểm tra người thao tác là owner.
5. Không cho owner tự xóa chính mình bằng luồng remove member.
6. Tìm thành viên có role không phải owner.
7. Xóa thành viên khỏi conversation_members.
8. Xóa cache group và thread.

Quy tắc:

- Chỉ owner được xóa thành viên.
- Owner không thể tự xóa bằng chức năng xóa thành viên.
- Không xóa được owner khác bằng remove member.

### 12.7. Rời nhóm và giải tán nhóm

Luồng:

1. Thành viên bấm rời nhóm.
2. MessageCallService kiểm tra user là thành viên nhóm.
3. Nếu sau khi user rời mà nhóm còn dưới 2 thành viên, hệ thống giải tán nhóm.
4. Khi giải tán, hệ thống xóa messages, conversation_members và conversation của nhóm.
5. Nếu owner rời nhóm mà nhóm vẫn đủ thành viên, owner phải chọn successorId.
6. Successor phải là thành viên còn lại trong nhóm.
7. Hệ thống chuyển role owner cho successor rồi xóa owner cũ khỏi nhóm.
8. Xóa cache group và thread của các thành viên bị ảnh hưởng.

Quy tắc:

- Owner rời nhóm cần chọn trưởng nhóm kế thừa nếu nhóm vẫn còn đủ thành viên.
- Nếu nhóm không còn đủ thành viên, nhóm bị giải tán.
- Thành viên thường có thể rời nhóm mà không cần successor.

## 13. Nghiệp vụ danh sách thread

### 13.1. Mục tiêu

Hiển thị các cuộc trò chuyện mà người dùng đang tham gia.

### 13.2. Luồng lấy thread

1. Frontend gọi /chat/threads.
2. WebServer lấy accountId từ cookie.
3. WebServer gọi MessageCallService /api/conversations/threads.
4. MessageCallService kiểm tra user tồn tại.
5. Lấy tất cả conversation mà user là thành viên.
6. Với mỗi conversation, lấy last message, tên hiển thị, avatar và vai trò owner nếu là nhóm.
7. Sắp xếp giảm dần theo thời gian tin nhắn cuối hoặc thời gian tạo conversation.
8. WebServer render partial _ChatThreads.

### 13.3. Quy tắc hiển thị

- Chat 1-1 hiển thị tên và avatar của người còn lại.
- Nhóm chat hiển thị title và avatar_url của nhóm.
- Nếu chưa có tin nhắn, snippet là "Chưa có tin nhắn".
- Nếu tin nhắn không có content rõ ràng, snippet thể hiện là đã gửi tệp đính kèm.
- UI có tab Chưa đọc và Nhóm, nhưng phần lọc nghiệp vụ chưa thấy triển khai đầy đủ trong mã đọc được.

## 14. Nghiệp vụ mở conversation và xem lịch sử tin nhắn

### 14.1. Luồng chính

1. Người dùng click vào một thread.
2. Frontend cập nhật header cuộc trò chuyện.
3. Frontend gọi /chat/conversation với conversationId.
4. WebServer lấy meId từ cookie.
5. WebServer gọi MessageCallService lấy tin nhắn.
6. MessageCallService kiểm tra người dùng là thành viên conversation.
7. Lấy tối đa 50 tin nhắn mới nhất mặc định.
8. Loại bỏ tin nhắn is_remove.
9. WebServer render partial _ConversationMessages.
10. Frontend thay nội dung vùng messageScroller và scroll xuống cuối.
11. Frontend gửi WebSocket subscribe conversationId.

### 14.2. Quy tắc nghiệp vụ

- Chỉ thành viên conversation được xem tin nhắn.
- Backend hỗ trợ limit từ 1 đến 200.
- Backend có hỗ trợ beforeMessageId để phân trang tin cũ, nhưng UI hiện chưa thấy dùng.
- Tin nhắn bị xóa mềm is_remove không được hiển thị.
- UI chèn mốc thời gian nếu khoảng cách giữa hai tin nhắn quá 15 phút.

## 15. Nghiệp vụ gửi tin nhắn text

### 15.1. Luồng chính

1. Người dùng nhập nội dung vào ô composer.
2. Bấm gửi hoặc nhấn Enter.
3. Frontend kiểm tra đã chọn conversation và nội dung không rỗng.
4. Frontend gọi /chat/send_message.
5. WebServer lấy senderId từ cookie, không tin senderId từ client.
6. WebServer gọi MessageCallService tạo message type text.
7. MessageCallService kiểm tra:
   - conversation tồn tại.
   - sender tồn tại.
   - sender là thành viên conversation.
   - content không rỗng.
   - parentMessageId nếu có phải thuộc conversation.
8. Lưu message vào bảng messages.
9. Xóa cache messages và thread của các thành viên conversation.
10. WebServer broadcast WebSocket event message-text đến các socket đang subscribe conversation, loại trừ sender.
11. Sender reload lại conversation để thấy tin vừa gửi.
12. Người nhận đang mở conversation sẽ append tin mới realtime.

### 15.2. Quy tắc nghiệp vụ

- Tin nhắn text bắt buộc có content.
- Sender lấy từ cookie, giúp tránh giả mạo senderId từ request frontend.
- Chỉ thành viên conversation được gửi tin.
- WebSocket chỉ gửi đến client đã subscribe conversation.

## 16. Nghiệp vụ gửi ảnh

### 16.1. Luồng chính

1. Người dùng bấm nút chọn ảnh hoặc GIF.
2. Browser mở file picker image.
3. Frontend hiển thị preview ảnh.
4. Khi gửi, frontend gửi multipart form đến /chat/send_image.
5. WebServer kiểm tra có file và conversationId hợp lệ.
6. WebServer lưu file vào wwwroot/uploads/chat với tên file GUID.
7. WebServer gọi MessageCallService tạo message type image với content là URL ảnh.
8. MessageCallService lưu message vào database.
9. WebServer broadcast event message-image tới người nhận đang subscribe.
10. Sender reload lại conversation.

### 16.2. Quy tắc nghiệp vụ

- Request có giới hạn kích thước 20MB.
- File ảnh chat hiện không kiểm tra extension chặt ở ConversationService, chỉ tạo tên theo extension file upload.
- URL ảnh lưu dạng /uploads/chat/{fileName}.
- Nội dung message image chính là URL ảnh.

## 17. Nghiệp vụ gửi ghi âm

### 17.1. Luồng chính

1. Người dùng bấm nút micro.
2. Browser xin quyền truy cập microphone.
3. Frontend dùng MediaRecorder để ghi âm.
4. Khi dừng ghi, frontend tạo audio preview.
5. Khi gửi, frontend tạo file voice.webm và gửi đến /chat/send_audio.
6. WebServer lưu file vào wwwroot/uploads/voice.
7. WebServer gọi MessageCallService tạo message type audio.
8. MessageCallService lưu message vào database.
9. WebServer broadcast event message-audio tới người nhận đang subscribe.
10. UI hiển thị audio player.

### 17.2. Quy tắc nghiệp vụ

- Audio được lưu dạng webm.
- Nội dung message audio là URL file voice.
- Nếu browser không cấp quyền micro, frontend báo không thể truy cập micro.

## 18. Nghiệp vụ realtime bằng WebSocket

### 18.1. Mục tiêu

Đảm bảo người nhận đang mở cuộc trò chuyện có thể thấy tin nhắn mới mà không cần reload trang.

### 18.2. Kết nối WebSocket

Endpoint WebSocket của hệ thống là /ws trên WebServer.

Khi browser kết nối:

1. WebServer kiểm tra request có phải WebSocket.
2. Lấy userId từ cookie claim NameIdentifier.
3. Nếu không có cookie, fallback bằng query userId.
4. Nếu không có userId, trả 401.
5. WebServer accept WebSocket.
6. WebSocketHandler đăng ký socket vào RealtimeHub.
7. Server gửi message hello về client.

### 18.3. Subscription conversation

Khi user mở conversation, client gửi subscribe conversationId. RealtimeHub lưu quan hệ socketId và conversationId trong memory.

Khi user đổi conversation, frontend subscribe conversation mới. Mã có hỗ trợ unsubscribe nhưng UI hiện chưa thấy gọi rõ ràng khi đổi thread.

### 18.4. Broadcast tin nhắn

Sau khi gửi thành công text/image/audio:

- WebServer gửi event message-text, message-image hoặc message-audio.
- Event được gửi cho tất cả socket đang subscribe conversationId.
- Sender bị loại trừ khỏi broadcast vì sender đã reload conversation sau khi gửi.

### 18.5. Đặc điểm kỹ thuật

- Một user có thể có nhiều socket, tương ứng nhiều tab.
- Toàn bộ connection state và subscription state nằm trong memory của WebServer.
- Nếu scale nhiều WebServer instance, realtime giữa các instance sẽ không đồng bộ nếu chưa có Redis pub/sub hoặc backplane.
- WebSocket subscribe hiện chỉ ghi nhận conversationId, chưa thấy bước kiểm tra membership ở tầng WebSocket. Backend API vẫn kiểm tra membership khi xem/gửi tin.

## 19. Nghiệp vụ gọi thoại và gọi video 1-1

### 19.1. Mục tiêu

Cho phép hai người dùng trong conversation 1-1 thực hiện cuộc gọi audio hoặc video thử nghiệm.

### 19.2. Luồng gọi đi

1. Người dùng mở conversation 1-1.
2. Bấm nút gọi thoại hoặc gọi video.
3. Frontend gọi /chat/peer để lấy thông tin me và peer.
4. WebServer gọi MessageCallService /api/conversations/{id}/peer.
5. MessageCallService kiểm tra conversation tồn tại, không phải group và người gọi là thành viên.
6. Frontend mở popup gọi.
7. Frontend gửi WebSocket call.send tới peer với payload invite.
8. Nếu peer online, peer nhận call.event kind invite.
9. Peer thấy popup cuộc gọi đến.

### 19.3. Luồng nhận cuộc gọi

1. Người nhận bấm nhận hoặc từ chối.
2. Nếu nhận, client gửi event accept về caller.
3. Caller tạo WebRTC offer.
4. Callee nhận offer, tạo answer.
5. Hai bên trao đổi ICE candidate qua WebSocket.
6. Khi kết nối thành công, video/audio stream được gắn vào popup.

### 19.4. Quy tắc nghiệp vụ

- Conversation phải là 1-1.
- Người gọi phải là thành viên conversation.
- Nếu người nhận đang bận cuộc gọi khác, frontend gửi busy.
- Nếu không có phản hồi trong 45 giây, caller hủy cuộc gọi.
- Người dùng có thể bật/tắt mic và camera.
- Audio call không bật camera.

### 19.5. Hạn chế

- WebRTC hiện chỉ cấu hình STUN Google, chưa có TURN server.
- Cuộc gọi có thể không ổn định trên mạng NAT/firewall phức tạp.
- Trạng thái cuộc gọi nằm trong frontend và WebServer memory, chưa có lưu lịch sử cuộc gọi.

## 20. Nghiệp vụ gọi nhóm

### 20.1. Mục tiêu

Cho phép các thành viên trong một nhóm chat tham gia cuộc gọi audio/video nhóm thử nghiệm.

### 20.2. Quản lý phòng gọi nhóm

WebServer dùng GroupCallRegistry để quản lý phòng gọi nhóm trong memory:

- Mỗi conversation nhóm chỉ có một room active tại một thời điểm.
- Room có roomId, conversationId, callType, startedByUserId, startedAtUtc, status.
- Mỗi participant có accountId, accountName, photoPath, status, micEnabled, cameraEnabled, joinedAtUtc, leftAtUtc.
- Trạng thái participant gồm invited, joined, declined, left.

### 20.3. Luồng bắt đầu gọi nhóm

1. Người dùng mở thread nhóm.
2. Bấm gọi thoại hoặc gọi video.
3. Frontend mở popup gọi nhóm.
4. Frontend xin quyền camera/micro.
5. Frontend gửi WebSocket group.call.start.
6. WebSocketHandler kiểm tra người gọi là thành viên nhóm bằng cách lấy GroupInfo.
7. GroupCallRegistry tạo room mới nếu chưa có room active.
8. Người gọi được đặt status joined.
9. Các thành viên còn lại được đặt status invited.
10. WebServer gửi group.call.event kind invite đến các thành viên.

### 20.4. Luồng tham gia/từ chối/rời cuộc gọi nhóm

- Thành viên nhận invite sẽ thấy popup incoming call nhóm.
- Nếu bấm nhận, client gửi group.call.accept, registry đổi status thành joined.
- Nếu từ chối, client gửi group.call.decline, registry đổi status thành declined.
- Nếu rời cuộc gọi, client gửi group.call.leave, registry đổi status thành left.
- Nếu không còn participant joined nào, room được chuyển status ended.
- Người dùng có thể kết thúc toàn bộ cuộc gọi bằng group.call.end.

### 20.5. Kết nối media nhóm

- Mỗi cặp participant joined tạo một RTCPeerConnection riêng.
- User có id nhỏ hơn sẽ chủ động tạo offer tới user id lớn hơn.
- Offer, answer và ICE candidate được gửi qua WebSocket group.call.signal.
- UI hiển thị grid video và sidebar participant.
- Người dùng có thể bật/tắt mic và camera, trạng thái được broadcast bằng group.call.state.

### 20.6. Hạn chế

- Room gọi nhóm nằm trong memory của WebServer.
- Nếu restart WebServer, toàn bộ room active bị mất.
- Nếu scale nhiều WebServer, room state không đồng bộ.
- Chưa có TURN server.
- Chưa lưu lịch sử cuộc gọi nhóm.

## 21. Nghiệp vụ hồ sơ cá nhân

### 21.1. Mục tiêu

Cho phép người dùng xem và chỉnh sửa trang cá nhân, bao gồm thông tin cơ bản, avatar, ảnh bìa và bài viết.

### 21.2. Xem trang cá nhân

Luồng:

1. Người dùng truy cập /profile để xem trang của mình hoặc /profile/{accountId} để xem người khác.
2. WebServer lấy viewerAccountId từ cookie.
3. WebServer gọi UserService /api/profile/{accountId}.
4. UserService trả thông tin account gồm tên, email, avatar, cover, ngày sinh, giới tính, bio.
5. WebServer gọi UserService /api/profile/{accountId}/posts.
6. UserService trả danh sách bài viết chưa bị xóa.
7. WebServer render trang profile.

### 21.3. Chỉnh sửa thông tin cá nhân

Người dùng chủ sở hữu profile có thể chỉnh sửa:

- Họ và tên.
- Tiểu sử.
- Ngày sinh.
- Giới tính.

Quy tắc:

- Account name bắt buộc.
- Bio có thể để trống.
- Ngày sinh có thể null.
- Giới tính có thể null.
- Sau khi cập nhật, UserService xóa cache profile, user theo id, user theo email và profile posts nếu cần.

### 21.4. Xóa thông tin phụ

Người dùng có thể xóa bio, ngày sinh và giới tính, nhưng vẫn giữ accountName hiện tại.

### 21.5. Upload avatar và ảnh bìa

Luồng:

1. Người dùng chọn ảnh avatar hoặc ảnh bìa.
2. WebServer kiểm tra file hợp lệ.
3. File được lưu vào wwwroot/uploads/profile.
4. WebServer gọi UserService update profile photos.
5. UserService cập nhật photo_path hoặc photo_background trong account.
6. Cache profile bị xóa.

Quy tắc:

- Avatar giới hạn 8MB.
- Cover giới hạn 12MB.
- Chỉ hỗ trợ jpg, jpeg, png, webp, gif.

## 22. Nghiệp vụ bài viết cá nhân

### 22.1. Mục tiêu

Cho phép người dùng tạo, sửa và xóa bài viết trên trang cá nhân.

### 22.2. Tạo bài viết

Luồng:

1. Người dùng mở modal tạo bài viết.
2. Nhập nội dung hoặc chọn ảnh.
3. Nếu có ảnh, WebServer lưu vào wwwroot/uploads/posts.
4. WebServer gọi UserService /api/profile/posts.
5. UserService kiểm tra account tồn tại.
6. Tạo bản ghi posts.
7. Nếu có mediaUrl, tạo thêm bản ghi post_media.
8. Xóa cache profile posts.
9. Trang reload để hiển thị bài viết mới.

Quy tắc:

- Bài viết phải có content hoặc media.
- Nếu có mediaUrl, post_type là image.
- Nếu không có mediaUrl, post_type là text.

### 22.3. Sửa bài viết

Luồng:

1. Chủ bài viết chọn menu sửa.
2. UI mở modal với nội dung và ảnh hiện có.
3. Người dùng sửa nội dung hoặc thay ảnh.
4. WebServer gọi UserService update post.
5. UserService kiểm tra postId thuộc accountId và chưa bị xóa.
6. Cập nhật content, media nếu có ảnh mới, post_type và update_at.
7. Xóa cache profile posts.

Quy tắc:

- Chỉ chủ bài viết được sửa trên UI.
- Backend kiểm tra post thuộc accountId.
- Nếu bài viết không còn content, không có media mới và không có media cũ thì bị từ chối.

### 22.4. Xóa bài viết

Luồng:

1. Chủ bài viết chọn xóa.
2. UI xác nhận.
3. WebServer gọi UserService delete post.
4. UserService tìm bài viết thuộc accountId và chưa bị xóa.
5. Set is_remove = true.
6. Cập nhật update_at.
7. Xóa cache profile posts.

Quy tắc:

- Xóa bài viết là xóa mềm.
- Danh sách bài viết chỉ lấy post có is_remove khác true.

### 22.5. Chức năng chưa hoàn chỉnh

UI có nút Thích, Bình luận, Chia sẻ nhưng chưa thấy endpoint xử lý tương ứng trong WebServer/UserService hiện tại.

## 23. Nghiệp vụ thông báo

### 23.1. Năng lực backend hiện có

NotificationService cung cấp các nghiệp vụ:

- Tạo thông báo.
- Lấy danh sách thông báo theo consumerId.
- Đánh dấu thông báo đã đọc.

### 23.2. Quy tắc nghiệp vụ

- Type và Content bắt buộc.
- SenderId và ConsumerId bắt buộc.
- Sender và Consumer phải tồn tại.
- Danh sách thông báo được sắp xếp mới nhất trước.
- Limit được giới hạn từ 1 đến 200.
- Khi tạo hoặc mark read, cache thông báo của consumer bị invalidated.

### 23.3. Tình trạng tích hợp UI

Topbar có icon chuông, nhưng trong phần WebServer đọc được chưa thấy controller/service/frontend tích hợp đầy đủ với NotificationService. Vì vậy có thể xem NotificationService là backend đã có API cơ bản, nhưng nghiệp vụ thông báo realtime/UI chưa hoàn chỉnh.

## 24. Nghiệp vụ cache

### 24.1. Mục tiêu

Giảm số lần truy vấn database cho các dữ liệu đọc nhiều như user, profile, thread, messages, group info, peer info và notifications.

### 24.2. Các nhóm cache

| Dữ liệu | TTL |
| --- | --- |
| User by id/email | 30 phút |
| Profile | 15 phút |
| Profile posts | 5 phút |
| Conversation threads | 30 giây |
| Conversation messages | 30 giây |
| Group info | 5 phút |
| Peer info | 5 phút |
| Notifications | 30 giây |
| Version stamp | 7 ngày |

### 24.3. Cơ chế invalidation

- Khi gửi tin nhắn: đổi version cache messages và xóa cache thread của thành viên conversation.
- Khi tạo conversation/group: xóa cache thread của thành viên liên quan.
- Khi đổi thông tin nhóm: đổi version group info và xóa cache thread.
- Khi cập nhật profile: xóa cache profile, user và posts.
- Khi tạo/sửa/xóa bài viết: xóa cache profile posts.
- Khi tạo/đọc notification: đổi version notification.

### 24.4. Fallback

Nếu không cấu hình Redis, hệ thống dùng NoopDistributedCache. Khi đó service vẫn chạy nhưng không có cache thật.

## 25. API nghiệp vụ chính

### 25.1. AuthService

| API | Nghiệp vụ |
| --- | --- |
| GET /api/auth/exists | Kiểm tra email đã tồn tại |
| POST /api/auth/login | Đăng nhập |
| POST /api/auth/register | Tạo account |

### 25.2. UserService

| API | Nghiệp vụ |
| --- | --- |
| GET /api/users/{id} | Lấy user theo id |
| GET /api/users/search | Tìm user theo email |
| GET /api/profile/{accountId} | Lấy profile |
| PUT /api/profile/{accountId} | Cập nhật profile |
| PUT /api/profile/{accountId}/photos | Cập nhật avatar/cover |
| GET /api/profile/{accountId}/posts | Lấy bài viết |
| POST /api/profile/posts | Tạo bài viết |
| PUT /api/profile/posts/{postId} | Sửa bài viết |
| DELETE /api/profile/posts/{postId} | Xóa mềm bài viết |

### 25.3. MessageCallService

| API | Nghiệp vụ |
| --- | --- |
| POST /api/conversations | Tạo hoặc lấy chat 1-1 |
| POST /api/conversations/groups | Tạo nhóm chat |
| GET /api/conversations/threads | Lấy danh sách thread |
| GET /api/conversations/{conversationId}/group | Lấy thông tin nhóm |
| POST /api/conversations/{conversationId}/group/join | Tham gia nhóm |
| PUT /api/conversations/{conversationId}/group | Cập nhật nhóm |
| POST /api/conversations/{conversationId}/group/leave | Rời nhóm |
| DELETE /api/conversations/{conversationId}/group/members/{memberId} | Xóa thành viên nhóm |
| GET /api/conversations/{conversationId}/messages | Lấy tin nhắn |
| POST /api/conversations/{conversationId}/messages | Gửi text |
| POST /api/conversations/{conversationId}/messages/image | Gửi image |
| POST /api/conversations/{conversationId}/messages/audio | Gửi audio |
| GET /api/conversations/{conversationId}/peer | Lấy thông tin peer 1-1 |
| POST /api/conversations/{conversationId}/mark-read | Đánh dấu đã đọc, hiện mới kiểm tra membership |

### 25.4. NotificationService

| API | Nghiệp vụ |
| --- | --- |
| POST /api/notifications | Tạo thông báo |
| GET /api/notifications | Lấy thông báo theo consumer |
| POST /api/notifications/{notificationId}/mark-read | Đánh dấu đã đọc |

### 25.5. WebServer

| Route | Nghiệp vụ |
| --- | --- |
| /Auth/Login | Màn hình đăng nhập |
| /Auth/Register | Màn hình đăng ký |
| POST /auth/login | Đăng nhập từ UI |
| POST /auth/register/send-otp | Gửi OTP đăng ký |
| POST /auth/register/verify-otp | Xác thực OTP và đăng ký |
| /home/main | Trang chat chính |
| /chat/threads | Partial danh sách thread |
| /chat/conversation | Partial tin nhắn conversation |
| /chat/search_view | Modal tìm user/tạo nhóm |
| /chat/search_user | Tìm user trả HTML |
| /chat/users/search | Tìm user trả JSON |
| /chat/conversations | Tạo chat riêng |
| /chat/groups | Tạo nhóm |
| /chat/groups/{conversationId} | Partial quản lý nhóm |
| /chat/groups/{conversationId}/join | Tham gia nhóm bằng link |
| /chat/send_message | Gửi text |
| /chat/send_image | Gửi ảnh |
| /chat/send_audio | Gửi audio |
| /chat/peer | Lấy thông tin peer |
| /call/popup | Popup gọi 1-1 |
| /call/incoming_popup | Popup cuộc gọi đến 1-1 |
| /call/group_popup | Popup gọi nhóm |
| /call/group_incoming_popup | Popup cuộc gọi nhóm đến |
| /profile | Trang cá nhân của mình |
| /profile/{accountId} | Trang cá nhân người khác |
| POST /profile/update | Cập nhật profile |
| POST /profile/avatar | Upload avatar |
| POST /profile/cover | Upload cover |
| POST /profile/posts | Tạo bài viết |
| POST /profile/posts/{postId}/update | Sửa bài viết |
| POST /profile/posts/{postId}/delete | Xóa bài viết |

## 26. Triển khai Docker và gateway

### 26.1. docker-compose

docker-compose.yml hiện khai báo:

- sqlserver: SQL Server 2022, database social_network, volume sqlserver_data, healthcheck.
- redis: Redis 7 Alpine, volume redis_data.
- gateway: Nginx public port 80.
- authservice.
- userservice.
- messagecallservice.
- notificationservice.
- webserver.

### 26.2. Gateway Nginx

Nginx route:

- / về WebServer.
- /ws về WebServer với header WebSocket.
- /api/auth/ về AuthService.
- /api/users/ về UserService.
- /api/conversations/ về MessageCallService.
- /api/notifications/ về NotificationService.
- /health trả text healthy.

### 26.3. Health endpoint

UserService, MessageCallService và NotificationService có /health trả trạng thái healthy. AuthService và WebServer chưa thấy map endpoint /health riêng trong Program.cs.

## 27. Phân quyền và bảo mật

### 27.1. Cơ chế đăng nhập

WebServer dùng cookie authentication. Các controller HomeController và ProfileController có [Authorize], nên người dùng phải đăng nhập mới truy cập được chat và profile.

### 27.2. Kiểm soát nghiệp vụ

- Gửi tin nhắn lấy senderId từ cookie, giảm rủi ro client giả sender.
- Backend MessageCallService kiểm tra người gửi/người xem là thành viên conversation.
- Quản lý nhóm kiểm tra owner trước khi đổi tên, đổi ảnh hoặc xóa thành viên.
- Profile update lấy accountId từ cookie, không lấy accountId từ form.
- Post update/delete kiểm tra post thuộc accountId.

### 27.3. Rủi ro bảo mật hiện tại

- Mật khẩu lưu plain text, cần hash bằng BCrypt/Argon2 trước khi dùng thật.
- AuthService login trả password về response.
- Connection string và mật khẩu SQL Server xuất hiện trong appsettings/docker-compose cho môi trường dev.
- Upload file local cần kiểm tra MIME type, dung lượng, extension chặt hơn.
- WebSocket subscribe chưa thấy kiểm tra membership theo conversation.
- Chưa có CSRF protection rõ cho các POST form nếu dùng cookie auth.
- Logout chưa hoàn chỉnh.

## 28. Các chức năng đã hoàn chỉnh tương đối

- Đăng nhập bằng email/mật khẩu.
- Đăng ký qua OTP email.
- Tìm user theo email.
- Tạo hoặc mở chat 1-1.
- Tạo nhóm chat.
- Xem danh sách thread.
- Xem lịch sử tin nhắn.
- Gửi text, ảnh, audio.
- Nhận tin nhắn realtime qua WebSocket.
- Xem và chỉnh sửa profile.
- Upload avatar và ảnh bìa.
- Tạo, sửa, xóa mềm bài viết cá nhân.
- Quản lý nhóm cơ bản: đổi tên, đổi ảnh, copy link, xóa thành viên, rời nhóm, chuyển owner.
- Thử nghiệm gọi 1-1 và gọi nhóm.
- Redis cache cho một số dữ liệu đọc nhiều.
- Docker Compose cơ bản với SQL Server, Redis, gateway và các service.

## 29. Các chức năng còn dang dở hoặc cần hoàn thiện

- Đăng xuất: UI mới alert demo, chưa thấy endpoint logout thật.
- Quên mật khẩu: UI có link nhưng chưa có nghiệp vụ.
- Tab Chưa đọc/Nhóm trên sidebar chưa thấy xử lý lọc đầy đủ.
- Đánh dấu chưa đọc, tắt thông báo, chặn, lưu trữ, xóa đoạn chat, báo cáo: menu có hiển thị nhưng chưa thấy nghiệp vụ backend.
- Mark read endpoint hiện chưa cập nhật trạng thái đọc thật.
- is_read đang là bool global, chưa đủ cho read receipt theo từng thành viên.
- Friendships có bảng nhưng chưa có flow kết bạn đầy đủ.
- NotificationService có API nhưng chưa tích hợp UI chuông thông báo và realtime notification.
- Like, comment, share bài viết có bảng/UI nhưng chưa thấy endpoint xử lý.
- Upload file đang lưu local trong WebServer, chưa phù hợp khi scale nhiều instance.
- WebSocket state và GroupCallRegistry nằm trong memory, chưa scale-out.
- Gọi WebRTC chưa có TURN server.
- Chưa có migration/initializer database chính thức, hiện chủ yếu dựa vào data.sql.
- Chưa có test tự động được thấy trong repo.

## 30. Đánh giá kiến trúc

### 30.1. Ưu điểm

- Tách project theo hướng microservice, dễ phân chia trách nhiệm.
- WebServer không truy cập database trực tiếp cho chat/profile mà gọi service qua HTTP.
- SharedKernel giúp tái sử dụng model, DTO và cache.
- Có Redis cache và fallback Noop để service vẫn chạy khi thiếu Redis.
- WebSocket realtime triển khai tương đối rõ, hỗ trợ nhiều tab cho một user.
- Nghiệp vụ nhóm chat khá đầy đủ so với một ứng dụng chat cơ bản.
- Có hướng Docker hóa đầy đủ các service.

### 30.2. Nhược điểm

- Một số service đang link source từ AuthService thay vì tách code độc lập hoàn toàn, làm ranh giới microservice chưa sạch.
- WebSocket, upload file và group call state phụ thuộc WebServer memory/local disk.
- Một số tính năng UI vượt trước backend, dẫn đến menu/nút chưa hoạt động thật.
- Bảo mật đăng nhập còn yếu do password plain text.
- Chưa có logout, forgot password, reset password.
- Chưa có cơ chế phân quyền theo policy rõ ràng ngoài cookie auth và kiểm tra thủ công trong service.

## 31. Đề xuất ưu tiên hoàn thiện

Ưu tiên 1: Bảo mật tài khoản

- Hash password.
- Không trả password trong response login.
- Thêm logout thật.
- Thêm reset password/quên mật khẩu nếu cần.
- Đưa secret ra khỏi appsettings/docker-compose production.

Ưu tiên 2: Hoàn thiện chat

- Thêm unsubscribe khi đổi conversation.
- Kiểm tra membership khi WebSocket subscribe.
- Hoàn thiện mark read theo từng user.
- Hoàn thiện tab chưa đọc.
- Bổ sung xóa/lưu trữ/chặn/báo cáo nếu UI đã có.

Ưu tiên 3: Scale realtime và upload

- Dùng Redis pub/sub hoặc backplane cho WebSocket.
- Dùng shared storage/object storage cho uploads.
- Cân nhắc sticky session nếu còn giữ WebSocket trong WebServer.

Ưu tiên 4: Hoàn thiện mạng xã hội

- Triển khai like/comment/share.
- Triển khai notification UI.
- Triển khai kết bạn dựa trên bảng friendships.

Ưu tiên 5: Production readiness

- Thêm health endpoint cho AuthService và WebServer.
- Thêm CI/CD build/test/push image.
- Chuẩn hóa database migration.
- Thêm logging tập trung và monitoring.
- Thêm giới hạn file upload theo MIME type và scan file nếu cần.

## 32. Kết luận

SystemChatBoxRealtime hiện là một hệ thống chat realtime có nền tảng nghiệp vụ khá rộng: xác thực, chat 1-1, nhóm chat, gửi text/ảnh/voice, WebSocket realtime, gọi WebRTC, hồ sơ cá nhân và bài viết. Phần nghiệp vụ chat và nhóm chat là phần nổi bật nhất, đã có luồng tạo nhóm, phân quyền owner/member, link mời, rời nhóm, chuyển owner và xóa thành viên.

Tuy nhiên, dự án vẫn ở mức phát triển/thử nghiệm. Các điểm cần xử lý trước khi dùng thực tế là bảo mật mật khẩu, logout, kiểm soát WebSocket subscription, lưu trữ file khi scale, đồng bộ realtime khi chạy nhiều instance, hoàn thiện notification, hoàn thiện các menu chat còn demo và bổ sung TURN server cho WebRTC.

Nếu dùng báo cáo này để trình bày, có thể nhấn mạnh rằng hệ thống đã chứng minh được các nghiệp vụ cốt lõi của ứng dụng chat realtime, nhưng cần một giai đoạn hardening để đạt mức production.
