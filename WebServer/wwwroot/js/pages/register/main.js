import { authService } from "../../services/authService.js";
import { load } from "../../utils/helper.js";
// Helpers
const $ = (id) => document.getElementById(id);
function setFieldError(fieldEl, errorEl, message) {
    fieldEl.classList.add("error");
    if (message) errorEl.textContent = message;
    errorEl.classList.add("show");
}
function clearFieldError(fieldEl, errorEl) {
    fieldEl.classList.remove("error");
    errorEl.classList.remove("show");
}

function setupEyeToggle(inputEl, fieldEl, btnEl) {
    function setShowing(state) {
        inputEl.type = state ? "text" : "password";
        btnEl.setAttribute("aria-pressed", String(state));
        btnEl.setAttribute("aria-label", state ? "Ẩn mật khẩu" : "Hiện mật khẩu");
        btnEl.classList.toggle("showing-pass", state);
    }

    function updateButtonVisibility() {
        if (inputEl.value.length > 0) fieldEl.classList.add("has-text");
        else {
            fieldEl.classList.remove("has-text");
            setShowing(false);
        }
    }

    inputEl.addEventListener("input", updateButtonVisibility);
    updateButtonVisibility();

    btnEl.addEventListener("click", () => {
        const isShowing = btnEl.classList.contains("showing-pass");
        setShowing(!isShowing);
    });
}

// Elements
const form = $("signup-form");

const nameField = $("name-field");
const nameInput = $("signup-accountname");
const nameError = $("name-error");

const contactField = $("contact-field");
const contactInput = $("signup-email");
const contactError = $("contact-error");

const passField = $("pass-field");
const passInput = $("signup-password");
const passError = $("pass-error");

const confirmField = $("confirm-field");
const confirmInput = $("signup-confirm-password");
const confirmError = $("confirm-error");

// Eye toggles
setupEyeToggle(passInput, passField, $("toggle-pass"));
setupEyeToggle(confirmInput, confirmField, $("toggle-confirm"));

// Clear error on typing
nameInput.addEventListener("input", () => clearFieldError(nameField, nameError));
contactInput.addEventListener("input", () => clearFieldError(contactField, contactError));
passInput.addEventListener("input", () => clearFieldError(passField, passError));
confirmInput.addEventListener("input", () => clearFieldError(confirmField, confirmError));

// Simple validators
function isValidEmail(v) {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v);
}
function isValidPhone(v) {
    return /^\d{9,11}$/.test(v.replace(/\s/g, ""));
}

let submitting = false;

form.addEventListener("submit", async (e) => {
    e.preventDefault();
    if (submitting) return;

    // reset previous errors
    clearFieldError(nameField, nameError);
    clearFieldError(contactField, contactError);
    clearFieldError(passField, passError);
    clearFieldError(confirmField, confirmError);

    const accountName = nameInput.value.trim();
    const contactVal = contactInput.value.trim();
    const password = passInput.value;
    const confirmPassword = confirmInput.value;

    // ===== Validate (sync) =====
    let ok = true;

    if (!accountName) {
        setFieldError(nameField, nameError, "Vui lòng nhập họ và tên.");
        ok = false;
    }

    if (!contactVal || !(isValidEmail(contactVal) || isValidPhone(contactVal))) {
        setFieldError(contactField, contactError, "Vui lòng nhập email hoặc số di động hợp lệ.");
        ok = false;
    }

    if (!password || password.length < 8) {
        setFieldError(passField, passError, "Mật khẩu phải có ít nhất 8 ký tự.");
        ok = false;
    }

    if (confirmPassword !== password) {
        setFieldError(confirmField, confirmError, "Mật khẩu nhập lại không khớp.");
        ok = false;
    }

    if (!ok) return;

    // ===== Call API (async) =====
    submitting = true;
    load(true);
    try {
        const res = await authService.register(accountName, contactVal, password);
        
        if (res.status === 200 || res.status === 201) {
            alert("Đăng kí thành công!");
            window.location.replace("/auth/login");
        } else {
            alert("Tài khoản đã tồn tại!");
        }
    } catch (err) {
        load(false);
        console.error("Error during registration:", err);
        alert("Tài khoản đã tồn tại!");
    } finally {
        load(false);
        submitting = false;
    }
});
