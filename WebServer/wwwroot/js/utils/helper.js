export function load(show) {
    const el = document.querySelector(".pageLoading");
    if (!el) return;
    el.style.display = show ? "flex" : "none";
}
