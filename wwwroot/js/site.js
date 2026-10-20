(() => {
    const button = document.querySelector("[data-menu-button]");
    const sidebar = document.querySelector("[data-sidebar]");
    const backdrop = document.querySelector("[data-backdrop]");
    if (!button || !sidebar || !backdrop) return;

    const closeMenu = () => {
        sidebar.classList.remove("is-open");
        backdrop.hidden = true;
        button.setAttribute("aria-expanded", "false");
    };

    const openMenu = () => {
        sidebar.classList.add("is-open");
        backdrop.hidden = false;
        button.setAttribute("aria-expanded", "true");
    };

    button.addEventListener("click", () => {
        if (sidebar.classList.contains("is-open")) closeMenu();
        else openMenu();
    });

    backdrop.addEventListener("click", closeMenu);
    sidebar.querySelectorAll("a").forEach((link) => {
        link.addEventListener("click", closeMenu);
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") closeMenu();
    });
})();
