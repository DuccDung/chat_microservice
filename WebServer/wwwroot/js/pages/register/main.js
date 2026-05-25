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

const otpPanel = $("otp-panel");
const otpField = $("otp-field");
const otpInput = $("signup-otp");
const otpError = $("otp-error");
const otpSentText = $("otp-sent-text");
const submitBtn = $("signup-submit");
const resendOtpBtn = $("resend-otp");
const editInfoBtn = $("edit-register-info");

// Eye toggles
setupEyeToggle(passInput, passField, $("toggle-pass"));
setupEyeToggle(confirmInput, confirmField, $("toggle-confirm"));

// Clear error on typing
nameInput.addEventListener("input", () => clearFieldError(nameField, nameError));
contactInput.addEventListener("input", () => clearFieldError(contactField, contactError));
passInput.addEventListener("input", () => clearFieldError(passField, passError));
confirmInput.addEventListener("input", () => clearFieldError(confirmField, confirmError));
otpInput.addEventListener("input", () => {
    otpInput.value = otpInput.value.replace(/\D/g, "").slice(0, 6);
    clearFieldError(otpField, otpError);
});

// Simple validators
function isValidEmail(v) {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v);
}

let submitting = false;
let otpStep = false;
let pendingRegister = null;

function setRegisterFieldsDisabled(disabled) {
    nameInput.disabled = disabled;
    contactInput.disabled = disabled;
    passInput.disabled = disabled;
    confirmInput.disabled = disabled;
    $("toggle-pass").disabled = disabled;
    $("toggle-confirm").disabled = disabled;
}

function getErrorMessage(err, fallback) {
    const data = err?.response?.data;
    if (!data) return fallback;
    if (typeof data === "object") return data.message || fallback;
    if (typeof data === "string") {
        try {
            const parsed = JSON.parse(data);
            return parsed.message || data || fallback;
        } catch {
            return data || fallback;
        }
    }
    return fallback;
}

function validateRegisterFields() {
    clearFieldError(nameField, nameError);
    clearFieldError(contactField, contactError);
    clearFieldError(passField, passError);
    clearFieldError(confirmField, confirmError);

    const accountName = nameInput.value.trim();
    const email = contactInput.value.trim();
    const password = passInput.value;
    const confirmPassword = confirmInput.value;

    let ok = true;

    if (!accountName) {
        setFieldError(nameField, nameError, "Vui lòng nhập họ và tên.");
        ok = false;
    }

    if (!email || !isValidEmail(email)) {
        setFieldError(contactField, contactError, "Vui lòng nhập email hợp lệ.");
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

    return ok ? { accountName, email, password } : null;
}

async function sendOtp() {
    const values = validateRegisterFields();
    if (!values) return;

    submitting = true;
    load(true);
    try {
        const res = await authService.sendRegisterOtp(values.accountName, values.email, values.password);
        if (res.status === 200) {
            pendingRegister = values;
            otpStep = true;
            otpPanel.hidden = false;
            otpSentText.textContent = `Nhập mã gồm 6 số đã gửi tới ${values.email}.`;
            submitBtn.textContent = "Xác nhận đăng ký";
            setRegisterFieldsDisabled(true);
            otpInput.focus();
        }
    } catch (err) {
        console.error("Error while sending register OTP:", err);
        alert(getErrorMessage(err, "Không thể gửi mã OTP. Vui lòng thử lại."));
    } finally {
        load(false);
        submitting = false;
    }
}

async function verifyOtp() {
    clearFieldError(otpField, otpError);

    const otp = otpInput.value.trim();
    if (!/^\d{6}$/.test(otp)) {
        setFieldError(otpField, otpError, "Vui lòng nhập mã OTP gồm 6 số.");
        return;
    }

    if (!pendingRegister) {
        otpStep = false;
        otpPanel.hidden = true;
        setRegisterFieldsDisabled(false);
        submitBtn.textContent = "Gửi mã OTP";
        return;
    }

    submitting = true;
    load(true);
    try {
        const res = await authService.verifyRegisterOtp(pendingRegister.email, otp);

        if (res.status === 200 || res.status === 201) {
            alert("Đăng kí thành công!");
            window.location.replace("/auth/login");
        }
    } catch (err) {
        console.error("Error during OTP verification:", err);
        setFieldError(otpField, otpError, getErrorMessage(err, "Mã OTP không đúng hoặc đã hết hạn."));
    } finally {
        load(false);
        submitting = false;
    }
}

editInfoBtn.addEventListener("click", () => {
    otpStep = false;
    otpPanel.hidden = true;
    otpInput.value = "";
    pendingRegister = null;
    setRegisterFieldsDisabled(false);
    submitBtn.textContent = "Gửi mã OTP";
    contactInput.focus();
});

resendOtpBtn.addEventListener("click", async () => {
    if (submitting) return;
    await sendOtp();
});

form.addEventListener("submit", async (e) => {
    e.preventDefault();
    if (submitting) return;

    if (!otpStep) await sendOtp();
    else await verifyOtp();
});
