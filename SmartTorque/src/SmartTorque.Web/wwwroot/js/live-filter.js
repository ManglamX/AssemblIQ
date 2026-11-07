(function () {
    "use strict";

    function submitForm(form) {
        if (form.dataset.liveSubmitting === "1") {
            return;
        }
        form.dataset.liveSubmitting = "1";

        var active = document.activeElement;
        if (active && active.name && form.contains(active)) {
            try {
                sessionStorage.setItem("liveFilterFocusName", active.name);
            } catch (e) { }
        }

        if (typeof form.requestSubmit === "function") {
            form.requestSubmit();
        } else {
            form.submit();
        }
    }

    function bindForm(form) {
        if (!form || form.dataset.liveFilterBound === "1") {
            return;
        }
        form.dataset.liveFilterBound = "1";

        var timer = null;

        form.addEventListener("input", function (e) {
            var el = e.target;
            if (!el || !el.matches("input, textarea")) {
                return;
            }
            if (el.type === "hidden" || el.type === "submit" || el.type === "button" ||
                el.type === "file" || el.type === "checkbox" || el.type === "radio" || el.type === "date") {
                return;
            }
            clearTimeout(timer);
            timer = setTimeout(function () { submitForm(form); }, 400);
        });

        form.addEventListener("change", function (e) {
            var el = e.target;
            if (!el || !el.matches("input, select, textarea")) {
                return;
            }
            if (el.type === "hidden" || el.type === "submit" || el.type === "button" || el.type === "file") {
                return;
            }
            clearTimeout(timer);
            submitForm(form);
        });

        form.addEventListener("submit", function () {
            form.dataset.liveSubmitting = "1";
        });
    }

    function restoreFocus() {
        var name;
        try {
            name = sessionStorage.getItem("liveFilterFocusName");
        } catch (e) {
            return;
        }
        if (!name) {
            return;
        }
        try {
            sessionStorage.removeItem("liveFilterFocusName");
        } catch (e) { }

        var forms = document.querySelectorAll("form[data-live-filter]");
        for (var f = 0; f < forms.length; f++) {
            var safe = name.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
            var el = forms[f].querySelector('[name="' + safe + '"]');
            if (!el || el.tagName === "SELECT" || el.type === "checkbox" || el.type === "radio" ||
                el.type === "hidden" || el.type === "submit") {
                continue;
            }
            el.focus();
            if (el.setSelectionRange && (el.type === "text" || el.type === "search" ||
                el.type === "tel" || el.type === "url" || el.type === "email" || el.type === "password")) {
                var len = el.value.length;
                el.setSelectionRange(len, len);
            }
            break;
        }
    }

    function init() {
        var forms = document.querySelectorAll("form[data-live-filter]");
        for (var i = 0; i < forms.length; i++) {
            bindForm(forms[i]);
        }
        restoreFocus();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
