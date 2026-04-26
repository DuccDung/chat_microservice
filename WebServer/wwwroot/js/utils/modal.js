// ===== App Modal Shell =====
const appModal = document.getElementById("appModal");
const appModalContent = document.getElementById("appModalContent");

export function openAppModal(html, options = {}) {
    // options: { wide: boolean }
    if (!appModal || !appModalContent) return;

    appModalContent.innerHTML = html ?? "";

    // wide option
    if (options.wide) appModal.classList.add("app_modal--wide");
    else appModal.classList.remove("app_modal--wide");

    appModal.hidden = false;
    appModal.setAttribute("aria-hidden", "false");

    // khóa scroll body (tuỳ bạn, nếu đang cần scroll chat thì bỏ)
    document.documentElement.style.overflow = "hidden";
    document.body.style.overflow = "hidden";
}

export function closeAppModal() {
    if (!appModal || !appModalContent) return;

    appModal.hidden = true;
    appModal.setAttribute("aria-hidden", "true");
    appModalContent.innerHTML = "";
    appModal.classList.remove("app_modal--wide");

    document.documentElement.style.overflow = "";
    document.body.style.overflow = "";
}

// Close: click overlay hoặc bất kỳ element nào có data-modal-close
document.addEventListener("click", (e) => {
    if (!appModal || appModal.hidden) return;

    // click overlay
    if (e.target.matches("[data-modal-close='overlay']")) {
        closeAppModal();
        return;
    }

    // click vào nút close trong content: <button data-modal-close="true">
    if (e.target.closest("[data-modal-close='true']")) {
        closeAppModal();
        return;
    }
});

// ESC to close
document.addEventListener("keydown", (e) => {
    if (!appModal || appModal.hidden) return;
    if (e.key === "Escape") closeAppModal();
});
