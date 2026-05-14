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

/*
    IMPORTANT:
    Admin sidebar hamburger functions.

    These work the same way as the user sidebar hamburger:
    - toggleAdminSidebar() opens/closes the admin sidebar
    - closeAdminSidebar() closes it when clicking the overlay
*/
function toggleAdminSidebar() {
    const sidebar = document.getElementById("adminSidebar");
    const overlay = document.getElementById("adminSidebarOverlay");

    if (sidebar) {
        sidebar.classList.toggle("show");
    }

    if (overlay) {
        overlay.classList.toggle("show");
    }
}

function closeAdminSidebar() {
    const sidebar = document.getElementById("adminSidebar");
    const overlay = document.getElementById("adminSidebarOverlay");

    if (sidebar) {
        sidebar.classList.remove("show");
    }

    if (overlay) {
        overlay.classList.remove("show");
    }
}

/*
    IMPORTANT:
    Close admin sidebar when pressing ESC.
*/
document.addEventListener("keydown", function (event) {
    if (event.key === "Escape") {
        closeAdminSidebar();
    }
});

/*
    IMPORTANT:
    If screen returns to desktop size, close the mobile admin sidebar.
*/
window.addEventListener("resize", function () {
    if (window.innerWidth > 993) {
        closeAdminSidebar();
    }
});

