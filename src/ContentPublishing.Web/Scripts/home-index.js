(function () {
    function setVisible(element, isVisible) {
        if (!element) {
            return;
        }

        if (isVisible) {
            element.classList.remove("hidden");
            return;
        }

        element.classList.add("hidden");
    }

    function applyRoleState(root, roleState) {
        var roleMap = {
            admin: !!roleState.isAdmin,
            reviewer: !!roleState.isReviewer,
            author: !!roleState.isAuthor
        };

        var roleVisibleNodes = root.querySelectorAll("[data-role-visible]");
        roleVisibleNodes.forEach(function (node) {
            var roleName = (node.getAttribute("data-role-visible") || "").toLowerCase();
            setVisible(node, !!roleMap[roleName]);
        });

        var roleHiddenNodes = root.querySelectorAll("[data-role-hidden]");
        roleHiddenNodes.forEach(function (node) {
            var roleName = (node.getAttribute("data-role-hidden") || "").toLowerCase();
            setVisible(node, !roleMap[roleName]);
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        var root = document.getElementById("homeDashboard");
        if (!root) {
            return;
        }

        if (window.appRoleState) {
            applyRoleState(root, window.appRoleState);
        }

        document.addEventListener("app:roleStateLoaded", function (event) {
            applyRoleState(root, (event && event.detail) || {});
        });
    });
})();
