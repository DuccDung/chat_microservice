import { api_origin } from "../core/api.js";

export const authService = {
    async login(email, password, rememberMe) {
        const payload = { email, password, rememberMe: rememberMe };
        const res = await api_origin.post("/auth/login", payload);
        return res; 
    },
    async sendRegisterOtp(accountName, email, password) {
        const payload = { accountName, email, password };
        const res = await api_origin.post("/auth/register/send-otp", payload);
        return res;
    },
    async verifyRegisterOtp(email, otp) {
        const payload = { email, otp };
        const res = await api_origin.post("/auth/register/verify-otp", payload);
        return res;
    }
};
