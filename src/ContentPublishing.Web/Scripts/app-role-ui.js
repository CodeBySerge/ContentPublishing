(function () {
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
                badgeClass: "bg-blue-100 text-blue-800",
                iconClass: "ri-shield-user-line"
            };
        }

        if (roleState.isReviewer) {
            return {
                name: "Reviewer",
                badgeClass: "bg-emerald-100 text-emerald-800",
                iconClass: "ri-file-search-line"
            };
        }

        if (roleState.isAuthor) {
            return {
                name: "Author",
                badgeClass: "bg-violet-100 text-violet-800",
                iconClass: "ri-quill-pen-line"
            };
        }

        return {
            name: "Unassigned",
            badgeClass: "bg-amber-100 text-amber-800",
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
            badge.classList.remove("bg-blue-100", "text-blue-800", "bg-emerald-100", "text-emerald-800", "bg-violet-100", "text-violet-800", "bg-amber-100", "text-amber-800");
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

    function applyRoleState(roleState) {
        applyRoleVisibility(roleState || {});
        applyRoleBadge(roleState || {});

        window.appRoleState = roleState || {};
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
            return;
        }

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
                applyRoleState(normalizeRoleState(responsePayload));
            })
            .catch(function () {
                applyRoleState({
                    isAuthenticated: false,
                    isAdmin: false,
                    isReviewer: false,
                    isAuthor: false
                });
            });
    }

    document.addEventListener("DOMContentLoaded", function () {
        loadRoleState();
    });
})();
