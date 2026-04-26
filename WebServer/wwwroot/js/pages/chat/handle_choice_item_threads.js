import { chatService } from "../../services/chatService.js";
import { subscribeConversation } from "../../services/ws-client.js";

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

    // Auto open first thread 
    autoOpenFirstThreadWhenReady();
}

function closeAllMenusExcept(menuToKeep) {
    document.querySelectorAll(".thread-menu").forEach((m) => {
        if (m !== menuToKeep) m.hidden = true;
    });
}

function setActiveItem(item) {
    document.querySelectorAll(".thread-item.active").forEach((li) => li.classList.remove("active"));
    item.classList.add("active");
}

/** Mở thread giống như click */
async function openThread(item) {
    const conversationId = item.dataset.id;
    if (!conversationId) return;

    setActiveItem(item);

    if (peerName) peerName.textContent = item.dataset.name || "Người dùng";
    if (peerAvatar) peerAvatar.src = item.dataset.avatar || peerAvatar.src;
    if (peerStatus) peerStatus.textContent = "";

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