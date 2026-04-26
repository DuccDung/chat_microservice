import { authService } from "../../services/authService.js";
import { load } from "../../utils/helper.js";

// ===== Helpers =====
const $ = (id) => document.getElementById(id);

const loginForm = $("auth_form-login");
const btnSubmit = $("form_login_btn_submit");
const pageLoading = $("pageLoading");

const emailField = $("email-field");
const emailInput = $("email");
const emailError = $("email-error");

const passField = $("pass-field");
const passInput = $("password");
const passError = $("pass-error");

const rememberMeInput = $("rememberMe");
const togglePassBtn = $("toggle-pass");

// ===== UI: page overlay loading =====
function setPageLoading(isLoading) {
    if (!pageLoading) return;
    pageLoading.style.display = isLoading ? "flex" : "none";
    pageLoading.setAttribute("aria-hidden", isLoading ? "false" : "true");
}

// ===== UI: button loading =====
function setLoginLoading(isLoading) {
    if (!btnSubmit) return;

    btnSubmit.classList.toggle("is-loading", !!isLoading);
    btnSubmit.disabled = !!isLoading;
}

// ===== UI: errors =====
function showFieldError(fieldEl, errorEl, message) {
    if (!fieldEl || !errorEl) return;
    fieldEl.classList.add("error");
    errorEl.textContent = message || "Thông tin không hợp lệ.";
    errorEl.classList.add("show");
}

function clearFieldError(fieldEl, errorEl) {
    if (!fieldEl || !errorEl) return;
    fieldEl.classList.remove("error");
    errorEl.textContent = "";
    errorEl.classList.remove("show");
}

function showLoginErrors({ email, password } = {}) {
    if (email) showFieldError(emailField, emailError, email);
    if (password) showFieldError(passField, passError, password);
}

function clearLoginErrors() {
    clearFieldError(emailField, emailError);
    clearFieldError(passField, passError);
}

// ===== Password toggle =====
function setShowingPassword(state) {
    if (!passInput || !togglePassBtn) return;
    passInput.type = state ? "text" : "password";
    togglePassBtn.setAttribute("aria-pressed", String(state));
    togglePassBtn.setAttribute("aria-label", state ? "Ẩn mật khẩu" : "Hiện mật khẩu");
    togglePassBtn.classList.toggle("showing-pass", !!state);
}

function updatePasswordButtonVisibility() {
    if (!passField || !passInput) return;

    const hasText = passInput.value.length > 0;
    passField.classList.toggle("has-text", hasText);

    if (!hasText) setShowingPassword(false);
}

// ===== Validate =====
function basicValidate() {
    clearLoginErrors();

    const emailVal = (emailInput?.value || "").trim();
    const passVal = passInput?.value || "";

    let ok = true;

    if (!emailVal) {
        showFieldError(emailField, emailError, "Vui lòng nhập email hoặc số di động.");
        ok = false;
    }

    if (!passVal) {
        showFieldError(passField, passError, "Vui lòng nhập mật khẩu.");
        ok = false;
    }

    return ok;
}

// ===== Handle submit =====
async function handleSubmit(e) {
    e.preventDefault();
    if (!basicValidate()) return;

    const email = (emailInput?.value || "").trim();
    const password = passInput?.value || "";
    const rememberMe = rememberMeInput?.checked ?? false;

    try {
        // bật loading 1 lần, không chồng chéo
        setLoginLoading(true);
        setPageLoading(true);

        // nếu bạn có spinner riêng trong helper:
        load?.(true);

        const res = await authService.login(email, password, rememberMe);

        if (res?.status === 200) {
            window.localStorage.setItem("user", JSON.stringify(res.data));
            window.location.href = "/home/main";
            return;
        }

        // backend trả nhưng không ok
        showLoginErrors({
            email: "Email hoặc số di động bạn nhập không kết nối với tài khoản nào.",
            password: "Mật khẩu bạn nhập không đúng. Vui lòng thử lại."
        });
    } catch (err) {
        showLoginErrors({
            email: "Email hoặc số di động bạn nhập không kết nối với tài khoản nào.",
            password: "Mật khẩu bạn nhập không đúng. Vui lòng thử lại."
        });
    } finally {
        load?.(false);
        setPageLoading(false);
        setLoginLoading(false);
    }
}

// ===== Wire events =====
function wireEvents() {
    if (loginForm) {
        loginForm.addEventListener("submit", handleSubmit);
    }

    if (emailInput) {
        emailInput.addEventListener("input", () => {
            clearFieldError(emailField, emailError);
        });
    }

    if (passInput) {
        passInput.addEventListener("input", () => {
            updatePasswordButtonVisibility();
            clearFieldError(passField, passError);
        });
        updatePasswordButtonVisibility();
    }

    if (togglePassBtn) {
        togglePassBtn.addEventListener("click", () => {
            const isShowing = togglePassBtn.classList.contains("showing-pass");
            setShowingPassword(!isShowing);
        });
    }
}

wireEvents();

// (Tuỳ chọn) expose nếu bạn muốn debug
window.TalkyLoginUI = {
    setLoginLoading,
    setPageLoading,
    showLoginErrors,
    clearLoginErrors
};
