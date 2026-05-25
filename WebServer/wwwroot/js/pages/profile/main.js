const page = document.querySelector(".profile-page");
const currentAccountId = page?.dataset.currentAccountId || "";
const defaultAvatar = page?.dataset.defaultAvatar || "/assets/images/avatar-default.png";
const defaultCover = page?.dataset.defaultCover || "/assets/images/cover_default.jpg";

const editProfileModal = document.getElementById("editProfileModal");
const editProfileForm = document.getElementById("editProfileForm");
const postModal = document.getElementById("postModal");
const postForm = document.getElementById("postForm");
const postModalTitle = document.getElementById("postModalTitle");
const postContentInput = document.getElementById("postContentInput");
const postFileInput = document.getElementById("postFileInput");
const postFilePicker = document.getElementById("postFilePicker");
const postFilePickerText = postFilePicker?.querySelector("strong");
const postImagePreview = document.getElementById("postImagePreview");
const removePostImage = document.getElementById("removePostImage");
const submitPostBtn = document.getElementById("submitPostBtn");

let editingPostId = null;
let editingPostCard = null;
let editingExistingImageUrl = "";

function openModal(modal) {
    modal.hidden = false;
    document.body.style.overflow = "hidden";
}

function closeModal(modal) {
    modal.hidden = true;
    document.body.style.overflow = "";
}

function closeAllModals() {
    document.querySelectorAll(".profile-modal").forEach(closeModal);
}

function getErrorMessage(error) {
    const fallback = "Có lỗi xảy ra. Vui lòng thử lại.";
    if (!error) return fallback;
    if (typeof error === "string") return error || fallback;
    return error.message || fallback;
}

async function parseResponse(res) {
    const text = await res.text();
    let data = null;
    if (text) {
        try {
            data = JSON.parse(text);
        } catch {
            data = text;
        }
    }

    if (!res.ok) {
        const message = typeof data === "object" ? data?.message : data;
        throw new Error(message || `Request failed: ${res.status}`);
    }

    return data;
}

async function postFormData(url, formData) {
    const res = await fetch(url, {
        method: "POST",
        body: formData
    });
    return parseResponse(res);
}

function setButtonBusy(button, busy) {
    if (!button) return;
    button.disabled = busy;
    if (busy) {
        button.dataset.originalText = button.textContent;
        button.textContent = "Đang xử lý...";
        return;
    }

    button.textContent = button.dataset.originalText || button.textContent;
    delete button.dataset.originalText;
}

function setImageFallbacks() {
    document.querySelectorAll("img").forEach((img) => {
        img.addEventListener("error", () => {
            img.src = defaultAvatar;
        }, { once: true });
    });
}

function openPostCreateModal() {
    editingPostId = null;
    editingPostCard = null;
    editingExistingImageUrl = "";
    postModalTitle.textContent = "Tạo bài viết";
    submitPostBtn.textContent = "Đăng";
    postContentInput.value = "";
    postFileInput.value = "";
    postFilePicker.hidden = false;
    if (postFilePickerText) postFilePickerText.textContent = "Thêm ảnh vào bài viết";
    if (removePostImage) removePostImage.hidden = false;
    hidePostPreview();
    openModal(postModal);
    postContentInput.focus();
}

function openPostEditModal(postCard) {
    if (currentAccountId && postCard.dataset.ownerId && postCard.dataset.ownerId !== currentAccountId) {
        alert("Bạn chỉ có thể sửa bài viết của chính mình.");
        return;
    }

    editingPostId = postCard.dataset.postId;
    editingPostCard = postCard;
    editingExistingImageUrl = postCard.querySelector(".profile-post__image")?.getAttribute("src") || "";
    const content = postCard.querySelector(".profile-post__content")?.textContent?.trim() || "";
    postModalTitle.textContent = "Sửa bài viết";
    submitPostBtn.textContent = "Lưu";
    postContentInput.value = content;
    postFileInput.value = "";
    postFilePicker.hidden = false;
    if (postFilePickerText) postFilePickerText.textContent = editingExistingImageUrl ? "Thay ảnh trong bài viết" : "Thêm ảnh vào bài viết";
    if (editingExistingImageUrl) {
        showExistingPostPreview(editingExistingImageUrl);
    } else {
        hidePostPreview();
    }
    openModal(postModal);
    postContentInput.focus();
}

function showPostPreview(file) {
    if (!file) return;
    const url = URL.createObjectURL(file);
    const img = postImagePreview.querySelector("img");
    img.src = url;
    if (removePostImage) removePostImage.hidden = false;
    postImagePreview.hidden = false;
}

function showExistingPostPreview(url) {
    const img = postImagePreview.querySelector("img");
    img.src = url;
    if (removePostImage) removePostImage.hidden = true;
    postImagePreview.hidden = false;
}

function hidePostPreview() {
    const img = postImagePreview.querySelector("img");
    img.removeAttribute("src");
    if (removePostImage) removePostImage.hidden = false;
    postImagePreview.hidden = true;
}

