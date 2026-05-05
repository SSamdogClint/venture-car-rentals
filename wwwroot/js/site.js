function toggleUserSidebar() {
    const sidebar = document.getElementById("userSidebar");
    const overlay = document.getElementById("userSidebarOverlay");

    if (sidebar && overlay) {
        sidebar.classList.toggle("show");
        overlay.classList.toggle("show");
    }
}

function closeUserSidebar() {
    const sidebar = document.getElementById("userSidebar");
    const overlay = document.getElementById("userSidebarOverlay");

    if (sidebar && overlay) {
        sidebar.classList.remove("show");
        overlay.classList.remove("show");
    }
}

window.addEventListener("resize", function () {
    if (window.innerWidth > 993) {
        closeUserSidebar();
    }
});
