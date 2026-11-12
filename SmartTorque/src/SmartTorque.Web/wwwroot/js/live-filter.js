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

        // Check if form has data-ajax attribute for AJAX submission
        if (form.hasAttribute("data-ajax")) {
            submitFormAjax(form);
        } else {
            // Fall back to traditional form submission
            if (typeof form.requestSubmit === "function") {
                form.requestSubmit();
            } else {
                form.submit();
            }
        }
    }

    function submitFormAjax(form) {
        var formData = new FormData(form);
        var url = form.action || window.location.href;
        var method = form.method || "GET";

        // Add loading indicator
        var container = document.querySelector(form.getAttribute("data-target") || "main");
        if (container) {
            container.style.opacity = "0.6";
            container.style.pointerEvents = "none";
        }

        fetch(url + "?" + new URLSearchParams(formData).toString(), {
            method: method,
            headers: {
                "X-Requested-With": "XMLHttpRequest"
            }
        })
        .then(function(response) {
            if (!response.ok) throw new Error("Network response was not ok");
            return response.text();
        })
        .then(function(html) {
            if (container) {
                container.innerHTML = html;
                container.style.opacity = "1";
                container.style.pointerEvents = "";
            }
            form.dataset.liveSubmitting = "";
            restoreFocus();
        })
        .catch(function(error) {
            console.error("Live filter AJAX error:", error);
            // Fall back to regular form submission on error
            form.dataset.liveSubmitting = "";
            if (typeof form.requestSubmit === "function") {
                form.requestSubmit();
            } else {
                form.submit();
            }
        });
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
            // Increased debounce from 400ms to 800ms for better performance
            timer = setTimeout(function () { submitForm(form); }, 800);
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

        form.addEventListener("submit", function (e) {
            // If AJAX mode, prevent default form submission
            if (form.hasAttribute("data-ajax")) {
                e.preventDefault();
                submitForm(form);
            } else {
                form.dataset.liveSubmitting = "1";
            }
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
