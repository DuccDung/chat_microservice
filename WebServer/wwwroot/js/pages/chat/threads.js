import { chatService } from "../../services/chatService.js";
import { load } from "../../utils/helper.js";
import { openAppModal } from "../../utils/modal.js"; // nếu bạn muốn mở personal trong modal

const threadsContainer = document.getElementById("threadList");

// helper: check đã có threads list chưa
function hasThreadsDom() {
    return !!document.querySelector(".thread-list") || !!document.querySelector(".thread-item");
}

// Load partial threads và inject vào container
export async function loadThreads() {
    if (!threadsContainer) return;

    try {
        load(true);
        const res = await chatService.getThreadsView(); // partial html
        threadsContainer.innerHTML = res.data;
        load(false);
    } catch (e) {
        console.log(e);
        load(false);
        threadsContainer.innerHTML = `<div class="threads-empty">Không tải được danh sách cuộc trò chuyện.</div>`;
    }
}

// auto load khi vào trang (tuỳ bạn)
loadThreads();


// ===============================================================
// Event delegation cho threads (vì partial load sau)
// ===============================================================
document.addEventListener("click", async (e) => {
    if (!hasThreadsDom()) return;

    // 1) click nút 3 chấm -> toggle menu (nếu bạn chưa xử lý ở partial)
    const moreBtn = e.target.closest(".thread-item .more-btn");
    if (moreBtn) {
        e.stopPropagation();
        const item = moreBtn.closest(".thread-item");
        const menu = item?.querySelector(".thread-menu");
        if (!menu) return;

        // đóng tất cả menu khác
        document.querySelectorAll(".thread-menu").forEach(m => {
            if (m !== menu) m.hidden = true;
        });

        menu.hidden = !menu.hidden;
        return;
    }

    // 2) click ngoài -> đóng menu
    if (!e.target.closest(".thread-menu")) {
        document.querySelectorAll(".thread-menu").forEach(m => m.hidden = true);
    }

    // 3) click vào thread item -> mở conversation theo data-id
    const threadItem = e.target.closest(".thread-item[data-id]");
    if (threadItem) {
        const conversationId = threadItem.dataset.id;
        // TODO: bạn gọi api lấy messages và render khung chat
        console.log("Open conversation:", conversationId);
        return;
    }

    // 4) ví dụ click "Xem trang cá nhân"
    const viewProfile = e.target.closest(".thread-menu .js-view-profile");
    if (viewProfile) {
        // Muốn mở profile đúng userId -> thread DTO cần thêm otherUserId.
        // Nếu hiện tại chưa có, bạn chỉ mở modal placeholder.
        openAppModal(`<div style="padding:16px">Chưa có otherUserId để mở profile. Cần thêm field vào API.</div>`);
        return;
    }

    // 5) ví dụ xóa đoạn chat
    const deleteChat = e.target.closest(".thread-menu .js-delete");
    if (deleteChat) {
        // TODO: call api delete conversation (nếu bạn có)
        alert("TODO: Xóa đoạn chat (chưa implement API).");
        return;
    }
});