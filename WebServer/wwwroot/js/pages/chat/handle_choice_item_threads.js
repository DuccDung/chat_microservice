import { chatService } from "../../services/chatService.js";
import { load } from "../../utils/helper.js";
import { openAppModal } from "../../utils/modal.js";
import { subscribeConversation } from "../../services/ws-client.js";
import { loadThreads } from "./threads.js";

const threadList = document.getElementById("threadList");
const peerName = document.getElementById("peerName");
const peerAvatar = document.getElementById("peerAvatar");
const peerStatus = document.getElementById("peerStatus");

console.log("threads-ui loaded", { threadList: !!threadList });

if (!threadList) {
    console.warn("Thread list not found (#threadList)");
} else {
    threadList.addEventListener("click", async (e) => {
        console.log("threadList click", e.target);

        const moreBtn = e.target.closest(".more-btn");
        if (moreBtn) {
            e.stopPropagation();
            const item = moreBtn.closest(".thread-item");
            if (!item) return;

            const menu = item.querySelector(".thread-menu");
            if (!menu) return;

            closeAllMenusExcept(menu);
            menu.hidden = !menu.hidden;
            return;
        }

        const groupInfo = e.target.closest(".js-group-info");
        if (groupInfo) {
            e.preventDefault();
            e.stopPropagation();

            const item = groupInfo.closest(".thread-item");
            closeAllMenusExcept(null);
            await openGroupInfo(item?.dataset.id);
            return;
        }

        const viewProfile = e.target.closest(".js-view-profile");
        if (viewProfile) {
            e.preventDefault();
            e.stopPropagation();

            const item = viewProfile.closest(".thread-item");
            const accountId = item?.dataset.otherAccountId;
            closeAllMenusExcept(null);

            if (accountId) {
                window.location.href = `/profile/${encodeURIComponent(accountId)}`;
            }
            return;
        }

        const item = e.target.closest(".thread-item");
        if (!item || !threadList.contains(item)) return;

        closeAllMenusExcept(null);
        await openThread(item);
    });

    document.addEventListener("click", (e) => {
        if (!e.target.closest("#threadList") && !e.target.closest(".thread-list")) {
            closeAllMenusExcept(null);
        }
    });

    autoOpenFirstThreadWhenReady();
}

document.addEventListener("click", async (e) => {
    const avatarBtn = e.target.closest(".group_manage__avatar_btn");
    if (avatarBtn) {
        avatarBtn.closest(".group_manage")?.querySelector(".group_manage__avatar_input")?.click();
        return;
    }

    const saveBtn = e.target.closest(".group_manage__save");
    if (saveBtn) {
        const modal = saveBtn.closest(".group_manage");
        const conversationId = modal?.dataset.conversationId;
        const title = modal?.querySelector(".group_manage__title_input")?.value?.trim() || "";
        const avatarInput = modal?.querySelector(".group_manage__avatar_input");
        const avatarFile = avatarInput?.files?.[0] || null;

        if (!conversationId) return;
        if (!title) {
            alert("Tên nhóm không được để trống.");
            return;
        }

        try {
            load(true);
            const updated = await chatService.updateGroupSettings(conversationId, title, avatarFile);
            await loadThreads();

            const res = await chatService.getGroupInfoView(conversationId);
            openAppModal(res.data);
            updateActiveConversationHeader(updated?.data);
            load(false);
        } catch (err) {
            console.error(err);
            load(false);
            alert(getErrorMessage(err, "Không lưu được thông tin nhóm."));
        }
        return;
    }

    const leaveBtn = e.target.closest(".group_manage__leave");
    if (leaveBtn) {
        const modal = leaveBtn.closest(".group_manage");
        const conversationId = modal?.dataset.conversationId;
        const isOwner = leaveBtn.dataset.isOwner === "true";
        const successorSelect = modal?.querySelector(".group_manage__successor");
        const successorId = successorSelect?.value ? Number(successorSelect.value) : null;

        if (!conversationId) return;

        if (isOwner && successorSelect && !successorId) {
            alert("Vui lòng chọn trưởng nhóm kế thừa trước khi rời nhóm.");
            return;
        }

        const confirmText = successorSelect
            ? "Rời nhóm và chuyển quyền trưởng nhóm cho người được chọn?"
            : "Rời nhóm? Nếu nhóm không còn đủ thành viên, nhóm sẽ bị giải tán.";

        if (!confirm(confirmText)) return;

        try {
            load(true);
            const result = await chatService.leaveGroup(conversationId, successorId);
            await loadThreads();
            clearConversationIfActive(conversationId, result?.data?.dissolved);
            document.querySelector("[data-modal-close='true']")?.click();
            load(false);
        } catch (err) {
            console.error(err);
            load(false);
            alert(getErrorMessage(err, "Không rời nhóm được."));
        }
        return;
    }

    const removeBtn = e.target.closest(".group_manage__remove[data-member-id]");
    if (removeBtn) {
        const modal = removeBtn.closest(".group_manage");
        const conversationId = modal?.dataset.conversationId;
        const memberId = removeBtn.dataset.memberId;

        if (!conversationId || !memberId) return;
        if (!confirm("Xóa thành viên này khỏi nhóm?")) return;

        try {
            load(true);
            await chatService.removeGroupMember(conversationId, memberId);
            const res = await chatService.getGroupInfoView(conversationId);
            openAppModal(res.data);
            load(false);
        } catch (err) {
            console.error(err);
            load(false);
            alert(getErrorMessage(err, "Không xóa được thành viên."));
        }
        return;
    }

    const copyBtn = e.target.closest(".group_manage__copy[data-copy-text]");
    if (copyBtn) {
        const text = copyBtn.dataset.copyText || "";

        try {
            await navigator.clipboard.writeText(text);
            copyBtn.textContent = "Đã sao chép";
            setTimeout(() => {
                copyBtn.textContent = "Sao chép";
            }, 1600);
        } catch {
            const input = copyBtn.closest(".group_manage__invite")?.querySelector("input");
            input?.select();
            document.execCommand("copy");
        }
    }
});

