// product-builder.js — dynamic component rows, reorder, image upload.
// No business logic here: it only assembles the form the server validates.
(function () {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        var form = document.getElementById("product-form");
        if (!form) return;

        var tbody = document.getElementById("components-body");
        var proto = document.getElementById("row-prototype");
        var empty = document.getElementById("components-empty");
        var countBadge = document.getElementById("component-count");

        function visibleRows() {
            return Array.prototype.slice.call(tbody.querySelectorAll(".component-row"));
        }

        function stepsOf(select) {
            var opt = select.options[select.selectedIndex];
            return opt && opt.dataset.steps ? opt.dataset.steps : "0";
        }

        function refreshRow(row) {
            var select = row.querySelector(".row-template-select");
            var badge = row.querySelector(".row-steps");
            if (select && badge) badge.textContent = stepsOf(select) + " steps";
        }

        function reindex() {
            var rows = visibleRows();
            rows.forEach(function (row, i) {
                var select = row.querySelector(".row-template-select");
                select.name = "Components[" + i + "].TemplateId";
                var nameInput = row.querySelector(".row-component-name");
                nameInput.name = "Components[" + i + "].ComponentName";
                row.querySelector(".row-quantity").name = "Components[" + i + "].Quantity";
                refreshRow(row);

                if (select && select.selectedIndex >= 0 && (!nameInput.value || nameInput.value.trim() === "")) {
                    var opt = select.options[select.selectedIndex];
                    if (opt && opt.dataset.name) {
                        nameInput.value = opt.dataset.name;
                    }
                }

                var up = row.querySelector(".row-up");
                var down = row.querySelector(".row-down");
                if (up) up.disabled = i === 0;
                if (down) down.disabled = i === rows.length - 1;
            });

            if (countBadge) countBadge.textContent = rows.length;
            if (empty) empty.style.display = rows.length === 0 ? "" : "none";
        }

        function addRow(templateId, label) {
            var row = proto.cloneNode(true);
            row.removeAttribute("id");
            row.querySelectorAll("select,input,button").forEach(function (el) { el.disabled = false; });
            row.style.display = "";
            var select = row.querySelector(".row-template-select");
            if (templateId) select.value = String(templateId);
            var nameInput = row.querySelector(".row-component-name");
            if (!label && select && select.selectedIndex >= 0) {
                var opt = select.options[select.selectedIndex];
                label = opt && opt.dataset.name ? opt.dataset.name : "";
            }
            if (label && !nameInput.value) nameInput.value = label;
            tbody.appendChild(row);
            reindex();
            nameInput.focus();
        }

        // Template picker
        document.querySelectorAll("#template-picker [data-template-id]").forEach(function (btn) {
            btn.addEventListener("click", function () {
                var name = btn.dataset.name;
                if (!name) {
                    var titleEl = btn.querySelector(".fw-semibold");
                    if (titleEl) name = titleEl.textContent.trim();
                }
                addRow(btn.dataset.templateId, name || "");
            });
        });

        // Template search
        var search = document.getElementById("template-search");
        if (search) {
            search.addEventListener("input", function () {
                var q = search.value.trim().toLowerCase();
                document.querySelectorAll("#template-picker [data-template-id]").forEach(function (btn) {
                    var hay = btn.dataset.search || "";
                    btn.style.display = hay.indexOf(q) === -1 ? "none" : "";
                });
            });

            var urlParams = new URLSearchParams(window.location.search);
            var paramCode = urlParams.get("templateCode");
            if (paramCode) {
                search.value = paramCode;
                search.dispatchEvent(new Event("input"));
            }
        }

        // Row actions (delegated)
        tbody.addEventListener("click", function (e) {
            var removeBtn = e.target.closest(".row-remove");
            if (removeBtn) {
                removeBtn.closest(".component-row").remove();
                reindex();
                return;
            }
            var upBtn = e.target.closest(".row-up");
            if (upBtn) {
                var rowUp = upBtn.closest(".component-row");
                var prev = rowUp.previousElementSibling;
                if (prev) { tbody.insertBefore(rowUp, prev); reindex(); }
                return;
            }
            var downBtn = e.target.closest(".row-down");
            if (downBtn) {
                var rowDown = downBtn.closest(".component-row");
                var next = rowDown.nextElementSibling;
                if (next) { tbody.insertBefore(next, rowDown); reindex(); }
            }
        });

        tbody.addEventListener("change", function (e) {
            if (e.target.classList.contains("row-template-select")) {
                var row = e.target.closest(".component-row");
                refreshRow(row);
                var nameInput = row.querySelector(".row-component-name");
                var opt = e.target.options[e.target.selectedIndex];
                var optName = opt && opt.dataset.name ? opt.dataset.name : "";
                if (optName && (!nameInput.value || nameInput.value.trim() === "")) {
                    nameInput.value = optName;
                }
            }
        });

        // Drag to reorder
        var dragged = null;
        tbody.addEventListener("mousedown", function (e) {
            var handle = e.target.closest(".drag-handle");
            var row = e.target.closest(".component-row");
            if (row) row.draggable = !!handle;
        });
        tbody.addEventListener("dragstart", function (e) {
            var row = e.target.closest(".component-row");
            if (!row) return;
            dragged = row;
            e.dataTransfer.effectAllowed = "move";
        });
        tbody.addEventListener("dragover", function (e) {
            e.preventDefault();
            var row = e.target.closest(".component-row");
            if (!row || !dragged || row === dragged) return;
            var rect = row.getBoundingClientRect();
            var before = e.clientY < rect.top + rect.height / 2;
            tbody.insertBefore(dragged, before ? row : row.nextElementSibling);
        });
        tbody.addEventListener("dragend", function () {
            dragged = null;
            visibleRows().forEach(function (r) { r.draggable = false; });
            reindex();
        });
        tbody.addEventListener("drop", function (e) { e.preventDefault(); });

        // Image upload (AJAX)
        var uploadBtn = document.getElementById("btn-upload-image");
        var fileInput = document.getElementById("image-file");
        var hiddenPath = document.getElementById("image-path");
        var preview = document.getElementById("image-preview");
        var previewWrap = document.getElementById("image-preview-wrap");
        var errorBox = document.getElementById("image-error");

        if (uploadBtn && fileInput) {
            uploadBtn.addEventListener("click", function () {
                if (errorBox) errorBox.style.display = "none";
                if (!fileInput.files || fileInput.files.length === 0) {
                    if (errorBox) { errorBox.textContent = "Choose an image first."; errorBox.style.display = ""; }
                    return;
                }
                var tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
                var fd = new FormData();
                fd.append("file", fileInput.files[0]);
                if (tokenInput) fd.append("__RequestVerificationToken", tokenInput.value);

                uploadBtn.disabled = true;
                fetch("/Product/UploadImage", { method: "POST", body: fd })
                    .then(function (r) { return r.json().then(function (d) { return { ok: r.ok, d: d }; }); })
                    .then(function (res) {
                        if (!res.ok) throw new Error(res.d.error || "Upload failed.");
                        hiddenPath.value = res.d.path;
                        if (preview) preview.src = res.d.url;
                        if (previewWrap) previewWrap.style.display = "";
                    })
                    .catch(function (err) {
                        if (errorBox) { errorBox.textContent = err.message; errorBox.style.display = ""; }
                    })
                    .finally(function () { uploadBtn.disabled = false; });
            });
        }

        // Initial state
        visibleRows().forEach(function (r) { r.draggable = false; });
        reindex();
    });
})();
