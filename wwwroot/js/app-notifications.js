document.addEventListener("DOMContentLoaded", function () {
    setupNotificationBell({
        rootId: "adminNotificationBell",
        countId: "adminNotificationCount",
        subtextId: "adminNotificationSubtext",
        itemsId: "adminNotificationItems",
        markAllId: "adminMarkAllNotificationsReadBtn"
    });

    setupNotificationBell({
        rootId: "userNotificationBell",
        countId: "userNotificationCount",
        subtextId: "userNotificationSubtext",
        itemsId: "userNotificationItems",
        markAllId: "userMarkAllNotificationsReadBtn"
    });
});

function setupNotificationBell(config) {
    const root = document.getElementById(config.rootId);

    if (!root) {
        return;
    }

    const feedUrl = root.getAttribute("data-feed-url");
    const markReadUrl = root.getAttribute("data-mark-read-url");
    const markAllReadUrl = root.getAttribute("data-mark-all-read-url");

    const tokenInput = root.querySelector('input[name="__RequestVerificationToken"]');
    const requestVerificationToken = tokenInput ? tokenInput.value : "";

    const countBadge = document.getElementById(config.countId);
    const subtext = document.getElementById(config.subtextId);
    const itemsContainer = document.getElementById(config.itemsId);
    const markAllButton = document.getElementById(config.markAllId);

    function getNotificationIcon(type) {
        switch ((type || "").toLowerCase()) {
            case "booking":
                return "bi-calendar-check";

            case "document":
                return "bi-file-earmark-check";

            case "penalty":
                return "bi-exclamation-triangle";

            case "maintenance":
                return "bi-tools";

            case "review":
                return "bi-star";

            default:
                return "bi-bell";
        }
    }

    function updateUnreadCount(unreadCount) {
        /*
            IMPORTANT:
            This updates the bell dropdown red badge.
        */
        if (countBadge) {
            if (unreadCount > 0) {
                countBadge.textContent = unreadCount > 99 ? "99+" : unreadCount;
                countBadge.classList.remove("d-none");
            }
            else {
                countBadge.textContent = "0";
                countBadge.classList.add("d-none");
            }
        }

        /*
            IMPORTANT:
            This updates the admin sidebar Notifications badge.
            It only works if the sidebar has:
            id="adminSidebarNotificationCount"
        */
        const adminSidebarBadge = document.getElementById("adminSidebarNotificationCount");

        if (adminSidebarBadge && config.rootId === "adminNotificationBell") {
            if (unreadCount > 0) {
                adminSidebarBadge.textContent = unreadCount > 99 ? "99+" : unreadCount;
                adminSidebarBadge.classList.remove("d-none");
            }
            else {
                adminSidebarBadge.textContent = "0";
                adminSidebarBadge.classList.add("d-none");
            }
        }

        if (subtext) {
            subtext.textContent = unreadCount > 0
                ? unreadCount + " unread notification(s)"
                : "No unread notifications";
        }
    }
    function createNotificationItem(notification) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "app-notification-item";

        if (!notification.isRead) {
            button.classList.add("unread");
        }

        button.setAttribute("data-id", notification.id);
        button.setAttribute("data-target-url", notification.targetUrl || "#");

        const iconBox = document.createElement("span");
        iconBox.className = "app-notification-icon";

        const icon = document.createElement("i");
        icon.className = "bi " + getNotificationIcon(notification.type);

        iconBox.appendChild(icon);

        const content = document.createElement("span");
        content.className = "app-notification-content";

        const title = document.createElement("strong");
        title.textContent = notification.title || "Notification";

        const message = document.createElement("small");
        message.textContent = notification.message || "";

        const date = document.createElement("em");
        date.textContent = notification.createdAt || "";

        content.appendChild(title);
        content.appendChild(message);
        content.appendChild(date);

        button.appendChild(iconBox);
        button.appendChild(content);

        return button;
    }

    function renderNotifications(notifications) {
        if (!itemsContainer) {
            return;
        }

        itemsContainer.innerHTML = "";

        if (!notifications || notifications.length === 0) {
            const empty = document.createElement("div");
            empty.className = "app-notification-empty";
            empty.textContent = "No notifications yet.";
            itemsContainer.appendChild(empty);
            return;
        }

        notifications.forEach(function (notification) {
            itemsContainer.appendChild(createNotificationItem(notification));
        });
    }

    async function loadNotifications() {
        try {
            const response = await fetch(feedUrl, {
                method: "GET",
                cache: "no-store"
            });

            if (!response.ok) {
                return;
            }

            const data = await response.json();

            updateUnreadCount(data.unreadCount || 0);
            renderNotifications(data.notifications || []);
        }
        catch (error) {
            console.error("Unable to load notifications.", error);
        }
    }

    async function markNotificationAsRead(notificationId, targetUrl) {
        try {
            const formData = new FormData();
            formData.append("id", notificationId);

            const response = await fetch(markReadUrl, {
                method: "POST",
                headers: {
                    "RequestVerificationToken": requestVerificationToken
                },
                body: formData
            });

            if (!response.ok) {
                window.location.href = targetUrl || "#";
                return;
            }

            const data = await response.json();

            if (data.success) {
                window.location.href = data.targetUrl || targetUrl || "#";
            }
            else {
                window.location.href = targetUrl || "#";
            }
        }
        catch (error) {
            console.error("Unable to mark notification as read.", error);
            window.location.href = targetUrl || "#";
        }
    }

    async function markAllNotificationsAsRead() {
        try {
            const response = await fetch(markAllReadUrl, {
                method: "POST",
                headers: {
                    "RequestVerificationToken": requestVerificationToken
                }
            });

            if (response.ok) {
                await loadNotifications();
            }
        }
        catch (error) {
            console.error("Unable to mark all notifications as read.", error);
        }
    }

    if (itemsContainer) {
        itemsContainer.addEventListener("click", function (event) {
            const item = event.target.closest(".app-notification-item");

            if (!item) {
                return;
            }

            const notificationId = item.getAttribute("data-id");
            const targetUrl = item.getAttribute("data-target-url");

            if (!notificationId) {
                window.location.href = targetUrl || "#";
                return;
            }

            markNotificationAsRead(notificationId, targetUrl);
        });
    }

    if (markAllButton) {
        markAllButton.addEventListener("click", function () {
            markAllNotificationsAsRead();
        });
    }

    /*
        IMPORTANT:
        Polling update.
        This refreshes the bell dropdown every 10 seconds.
    */
    loadNotifications();
    setInterval(loadNotifications, 10000);
}