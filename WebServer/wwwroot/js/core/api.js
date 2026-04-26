import axios from "https://cdn.jsdelivr.net/npm/axios@1.6.8/+esm";

export const api_origin = axios.create({
    baseURL: window.location.origin,
    withCredentials: true,
    headers: {
        "Accept": "application/json",
        "Content-Type": "application/json",
    },
});

api_origin.interceptors.request.use((config) => {
    // Nếu là FormData thì xoá Content-Type để axios tự set boundary
    if (config.data instanceof FormData) {
        delete config.headers["Content-Type"];
        delete config.headers["content-type"];
    }
    return config;
});