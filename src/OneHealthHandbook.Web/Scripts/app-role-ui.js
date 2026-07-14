(function () {
    var clearLoadingTimeoutId = null;

    function setAppState(state) {
        document.body.setAttribute("data-app-state", state);
    }

    function clearLoadingStateWithFallback() {
        if (clearLoadingTimeoutId) {
            clearTimeout(clearLoadingTimeoutId);
        }

        clearLoadingTimeoutId = setTimeout(function () {
            if (document.body.getAttribute("data-app-state") === "loading") {
                setAppState("ready");
                showPageMessage("error", "Role state took too long to load. Showing default navigation.");
            }
        }, 4000);
    }

    function showPageMessage(type, message) {
        var messageNode = document.getElementById("appPageMessage");
        if (!messageNode) {
            return;
        }

        if (!message) {
            messageNode.textContent = "";
            messageNode.className = "app-page-message";
            messageNode.removeAttribute("aria-live");
            return;
        }

        messageNode.textContent = message;
        messageNode.className = "app-page-message " + type;
        messageNode.setAttribute("aria-live", "polite");
    }

    function applyEmptyStates(roleState) {
        var hasPrivilegedRole = !!(roleState.isAdmin || roleState.isReviewer || roleState.isAuthor);
        document.querySelectorAll("[data-empty-on-no-role]").forEach(function (node) {
            node.setAttribute("data-empty", hasPrivilegedRole ? "false" : "true");
        });
    }

    function setVisible(element, isVisible) {
        if (!element) {
            return;
        }

        if (isVisible) {
            element.classList.remove("hidden");
        } else {
            element.classList.add("hidden");
        }
    }

    function getRoleMeta(roleState) {
        if (roleState.isAdmin) {
            return {
                name: "Admin",
                badgeClass: "app-role-admin",
                iconClass: "ri-shield-user-line"
            };
        }

        if (roleState.isReviewer) {
            return {
                name: "Reviewer",
                badgeClass: "app-role-reviewer",
                iconClass: "ri-file-search-line"
            };
        }

        if (roleState.isAuthor) {
            return {
                name: "Author",
                badgeClass: "app-role-author",
                iconClass: "ri-quill-pen-line"
            };
        }

        return {
            name: "Unassigned",
            badgeClass: "app-role-unassigned",
            iconClass: "ri-user-line"
        };
    }

    function applyRoleVisibility(roleState) {
        var roleMap = {
            admin: !!roleState.isAdmin,
            reviewer: !!roleState.isReviewer,
            author: !!roleState.isAuthor
        };

        document.querySelectorAll("[data-role-visible]").forEach(function (node) {
            var roleName = (node.getAttribute("data-role-visible") || "").toLowerCase();
            setVisible(node, !!roleMap[roleName]);
        });

        document.querySelectorAll("[data-role-hidden]").forEach(function (node) {
            var roleName = (node.getAttribute("data-role-hidden") || "").toLowerCase();
            setVisible(node, !roleMap[roleName]);
        });
    }

    function applyRoleBadge(roleState) {
        var roleMeta = getRoleMeta(roleState);
        var badgeTargets = [
            document.getElementById("appRoleBadge"),
            document.getElementById("appSidebarRoleBadge")
        ].filter(Boolean);

        badgeTargets.forEach(function (badge) {
            badge.classList.remove("app-role-admin", "app-role-reviewer", "app-role-author", "app-role-unassigned");
            roleMeta.badgeClass.split(" ").forEach(function (cssClass) {
                badge.classList.add(cssClass);
            });
        });

        document.querySelectorAll("[data-role-text]").forEach(function (node) {
            node.textContent = roleMeta.name;
        });

        document.querySelectorAll("[data-role-icon]").forEach(function (icon) {
            icon.className = roleMeta.iconClass;
            icon.setAttribute("aria-hidden", "true");
        });
    }

    function applyRoleState(roleState, options) {
        var settings = options || {};

        applyRoleVisibility(roleState || {});
        applyRoleBadge(roleState || {});
        applyEmptyStates(roleState || {});

        window.appRoleState = roleState || {};
        setAppState("ready");

        if (!settings.preserveMessage) {
            showPageMessage("", "");
        }

        document.dispatchEvent(new CustomEvent("app:roleStateLoaded", {
            detail: window.appRoleState
        }));
    }

    function normalizeRoleState(responsePayload) {
        if (!responsePayload) {
            return {};
        }

        if (responsePayload.value && typeof responsePayload.value === "object") {
            return responsePayload.value;
        }

        return responsePayload;
    }

    function loadRoleState() {
        var roleStateUrl = document.body.getAttribute("data-role-state-url");
        if (!roleStateUrl) {
            setAppState("ready");
            return;
        }

        setAppState("loading");
        clearLoadingStateWithFallback();

        fetch(roleStateUrl, {
            method: "GET",
            credentials: "same-origin",
            headers: {
                "Accept": "application/json"
            }
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Unable to load role state");
                }

                return response.json();
            })
            .then(function (responsePayload) {
                if (clearLoadingTimeoutId) {
                    clearTimeout(clearLoadingTimeoutId);
                    clearLoadingTimeoutId = null;
                }
                applyRoleState(normalizeRoleState(responsePayload));
            })
            .catch(function () {
                if (clearLoadingTimeoutId) {
                    clearTimeout(clearLoadingTimeoutId);
                    clearLoadingTimeoutId = null;
                }
                applyRoleState({
                    isAuthenticated: false,
                    isAdmin: false,
                    isReviewer: false,
                    isAuthor: false
                }, {
                    preserveMessage: true
                });
                showPageMessage("error", "We could not load role state. Limited navigation is shown.");
            });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", function () {
            loadRoleState();
        });
    } else {
        loadRoleState();
    }
})();
