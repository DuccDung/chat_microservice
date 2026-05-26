import { chatService } from "../../services/chatService.js";
import { load } from "../../utils/helper.js";

const threadsContainer = document.getElementById("threadList");

export async function loadThreads() {
    if (!threadsContainer) return;

    try {
        load(true);
        const res = await chatService.getThreadsView();
        threadsContainer.innerHTML = res.data;
        threadsContainer.dispatchEvent(new CustomEvent("threads:loaded"));
        load(false);
    } catch (e) {
        console.log(e);
        load(false);
        threadsContainer.innerHTML = `<div class="threads-empty">Không tải được danh sách cuộc trò chuyện.</div>`;
    }
}