async function uploadProfileImage(input, url, onDone) {
    const file = input.files?.[0];
    if (!file) return;

    const formData = new FormData();
    formData.append("file", file);

    try {
        await postFormData(url, formData).then(onDone);
    } catch (error) {
        alert(getErrorMessage(error));
    } finally {
        input.value = "";
    }
}

document.addEventListener("click", async (event) => {
    if (event.target.closest("[data-close-modal]")) {
        closeAllModals();
        return;
    }

    if (event.target.closest("#openEditProfile") || event.target.closest("#openEditBio")) {
        openModal(editProfileModal);
        document.getElementById("profileAccountNameInput")?.focus();
        return;
    }

    if (event.target.closest("#openPostComposer") || event.target.closest("#composerInputBtn") || event.target.closest("#composerTextBtn")) {
        openPostCreateModal();
        return;
    }

    if (event.target.closest("#composerPhotoBtn")) {
        openPostCreateModal();
        postFileInput.click();
        return;
    }

    if (event.target.closest("#avatarUploadBtn")) {
        document.getElementById("avatarInput")?.click();
        return;
    }

    if (event.target.closest("#coverUploadBtn")) {
        document.getElementById("coverInput")?.click();
        return;
    }

    const menuBtn = event.target.closest(".post-menu-btn");
    if (menuBtn) {
        const menu = menuBtn.closest(".profile-post__menu")?.querySelector(".post-menu");
        document.querySelectorAll(".post-menu").forEach((item) => {
            if (item !== menu) item.hidden = true;
        });
        if (menu) menu.hidden = !menu.hidden;
        return;
    }

    if (!event.target.closest(".profile-post__menu")) {
        document.querySelectorAll(".post-menu").forEach((menu) => {
            menu.hidden = true;
        });
    }

    const editPostBtn = event.target.closest(".js-edit-post");
    if (editPostBtn) {
        const postCard = editPostBtn.closest(".profile-post");
        if (postCard) openPostEditModal(postCard);
        return;
    }

    const deletePostBtn = event.target.closest(".js-delete-post");
    if (deletePostBtn) {
        const postCard = deletePostBtn.closest(".profile-post");
        const postId = postCard?.dataset.postId;
        if (!postId) return;
        if (!confirm("Xóa bài viết này?")) return;

        try {
            await fetch(`/profile/posts/${postId}/delete`, { method: "POST" }).then(parseResponse);
            postCard.remove();
        } catch (error) {
            alert(getErrorMessage(error));
        }
    }
});

document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") closeAllModals();
});

document.getElementById("avatarInput")?.addEventListener("change", (event) => {
    uploadProfileImage(event.target, "/profile/avatar", (data) => {
        document.querySelectorAll(".profile-avatar, .profile-composer__top img, .profile-post-author img").forEach((img) => {
            img.src = data.url || defaultAvatar;
        });
    });
});

document.getElementById("coverInput")?.addEventListener("change", (event) => {
    uploadProfileImage(event.target, "/profile/cover", (data) => {
        const cover = document.querySelector(".profile-cover");
        cover.style.backgroundImage = `url('${data.url || defaultCover}')`;
    });
});

editProfileForm?.addEventListener("submit", async (event) => {
    event.preventDefault();
    const submit = editProfileForm.querySelector("button[type='submit']");
    setButtonBusy(submit, true);

    try {
        const formData = new FormData(editProfileForm);
        await postFormData("/profile/update", formData);
        window.location.reload();
    } catch (error) {
        alert(getErrorMessage(error));
    } finally {
        setButtonBusy(submit, false);
    }
});

document.getElementById("clearProfileInfo")?.addEventListener("click", async () => {
    if (!confirm("Xóa tiểu sử, ngày sinh và giới tính khỏi trang cá nhân?")) return;

    try {
        await fetch("/profile/clear-info", { method: "POST" }).then(parseResponse);
        window.location.reload();
    } catch (error) {
        alert(getErrorMessage(error));
    }
});

postFileInput?.addEventListener("change", () => {
    hidePostPreview();
    showPostPreview(postFileInput.files?.[0]);
});

removePostImage?.addEventListener("click", () => {
    postFileInput.value = "";
    if (editingPostId && editingExistingImageUrl) {
        showExistingPostPreview(editingExistingImageUrl);
        return;
    }

    hidePostPreview();
});

postForm?.addEventListener("submit", async (event) => {
    event.preventDefault();
    setButtonBusy(submitPostBtn, true);

    try {
        const formData = new FormData(postForm);
        if (editingPostId) {
            await postFormData(`/profile/posts/${editingPostId}/update`, formData);
        } else {
            await postFormData("/profile/posts", formData);
        }
        window.location.reload();
    } catch (error) {
        alert(getErrorMessage(error));
    } finally {
        setButtonBusy(submitPostBtn, false);
    }
});

setImageFallbacks();