document.addEventListener("change", (e) => {
    const input = e.target.closest(".group_manage__avatar_input");
    if (!input) return;

    const file = input.files?.[0];
    if (!file) return;

    const preview = input.closest(".group_manage")?.querySelector(".group_manage__group_avatar");
    if (preview) {
        preview.src = URL.createObjectURL(file);
    }
});

function closeAllMenusExcept(menuToKeep) {
    document.querySelectorAll(".thread-menu").forEach((m) => {
        if (m !== menuToKeep) m.hidden = true;
    });
}

function setActiveItem(item) {
    document.querySelectorAll(".thread-item.active").forEach((li) => li.classList.remove("active"));
    item.classList.add("active");
}

async function openThread(item) {
    const conversationId = item.dataset.id;
    if (!conversationId) return;

    setActiveItem(item);

    if (peerName) peerName.textContent = item.dataset.name || "Người dùng";
    if (peerAvatar) peerAvatar.src = item.dataset.avatar || peerAvatar.src;
    if (peerStatus) peerStatus.textContent = item.dataset.isGroup === "true" ? "Nhóm chat" : "";

    item.classList.remove("highlight", "unread");
    const badge = item.querySelector(".unread-badge");
    if (badge) badge.remove();

    await loadMessages(conversationId);
    subscribeConversation(parseInt(conversationId, 10));
}

async function loadMessages(conversationId) {
    const scroller = document.getElementById("messageScroller");
    if (!scroller) return;

    scroller.innerHTML = `<div class="loading">Đang tải...</div>`;

    try {
        const res = await chatService.getConversationView(conversationId);
        const html = res.data;

        const temp = document.createElement("div");
        temp.innerHTML = html;

        const newSection = temp.querySelector("#messageScroller");
        scroller.innerHTML = newSection ? newSection.innerHTML : html;

        scroller.scrollTop = scroller.scrollHeight;
    } catch (err) {
        console.error(err);
        scroller.innerHTML = `<div class="error">Không tải được tin nhắn.</div>`;
    }
}

async function openGroupInfo(conversationId) {
    if (!conversationId) return;

    try {
        load(true);
        const res = await chatService.getGroupInfoView(conversationId);
        openAppModal(res.data);
        load(false);
    } catch (err) {
        console.error(err);
        load(false);
        alert(getErrorMessage(err, "Không tải được thông tin nhóm."));
    }
}

function getErrorMessage(error, fallback) {
    return error?.response?.data?.message || error?.message || fallback;
}

function updateActiveConversationHeader(group) {
    if (!group) return;

    const active = document.querySelector(`.thread-item[data-id="${group.conversationId}"]`);
    if (active) {
        active.dataset.name = group.title || active.dataset.name;
        active.dataset.avatar = group.avatarUrl || active.dataset.avatar;
        active.click();
    } else {
        if (peerName && group.title) peerName.textContent = group.title;
        if (peerAvatar && group.avatarUrl) peerAvatar.src = group.avatarUrl;
    }
}

function clearConversationIfActive(conversationId, dissolved) {
    const active = document.querySelector(`.thread-item.active[data-id="${conversationId}"]`);
    if (!active) return;

    if (peerName) peerName.textContent = dissolved ? "Nhóm đã giải tán" : "Bạn đã rời nhóm";
    if (peerStatus) peerStatus.textContent = "";
    if (peerAvatar) peerAvatar.src = "/assets/icons/group-default.svg";

    const scroller = document.getElementById("messageScroller");
    if (scroller) {
        scroller.innerHTML = `<div class="threads-empty">${dissolved ? "Nhóm đã giải tán." : "Bạn đã rời khỏi nhóm này."}</div>`;
    }
}

function autoOpenFirstThreadWhenReady() {
    const maxWaitMs = 5000;
    const intervalMs = 50;
    const start = Date.now();

    const timer = setInterval(async () => {
        const firstItem = threadList?.querySelector(".thread-item");
        if (firstItem) {
            clearInterval(timer);

            const alreadyActive = threadList.querySelector(".thread-item.active");
            if (alreadyActive) return;

            await openThread(firstItem);
            return;
        }

        if (Date.now() - start > maxWaitMs) {
            clearInterval(timer);
            console.warn("autoOpenFirstThreadWhenReady: timeout - no thread items found");
        }
    }, intervalMs);
}
