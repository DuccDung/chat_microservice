import { api_origin } from "../core/api.js";

export const authService = {
    async login(email, password, rememberMe) {
        const payload = { email, password, rememberMe: rememberMe };
        const res = await api_origin.post("/auth/login", payload);
        return res; 
    },
    async register(accountName, email, password) {
        const payload = { accountName, email, password };
        const res = await api_origin.post("/auth/register", payload);
        return res;
    }
};
