import { chatService } from "../../services/chatService.js";
import { load } from "../../utils/helper.js";
import { openAppModal, closeAppModal } from "../../utils/modal.js";
import { loadThreads } from "./threads.js";
const btn_new_chat = document.getElementById("newMsgBtn");

btn_new_chat?.addEventListener("click", async () => {
    try {
        load(true);
        const res = await chatService.getFormSearch(); // trả về partial _FormFriends
        load(false);

        // Mở modal shell và inject content
        openAppModal(res.data);

        // Optional: focus input ngay khi mở
        queueMicrotask(() => {
            document.getElementById("friendEmailInput")?.focus();
        });

    } catch (error) {
        console.log(error);
        load(false);
        alert("Đã có lỗi xảy ra, vui lòng thử lại sau.");
    }
});

//===============================================================
//================== load threads ===============================
//===============================================================
await loadThreads();
//===============================================================
//================== Handle form friend (inside modal)===========
//===============================================================

// Helpers
function isFriendsModalOpen() {
    return !!document.querySelector(".form_friends");
}

function closeFriendsModal() {
    closeAppModal();
}

async function doSearch() {
    const input = document.getElementById("friendEmailInput");
    const results = document.getElementById("friendResults");
    if (!input || !results) return;

    const email = input.value?.trim();
    if (!email) {
        results.innerHTML = `<div class="form_friends__empty">Nhập email để tìm kiếm.</div>`;
        return;
    }

    try {
        load(true);
        const res = await chatService.searchUsersByEmail(email);
        results.innerHTML = res.data; // partial HTML results
        load(false);
    } catch (e) {
        console.log(e);
        results.innerHTML = `<div class="form_friends__empty">Có lỗi khi tìm kiếm.</div>`;
        load(false);
    }
}

// NEW: click item -> lấy userId -> gọi api lấy partial personal -> inject
async function openPersonalByUserId(userId) {
    if (!userId) return;

    try {
        load(true);

        const res = await chatService.getPersonalView(userId);

        // Inject view personal vào modal (thường bạn dùng chung appModal)
        openAppModal(res.data);

        load(false);
    } catch (e) {
        console.log(e);
        load(false);
        alert("Không thể tải thông tin cá nhân. Vui lòng thử lại.");
    }
}

// Event delegation (vì content load sau)
document.addEventListener("click", (e) => {
    if (!isFriendsModalOpen()) return;

    // Close icon (X)
    if (e.target.closest(".form_friends__icon_btn")) {
        closeFriendsModal();
        return;
    }

    // Cancel button (Hủy)
    if (e.target.closest(".form_friends__btn--ghost")) {
        closeFriendsModal();
        return;
    }

    // Search button
    if (e.target.closest("#friendSearchBtn")) {
        doSearch();
        return;
    }

    // ✅ NEW: click vào item trong kết quả -> mở personal
    const item = e.target.closest(".form_friends__item[data-user-id]");
    if (item) {
        const userId = item.dataset.userId; // string
        openPersonalByUserId(userId);
        return;
    }
});

// Enter để search + ESC đóng
document.addEventListener("keydown", (e) => {
    if (!isFriendsModalOpen()) return;

    if (e.key === "Escape") {
        closeFriendsModal();
        return;
    }

    if (e.key === "Enter") {
        const active = document.activeElement;
        if (active?.id === "friendEmailInput") {
            e.preventDefault();
            doSearch();
        }
    }
});
