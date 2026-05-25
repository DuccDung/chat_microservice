import { api_origin } from "../core/api.js";

export const chatService = {
    async getFormSearch() {
        return await api_origin.get("/chat/search_view");
    },

    async searchUsersByEmail(email) {
        return await api_origin.get("/chat/search_user", {
            params: { email }
        });
    },
    async searchUserJson(email) {
        return await api_origin.get("/chat/users/search", {
            params: { email }
        });
    },
    async createConversation(friendId) {
        return await api_origin.post("/chat/conversations", {
            friendId
        });
    },
    async createGroup(title, memberIds) {
        return await api_origin.post("/chat/groups", {
            title,
            memberIds
        });
    },
    async getGroupInfoView(conversationId) {
        return await api_origin.get(`/chat/groups/${conversationId}`);
    },
    async updateGroupSettings(conversationId, title, avatarFile = null) {
        const formData = new FormData();
        formData.append("Title", title ?? "");

        if (avatarFile) {
            formData.append("Avatar", avatarFile);
        }

        return await api_origin.post(`/chat/groups/${conversationId}/settings`, formData, {
            headers: {
                "Content-Type": "multipart/form-data"
            }
        });
    },
    async removeGroupMember(conversationId, memberId) {
        return await api_origin.post(`/chat/groups/${conversationId}/members/${memberId}/remove`);
    },
    async getPersonalView(userId) {
        return await api_origin.get("/chat/personal", {
            params: { userId }
        });
    },
    async getThreadsView() {
        return await api_origin.get("/chat/threads"); // HTML partial
    },
    async getConversationView(conversationId) {
        return await api_origin.get("/chat/conversation", {
            params: { conversationId }
        });
    },
    async sendTextMessage(conversationId, content, parentMessageId = null) {
        return await api_origin.post("/chat/send_message", {
            conversationId,
            content,
            parentMessageId
        });
    },
    async sendImageMessage(conversationId, file, parentMessageId = null) {
        const formData = new FormData();
        formData.append("conversationId", conversationId);
        formData.append("file", file);

        if (parentMessageId !== null) {
            formData.append("parentMessageId", parentMessageId);
        }

        return await api_origin.post("/chat/send_image", formData, {
            headers: {
                "Content-Type": "multipart/form-data"
            }
        });
    },
    async sendAudioMessage(conversationId, file, parentMessageId = null) {
        const formData = new FormData();
        formData.append("ConversationId", String(conversationId));
        formData.append("File", file);

        if (parentMessageId !== null && parentMessageId !== undefined) {
            formData.append("ParentMessageId", String(parentMessageId));
        }

        return await api_origin.post("/chat/send_audio", formData);
    },
    async getPeer(conversationId) {
        return await api_origin.get("/chat/peer", {
            params: { conversationId }
        });
    },
};
export const callService = {
    async getIncomingPopup(payload) {
        return await api_origin.get("/call/incoming_popup", {
            params: payload
        });
    },
     async getCallPopup(conversationId, callType = "video") {
        return await api_origin.get("/call/popup", {
            params: { conversationId, callType }
        });
    }
};
