import { chatService } from "../../services/chatService.js";
import { load } from "../../utils/helper.js";
import { openAppModal, closeAppModal } from "../../utils/modal.js";
import { loadThreads } from "./threads.js";

const btn_new_chat = document.getElementById("newMsgBtn");
const groupMembers = new Map();

btn_new_chat?.addEventListener("click", async () => {
    try {
        load(true);
        const res = await chatService.getFormSearch();
        load(false);

        groupMembers.clear();
        openAppModal(res.data);

        queueMicrotask(() => {
            setComposeMode("direct");
            document.getElementById("friendEmailInput")?.focus();
        });
    } catch (error) {
        console.log(error);
        load(false);
        alert("Đã có lỗi xảy ra, vui lòng thử lại sau.");
    }
});

await loadThreads();

function isFriendsModalOpen() {
    return !!document.querySelector(".form_friends");
}

function closeFriendsModal() {
    groupMembers.clear();
    closeAppModal();
}

function getComposeMode() {
    return document.querySelector(".form_friends__mode.is-active")?.dataset.composeMode || "direct";
}

function setComposeMode(mode) {
    const isGroup = mode === "group";
    document.querySelector(".form_friends")?.setAttribute("data-mode", mode);

    document.querySelectorAll(".form_friends__mode").forEach((btn) => {
        btn.classList.toggle("is-active", btn.dataset.composeMode === mode);
    });

    const groupFields = document.querySelector(".form_friends__group_fields");
    const createGroupBtn = document.getElementById("createGroupBtn");
    const results = document.getElementById("friendResults");

    if (groupFields) groupFields.hidden = !isGroup;
    if (createGroupBtn) createGroupBtn.hidden = !isGroup;

    if (results) {
        results.innerHTML = isGroup
            ? `<div class="form_friends__empty">Tìm email để thêm thành viên vào nhóm.</div>`
            : `<div class="form_friends__empty">Không có kết quả.</div>`;
    }

    renderSelectedMembers();
}

function escapeHtml(value) {
    return String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}

function getErrorMessage(error, fallback) {
    return error?.response?.data?.message || error?.message || fallback;
}

function renderSelectedMembers() {
    const wrap = document.getElementById("groupSelectedMembers");
    if (!wrap) return;

    if (groupMembers.size === 0) {
        wrap.innerHTML = `<div class="form_friends__empty form_friends__empty--compact">Chưa chọn thành viên.</div>`;
        return;
    }

    wrap.innerHTML = Array.from(groupMembers.values()).map((user) => `
        <button class="form_friends__member_chip" type="button" data-remove-group-member="${user.accountId}">
            <span>${escapeHtml(user.accountName || user.email || "Người dùng")}</span>
            <span aria-hidden="true">x</span>
        </button>
    `).join("");
}

function renderGroupCandidate(user) {
    const results = document.getElementById("friendResults");
    if (!results) return;

    const accountId = Number(user?.accountId || 0);
    if (!accountId) {
        results.innerHTML = `<div class="form_friends__empty">Không tìm thấy người dùng nào.</div>`;
        return;
    }

    const alreadySelected = groupMembers.has(accountId);
    const avatar = user.photoPath || "/assets/images/avatar-default.png";
    const name = user.accountName || "Người dùng";
    const email = user.email || "";

    results.innerHTML = `
        <div class="form_friends__list">
            <div class="form_friends__item">
                <div class="form_friends__avatar">
                    <img alt="" src="${escapeHtml(avatar)}" />
                </div>
                <div class="form_friends__meta">
                    <p class="form_friends__name">${escapeHtml(name)}</p>
                    <p class="form_friends__sub">${escapeHtml(email)}</p>
                </div>
                <div class="form_friends__right">
                    <button class="form_friends__btn form_friends__btn--primary js-add-group-member"
                            type="button"
                            data-user-id="${accountId}"
                            data-user-name="${escapeHtml(name)}"
                            data-user-email="${escapeHtml(email)}"
                            data-user-photo="${escapeHtml(avatar)}"
                            ${alreadySelected ? "disabled" : ""}>
                        ${alreadySelected ? "Đã thêm" : "Thêm"}
                    </button>
                </div>
            </div>
        </div>`;
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

        if (getComposeMode() === "group") {
            const res = await chatService.searchUserJson(email);
            renderGroupCandidate(res.data);
        } else {
            const res = await chatService.searchUsersByEmail(email);
            results.innerHTML = res.data;
        }

        load(false);
    } catch (e) {
        console.log(e);
        results.innerHTML = `<div class="form_friends__empty">${escapeHtml(getErrorMessage(e, "Có lỗi khi tìm kiếm."))}</div>`;
        load(false);
    }
}

async function openPersonalByUserId(userId) {
    if (!userId) return;

    try {
        load(true);
        const res = await chatService.getPersonalView(userId);
        openAppModal(res.data);
        load(false);
    } catch (e) {
        console.log(e);
        load(false);
        alert("Không thể tải thông tin cá nhân. Vui lòng thử lại.");
    }
}

async function createGroup() {
    const title = document.getElementById("groupTitleInput")?.value?.trim() || "";
    const memberIds = Array.from(groupMembers.keys());

    if (memberIds.length === 0) {
        alert("Vui lòng chọn ít nhất một thành viên khác bạn.");
        return;
    }

    try {
        load(true);
        const res = await chatService.createGroup(title, memberIds);
        await loadThreads();
        load(false);
        closeFriendsModal();

        const conversationId = res?.data?.conversationId;
        if (conversationId) {
            setTimeout(() => {
                document.querySelector(`.thread-item[data-id="${conversationId}"]`)?.click();
            }, 50);
        }
    } catch (e) {
        console.log(e);
        load(false);
        alert(getErrorMessage(e, "Không tạo được nhóm. Vui lòng thử lại."));
    }
}

document.addEventListener("click", (e) => {
    if (!isFriendsModalOpen()) return;

    const modeBtn = e.target.closest(".form_friends__mode[data-compose-mode]");
    if (modeBtn) {
        setComposeMode(modeBtn.dataset.composeMode);
        return;
    }

    if (e.target.closest(".form_friends__icon_btn")) {
        closeFriendsModal();
        return;
    }

    if (e.target.closest(".form_friends__btn--ghost")) {
        closeFriendsModal();
        return;
    }

    if (e.target.closest("#friendSearchBtn")) {
        doSearch();
        return;
    }

    if (e.target.closest("#createGroupBtn")) {
        createGroup();
        return;
    }

    const addMemberBtn = e.target.closest(".js-add-group-member[data-user-id]");
    if (addMemberBtn) {
        const accountId = Number(addMemberBtn.dataset.userId || 0);
        if (!accountId) return;

        groupMembers.set(accountId, {
            accountId,
            accountName: addMemberBtn.dataset.userName || "",
            email: addMemberBtn.dataset.userEmail || "",
            photoPath: addMemberBtn.dataset.userPhoto || ""
        });

        addMemberBtn.textContent = "Đã thêm";
        addMemberBtn.disabled = true;
        renderSelectedMembers();
        document.getElementById("friendEmailInput")?.select();
        return;
    }

    const removeChip = e.target.closest("[data-remove-group-member]");
    if (removeChip) {
        const accountId = Number(removeChip.dataset.removeGroupMember || 0);
        groupMembers.delete(accountId);
        renderSelectedMembers();
        return;
    }

    const item = e.target.closest(".form_friends__item[data-user-id]");
    if (item && getComposeMode() === "direct") {
        openPersonalByUserId(item.dataset.userId);
    }
});

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
