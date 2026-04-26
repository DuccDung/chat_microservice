// mobile-sidebar.js
// Điều khiển hiển thị list/chat ở mobile bằng class: .app.show-list

const app = document.querySelector(".app");
const threadList = document.getElementById("threadList");

//  Bạn tự gắn id vào nút (ví dụ)
// - nút mở list (ở màn chat): id="openListBtn"
// - nút back quay lại list: id="backBtn" (bạn đã có)
const openListBtn = document.getElementById("openListBtn"); // bạn tự tạo nút này
const backBtn = document.getElementById("backBtn");

function isMobile() {
    return window.matchMedia("(max-width: 900px)").matches;
}

function showList() {
    if (!app) return;
    app.classList.add("show-list");
}

function showChat() {
    if (!app) return;
    app.classList.remove("show-list");
}

//  1) Khi login / load page trên mobile: hiển thị list trước
document.addEventListener("DOMContentLoaded", () => {
    if (!app) return;

    if (isMobile()) {
        showList(); // mở sidebar list trước
    } else {
        showChat(); // PC luôn hiện chat pane
    }
});

//  2) Khi click vào 1 conversation item: trên mobile -> ẩn list, hiện chat
if (threadList) {
    threadList.addEventListener("click", (e) => {
        const item = e.target.closest(".thread-item");
        if (!item) return;

        // nếu click 3 chấm menu thì không đổi view
        if (e.target.closest(".more-btn") || e.target.closest(".thread-menu")) return;

        if (isMobile()) {
            showChat();
        }
    });
}

//  3) Nút back: quay lại list (mobile)
if (backBtn) {
    backBtn.addEventListener("click", (e) => {
        e.preventDefault();
        if (!isMobile()) return;
        showList();
    });
}

//  4) Nút “mở danh sách conversation” (bạn tự gắn id): hiện list
if (openListBtn) {
    openListBtn.addEventListener("click", (e) => {
        e.preventDefault();
        if (!isMobile()) return;
        showList();
    });
}

//  5) Khi xoay màn hình / resize: tự cân lại state
window.addEventListener("resize", () => {
    if (!app) return;

    if (!isMobile()) {
        // về PC -> luôn hiện chat pane (không cần show-list nữa)
        showChat();
    } else {
        // về mobile:
        // nếu chưa chọn thread nào active thì ưu tiên hiện list
        const hasActive = !!document.querySelector(".thread-item.active");
        if (!hasActive) showList();
    }
});