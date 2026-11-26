// product-builder.js — dynamic component rows, integrated template generation, Cropper.js image editing, visual marker placement, similarity comparison.
(function () {
    "use strict";

    document.addEventListener("DOMContentLoaded", function () {
        var form = document.getElementById("product-form");
        if (!form) return;

        var tbody = document.getElementById("components-body");
        var proto = document.getElementById("row-prototype");
        var empty = document.getElementById("components-empty");
        var countBadge = document.getElementById("component-count");
        var alertBanner = document.getElementById("builder-alert-banner");
        var alertMsg = document.getElementById("builder-alert-msg");

        var alertOwnerStep = null;
        var editingTemplateId = null;
        var editingComponentRowIdx = null;
        var viewingTemplateId = null;
        var viewingComponentRowIdx = null;
        var isTemplateViewOnly = false;

        function showAlert(message, isError, ownerStep) {
            if (!alertBanner || !alertMsg) return;
            alertMsg.textContent = message;
            alertBanner.className = isError
                ? "alert alert-danger alert-dismissible fade show mb-4"
                : "alert alert-success alert-dismissible fade show mb-4";
            alertBanner.classList.remove("d-none");
            alertOwnerStep = typeof ownerStep === "number" ? ownerStep : null;
            alertBanner.scrollIntoView({ behavior: "smooth", block: "nearest" });
        }

        function hideAlert() {
            if (!alertBanner) return;
            alertBanner.classList.add("d-none");
            alertOwnerStep = null;
        }

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
            var step2Count = document.getElementById("step2-attached-count");
            if (step2Count) step2Count.textContent = rows.length;
            if (empty) empty.style.display = rows.length === 0 ? "" : "none";
            renderStep2AddedTemplates();

        }

        function renderStep2AddedTemplates() {
            var list = document.getElementById("step2-added-list");
            var badge = document.getElementById("step2-added-badge");
            var step2Count = document.getElementById("step2-attached-count");
            if (!list) return;

            var rows = visibleRows();
            if (badge) badge.textContent = rows.length;
            if (step2Count) step2Count.textContent = rows.length;

            if (rows.length === 0) {
                list.innerHTML = '<div class="text-muted small text-center py-3 px-2 border rounded bg-light" id="step2-added-empty">' +
                    '<i class="bi bi-inbox d-block fs-4 text-secondary mb-1"></i>' +
                    'No templates attached yet.<br><span style="font-size:0.75rem">Add from library below or generate on the left.</span>' +
                    '</div>';
                return;
            }

            var html = "";
            rows.forEach(function (row, idx) {
                var select = row.querySelector(".row-template-select");
                var nameInput = row.querySelector(".row-component-name");
                var qtyInput = row.querySelector(".row-quantity");
                var opt = select && select.selectedIndex >= 0 ? select.options[select.selectedIndex] : null;
                var compName = (nameInput && nameInput.value.trim()) || (opt && opt.dataset.name) || ("Component " + (idx + 1));
                var steps = (opt && opt.dataset.steps) || "0";
                var optText = opt ? opt.textContent : "";
                var code = optText.indexOf("·") >= 0 ? optText.split("·")[0].trim() : (opt ? opt.textContent.trim() : "");
                var qty = (qtyInput && qtyInput.value) || "1";
                var templateId = (select && select.value) ? parseInt(select.value, 10) : 0;
                var isCurrentlyEditing = (editingTemplateId && editingTemplateId === templateId);
                var isCurrentlyViewing = (!isCurrentlyEditing && viewingTemplateId && viewingTemplateId === templateId);
                var activeClass = isCurrentlyEditing
                    ? "border-primary bg-primary-subtle shadow-sm"
                    : (isCurrentlyViewing ? "border-info bg-info-subtle shadow-sm" : "bg-white");

                html += '<div class="list-group-item d-flex justify-content-between align-items-center px-2 py-2 border rounded mb-1 ' + activeClass + ' step2-added-item" data-index="' + idx + '" data-template-id="' + templateId + '" style="cursor: pointer; transition: all 0.15s ease;" title="Click to view template details">' +
                    '<div class="me-2 text-truncate flex-grow-1 step2-item-click" data-index="' + idx + '" data-template-id="' + templateId + '">' +
                        '<div class="fw-semibold small text-dark text-truncate d-flex align-items-center gap-1" title="' + compName + '">' +
                            '<span class="badge bg-light text-dark border">#' + (idx + 1) + '</span>' +
                            '<span class="text-truncate">' + compName + '</span>' +
                            (isCurrentlyEditing
                                ? '<span class="badge bg-warning text-dark ms-1" style="font-size:0.65rem"><i class="bi bi-pencil-fill me-0.5"></i>Editing</span>'
                                : (isCurrentlyViewing
                                    ? '<span class="badge bg-info text-dark ms-1" style="font-size:0.65rem"><i class="bi bi-eye-fill me-0.5"></i>Viewing</span>'
                                    : '<span class="badge bg-primary-subtle text-primary border border-primary-subtle ms-1 btn-edit-step2-added" data-index="' + idx + '" data-template-id="' + templateId + '" style="font-size:0.65rem; cursor:pointer;" title="Click to edit template"><i class="bi bi-pencil me-0.5"></i>Edit</span>')) +
                        '</div>' +
                        '<div class="text-muted" style="font-size:0.72rem">' +
                            (code ? '<span class="fw-medium text-secondary">' + code + '</span> &middot; ' : '') +
                            steps + ' steps &middot; Qty: ' + qty +
                        '</div>' +
                    '</div>' +
                    '<div class="d-flex align-items-center gap-1">' +
                        '<button type="button" class="btn btn-sm btn-outline-primary py-0 px-2 btn-edit-step2-added" data-index="' + idx + '" data-template-id="' + templateId + '" title="Edit template in editor">' +
                            '<i class="bi bi-pencil"></i>' +
                        '</button>' +
                        '<button type="button" class="btn btn-sm btn-outline-danger py-0 px-2 btn-remove-step2-added" data-index="' + idx + '" title="Remove from product">' +
                            '<i class="bi bi-trash3"></i>' +
                        '</button>' +
                    '</div>' +
                '</div>';
            });

            list.innerHTML = html;

            list.querySelectorAll(".btn-remove-step2-added").forEach(function (btn) {
                btn.addEventListener("click", function (e) {
                    e.stopPropagation();
                    var targetIdx = parseInt(btn.dataset.index, 10);
                    var currentRows = visibleRows();
                    if (currentRows[targetIdx]) {
                        var sel = currentRows[targetIdx].querySelector(".row-template-select");
                        var tId = sel ? parseInt(sel.value, 10) : 0;
                        currentRows[targetIdx].remove();
                        reindex();
                        if ((editingTemplateId && editingTemplateId === tId) || (viewingTemplateId && viewingTemplateId === tId)) {
                            resetTemplateEditor();
                        }
                    }
                });
            });

            list.querySelectorAll(".btn-edit-step2-added").forEach(function (btn) {
                btn.addEventListener("click", function (e) {
                    e.stopPropagation();
                    var tId = parseInt(btn.dataset.templateId, 10);
                    var rowIdx = parseInt(btn.dataset.index, 10);
                    if (tId > 0) {
                        loadTemplateIntoEditor(tId, rowIdx, false);
                    }
                });
            });

            list.querySelectorAll(".step2-added-item").forEach(function (item) {
                item.addEventListener("click", function (e) {
                    if (e.target.closest(".btn-remove-step2-added") || e.target.closest(".btn-edit-step2-added")) return;
                    var tId = parseInt(item.dataset.templateId, 10);
                    var rowIdx = parseInt(item.dataset.index, 10);
                    if (tId > 0) {
                        loadTemplateIntoEditor(tId, rowIdx, true);
                    }
                });
            });
        }
        function addRow(templateId, label, code, stepCount) {
            var row = proto.cloneNode(true);
            row.removeAttribute("id");
            row.querySelectorAll("select,input,button").forEach(function (el) { el.disabled = false; });
            row.style.display = "";
            var select = row.querySelector(".row-template-select");
            if (templateId) {
                var tidStr = String(templateId);
                var hasOpt = Array.from(select.options).some(function (o) { return o.value === tidStr; });
                if (!hasOpt) {
                    var newOpt = document.createElement("option");
                    newOpt.value = tidStr;
                    newOpt.dataset.steps = stepCount != null ? String(stepCount) : "0";
                    newOpt.dataset.name = label || ("Template #" + tidStr);
                    if (code) newOpt.dataset.code = code;
                    newOpt.textContent = (code ? code + " · " : "") + (label || ("Template #" + tidStr));
                    select.appendChild(newOpt);
                }
                select.value = tidStr;
            }
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

        function appendTemplateToCatalogue(created) {
            // 1. Add to prototype select options
            var protoSelect = proto.querySelector(".row-template-select");
            var newOpt = document.createElement("option");
            newOpt.value = created.templateId;
            newOpt.dataset.steps = created.stepCount;
            newOpt.dataset.name = created.name;
            newOpt.textContent = created.label;
            protoSelect.appendChild(newOpt);

            // 2. Add to all existing row selects
            document.querySelectorAll(".component-row .row-template-select").forEach(function (sel) {
                sel.appendChild(newOpt.cloneNode(true));
            });

            // 3. Add to left template-picker list
            var picker = document.getElementById("template-picker");
            var emptyMsg = document.getElementById("picker-empty-msg");
            if (emptyMsg) emptyMsg.style.display = "none";

            var itemDiv = document.createElement("div");
            itemDiv.className = "list-group-item d-flex justify-content-between align-items-center px-2 py-2 catalogue-item";
            itemDiv.dataset.templateId = created.templateId;
            itemDiv.dataset.templateCode = created.templateCode;
            itemDiv.dataset.deptId = created.departmentId || "";
            itemDiv.dataset.steps = created.stepCount;
            itemDiv.dataset.version = created.version || 1;
            itemDiv.dataset.label = created.label;
            itemDiv.dataset.name = created.name;
            itemDiv.dataset.search = (created.templateCode + " " + created.name).toLowerCase();
            itemDiv.innerHTML = '<span class="flex-grow-1 text-truncate me-2 cat-item-text" style="cursor: pointer;" title="Click to edit \'' + created.name + '\' in editor">' +
                '<span class="d-block fw-semibold text-dark small text-truncate cat-item-title">' + created.name + '</span>' +
                '<span class="d-block text-muted cat-item-meta" style="font-size:0.75rem">' + created.templateCode + ' · v' + (created.version || 1) + ' · <span class="cat-item-steps-num">' + created.stepCount + '</span> steps</span></span>' +
                '<div class="d-flex align-items-center gap-1 flex-shrink-0">' +
                    '<button type="button" class="btn btn-sm btn-outline-secondary py-0 px-1.5 btn-cat-edit" data-template-id="' + created.templateId + '" title="Edit template in editor"><i class="bi bi-pencil"></i></button>' +
                    '<button type="button" class="btn btn-sm btn-outline-primary py-0 px-2 btn-cat-add" data-template-id="' + created.templateId + '" title="Attach to product"><i class="bi bi-plus"></i> Add</button>' +
                '</div>';

            if (picker) {
                picker.insertBefore(itemDiv, picker.firstChild);
            }

            var catBadge = document.getElementById("catalogue-badge");
            if (catBadge) {
                var c = parseInt(catBadge.textContent || "0", 10) + 1;
                catBadge.textContent = c;
            }
            var catBadgeSec = document.getElementById("catalogue-badge-secondary");
            if (catBadgeSec) {
                var c2 = parseInt(catBadgeSec.textContent || "0", 10) + 1;
                catBadgeSec.textContent = c2;
            }
        }

        // Delegated click & change listeners for Catalogue Library (#template-picker)
        var templatePicker = document.getElementById("template-picker");
        if (templatePicker) {
            templatePicker.addEventListener("click", function (e) {
                // If user clicked inside the version dropdown, do nothing (let native dropdown open)
                if (e.target.closest(".cat-version-select")) {
                    return;
                }

                var item = e.target.closest(".catalogue-item");
                if (!item) return;

                var tId = parseInt(item.dataset.templateId, 10);
                var name = item.dataset.name || "";
                var editBtn = e.target.closest(".btn-cat-edit");
                var itemText = e.target.closest(".cat-item-text");
                var addBtn = e.target.closest(".btn-cat-add");

                if (editBtn || itemText) {
                    e.preventDefault();
                    e.stopPropagation();
                    var editTargetId = (editBtn && editBtn.dataset.templateId) ? parseInt(editBtn.dataset.templateId, 10) : tId;
                    if (editTargetId > 0) {
                        loadTemplateIntoEditor(editTargetId, null);
                    }
                    return;
                }

                if (addBtn) {
                    e.preventDefault();
                    e.stopPropagation();
                    var targetId = addBtn.dataset.templateId ? parseInt(addBtn.dataset.templateId, 10) : tId;
                    openTemplatePreviewModal(targetId > 0 ? targetId : tId);
                }
            });

            templatePicker.addEventListener("change", function (e) {
                var select = e.target.closest(".cat-version-select");
                if (!select) return;
                var item = select.closest(".catalogue-item");
                if (!item) return;

                var opt = select.selectedOptions ? select.selectedOptions[0] : null;
                if (!opt) return;

                var tId = opt.value;
                var version = opt.dataset.version;
                var steps = opt.dataset.steps;
                var name = opt.dataset.name || item.dataset.name;
                var code = opt.dataset.code || item.dataset.templateCode;
                var label = opt.dataset.label;

                // Update item attributes to the selected version
                item.dataset.templateId = tId;
                item.dataset.steps = steps;
                item.dataset.version = version;
                if (label) item.dataset.label = label;
                if (name) item.dataset.name = name;

                // Update meta subtitle: e.g. "TW-4B-25NM-T1.9 · v1 · 6 steps"
                var metaEl = item.querySelector(".cat-item-meta");
                if (metaEl) {
                    metaEl.innerHTML = code + ' &middot; v' + version + ' &middot; <span class="cat-item-steps-num">' + steps + '</span> steps';
                }

                // Update buttons data-template-id so Edit or Add operates on this exact selected version!
                var editBtn = item.querySelector(".btn-cat-edit");
                if (editBtn) editBtn.dataset.templateId = tId;
                var addBtn = item.querySelector(".btn-cat-add");
                if (addBtn) addBtn.dataset.templateId = tId;
            });
        }

        // Template search & department filter
        function filterCatalogue() {
            var searchEl = document.getElementById("template-search");
            var q = searchEl ? searchEl.value.trim().toLowerCase() : "";
            var deptEl = document.getElementById("DepartmentId");
            var selectedDept = deptEl && deptEl.tagName === "SELECT" && deptEl.value ? deptEl.value : null;

            var visibleCount = 0;
            document.querySelectorAll("#template-picker .catalogue-item").forEach(function (btn) {
                var hay = btn.dataset.search || "";
                var itemDept = btn.dataset.deptId;
                var matchSearch = !q || hay.indexOf(q) !== -1;
                var matchDept = !selectedDept || !itemDept || itemDept === selectedDept;

                if (matchSearch && matchDept) {
                    btn.style.display = "";
                    visibleCount++;
                } else {
                    btn.style.display = "none";
                }
            });

            var catBadgeSec = document.getElementById("catalogue-badge-secondary");
            if (catBadgeSec) catBadgeSec.textContent = visibleCount;
        }

        var search = document.getElementById("template-search");
        if (search) {
            search.addEventListener("input", filterCatalogue);

            var urlParams = new URLSearchParams(window.location.search);
            var paramCode = urlParams.get("templateCode");
            if (paramCode) {
                search.value = paramCode;
            }
        }

        var deptSelectEl = document.getElementById("DepartmentId");
        if (deptSelectEl && deptSelectEl.tagName === "SELECT") {
            deptSelectEl.addEventListener("change", filterCatalogue);
        }
        filterCatalogue();

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

        // Product Image Gallery (multiple images, pick order = display order)
        // Picking files opens the shared crop editor for each one in sequence;
        // both the cropped result and the untouched file upload to
        // /Product/UploadImage and join the gallery. Row 0 is the main picture.
        var fileInput = document.getElementById("image-file");
        var hiddenPath = document.getElementById("image-path");
        var listWrap = document.getElementById("image-list-wrap");
        var listBody = document.getElementById("image-list");
        var viewModalEl = document.getElementById("image-view-modal");
        var viewModal = viewModalEl && window.bootstrap ? new window.bootstrap.Modal(viewModalEl) : null;
        var viewImg = document.getElementById("image-view-img");
        var errorBox = document.getElementById("image-error");
        var productCropFile = null; // file currently in the crop editor
        var productImages = [];     // gallery state: ordered productImagePaths
        var productQueue = [];      // picked files still waiting for the crop editor
        var productCropConsumed = false;   // current file was handed to upload (or bailed on)
        var productHideSettled = false;    // crop modal finished closing
        var productUploadSettled = false;  // current upload finished (success or fail)
        var productImageSeq = 0;    // hidden-input indices so removals stay binder-safe

        function showProductImageError(message) {
            if (errorBox) { errorBox.textContent = message; errorBox.style.display = ""; }
        }

        function hideProductImageError() {
            if (errorBox) errorBox.style.display = "none";
        }

        function productImageUrl(path) {
            return "/" + path + "?t=" + Date.now();
        }

        function addProductImage(path) {
            path = String(path || "").trim();
            if (!path) return;
            var exists = productImages.some(function (p) { return p.toLowerCase() === path.toLowerCase(); });
            if (exists) return;
            productImages.push(path);
            hideProductImageError();
            renderProductImages();
        }

        function removeProductImage(path) {
            productImages = productImages.filter(function (p) { return p.toLowerCase() !== String(path).toLowerCase(); });
            renderProductImages();
        }

        function renderProductImages() {
            if (hiddenPath) hiddenPath.value = productImages[0] || "";
            if (!listWrap || !listBody) return;

            // Rebuild both the chips and the form hidden inputs (starts with the
            // seed inputs rendered server-side in edit mode).
            Array.prototype.slice.call(listWrap.querySelectorAll("input[type=hidden]")).forEach(function (inp) {
                if (inp.name === "Images.Index" || /^Images\[\d+\]$/.test(inp.name || "")) inp.remove();
            });
            listBody.innerHTML = "";

            productImages.forEach(function (path, i) {
                var chip = document.createElement("div");
                chip.className = "product-image-chip border rounded p-1 d-flex align-items-center gap-1";
                chip.setAttribute("data-path", path);
                chip.title = path.split("/").pop();

                var img = document.createElement("img");
                img.src = productImageUrl(path);
                img.alt = "Product image " + (i + 1);
                img.className = "rounded";
                img.style.cssText = "height:64px; width:64px; object-fit:cover;";
                chip.appendChild(img);

                var btnGroup = document.createElement("div");
                btnGroup.className = "d-flex flex-column gap-1";

                var viewAnchor = document.createElement("button");
                viewAnchor.type = "button";
                viewAnchor.className = "btn btn-sm btn-outline-secondary p-1 border-0";
                viewAnchor.title = "View larger";
                viewAnchor.innerHTML = '<i class="bi bi-eye"></i>';
                btnGroup.appendChild(viewAnchor);

                var removeBtn = document.createElement("button");
                removeBtn.type = "button";
                removeBtn.className = "btn btn-sm btn-outline-danger p-1 border-0";
                removeBtn.title = "Remove image";
                removeBtn.innerHTML = '<i class="bi bi-trash"></i>';
                btnGroup.appendChild(removeBtn);

                chip.appendChild(btnGroup);
                listBody.appendChild(chip);

                var seq = productImageSeq++;
                var metaInput = document.createElement("input");
                metaInput.type = "hidden";
                metaInput.name = "Images.Index";
                metaInput.value = seq;
                var valInput = document.createElement("input");
                valInput.type = "hidden";
                valInput.name = "Images[" + seq + "]";
                valInput.value = path;
                listWrap.appendChild(metaInput);
                listWrap.appendChild(valInput);
            });

            listWrap.style.display = productImages.length ? "" : "none";
        }

        // Edit mode: seed the gallery from the hidden inputs rendered server-side.
        if (listWrap) {
            var seeded = [];
            Array.prototype.slice.call(listWrap.querySelectorAll("input[type=hidden]")).forEach(function (inp) {
                var m = /^Images\[(\d+)\]$/.exec(inp.name || "");
                if (m && inp.value) seeded.push({ idx: parseInt(m[1], 10), path: inp.value });
            });
            seeded.sort(function (a, b) { return a.idx - b.idx; });
            productImages = seeded.map(function (s) { return s.path; });
        }

        // Chip actions: eye opens the zoomable viewer, trash removes from the gallery,
        // clicking the thumbnail also opens the viewer.
        if (listBody) {
            listBody.addEventListener("click", function (e) {
                var chip = e.target.closest(".product-image-chip");
                if (!chip) return;
                var path = chip.getAttribute("data-path");
                if (e.target.closest(".btn-outline-danger")) {
                    removeProductImage(path);
                    refreshStep3Summary();
                    return;
                }
                if (!viewImg) return;
                viewImg.src = productImageUrl(path);
                resetViewZoom();
                if (viewModal) viewModal.show();
            });
        }

        function uploadProductImage(fileOrBlob, displayName) {
            hideProductImageError();
            var tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
            var fd = new FormData();
            fd.append("file", fileOrBlob, displayName);
            if (tokenInput) fd.append("__RequestVerificationToken", tokenInput.value);

            return fetch("/Product/UploadImage", { method: "POST", body: fd })
                .then(function (r) { return r.json().then(function (d) { return { ok: r.ok, d: d }; }); })
                .then(function (res) {
                    if (!res.ok) throw new Error(res.d.error || "Upload failed.");
                    addProductImage(res.d.path);
                });
        }

        function openNextProductCrop() {
            var next = productQueue[0];
            if (!next) return;
            productCropFile = next;
            productCropConsumed = false;
            if (_blobUrl) URL.revokeObjectURL(_blobUrl);
            _blobUrl = URL.createObjectURL(next);
            openCropModal(_blobUrl, "product");
        }

        // Advance the crop batch only after both the modal close transition and
        // the current upload have finished (avoids re-opening mid-hide).
        function maybeAdvanceProductQueue() {
            if (!productHideSettled || !productUploadSettled) return;
            productHideSettled = false;
            productUploadSettled = false;
            if (productQueue.length) openNextProductCrop();
        }

        function uploadProductBlob(fileOrBlob, displayName) {
            productCropConsumed = true;
            productUploadSettled = false;
            uploadProductImage(fileOrBlob, displayName)
                .then(function () {
                    productQueue.shift();
                    productUploadSettled = true;
                    maybeAdvanceProductQueue();
                })
                .catch(function (err) {
                    productUploadSettled = true;
                    productQueue = [];
                    showProductImageError(err.message);
                });
        }

        if (fileInput) {
            fileInput.addEventListener("change", function () {
                var files = Array.prototype.slice.call(this.files || []);
                this.value = ""; // allow re-selection
                if (!files.length) return;
                var maxBytes = 5 * 1024 * 1024;
                var oversized = files.filter(function (f) { return f.size > maxBytes; });
                if (oversized.length) {
                    showProductImageError("File too large: " + oversized.map(function (f) { return f.name + " (" + (f.size / 1024 / 1024).toFixed(1) + " MB)"; }).join(", ") + ". Maximum 5 MB per image.");
                    return;
                }
                productQueue = files;
                hideProductImageError();
                openNextProductCrop();
            });
        }

        // -------------------------------------------------------------
        // Zoomable large view: fit-to-size, wheel/pinch zoom, drag pan
        // -------------------------------------------------------------
        var viewViewport = document.getElementById("image-view-viewport");
        var viewZoomBadge = document.getElementById("image-view-zoom");
        var viewZoom = { scale: 1, x: 0, y: 0 };
        var viewDragging = false;
        var viewDragLast = { x: 0, y: 0 };
        var viewPinchState = null;

        function applyViewTransform() {
            if (!viewImg) return;
            viewImg.style.transform = "translate(" + viewZoom.x + "px, " + viewZoom.y + "px) scale(" + viewZoom.scale + ")";
            if (viewZoomBadge) viewZoomBadge.textContent = Math.round(viewZoom.scale * 100) + "%";
        }

        function resetViewZoom() {
            viewZoom.scale = 1;
            viewZoom.x = 0;
            viewZoom.y = 0;
            viewDragging = false;
            viewPinchState = null;
            applyViewTransform();
        }

        // Zoom keeping the point under the cursor/pinch midpoint fixed.
        function zoomViewAt(clientX, clientY, nextScale) {
            var scale = Math.min(Math.max(nextScale, 1), 6);
            if (scale <= 1) { resetViewZoom(); return; } // snap back to fit when fully zoomed out
            if (!viewViewport) return;
            var rect = viewViewport.getBoundingClientRect();
            var centerX = rect.left + rect.width / 2;
            var centerY = rect.top + rect.height / 2;
            var k = scale / viewZoom.scale;
            viewZoom.x = clientX - centerX - (clientX - centerX - viewZoom.x) * k;
            viewZoom.y = clientY - centerY - (clientY - centerY - viewZoom.y) * k;
            viewZoom.scale = scale;
            applyViewTransform();
        }

        function viewTouchDistance(touches) {
            var dx = touches[0].clientX - touches[1].clientX;
            var dy = touches[0].clientY - touches[1].clientY;
            return Math.hypot(dx, dy) || 1;
        }

        if (viewViewport) {
            viewViewport.addEventListener("wheel", function (e) {
                e.preventDefault();
                var factor = e.deltaY < 0 ? 1.12 : 1 / 1.12;
                zoomViewAt(e.clientX, e.clientY, viewZoom.scale * factor);
            }, { passive: false });

            viewViewport.addEventListener("dblclick", function (e) {
                e.preventDefault();
                zoomViewAt(e.clientX, e.clientY, viewZoom.scale > 1 ? 1 : 2);
            });

            viewViewport.addEventListener("pointerdown", function (e) {
                if (viewZoom.scale <= 1 || viewPinchState) return;
                viewDragging = true;
                viewDragLast = { x: e.clientX, y: e.clientY };
                if (viewViewport.setPointerCapture) viewViewport.setPointerCapture(e.pointerId);
            });

            viewViewport.addEventListener("pointermove", function (e) {
                if (!viewDragging || viewPinchState) return;
                viewZoom.x += e.clientX - viewDragLast.x;
                viewZoom.y += e.clientY - viewDragLast.y;
                viewDragLast = { x: e.clientX, y: e.clientY };
                applyViewTransform();
            });

            var endViewDrag = function () { viewDragging = false; };
            viewViewport.addEventListener("pointerup", endViewDrag);
            viewViewport.addEventListener("pointercancel", endViewDrag);

            viewViewport.addEventListener("touchstart", function (e) {
                if (e.touches.length === 2) {
                    viewPinchState = { dist: viewTouchDistance(e.touches), scale: viewZoom.scale };
                    viewDragging = false;
                    e.preventDefault();
                }
            }, { passive: false });

            viewViewport.addEventListener("touchmove", function (e) {
                if (viewPinchState && e.touches.length === 2) {
                    var dist = viewTouchDistance(e.touches);
                    var midX = (e.touches[0].clientX + e.touches[1].clientX) / 2;
                    var midY = (e.touches[0].clientY + e.touches[1].clientY) / 2;
                    zoomViewAt(midX, midY, viewPinchState.scale * (dist / viewPinchState.dist));
                    e.preventDefault();
                }
            }, { passive: false });

            viewViewport.addEventListener("touchend", function () {
                viewPinchState = null;
            });
        }

        // ================================================================
        // INTEGRATED TEMPLATE GENERATOR: CROPPER, MARKERS & SIMILARITY
        // ================================================================
        var genTplName = document.getElementById("gen-tpl-name");
        var genTplMode = document.getElementById("gen-tpl-mode");
        var genTplTolMode = document.getElementById("gen-tpl-tol-mode");
        var genStepsTbody = document.getElementById("gen-steps-tbody");
        var genStepsCount = document.getElementById("gen-steps-count");
        var genError = document.getElementById("gen-template-error");
        var btnApplyQuick = document.getElementById("btn-apply-quick-steps");
        var btnAddStep = document.getElementById("btn-add-gen-step");
        var btnGenerateTemplate = document.getElementById("btn-generate-template");

        // Image & Marker elements
        var genFileInput = document.getElementById("gen-file-input");
        var genImagePath = document.getElementById("gen-image-path");
        var genImageRow = document.getElementById("gen-image-view-row");
        var genImagePreviewBtn = document.getElementById("btn-gen-view-image");
        var genImagePlaceholder = document.getElementById("gen-image-placeholder");
        var genUploadStatus = document.getElementById("gen-upload-status");
        var genUploadError = document.getElementById("gen-upload-error");
        var btnGenChooseImage = document.getElementById("btn-gen-choose-image");
        var genUploadBtnText = document.getElementById("gen-upload-btn-text");
        var btnGenEditImage = document.getElementById("btn-gen-edit-image");
        var btnGenRemoveImage = document.getElementById("btn-gen-remove-image");
        var genMarkerPanel = document.getElementById("gen-marker-panel");
        var genImageContainer = document.getElementById("gen-image-container");
        var genComponentImage = document.getElementById("gen-component-image");
        var genSelectedStepLabel = document.getElementById("gen-selected-step-label");

        // Marker Size State & Selection State
        var currentSelectedStep = 0;
        var currentMarkerSize = parseInt(localStorage.getItem("smarttorque_marker_size") || "24", 10);

        // Eye button: open the uploaded component image in the shared
        // zoomable viewer instead of a small inline preview.
        if (genImagePreviewBtn) {
            genImagePreviewBtn.addEventListener("click", function (e) {
                e.stopPropagation();
                if (!genImagePath.value || !viewImg) return;
                var viewUrl = "";
                if (genComponentImage && genComponentImage.src) {
                    viewUrl = genComponentImage.src.split("?")[0] + "?t=" + Date.now();
                } else {
                    viewUrl = "/" + genImagePath.value + "?t=" + Date.now();
                }
                viewImg.src = viewUrl;
                resetViewZoom();
                if (viewModal) viewModal.show();
            });
        }

        // Step-3 product summary: eye overlay (and clicking the thumbnail)
        // opens the same zoomable viewer with the summary image.
        var summaryViewBtn = document.getElementById("btn-summary-view-image");
        var summaryImagePreview = document.getElementById("summary-image-preview");

        function openSummaryViewer() {
            if (!viewImg || !summaryImagePreview) return;
            if (summaryImagePreview.style.display === "none" || !summaryImagePreview.getAttribute("src")) return;
            viewImg.src = summaryImagePreview.src.split("?")[0] + "?t=" + Date.now();
            resetViewZoom();
            if (viewModal) viewModal.show();
        }

        if (summaryViewBtn) {
            summaryViewBtn.addEventListener("click", function (e) {
                e.stopPropagation();
                openSummaryViewer();
            });
        }
        if (summaryImagePreview) {
            summaryImagePreview.addEventListener("click", openSummaryViewer);
        }

        // Similarity Modal elements
        var modalEl = document.getElementById("similarity-modal");
        var similarityModal = modalEl && window.bootstrap ? new window.bootstrap.Modal(modalEl) : null;
        var btnChooseExisting = document.getElementById("btn-choose-existing");
        var btnChooseNew = document.getElementById("btn-choose-new");

        var currentExistingMatch = null;
        var currentNewRequest = null;

        // Save Template as New Version Confirmation Modal
        var pendingUpdatePayload = null;
        var btnConfirmSaveAndAdd = document.getElementById("btn-confirm-save-and-add");
        if (btnConfirmSaveAndAdd) {
            btnConfirmSaveAndAdd.addEventListener("click", function () {
                var confirmModalEl = document.getElementById("save-template-confirm-modal");
                if (confirmModalEl && window.bootstrap && window.bootstrap.Modal) {
                    var m = bootstrap.Modal.getInstance(confirmModalEl);
                    if (m) m.hide();
                }
                if (pendingUpdatePayload) {
                    executeSaveTemplate(pendingUpdatePayload, true);
                }
            });
        }

        var btnConfirmSaveLibraryOnly = document.getElementById("btn-confirm-save-library-only");
        if (btnConfirmSaveLibraryOnly) {
            btnConfirmSaveLibraryOnly.addEventListener("click", function () {
                var confirmModalEl = document.getElementById("save-template-confirm-modal");
                if (confirmModalEl && window.bootstrap && window.bootstrap.Modal) {
                    var m = bootstrap.Modal.getInstance(confirmModalEl);
                    if (m) m.hide();
                }
                if (pendingUpdatePayload) {
                    executeSaveTemplate(pendingUpdatePayload, false);
                }
            });
        }

        // ================================================================
        // EDIT EXISTING TEMPLATE MANAGEMENT
        // ================================================================
        function setTemplateEditorReadOnly(readOnly) {
            isTemplateViewOnly = !!readOnly;
            var idsToToggle = [
                "gen-tpl-name", "gen-tpl-mode", "gen-tpl-tol-mode",
                "quick-bolt-count", "quick-target", "quick-tol", "btn-apply-quick-steps",
                "gen-file-input", "btn-gen-edit-image", "btn-gen-remove-image",
                "gen-marker-size-slider", "btn-type-distance", "btn-type-radius"
            ];
            idsToToggle.forEach(function (id) {
                var el = document.getElementById(id);
                if (el) el.disabled = isTemplateViewOnly;
            });

            var chooseImgBtn = document.getElementById("btn-gen-choose-image");
            if (chooseImgBtn) {
                chooseImgBtn.classList.toggle("disabled", isTemplateViewOnly);
                chooseImgBtn.style.pointerEvents = isTemplateViewOnly ? "none" : "";
            }

            var addStepBtn = document.getElementById("btn-add-gen-step");
            if (addStepBtn) addStepBtn.classList.toggle("d-none", isTemplateViewOnly);

            document.querySelectorAll(".gen-size-preset").forEach(function (b) {
                b.disabled = isTemplateViewOnly;
            });

            var stepsTbody = document.getElementById("gen-steps-tbody");
            if (stepsTbody) {
                stepsTbody.querySelectorAll("input, select, button").forEach(function (el) {
                    el.disabled = isTemplateViewOnly;
                });
            }

            if (genImageContainer) {
                genImageContainer.style.cursor = isTemplateViewOnly ? "default" : "crosshair";
            }
        }

        function setTemplateEditorMode(mode, tpl) {
            var editBadge = document.getElementById("gen-editing-badge");
            var editName = document.getElementById("gen-editing-name");
            var btnCancel = document.getElementById("btn-cancel-edit-template");
            var btnCancelBottom = document.getElementById("btn-cancel-edit-bottom");
            var sectionTitle = document.getElementById("gen-section-title");

            var isView = (mode === "view" && tpl);
            var isEditing = ((mode === true || mode === "edit") && tpl);

            if (isView) {
                editingTemplateId = null;
                editingComponentRowIdx = null;
                viewingTemplateId = tpl.templateId;
                setTemplateEditorReadOnly(true);
                if (editBadge) {
                    editBadge.className = "badge bg-info-subtle text-info-emphasis border border-info-subtle";
                    editBadge.innerHTML = '<i class="bi bi-eye me-1"></i>Viewing: <span id="gen-editing-name">' + ((tpl.templateCode || "") + " (" + (tpl.name || "") + ")") + '</span>';
                }
                if (btnCancel) btnCancel.classList.remove("d-none");
                if (btnCancelBottom) {
                    btnCancelBottom.classList.remove("d-none");
                    btnCancelBottom.innerHTML = '<i class="bi bi-x-circle me-1"></i>Close View';
                }
                if (btnGenerateTemplate) {
                    btnGenerateTemplate.className = "btn btn-outline-primary py-2 fw-semibold flex-grow-1 shadow-sm";
                    btnGenerateTemplate.innerHTML = '<i class="bi bi-pencil-square me-1"></i>Switch to Edit Mode';
                }
                if (sectionTitle) {
                    sectionTitle.innerHTML = '<i class="bi bi-eye text-info me-2"></i>View Fastening Template';
                }
                var verBadgeView = document.getElementById("gen-auto-template-version");
                if (verBadgeView) {
                    verBadgeView.textContent = "v" + (tpl.version || 1);
                    verBadgeView.classList.remove("d-none");
                }
                var codeBadgeView = document.getElementById("gen-auto-template-code");
                if (codeBadgeView && tpl.templateCode) {
                    codeBadgeView.textContent = tpl.templateCode;
                }
                var codeHintView = document.getElementById("gen-auto-code-hint");
                if (codeHintView) codeHintView.textContent = "Read-only view (click Edit to modify)";
            } else if (isEditing) {
                viewingTemplateId = null;
                viewingComponentRowIdx = null;
                setTemplateEditorReadOnly(false);
                if (editBadge) {
                    editBadge.className = "badge bg-warning text-dark border border-warning-subtle";
                    editBadge.innerHTML = '<i class="bi bi-pencil-square me-1"></i>Editing: <span id="gen-editing-name">' + ((tpl.templateCode || "") + " (" + (tpl.name || "") + ")") + '</span>';
                }
                if (btnCancel) btnCancel.classList.remove("d-none");
                if (btnCancelBottom) {
                    btnCancelBottom.classList.remove("d-none");
                    btnCancelBottom.innerHTML = '<i class="bi bi-x-circle me-1"></i>Cancel Edit';
                }
                if (btnGenerateTemplate) {
                    btnGenerateTemplate.className = "btn btn-warning text-dark py-2 fw-semibold flex-grow-1 shadow-sm";
                    btnGenerateTemplate.innerHTML = '<i class="bi bi-check2-circle me-1"></i>Update Template &amp; Save Changes';
                }
                if (sectionTitle) {
                    sectionTitle.innerHTML = '<i class="bi bi-pencil-square text-warning me-2"></i>Edit Fastening Template';
                }
                var verBadge = document.getElementById("gen-auto-template-version");
                if (verBadge) {
                    verBadge.textContent = "v" + (tpl.version || 1);
                    verBadge.classList.remove("d-none");
                }
                var codeBadge = document.getElementById("gen-auto-template-code");
                if (codeBadge && tpl.templateCode) {
                    codeBadge.textContent = tpl.templateCode;
                }
                var codeHint = document.getElementById("gen-auto-code-hint");
                if (codeHint) codeHint.textContent = "Loaded from existing template (updates live if specs change)";
            } else {
                editingTemplateId = null;
                editingComponentRowIdx = null;
                viewingTemplateId = null;
                viewingComponentRowIdx = null;
                setTemplateEditorReadOnly(false);
                if (editBadge) editBadge.classList.add("d-none");
                if (btnCancel) btnCancel.classList.add("d-none");
                if (btnCancelBottom) btnCancelBottom.classList.add("d-none");
                if (btnGenerateTemplate) {
                    btnGenerateTemplate.className = "btn btn-primary py-2 fw-semibold flex-grow-1";
                    btnGenerateTemplate.innerHTML = '<i class="bi bi-magic me-1"></i>Generate Template &amp; Attach to Product';
                }
                if (sectionTitle) {
                    sectionTitle.innerHTML = '<i class="bi bi-magic text-primary me-2"></i>Generate Fastening Template';
                }
                var verBadge2 = document.getElementById("gen-auto-template-version");
                if (verBadge2) verBadge2.classList.add("d-none");
                var codeHint2 = document.getElementById("gen-auto-code-hint");
                if (codeHint2) codeHint2.textContent = "Auto-generated from fastening specifications";
                if (typeof updateAutoGeneratedCodePreview === "function") updateAutoGeneratedCodePreview();
            }
        }

        function resetTemplateEditor() {
            setTemplateEditorMode(false);
            if (genTplName) {
                genTplName.value = "";
                genTplName.classList.remove("is-invalid");
            }
            if (genTplMode) {
                genTplMode.value = "Torque";
                genTplMode.dispatchEvent(new Event("change"));
            }
            if (genTplTolMode) {
                genTplTolMode.value = "Absolute";
                genTplTolMode.dispatchEvent(new Event("change"));
            }
            if (genImagePath) genImagePath.value = "";
            if (genComponentImage) genComponentImage.src = "";
            if (genImageRow) genImageRow.classList.add("d-none");
            if (genImagePlaceholder) genImagePlaceholder.classList.remove("d-none");
            if (genMarkerPanel) genMarkerPanel.classList.add("d-none");
            if (genUploadBtnText) genUploadBtnText.textContent = "Upload Component Drawing / Photo";
            if (btnGenEditImage) btnGenEditImage.classList.add("d-none");
            if (btnGenRemoveImage) btnGenRemoveImage.classList.add("d-none");
            if (genMarkerLayer) genMarkerLayer.innerHTML = "";

            var qCountEl = document.getElementById("quick-bolt-count");
            var qTargetEl = document.getElementById("quick-target");
            var qTolEl = document.getElementById("quick-tol");
            var qCount = qCountEl ? parseInt(qCountEl.value, 10) || 4 : 4;
            var qTarget = qTargetEl ? parseFloat(qTargetEl.value) || 25.0 : 25.0;
            var qTol = qTolEl ? parseFloat(qTolEl.value) || 2.0 : 2.0;

            if (typeof populateQuickSteps === "function") {
                populateQuickSteps(qCount, qTarget, qTol);
            }
            renderStep2AddedTemplates();
            showAlert("Switched to new template creation mode.", false, 2);
        }

        var btnCancelTop = document.getElementById("btn-cancel-edit-template");
        if (btnCancelTop) {
            btnCancelTop.addEventListener("click", resetTemplateEditor);
        }
        var btnCancelBottom = document.getElementById("btn-cancel-edit-bottom");
        if (btnCancelBottom) {
            btnCancelBottom.addEventListener("click", resetTemplateEditor);
        }

        function updateTemplateInCatalogueAndDropdowns(updated, oldTemplateId) {
            var oldIdStr = String(oldTemplateId);
            var newIdStr = String(updated.templateId);

            // 1. Update/add in prototype select
            if (proto) {
                var protoSelect = proto.querySelector(".row-template-select");
                if (protoSelect) {
                    // Remove "(Latest)" tag from previous versions of the same template
                    for (var p = 0; p < protoSelect.options.length; p++) {
                        var pOpt = protoSelect.options[p];
                        if (pOpt.value === oldIdStr || (updated.templateCode && pOpt.textContent.indexOf(updated.templateCode) !== -1)) {
                            pOpt.textContent = pOpt.textContent.replace(" (Latest)", "");
                        }
                    }
                    var foundProtoOpt = false;
                    for (var p2 = 0; p2 < protoSelect.options.length; p2++) {
                        var pOpt2 = protoSelect.options[p2];
                        if (pOpt2.value === newIdStr) {
                            pOpt2.dataset.steps = updated.stepCount;
                            pOpt2.dataset.name = updated.name;
                            pOpt2.textContent = updated.label;
                            foundProtoOpt = true;
                            break;
                        }
                    }
                    if (!foundProtoOpt) {
                        var newOpt = document.createElement("option");
                        newOpt.value = newIdStr;
                        newOpt.dataset.steps = updated.stepCount;
                        newOpt.dataset.name = updated.name;
                        newOpt.textContent = updated.label;
                        protoSelect.appendChild(newOpt);
                    }
                }
            }

            // 2. Update/add in all existing rows
            document.querySelectorAll(".component-row .row-template-select").forEach(function (sel) {
                for (var j = 0; j < sel.options.length; j++) {
                    var o = sel.options[j];
                    if (o.value === oldIdStr || (updated.templateCode && o.textContent.indexOf(updated.templateCode) !== -1)) {
                        o.textContent = o.textContent.replace(" (Latest)", "");
                    }
                }
                var found = false;
                for (var k = 0; k < sel.options.length; k++) {
                    var o2 = sel.options[k];
                    if (o2.value === newIdStr) {
                        o2.dataset.steps = updated.stepCount;
                        o2.dataset.name = updated.name;
                        o2.textContent = updated.label;
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    var optClone = document.createElement("option");
                    optClone.value = newIdStr;
                    optClone.dataset.steps = updated.stepCount;
                    optClone.dataset.name = updated.name;
                    optClone.textContent = updated.label;
                    sel.appendChild(optClone);
                }
            });

            // 3. Update in Catalogue Library list
            var catItems = document.querySelectorAll("#template-picker .catalogue-item");
            var foundCatItem = null;
            catItems.forEach(function (ci) {
                if (ci.dataset.templateCode === updated.templateCode ||
                    ci.dataset.templateId === oldIdStr ||
                    ci.dataset.templateId === newIdStr) {
                    foundCatItem = ci;
                }
            });

            if (foundCatItem) {
                var ci = foundCatItem;
                var oldVer = parseInt(ci.dataset.version || "1", 10);
                var newVer = updated.version || (oldVer + 1);

                ci.dataset.templateId = newIdStr;
                ci.dataset.templateCode = updated.templateCode;
                ci.dataset.steps = updated.stepCount;
                ci.dataset.version = newVer;
                ci.dataset.name = updated.name;
                ci.dataset.label = updated.label;
                ci.dataset.search = (updated.templateCode + " " + updated.name).toLowerCase();

                var titleEl = ci.querySelector(".cat-item-title") || ci.querySelector(".fw-semibold");
                if (titleEl) titleEl.textContent = updated.name;

                var metaEl = ci.querySelector(".cat-item-meta") || ci.querySelector(".text-muted");
                if (metaEl) {
                    metaEl.innerHTML = updated.templateCode + " &middot; v" + newVer + " &middot; <span class=\"cat-item-steps-num\">" + updated.stepCount + "</span> steps";
                }

                var editBtn = ci.querySelector(".btn-cat-edit");
                if (editBtn) editBtn.dataset.templateId = newIdStr;
                var addBtn = ci.querySelector(".btn-cat-add");
                if (addBtn) addBtn.dataset.templateId = newIdStr;

                // Version selector dropdown in catalogue card
                var vSelect = ci.querySelector(".cat-version-select");
                if (vSelect) {
                    for (var s = 0; s < vSelect.options.length; s++) {
                        vSelect.options[s].textContent = vSelect.options[s].textContent.replace(" (Latest)", "");
                    }
                    var foundOpt = false;
                    for (var s2 = 0; s2 < vSelect.options.length; s2++) {
                        if (vSelect.options[s2].value === newIdStr) {
                            vSelect.options[s2].selected = true;
                            vSelect.options[s2].textContent = "v" + newVer + " (Latest)";
                            vSelect.options[s2].dataset.steps = updated.stepCount;
                            foundOpt = true;
                            break;
                        }
                    }
                    if (!foundOpt) {
                        var optNew = document.createElement("option");
                        optNew.value = newIdStr;
                        optNew.dataset.version = newVer;
                        optNew.dataset.steps = updated.stepCount;
                        optNew.dataset.name = updated.name;
                        optNew.dataset.code = updated.templateCode;
                        optNew.dataset.label = updated.label;
                        optNew.dataset.islatest = "true";
                        optNew.selected = true;
                        optNew.textContent = "v" + newVer + " (Latest)";
                        vSelect.insertBefore(optNew, vSelect.firstChild);
                    }
                } else {
                    // Previously only 1 version; insert Arduino-style version dropdown before buttons
                    var actionsDiv = ci.querySelector(".d-flex.align-items-center.gap-1");
                    if (actionsDiv) {
                        var newSelect = document.createElement("select");
                        newSelect.className = "form-select form-select-sm cat-version-select py-0 px-1 border-secondary-subtle";
                        newSelect.style.cssText = "width:auto; max-width:110px; font-size:0.72rem; font-weight:600; height:24px; line-height:1; display:inline-block; border-radius:4px;";
                        newSelect.title = "Select template version";

                        var opt1 = document.createElement("option");
                        opt1.value = newIdStr;
                        opt1.dataset.version = newVer;
                        opt1.dataset.steps = updated.stepCount;
                        opt1.dataset.name = updated.name;
                        opt1.dataset.code = updated.templateCode;
                        opt1.dataset.label = updated.label;
                        opt1.dataset.islatest = "true";
                        opt1.selected = true;
                        opt1.textContent = "v" + newVer + " (Latest)";
                        newSelect.appendChild(opt1);

                        var opt2 = document.createElement("option");
                        opt2.value = oldIdStr;
                        opt2.dataset.version = oldVer;
                        opt2.dataset.steps = ci.dataset.steps || updated.stepCount;
                        opt2.dataset.name = updated.name;
                        opt2.dataset.code = updated.templateCode;
                        opt2.dataset.islatest = "false";
                        opt2.textContent = "v" + oldVer;
                        newSelect.appendChild(opt2);

                        actionsDiv.insertBefore(newSelect, actionsDiv.firstChild);
                    }
                }
            }
        }

        function executeSaveTemplate(updatePayload, addToProduct) {
            btnGenerateTemplate.disabled = true;
            btnGenerateTemplate.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Saving new version...';

            fetch("/Product/UpdateInlineTemplate", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(updatePayload)
            })
            .then(function (r) {
                if (!r.ok) {
                    return r.text().then(function (text) {
                        var msg = "Failed to update template specification.";
                        try {
                            var json = JSON.parse(text);
                            msg = json.error || json.title || msg;
                        } catch (_) {
                            if (text && text.length < 200) msg = text;
                        }
                        throw new Error(msg);
                    });
                }
                return r.json();
            })
            .then(function (updated) {
                var oldId = updatePayload.templateId;
                editingTemplateId = updated.templateId;

                // Update in dropdowns & catalogue
                updateTemplateInCatalogueAndDropdowns(updated, oldId);

                if (addToProduct) {
                    // Update current row in #components-body if associated, or add new row
                    var currentRows = visibleRows();
                    var targetRow = (editingComponentRowIdx != null && currentRows[editingComponentRowIdx])
                        ? currentRows[editingComponentRowIdx]
                        : null;

                    if (!targetRow) {
                        for (var r = 0; r < currentRows.length; r++) {
                            var s = currentRows[r].querySelector(".row-template-select");
                            if (s && (s.value === String(oldId) || s.value === String(updated.templateId))) {
                                targetRow = currentRows[r];
                                editingComponentRowIdx = r;
                                break;
                            }
                        }
                    }

                    if (targetRow) {
                        var sel = targetRow.querySelector(".row-template-select");
                        if (sel) sel.value = String(updated.templateId);
                        var nameInput = targetRow.querySelector(".row-component-name");
                        if (nameInput) nameInput.value = updated.name;
                        var stepBadge = targetRow.querySelector(".row-steps");
                        if (stepBadge) stepBadge.textContent = updated.stepCount + " steps";
                    } else {
                        // User edited a catalogue template not yet attached to this product: add it now!
                        addRow(updated.templateId, updated.name);
                    }

                    renderStep2AddedTemplates();
                    setTemplateEditorMode(true, updated);

                    showAlert("Template '" + updated.name + "' (" + updated.templateCode + " v" + updated.version + ") saved as new version and added to product! Original template remains unchanged.", false, 2);
                } else {
                    // User chose NOT to add to current product: save as new version in catalogue only
                    resetTemplateEditor();
                    renderStep2AddedTemplates();

                    showAlert("Template '" + updated.name + "' (" + updated.templateCode + " v" + updated.version + ") saved as new version in the catalogue library. Current product was not modified.", false, 2);
                }
            })
            .catch(function (err) {
                showAlert(err.message, true, 2);
            })
            .finally(function () {
                btnGenerateTemplate.disabled = false;
                if (editingTemplateId) {
                    btnGenerateTemplate.innerHTML = '<i class="bi bi-check2-circle me-1"></i>Update Template &amp; Save Changes';
                } else {
                    btnGenerateTemplate.innerHTML = '<i class="bi bi-magic me-1"></i>Generate Template &amp; Attach to Product';
                }
                pendingUpdatePayload = null;
            });
        }

        function loadTemplateIntoEditor(templateId, rowIdx, readOnly) {
            if (!templateId || templateId <= 0) return;
            var viewOnly = (readOnly === true);

            showAlert("Loading template specifications...", false, 2);

            fetch("/Product/GetTemplateDetails?templateId=" + encodeURIComponent(templateId))
                .then(function (r) {
                    if (!r.ok) throw new Error("Could not load template details.");
                    return r.json();
                })
                .then(function (tpl) {
                    if (rowIdx == null) {
                        var rows = visibleRows();
                        for (var i = 0; i < rows.length; i++) {
                            var sel = rows[i].querySelector(".row-template-select");
                            if (sel && parseInt(sel.value, 10) === tpl.templateId) {
                                rowIdx = i;
                                break;
                            }
                        }
                    }

                    if (viewOnly) {
                        editingTemplateId = null;
                        editingComponentRowIdx = null;
                        viewingTemplateId = tpl.templateId;
                        viewingComponentRowIdx = rowIdx;
                    } else {
                        viewingTemplateId = null;
                        viewingComponentRowIdx = null;
                        editingTemplateId = tpl.templateId;
                        editingComponentRowIdx = rowIdx;
                    }

                    // 1. Template name & modes
                    if (genTplName) {
                        genTplName.value = tpl.name || "";
                        genTplName.classList.remove("is-invalid");
                    }
                    if (genTplMode) {
                        genTplMode.value = tpl.mode || "Torque";
                        genTplMode.dispatchEvent(new Event("change"));
                    }
                    if (genTplTolMode) {
                        genTplTolMode.value = tpl.toleranceMode || "Absolute";
                        genTplTolMode.dispatchEvent(new Event("change"));
                    }

                    // 2. Clear & populate steps
                    if (genStepsTbody) {
                        genStepsTbody.innerHTML = "";
                        (tpl.steps || []).forEach(function (s, sIdx) {
                            var lbl = s.label;
                            if (tpl.mode === "Vernier" && (/^bolt\s*\d+$/i.test(lbl) || !lbl)) {
                                lbl = "Distance " + (sIdx + 1);
                            }
                            var tr = createGenStepRow(sIdx + 1, lbl, s.target, s.tolerancePlus, s.markerX, s.markerY, s.markerX2, s.markerY2, s.markerSize);
                            genStepsTbody.appendChild(tr);
                        });
                        updateGenStepCount();
                        syncModeTexts();
                    }

                    // 3. Image & Markers
                    if (tpl.imagePath) {
                        genImagePath.value = tpl.imagePath;
                        var displayUrl = (tpl.imageUrl || ("/" + tpl.imagePath)) + "?t=" + Date.now();
                        if (genComponentImage) {
                            genComponentImage.src = displayUrl;
                        }
                        if (genImageRow) genImageRow.classList.remove("d-none");
                        if (genImagePlaceholder) genImagePlaceholder.classList.add("d-none");
                        if (genMarkerPanel) genMarkerPanel.classList.remove("d-none");
                        if (genUploadBtnText) genUploadBtnText.textContent = "Replace Image";
                        if (btnGenEditImage) btnGenEditImage.classList.remove("d-none");
                        if (btnGenRemoveImage) btnGenRemoveImage.classList.remove("d-none");

                        if (genComponentImage && genComponentImage.complete && genComponentImage.naturalWidth > 0) {
                            syncGenMarkerLayer();
                            renderGenMarkers();
                        } else if (genComponentImage) {
                            genComponentImage.onload = function () {
                                syncGenMarkerLayer();
                                renderGenMarkers();
                            };
                        }
                    } else {
                        genImagePath.value = "";
                        if (genComponentImage) genComponentImage.src = "";
                        if (genImageRow) genImageRow.classList.add("d-none");
                        if (genImagePlaceholder) genImagePlaceholder.classList.remove("d-none");
                        if (genMarkerPanel) genMarkerPanel.classList.add("d-none");
                        if (genUploadBtnText) genUploadBtnText.textContent = "Upload Component Drawing / Photo";
                        if (btnGenEditImage) btnGenEditImage.classList.add("d-none");
                        if (btnGenRemoveImage) btnGenRemoveImage.classList.add("d-none");
                        if (genMarkerLayer) genMarkerLayer.innerHTML = "";
                    }

                    if (tpl.steps && tpl.steps.length > 0) {
                        selectGenStep(0);
                    }

                    // 4. Update UI to "Viewing" or "Editing" mode
                    setTemplateEditorMode(viewOnly ? "view" : "edit", tpl);
                    renderStep2AddedTemplates();

                    // Scroll generator into view smoothly
                    var col = document.getElementById("template-generator-col");
                    if (col) {
                        col.scrollIntoView({ behavior: "smooth", block: "start" });
                    }

                    showAlert(
                        viewOnly
                            ? ("Viewing template '" + tpl.name + "' (read-only). Click the Edit button to make changes.")
                            : ("Template '" + tpl.name + "' loaded for editing. You can adjust any step, target, tolerance, visual pin, or drawing and save."),
                        false,
                        2
                    );
                })
                .catch(function (err) {
                    showAlert("Failed to load template: " + err.message, true, 2);
                });
        }

        // -------------------------------------------------------------
        // Step Rows Management
        // -------------------------------------------------------------
        function formatSpecNum(val) {
            var num = parseFloat(val);
            if (isNaN(num)) return "0";
            var rounded = Math.round(num * 100) / 100;
            return (rounded === Math.floor(rounded)) ? String(Math.floor(rounded)) : String(rounded);
        }

        function updateAutoGeneratedCodePreview() {
            var codeEl = document.getElementById("gen-auto-template-code");
            if (!codeEl || !genStepsTbody) return;

            var isVernier = isVernierMode();
            var isPercent = genTplTolMode && genTplTolMode.value === "Percent";
            var rows = Array.from(genStepsTbody.querySelectorAll("tr"));

            if (rows.length === 0) {
                codeEl.textContent = isVernier ? "VC-0S" : "TW-0B";
                return;
            }

            var targets = [];
            var firstTol = null;
            rows.forEach(function (r) {
                var tInput = r.querySelector(".step-target");
                var tolInput = r.querySelector(".step-tol");
                var tStr = formatSpecNum(tInput ? tInput.value : 0);
                if (targets.indexOf(tStr) === -1) targets.push(tStr);
                if (firstTol === null) firstTol = formatSpecNum(tolInput ? tolInput.value : 0);
            });

            var tolPart = isPercent ? ("T" + firstTol + "P") : ("T" + firstTol);
            var prefix = isVernier ? ("VC-" + rows.length + "S-" + targets.join("_") + "MM-" + tolPart)
                                   : ("TW-" + rows.length + "B-" + targets.join("_") + "NM-" + tolPart);
            codeEl.textContent = prefix.toUpperCase().slice(0, 50);
        }

        function updateGenStepCount() {
            if (!genStepsTbody) return;
            var rows = genStepsTbody.querySelectorAll("tr");
            if (genStepsCount) genStepsCount.textContent = rows.length;
            updateAutoGeneratedCodePreview();
        }

        if (genStepsTbody) {
            genStepsTbody.addEventListener("input", updateAutoGeneratedCodePreview);
            genStepsTbody.addEventListener("change", updateAutoGeneratedCodePreview);
        }
        if (genTplMode) genTplMode.addEventListener("change", updateAutoGeneratedCodePreview);
        if (genTplTolMode) genTplTolMode.addEventListener("change", updateAutoGeneratedCodePreview);

        function isVernierMode() {
            return genTplMode && genTplMode.value === "Vernier";
        }
        function isMixedMode() {
            return genTplMode && genTplMode.value === "Mixed";
        }
        function isStepVernier(row) {
            if (isVernierMode()) return true;
            if (!isMixedMode() || !row) return false;
            var dt = getStepDimType("", row);
            return dt === "distance" || dt === "radius";
        }

        function isRowMarkerPlaced(row) {
            if (!row) return false;
            var inputX = row.querySelector(".step-marker-x");
            var inputY = row.querySelector(".step-marker-y");
            var p1 = inputX && inputY && inputX.value && inputY.value && !isNaN(parseFloat(inputX.value)) && !isNaN(parseFloat(inputY.value));
            if (!p1) return false;
            if (isStepVernier(row)) {
                var dimType = getStepDimType("", row);
                if (dimType === "radius") return true;
                var inputX2 = row.querySelector(".step-marker-x2");
                var inputY2 = row.querySelector(".step-marker-y2");
                var p2 = inputX2 && inputY2 && inputX2.value && inputY2.value && !isNaN(parseFloat(inputX2.value)) && !isNaN(parseFloat(inputY2.value));
                return !!p2;
            }
            return true;
        }

        function syncModeTexts() {
            var isVernier = isVernierMode();
            var unitLabels = document.querySelectorAll(".unit-label");
            var u = isVernier ? "mm" : "N·m";
            unitLabels.forEach(function (l) { l.textContent = u; });

            var countLbl = document.getElementById("quick-count-label");
            if (countLbl) countLbl.textContent = isVernier ? "Distance Count" : "Bolt Count";
            var presetHeading = document.getElementById("quick-preset-heading");
            if (presetHeading) presetHeading.textContent = isVernier ? "Quick Distance Preset" : "Quick Bolt Preset";
            var stepsHeading = document.getElementById("gen-steps-type-heading");
            if (stepsHeading) stepsHeading.textContent = isVernier ? "Measurement Steps" : "Fastening Steps";
            var addBtnText = document.getElementById("btn-add-step-text");
            if (addBtnText) addBtnText.textContent = isVernier ? "Add Distance Step" : "Add Bolt Step";
            var colMarker = document.getElementById("col-header-marker");
            if (colMarker) colMarker.textContent = isVernier ? "Marker" : "Pin";

            var markerTooltip = document.getElementById("gen-marker-tooltip");
            if (markerTooltip) {
                markerTooltip.setAttribute("data-tooltip", isVernier
                    ? "Click 2 points for distance · Click circle for radius · drag grips to adjust"
                    : "Click image to pin bolt position · drag a pin to move it");
            }
            var instrIcon = document.getElementById("gen-marker-instruction-icon");
            if (instrIcon) {
                instrIcon.className = isVernier ? "bi bi-rulers text-success me-1" : "bi bi-pin-map-fill text-danger me-1";
            }
            var mmTitleIcon = document.getElementById("mm-title-icon");
            if (mmTitleIcon) {
                mmTitleIcon.className = isVernier ? "bi bi-rulers text-success me-1" : "bi bi-pin-map-fill text-danger me-1";
            }
            var mmTitleText = document.getElementById("mm-title-text");
            if (mmTitleText) {
                mmTitleText.textContent = isVernier ? "CAD Dimension Specification (Caliper)" : "Mark Step Positions";
            }
            var mmInstr = document.getElementById("mm-placement-instruction");
            if (mmInstr) {
                mmInstr.innerHTML = isVernier
                    ? "Click 2 points for linear distance or click circle for radius &middot; drag grips to fine-tune"
                    : "Click the image to place the pin &middot; drag a pin to fine-tune it";
            }

            var isVernier = isVernierMode();
            var isMixed = isMixedMode();
            var vernierToolbar = document.getElementById("vernier-type-toolbar");
            if (vernierToolbar) vernierToolbar.classList.toggle("d-none", !(isVernier || isMixed));
            var boltTypeBtn = document.getElementById("btn-type-bolt");
            if (boltTypeBtn) boltTypeBtn.classList.toggle("d-none", !isMixed);
            var mmBoltTypeBtn = document.getElementById("mm-btn-type-bolt");
            if (mmBoltTypeBtn) mmBoltTypeBtn.classList.toggle("d-none", !isMixed);
            var mmVernierToolbar = document.getElementById("mm-vernier-toolbar");
            if (mmVernierToolbar) mmVernierToolbar.classList.toggle("d-none", !(isVernier || isMixed));
            var torqueSizeToolbar = document.getElementById("torque-marker-size-toolbar");
            if (torqueSizeToolbar) torqueSizeToolbar.classList.toggle("d-none", isVernier);

            document.querySelectorAll(".step-dim-type-btn").forEach(function (b) {
                b.classList.toggle("d-none", !(isVernier || isMixed));
            });

            // Automatically adapt step labels when switching between Vernier and Torque mode
            var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : [];
            rows.forEach(function (r, idx) {
                var lblInput = r.querySelector(".step-label");
                if (lblInput) {
                    var v = lblInput.value.trim();
                    if (isVernier) {
                        if (/^bolt\s*\d+$/i.test(v) || !v) {
                            var dimType = getStepDimType("", r);
                            lblInput.value = dimType === "radius" ? ("Radius " + (idx + 1)) : ("Distance " + (idx + 1));
                        }
                    } else {
                        if (/^(distance|dimension|radius|r)\s*\d+$/i.test(v) || !v) {
                            lblInput.value = "Bolt " + (idx + 1);
                        }
                    }
                }
            });

            updateStepInstructionPrompt(currentSelectedStep);
        }

        function getStepDimType(label, tr) {
            if (tr) {
                var dimInput = tr.querySelector(".step-dim-type");
                if (dimInput && dimInput.value) return dimInput.value;
            }
            if (!label) return "distance";
            var l = label.trim().toLowerCase();
            if (/^(r\b|r\d|radius|rad)/i.test(l) || /\bradius\b/i.test(l)) return "radius";
            return "distance";
        }

        function setVernierStepType(type) {
            var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : [];
            if (!rows.length) return;
            if (currentSelectedStep < 0 || currentSelectedStep >= rows.length) currentSelectedStep = 0;
            var tr = rows[currentSelectedStep];
            if (!tr) return;
            var dimTypeInput = tr.querySelector(".step-dim-type");
            if (dimTypeInput) dimTypeInput.value = type;
            var lblInput = tr.querySelector(".step-label");
            var seq = currentSelectedStep + 1;
            if (lblInput) {
                var cur = lblInput.value.trim();
                if (/^(dimension|distance|bolt|r|radius)\s*\d+$/i.test(cur) || !cur) {
                    lblInput.value = type === "torque" ? ("Bolt " + seq) : (type === "radius" ? ("Radius " + seq) : ("Distance " + seq));
                }
            }
            var typeBtn = tr.querySelector(".step-dim-type-btn");
            if (typeBtn) {
                typeBtn.querySelector(".dim-type-icon").textContent = type === "torque" ? "🔧" : (type === "radius" ? "R" : "↔");
            }
            if (type === "radius" || type === "torque") {
                var inpX2 = tr.querySelector(".step-marker-x2");
                var inpY2 = tr.querySelector(".step-marker-y2");
                if (inpX2) inpX2.value = "";
                if (inpY2) inpY2.value = "";
            }
            var placed = isRowMarkerPlaced(tr);
            var pinIcon = tr.querySelector(".btn-marker-pin i");
            if (pinIcon) {
                var inpX1 = tr.querySelector(".step-marker-x");
                var inpY1 = tr.querySelector(".step-marker-y");
                var hasP1 = inpX1 && inpY1 && inpX1.value && inpY1.value;
                pinIcon.className = placed ? "bi bi-rulers text-success" : (hasP1 ? "bi bi-rulers text-warning" : "bi bi-geo-alt text-secondary opacity-50");
            }
            tr.classList.toggle("table-danger", !placed);

            syncVernierTypeButtons(type);
            updateStepInstructionPrompt(currentSelectedStep);
            renderGenMarkers();
            if (mmModalEl && mmModalEl.classList.contains("show")) renderMmMarkers();
        }

        function syncVernierTypeButtons(type) {
            document.querySelectorAll("#vernier-type-btn-group button, #mm-vernier-toolbar button").forEach(function (b) {
                var bt = b.dataset.vernierType || b.dataset.type;
                var isMatch = bt === type;
                b.classList.toggle("active", isMatch);
                b.classList.toggle("btn-success", isMatch);
                b.classList.toggle("btn-outline-success", !isMatch);
            });
            var hint = document.getElementById("vernier-type-hint");
            if (hint) {
                hint.textContent = type === "radius"
                    ? "Click 2 points: Center & Circumference edge"
                    : "Click 2 points: Start & End of distance";
            }
        }

        function createGenStepRow(seq, label, target, tol, markerX, markerY, markerX2, markerY2, markerSize) {
            // Support legacy 7-parameter calls: (seq, label, target, tol, markerX, markerY, markerSize)
            if (arguments.length === 7 && typeof markerX2 === "number" && markerSize === undefined) {
                markerSize = markerX2;
                markerX2 = null;
                markerY2 = null;
            }

            var tr = document.createElement("tr");
            tr.className = "gen-step-row";
            tr.dataset.index = seq - 1;
            tr.style.cursor = "pointer";

            var hasP1 = markerX != null && markerY != null && !isNaN(parseFloat(markerX)) && !isNaN(parseFloat(markerY));
            var hasP2 = markerX2 != null && markerY2 != null && !isNaN(parseFloat(markerX2)) && !isNaN(parseFloat(markerY2));
            var isVernier = isVernierMode();
            var isPlaced = isVernier ? (hasP1 && hasP2) : hasP1;

            var pinIconClass = isPlaced
                ? (isVernier ? "bi-rulers text-success" : "bi-geo-alt-fill text-success")
                : (hasP1 && isVernier ? "bi-rulers text-warning" : "bi-geo-alt text-secondary opacity-50");

            var dimType = getStepDimType(label);
            var dimTypeIcon = dimType === "radius" ? "R" : "↔";

            var labelCellHtml = isVernier
                ? ('<td>' +
                    '<div class="input-group input-group-sm">' +
                        '<button type="button" class="btn btn-outline-secondary px-1 step-dim-type-btn" title="Toggle: Line Distance (↔) / Radius (R)">' +
                            '<span class="dim-type-icon fw-bold" style="font-size:11px; font-family:monospace; min-width:14px; display:inline-block;">' + dimTypeIcon + '</span>' +
                        '</button>' +
                        '<input type="text" class="form-control form-control-sm py-0 step-label" value="' + label + '" />' +
                    '</div>' +
                '</td>')
                : ('<td><input type="text" class="form-control form-control-sm py-0 step-label" value="' + label + '" /></td>');

            tr.innerHTML = '<td class="fw-bold text-center text-muted seq-cell">' + seq + '</td>' +
                labelCellHtml +
                '<td><input type="number" step="0.1" class="form-control form-control-sm py-0 step-target" value="' + target + '" /></td>' +
                '<td><input type="number" step="0.1" class="form-control form-control-sm py-0 step-tol" value="' + tol + '" /></td>' +
                '<td class="text-center align-middle">' +
                    '<button type="button" class="btn btn-sm btn-link p-0 text-decoration-none btn-marker-pin" title="' + (isVernier ? 'Open caliper marking view' : 'Open marking view') + '">' +
                        '<i class="bi ' + pinIconClass + '"></i>' +
                    '</button>' +
                    '<input type="hidden" class="step-marker-x" value="' + (hasP1 ? markerX : '') + '" />' +
                    '<input type="hidden" class="step-marker-y" value="' + (hasP1 ? markerY : '') + '" />' +
                    '<input type="hidden" class="step-marker-x2" value="' + (hasP2 ? markerX2 : '') + '" />' +
                    '<input type="hidden" class="step-marker-y2" value="' + (hasP2 ? markerY2 : '') + '" />' +
                    '<input type="hidden" class="step-marker-size" value="' + (markerSize ? markerSize : '') + '" />' +
                    '<input type="hidden" class="step-dim-type" value="' + dimType + '" />' +
                '</td>' +
                '<td class="text-end"><button type="button" class="btn btn-sm btn-link text-danger p-0 step-del" title="Remove step"><i class="bi bi-x-circle"></i></button></td>';

            var typeBtn = tr.querySelector(".step-dim-type-btn");
            var lblInput = tr.querySelector(".step-label");
            if (typeBtn && lblInput) {
                typeBtn.addEventListener("click", function (e) {
                    e.stopPropagation();
                    var cur = getStepDimType(lblInput.value, tr);
                    var nextType = isMixedMode()
                        ? (cur === "torque" ? "distance" : (cur === "distance" ? "radius" : "torque"))
                        : (cur === "distance" ? "radius" : "distance");
                    var dimInput = tr.querySelector(".step-dim-type");
                    if (dimInput) dimInput.value = nextType;
                    if (nextType === "torque") {
                        lblInput.value = "Bolt " + seq;
                        typeBtn.querySelector(".dim-type-icon").textContent = "🔧";
                        var inpX2 = tr.querySelector(".step-marker-x2");
                        var inpY2 = tr.querySelector(".step-marker-y2");
                        if (inpX2) inpX2.value = "";
                        if (inpY2) inpY2.value = "";
                    } else if (nextType === "radius") {
                        lblInput.value = "Radius " + seq;
                        typeBtn.querySelector(".dim-type-icon").textContent = "R";
                        var inpX2 = tr.querySelector(".step-marker-x2");
                        var inpY2 = tr.querySelector(".step-marker-y2");
                        if (inpX2) inpX2.value = "";
                        if (inpY2) inpY2.value = "";
                    } else {
                        lblInput.value = "Distance " + seq;
                        typeBtn.querySelector(".dim-type-icon").textContent = "↔";
                    }
                    var placed = isRowMarkerPlaced(tr);
                    var pinIcon = tr.querySelector(".btn-marker-pin i");
                    if (pinIcon) {
                        var inpX1 = tr.querySelector(".step-marker-x");
                        var inpY1 = tr.querySelector(".step-marker-y");
                        var hasP1 = inpX1 && inpY1 && inpX1.value && inpY1.value;
                        pinIcon.className = placed ? "bi bi-rulers text-success" : (hasP1 ? "bi bi-rulers text-warning" : "bi bi-geo-alt text-secondary opacity-50");
                    }
                    tr.classList.toggle("table-danger", !placed);
                    if (currentSelectedStep === (seq - 1)) {
                        syncVernierTypeButtons(nextType);
                        updateStepInstructionPrompt(currentSelectedStep);
                    }
                    renderGenMarkers();
                    if (typeof renderMmMarkers === "function") renderMmMarkers();
                });

                lblInput.addEventListener("input", function () {
                    var dt = getStepDimType(this.value, tr);
                    var icon = dt === "radius" ? "R" : "↔";
                    var iconEl = typeBtn.querySelector(".dim-type-icon");
                    if (iconEl) iconEl.textContent = icon;
                    var dimInput = tr.querySelector(".step-dim-type");
                    if (dimInput) dimInput.value = dt;
                    syncVernierTypeButtons(dt);
                    renderGenMarkers();
                    if (typeof renderMmMarkers === "function") renderMmMarkers();
                });
            }

            var targetInput = tr.querySelector(".step-target");
            if (targetInput) {
                targetInput.addEventListener("input", function () {
                    renderGenMarkers();
                    if (typeof renderMmMarkers === "function") renderMmMarkers();
                });
            }

            tr.addEventListener("click", function (e) {
                if (e.target.closest(".step-del")) return;
                var pinBtn = e.target.closest(".btn-marker-pin");
                if (pinBtn) {
                    openMarkerModal(parseInt(tr.dataset.index, 10));
                    return;
                }
                selectGenStep(parseInt(tr.dataset.index, 10));
            });

            return tr;
        }

        function updateStepInstructionPrompt(idx) {
            var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : [];
            var r = rows[idx];
            var lbl = r ? r.querySelector(".step-label") : null;
            var isVernier = isVernierMode();
            var stepType = isVernier ? getStepDimType(lbl ? lbl.value : "", r) : "torque";
            var defaultPrefix = isVernier ? (stepType === "radius" ? "Radius " : "Distance ") : "Bolt ";
            var stepName = (lbl && lbl.value.trim()) ? lbl.value.trim() : (defaultPrefix + (idx + 1));
            if (isVernier) {
                stepName = stepName.replace(/^bolt\s*/i, stepType === "radius" ? "Radius " : "Distance ");
                if (lbl && /^bolt\s*\d+$/i.test(lbl.value.trim())) {
                    lbl.value = stepName;
                }
            }
            
            if (genSelectedStepLabel) genSelectedStepLabel.textContent = stepName;
            if (mmSelLabel) mmSelLabel.textContent = stepName;

            var selInstr = document.getElementById("gen-selected-step-instruction");
            var mmInstr = document.getElementById("mm-placement-instruction");
            if (r) {
                var mx1 = r.querySelector(".step-marker-x");
                var my1 = r.querySelector(".step-marker-y");
                var mx2 = r.querySelector(".step-marker-x2");
                var my2 = r.querySelector(".step-marker-y2");
                var hasP1 = mx1 && my1 && mx1.value && my1.value;
                var hasP2 = mx2 && my2 && mx2.value && my2.value;

                var prompt = "";
                if (isVernier) {
                    syncVernierTypeButtons(stepType);
                    if (stepType === "radius") {
                        if (hasP1) {
                            prompt = "Radius mapped. Drag point on circle to adjust, or click to re-position.";
                        } else {
                            prompt = "Click on the circle / arc to place radius marker.";
                        }
                    } else {
                        if (hasP1 && !hasP2) {
                            prompt = "Point 1 placed. Click 2nd point on the image to measure distance.";
                        } else if (hasP1 && hasP2) {
                            prompt = "Line distance mapped. Drag snap grips to adjust, or click to re-measure.";
                        } else {
                            prompt = "Click 1st point (start of distance) on the image.";
                        }
                    }
                } else {
                    if (hasP1) {
                        prompt = "Pin placed. Drag pin to adjust, or click to re-position.";
                    } else {
                        prompt = "Click on the image below to drop pin.";
                    }
                }
                if (selInstr) selInstr.textContent = prompt;
                var stepTooltip = document.getElementById("gen-step-tooltip");
                if (stepTooltip) stepTooltip.setAttribute("data-tooltip", prompt);
                if (mmInstr) mmInstr.textContent = prompt;
            }
        }

        function selectGenStep(index, skipMarkerRender) {
            currentSelectedStep = index;
            var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : [];
            rows.forEach(function (r, idx) {
                if (idx === index) {
                    r.classList.add("table-primary");
                } else {
                    r.classList.remove("table-primary");
                }
            });
            updateStepInstructionPrompt(index);

            // A full re-render rebuilds (i.e. destroys) the dots — that would kill
            // an in-progress drag, which is why the skip hint exists.
            if (!skipMarkerRender) renderGenMarkers();

            // Size controls follow the selection: a placed marker shows its own
            // size, an unplaced row shows the default for future placements.
            var selRow = rows[index];
            if (selRow) {
                var xInp = selRow.querySelector(".step-marker-x");
                var placed = xInp && xInp.value;
                syncMarkerSizeControls(placed ? getRowMarkerSize(selRow) : currentMarkerSize);
            }
            if (typeof mmRefreshSelLabel === "function") mmRefreshSelLabel();
        }

        function autoAdvanceToNextUnplacedStep(isModal) {
            var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : [];
            if (!rows.length) return;
            var nextIdx = -1;
            for (var i = currentSelectedStep + 1; i < rows.length; i++) {
                if (!isRowMarkerPlaced(rows[i])) {
                    nextIdx = i;
                    break;
                }
            }
            if (nextIdx === -1) {
                for (var j = 0; j < currentSelectedStep; j++) {
                    if (!isRowMarkerPlaced(rows[j])) {
                        nextIdx = j;
                        break;
                    }
                }
            }
            if (nextIdx !== -1) {
                if (isModal) selectMmInplace(nextIdx); else selectGenStep(nextIdx);
            }
        }

        function populateQuickSteps(count, target, tol) {
            if (!genStepsTbody) return;
            genStepsTbody.innerHTML = "";
            count = Math.max(1, parseInt(count, 10) || 4);
            target = parseFloat(target) || 25.0;
            tol = parseFloat(tol) || 2.0;

            var prefix = isVernierMode() ? "Distance " : "Bolt ";
            for (var i = 1; i <= count; i++) {
                genStepsTbody.appendChild(createGenStepRow(i, prefix + i, target, tol));
            }
            updateGenStepCount();
            selectGenStep(0);
            renderGenMarkers();
        }

        // Mode switch changes unit label & markers
        if (genTplMode) {
            genTplMode.addEventListener("change", function () {
                syncModeTexts();
                renderGenMarkers();
                if (typeof renderMmMarkers === "function") renderMmMarkers();
            });
        }

        if (btnApplyQuick) {
            btnApplyQuick.addEventListener("click", function () {
                var c = document.getElementById("quick-bolt-count").value;
                var t = document.getElementById("quick-target").value;
                var tol = document.getElementById("quick-tol").value;
                populateQuickSteps(c, t, tol);
            });
        }

        if (btnAddStep) {
            btnAddStep.addEventListener("click", function () {
                var nextSeq = (genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row").length : 0) + 1;
                var defTarget = document.getElementById("quick-target") ? document.getElementById("quick-target").value : "25.0";
                var defTol = document.getElementById("quick-tol") ? document.getElementById("quick-tol").value : "2.0";
                var prefix = isVernierMode() ? "Distance " : "Bolt ";
                var row = createGenStepRow(nextSeq, prefix + nextSeq, defTarget, defTol);
                genStepsTbody.appendChild(row);
                updateGenStepCount();
                selectGenStep(nextSeq - 1);
            });
        }

        if (genStepsTbody) {
            genStepsTbody.addEventListener("click", function (e) {
                var delBtn = e.target.closest(".step-del");
                if (delBtn) {
                    var tr = delBtn.closest(".gen-step-row");
                    tr.remove();
                    // renumber
                    genStepsTbody.querySelectorAll(".gen-step-row").forEach(function (r, idx) {
                        r.dataset.index = idx;
                        r.querySelector(".seq-cell").textContent = idx + 1;
                    });
                    updateGenStepCount();
                    if (currentSelectedStep >= genStepsTbody.querySelectorAll(".gen-step-row").length) {
                        currentSelectedStep = Math.max(0, genStepsTbody.querySelectorAll(".gen-step-row").length - 1);
                    }
                    selectGenStep(currentSelectedStep);
                    renderGenMarkers();
                }
            });
        }

        function collectGenSteps() {
            var rows = genStepsTbody.querySelectorAll(".gen-step-row");
            var steps = [];
            var mode = genTplMode ? genTplMode.value : "Torque";
            var tolMode = genTplTolMode ? genTplTolMode.value : "Absolute";

            rows.forEach(function (tr, idx) {
                var dimType = getStepDimType("", tr);
                var stepIsVernier = mode === "Vernier" || (mode === "Mixed" && (dimType === "distance" || dimType === "radius"));
                var instrument = stepIsVernier ? "Vernier" : "Torque";
                var defaultPrefix = stepIsVernier ? (dimType === "radius" ? "Radius " : "Distance ") : "Bolt ";
                var label = tr.querySelector(".step-label").value.trim() || (defaultPrefix + (idx + 1));
                if (mode === "Vernier") {
                    label = label.replace(/^bolt\s*/i, "Distance ");
                }
                var target = parseFloat(tr.querySelector(".step-target").value) || 0;
                var tol = parseFloat(tr.querySelector(".step-tol").value) || 0;
                var inputX = tr.querySelector(".step-marker-x");
                var inputY = tr.querySelector(".step-marker-y");
                var inputX2 = tr.querySelector(".step-marker-x2");
                var inputY2 = tr.querySelector(".step-marker-y2");
                var markerX = inputX && inputX.value ? parseFloat(inputX.value) : null;
                var markerY = inputY && inputY.value ? parseFloat(inputY.value) : null;
                var markerX2 = inputX2 && inputX2.value ? parseFloat(inputX2.value) : null;
                var markerY2 = inputY2 && inputY2.value ? parseFloat(inputY2.value) : null;
                var sizeInput = tr.querySelector(".step-marker-size");
                var markerSize = markerX != null && sizeInput && sizeInput.value ? parseInt(sizeInput.value, 10) : null;
                if (markerSize != null && isNaN(markerSize)) markerSize = null;

                steps.push({
                    sequenceNo: idx + 1,
                    label: label,
                    instrument: instrument,
                    target: target,
                    tolerancePlus: tol,
                    toleranceMinus: tol,
                    toleranceMode: tolMode,
                    markerX: markerX,
                    markerY: markerY,
                    markerX2: markerX2,
                    markerY2: markerY2,
                    markerSize: markerSize,
                    maxRetries: 999
                });
            });
            return steps;
        }

        // -------------------------------------------------------------
        // Compulsory marker assignment: a step without placed marker(s)
        // blocks template generation and Step-3 navigation.
        // -------------------------------------------------------------
        function firstUnmarkedGenRow() {
            if (!genImagePath || !genImagePath.value) return null;
            var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : null;
            if (!rows) return null;
            for (var i = 0; i < rows.length; i++) {
                if (!isRowMarkerPlaced(rows[i])) {
                    return { row: rows[i], index: i };
                }
            }
            return null;
        }

        function refreshUnplacedMarkers() {
            var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : [];
            rows.forEach(function (r) {
                var placed = isRowMarkerPlaced(r);
                r.classList.toggle("table-danger", !placed);
            });
        }

        function focusUnplacedMarker(unmarked) {
            if (!unmarked) return;
            refreshUnplacedMarkers();
            selectGenStep(unmarked.index);
            unmarked.row.scrollIntoView({ behavior: "smooth", block: "center" });
        }

        // -------------------------------------------------------------
        // Marker Size & Rendering on Image
        // -------------------------------------------------------------
        var genMarkerLayer = document.getElementById("gen-marker-layer");
        var genSizeSlider = document.getElementById("gen-marker-size-slider");
        var genSizeValue = document.getElementById("gen-marker-size-value");
        var genDrag = null; // active marker drag: { idx, dot }
        var genSuppressClick = false; // swallow the canvas click after a dot rebuild

        function clampPct(value) {
            return Math.max(0, Math.min(100, value));
        }

        // The dark container letterboxes the centered image, so the pin overlay
        // must mirror the img box exactly or pins drift away from their clicks.
        function syncGenMarkerLayer() {
            if (!genMarkerLayer || !genImageContainer || !genComponentImage) return;
            var rect = genComponentImage.getBoundingClientRect();
            if (!rect.width || !rect.height) return;
            var containerRect = genImageContainer.getBoundingClientRect();
            genMarkerLayer.style.left = (rect.left - containerRect.left) + "px";
            genMarkerLayer.style.top = (rect.top - containerRect.top) + "px";
            genMarkerLayer.style.width = rect.width + "px";
            genMarkerLayer.style.height = rect.height + "px";
        }

        function getRowMarkerSize(tr) {
            if (!tr) return currentMarkerSize;
            var s = tr.querySelector(".step-marker-size");
            var v = s && s.value ? parseInt(s.value, 10) : NaN;
            return isNaN(v) ? currentMarkerSize : Math.max(14, Math.min(42, v));
        }

        function setMarkerSize(size) {
            size = Math.max(14, Math.min(42, Math.round(parseFloat(size) || 24)));

            // Per-marker sizing: when the selected row has a placed marker the
            // change applies to THAT marker only, otherwise it becomes the
            // default for future placements.
            var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : [];
            var row = rows[currentSelectedStep];
            var inputX = row ? row.querySelector(".step-marker-x") : null;
            if (row && inputX && inputX.value) {
                var sizeInput = row.querySelector(".step-marker-size");
                if (sizeInput) sizeInput.value = size;
            } else {
                currentMarkerSize = size;
                localStorage.setItem("smarttorque_marker_size", size);
            }
            // Keep BOTH surfaces live: the inline panel and the large-view popup.
            renderGenMarkers();
            if (mmModalEl && mmModalEl.classList.contains("show")) renderMmMarkers();
            syncMarkerSizeControls(size);
        }

        function syncMarkerSizeControls(size) {
            var mmSlider = document.getElementById("mm-marker-size-slider");
            var mmValue = document.getElementById("mm-marker-size-value");
            if (genSizeSlider) genSizeSlider.value = size;
            if (mmSlider) mmSlider.value = size;
            if (genSizeValue) genSizeValue.textContent = size + "px";
            if (mmValue) mmValue.textContent = size + "px";
            document.querySelectorAll(".gen-size-preset, .mm-size-preset").forEach(function (btn) {
                var pSize = parseInt(btn.dataset.size || "0", 10);
                btn.classList.toggle("active", pSize === size);
            });
        }

        document.querySelectorAll(".gen-size-preset").forEach(function (btn) {
            btn.addEventListener("click", function () {
                setMarkerSize(this.dataset.size);
            });
        });

        if (genSizeSlider) {
            genSizeSlider.addEventListener("input", function () {
                setMarkerSize(this.value);
            });
        }

        if (genComponentImage) {
            genComponentImage.addEventListener("load", function () { syncGenMarkerLayer(); });
        }
        window.addEventListener("resize", function () { syncGenMarkerLayer(); });

        function updateRowMarker(idx, x1, y1, x2, y2) {
            var row = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row")[idx] : null;
            if (!row) return;
            var inputX = row.querySelector(".step-marker-x");
            var inputY = row.querySelector(".step-marker-y");
            var inputX2 = row.querySelector(".step-marker-x2");
            var inputY2 = row.querySelector(".step-marker-y2");

            if (inputX && x1 !== undefined) inputX.value = (x1 != null && x1 !== "") ? (typeof x1 === "number" ? x1.toFixed(3) : x1) : "";
            if (inputY && y1 !== undefined) inputY.value = (y1 != null && y1 !== "") ? (typeof y1 === "number" ? y1.toFixed(3) : y1) : "";
            if (inputX2 && x2 !== undefined) inputX2.value = (x2 != null && x2 !== "") ? (typeof x2 === "number" ? x2.toFixed(3) : x2) : "";
            if (inputY2 && y2 !== undefined) inputY2.value = (y2 != null && y2 !== "") ? (typeof y2 === "number" ? y2.toFixed(3) : y2) : "";

            var sizeInput = row.querySelector(".step-marker-size");
            if (sizeInput && !sizeInput.value) {
                sizeInput.value = currentMarkerSize;
            }
            var placed = isRowMarkerPlaced(row);
            var pinIcon = row.querySelector(".btn-marker-pin i");
            if (pinIcon) {
                if (isVernierMode()) {
                    var hasP1 = inputX && inputY && inputX.value && inputY.value;
                    pinIcon.className = placed ? "bi bi-rulers text-success" : (hasP1 ? "bi bi-rulers text-warning" : "bi bi-geo-alt text-secondary opacity-50");
                } else {
                    pinIcon.className = placed ? "bi bi-geo-alt-fill text-success" : "bi bi-geo-alt text-secondary opacity-50";
                }
            }
            row.classList.toggle("table-danger", !placed);
        }

        function buildGenMarkerDot(idx, size) {
            var isSelected = idx === currentSelectedStep;
            var sz = Math.max(14, Math.min(42, parseInt(size, 10) || currentMarkerSize));
            var dot = document.createElement("div");
            dot.className = "gen-marker-dot shadow-sm" + (isSelected ? " active-marker" : "");
            dot.style.position = "absolute";
            dot.style.width = sz + "px";
            dot.style.height = sz + "px";
            dot.style.borderRadius = "50%";
            dot.style.transform = "translate(-50%, -50%)";
            dot.style.display = "flex";
            dot.style.alignItems = "center";
            dot.style.justifyContent = "center";
            dot.style.fontSize = Math.max(9, Math.round(sz * 0.48)) + "px";
            dot.style.fontWeight = "700";
            dot.style.color = "#fff";
            dot.style.cursor = "grab";
            dot.style.background = isSelected ? "var(--bs-primary, #0d6efd)" : "var(--bs-success, #198754)";
            dot.style.border = isSelected ? "2px solid #fff" : "2px solid rgba(255,255,255,0.85)";
            dot.style.boxShadow = isSelected ? "0 0 0 3px rgba(13,110,253,0.5), 0 2px 4px rgba(0,0,0,0.4)" : "0 2px 4px rgba(0,0,0,0.3)";
            dot.style.transition = "width 0.15s, height 0.15s";
            dot.style.zIndex = isSelected ? "10" : "5";
            dot.style.touchAction = "none";
            dot.style.pointerEvents = "auto";
            dot.textContent = idx + 1;
            return dot;
        }

        var CAD_GREEN = "#168039"; // Professional AutoCAD Dimension Green (Layer 3)
        var CAD_GREEN_ACTIVE = "#107c41"; // Active / Selected Green
        var CAD_TEXT_RED = "#d32f2f"; // Engineering Technical Red (matching CAD drawing: 10, 50, 76x76, Ø24)
        var CAD_BLUE = "#2563eb"; // Centerline & Osnap blue

        function getStepCadColor(idx, isSelected) {
            return isSelected ? CAD_GREEN_ACTIVE : CAD_GREEN;
        }

        function buildCadSnapNode(idx, pointNum, x, y, isSelected, isModal, cadColor) {
            var h = document.createElement("div");
            h.className = "cad-snap-node" + (isSelected ? " active-node" : "");
            h.style.left = clampPct(x) + "%";
            h.style.top = clampPct(y) + "%";
            h.dataset.stepIndex = idx;
            h.dataset.pointNum = pointNum;
            h.title = "Step #" + (idx + 1) + " (Point " + pointNum + ") - Drag Osnap grip to adjust";

            var dot = document.createElement("div");
            dot.className = "cad-snap-dot";
            dot.style.borderColor = isSelected ? "#ffffff" : CAD_GREEN;
            dot.style.background = isSelected ? CAD_GREEN : "#ffffff";
            h.appendChild(dot);
            return h;
        }

        // Pointer dragging + tap-to-select for one dot (Torque mode inline)
        function attachGenMarkerDot(dot, idx) {
            dot.addEventListener("pointerdown", function (e) {
                if (!genComponentImage || !genImagePath.value) return;
                if (e.button !== undefined && e.button !== 0) return;
                e.preventDefault();
                e.stopPropagation();
                // Light-weight selection: a full render would destroy THIS dot
                // (a rebuild removes all dots) and kill the active pointer capture.
                selectGenStep(idx, true);
                genDrag = { idx: idx, dot: dot };
                dot.style.cursor = "grabbing";
                try { dot.setPointerCapture(e.pointerId); } catch (err) { }
            });

            dot.addEventListener("pointermove", function (e) {
                if (!genDrag || genDrag.dot !== dot) return;
                var rect = genComponentImage.getBoundingClientRect();
                if (!rect.width || !rect.height) return;
                var x = clampPct((e.clientX - rect.left) / rect.width * 100);
                var y = clampPct((e.clientY - rect.top) / rect.height * 100);
                updateRowMarker(idx, x.toFixed(3), y.toFixed(3));
                dot.style.left = x + "%";
                dot.style.top = y + "%";
            });

            var finishDrag = function () {
                if (!genDrag || genDrag.dot !== dot) return;
                genDrag = null;
                dot.style.cursor = "grab";
                genSuppressClick = true;
                setTimeout(function () { genSuppressClick = false; }, 150);
                renderGenMarkers();
            };
            dot.addEventListener("pointerup", finishDrag);
            dot.addEventListener("pointercancel", finishDrag);

            dot.addEventListener("click", function (e) {
                e.stopPropagation();
            });
        }

        // Pointer dragging for Vernier CAD snap nodes
        function attachVernierHandleDrag(handle, idx, pointNum, isModal) {
            handle.addEventListener("pointerdown", function (e) {
                if (!genImagePath.value) return;
                if (e.button !== undefined && e.button !== 0) return;
                e.preventDefault();
                e.stopPropagation();
                if (isModal) selectMmInplace(idx, true); else selectGenStep(idx, true);
                genDrag = { idx: idx, pointNum: pointNum, handle: handle, isModal: isModal };
                handle.style.cursor = "grabbing";
                try { handle.setPointerCapture(e.pointerId); } catch (err) { }
            });

            handle.addEventListener("pointermove", function (e) {
                if (!genDrag || genDrag.handle !== handle) return;
                var img = isModal ? mmImage : genComponentImage;
                if (!img) return;
                var rect = img.getBoundingClientRect();
                if (!rect.width || !rect.height) return;
                var x = clampPct((e.clientX - rect.left) / rect.width * 100);
                var y = clampPct((e.clientY - rect.top) / rect.height * 100);

                if (pointNum === 1) {
                    updateRowMarker(idx, x.toFixed(3), y.toFixed(3), undefined, undefined);
                } else if (pointNum === 2) {
                    updateRowMarker(idx, undefined, undefined, x.toFixed(3), y.toFixed(3));
                }
                handle.style.left = x + "%";
                handle.style.top = y + "%";

                renderMarkerSurface(isModal);
            });

            var finishDrag = function () {
                if (!genDrag || genDrag.handle !== handle) return;
                genDrag = null;
                handle.style.cursor = "crosshair";
                if (isModal) {
                    mmSuppressClick = true;
                    setTimeout(function () { mmSuppressClick = false; }, 150);
                } else {
                    genSuppressClick = true;
                    setTimeout(function () { genSuppressClick = false; }, 150);
                }
                renderGenMarkers();
                if (typeof renderMmMarkers === "function") renderMmMarkers();
            };

            handle.addEventListener("pointerup", finishDrag);
            handle.addEventListener("pointercancel", finishDrag);
            handle.addEventListener("click", function (e) {
                e.stopPropagation();
            });
        }

        // Unified rendering engine for inline panel and large modal
        function renderMarkerSurface(isModal) {
            var layer = isModal ? mmLayer : genMarkerLayer;
            var img = isModal ? mmImage : genComponentImage;
            if (!layer || !img || !img.src || !genImagePath.value) return;

            if (isModal) syncMmMarkerLayer(); else syncGenMarkerLayer();

            layer.querySelectorAll(".gen-marker-dot, .cad-dim-badge, .gen-marker-svg").forEach(function (d) { d.remove(); });
            layer.querySelectorAll(".cad-snap-node").forEach(function (d) {
                if (genDrag && genDrag.handle === d) return;
                d.remove();
            });

            var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : [];
            if (!rows.length) return;

            var isVernier = isVernierMode() || isMixedMode();
            var layerRect = layer.getBoundingClientRect();
            var W = layerRect.width || img.clientWidth;
            var H = layerRect.height || img.clientHeight;
            if (W <= 0 || H <= 0) {
                setTimeout(function () {
                    if (isModal) syncMmMarkerLayer(); else syncGenMarkerLayer();
                    renderMarkerSurface(isModal);
                }, 60);
                return;
            }

            var svg = null;
            if (isVernier) {
                svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
                svg.setAttribute("class", "gen-marker-svg");
                svg.setAttribute("viewBox", "0 0 " + W + " " + H);
                svg.style.position = "absolute";
                svg.style.top = "0";
                svg.style.left = "0";
                svg.style.width = "100%";
                svg.style.height = "100%";
                svg.style.pointerEvents = "none";
                svg.style.overflow = "visible";
                svg.style.zIndex = "8";
                layer.appendChild(svg);
            }

            rows.forEach(function (tr, idx) {
                var isSelected = idx === currentSelectedStep;
                var inputX = tr.querySelector(".step-marker-x");
                var inputY = tr.querySelector(".step-marker-y");
                var inputX2 = tr.querySelector(".step-marker-x2");
                var inputY2 = tr.querySelector(".step-marker-y2");

                var x1 = inputX && inputX.value && !isNaN(parseFloat(inputX.value)) ? clampPct(parseFloat(inputX.value)) : null;
                var y1 = inputY && inputY.value && !isNaN(parseFloat(inputY.value)) ? clampPct(parseFloat(inputY.value)) : null;
                var x2 = inputX2 && inputX2.value && !isNaN(parseFloat(inputX2.value)) ? clampPct(parseFloat(inputX2.value)) : null;
                var y2 = inputY2 && inputY2.value && !isNaN(parseFloat(inputY2.value)) ? clampPct(parseFloat(inputY2.value)) : null;

                var stepIsVernier = isStepVernier(tr);
                var lblInput = tr.querySelector(".step-label");
                var dimType = stepIsVernier ? getStepDimType(lblInput ? lblInput.value : "", tr) : "torque";
                var defaultPrefix = stepIsVernier ? (dimType === "radius" ? "Radius " : "Distance ") : "Bolt ";
                var stepName = (lblInput && lblInput.value.trim()) || (defaultPrefix + (idx + 1));
                if (stepIsVernier) {
                    stepName = stepName.replace(/^bolt\s*/i, dimType === "radius" ? "Radius " : "Distance ");
                }
                var targetVal = (tr.querySelector(".step-target") && tr.querySelector(".step-target").value) || "0";
                var tolVal = (tr.querySelector(".step-tol") && tr.querySelector(".step-tol").value) || "0";

                if (!stepIsVernier) {
                    if (x1 != null && y1 != null) {
                        var dot = buildGenMarkerDot(idx, getRowMarkerSize(tr));
                        dot.style.left = x1 + "%";
                        dot.style.top = y1 + "%";
                        dot.textContent = idx + 1;
                        layer.appendChild(dot);
                        if (isModal) attachMmDot(dot, idx); else attachGenMarkerDot(dot, idx);
                    }
                } else {
                    var strokeCol = getStepCadColor(idx, isSelected);
                    var strokeWidth = isSelected ? 2.0 : 1.4;

                    if (dimType === "radius") {
                        // -------------------------------------------------------------
                        // CAD Radius Dimension: Single Point on Circle + Text Directly Above
                        // -------------------------------------------------------------
                        if (x1 != null && y1 != null) {
                            var px1 = (x1 / 100) * W;
                            var py1 = (y1 / 100) * H;

                            // 1. Engineering Text directly above the dot (e.g. "R 25")
                            var txtRad = document.createElementNS("http://www.w3.org/2000/svg", "text");
                            txtRad.setAttribute("x", px1);
                            txtRad.setAttribute("y", py1 - 13);
                            txtRad.setAttribute("fill", CAD_TEXT_RED);
                            txtRad.setAttribute("font-family", "Consolas, 'Roboto Mono', 'SF Mono', monospace");
                            txtRad.setAttribute("font-size", isSelected ? "13" : "11");
                            txtRad.setAttribute("font-weight", "700");
                            txtRad.setAttribute("text-anchor", "middle");
                            txtRad.setAttribute("dominant-baseline", "central");
                            txtRad.style.paintOrder = "stroke fill";
                            txtRad.style.stroke = "#ffffff";
                            txtRad.style.strokeWidth = "3px";
                            txtRad.style.strokeLinejoin = "round";
                            txtRad.style.cursor = "pointer";
                            txtRad.style.pointerEvents = "auto";
                            txtRad.textContent = "R " + targetVal;
                            txtRad.addEventListener("click", function (e) {
                                e.stopPropagation();
                                if (isModal) selectMmInplace(idx); else selectGenStep(idx);
                            });
                            svg.appendChild(txtRad);

                            // 2. Single precision CAD snap grip node on the circle
                            var h1 = null;
                            if (genDrag && genDrag.idx === idx && genDrag.pointNum === 1 && genDrag.handle && layer.contains(genDrag.handle)) {
                                h1 = genDrag.handle;
                                h1.style.left = clampPct(x1) + "%";
                                h1.style.top = clampPct(y1) + "%";
                            } else {
                                h1 = buildCadSnapNode(idx, 1, x1, y1, isSelected, isModal, strokeCol);
                                h1.title = stepName + " (Radius: " + targetVal + " mm) - Drag point on circle to adjust";
                                layer.appendChild(h1);
                                attachVernierHandleDrag(h1, idx, 1, isModal);
                            }
                        }
                    } else {
                        // -------------------------------------------------------------
                        // CAD Linear Distance Dimension (2 points)
                        // -------------------------------------------------------------
                        if (x1 != null && y1 != null && x2 != null && y2 != null) {
                            var px1 = (x1 / 100) * W;
                            var py1 = (y1 / 100) * H;
                            var px2 = (x2 / 100) * W;
                            var py2 = (y2 / 100) * H;

                            var dx = px2 - px1;
                            var dy = py2 - py1;
                            var dist = Math.hypot(dx, dy) || 1;
                            var ux = dx / dist;
                            var uy = dy / dist;
                            var nx = -uy;
                            var ny = ux;

                            var tickLen = isSelected ? 12 : 9;

                            // 1. Witness Line 1
                            var lineW1 = document.createElementNS("http://www.w3.org/2000/svg", "line");
                            lineW1.setAttribute("x1", px1 - nx * tickLen);
                            lineW1.setAttribute("y1", py1 - ny * tickLen);
                            lineW1.setAttribute("x2", px1 + nx * (tickLen + 3));
                            lineW1.setAttribute("y2", py1 + ny * (tickLen + 3));
                            lineW1.setAttribute("stroke", strokeCol);
                            lineW1.setAttribute("stroke-width", strokeWidth);
                            lineW1.setAttribute("stroke-linecap", "round");
                            svg.appendChild(lineW1);

                            // 2. Witness Line 2
                            var lineW2 = document.createElementNS("http://www.w3.org/2000/svg", "line");
                            lineW2.setAttribute("x1", px2 - nx * tickLen);
                            lineW2.setAttribute("y1", py2 - ny * tickLen);
                            lineW2.setAttribute("x2", px2 + nx * (tickLen + 3));
                            lineW2.setAttribute("y2", py2 + ny * (tickLen + 3));
                            lineW2.setAttribute("stroke", strokeCol);
                            lineW2.setAttribute("stroke-width", strokeWidth);
                            lineW2.setAttribute("stroke-linecap", "round");
                            svg.appendChild(lineW2);

                            // 3. Connecting Dimension Line
                            var mainLine = document.createElementNS("http://www.w3.org/2000/svg", "line");
                            mainLine.setAttribute("x1", px1);
                            mainLine.setAttribute("y1", py1);
                            mainLine.setAttribute("x2", px2);
                            mainLine.setAttribute("y2", py2);
                            mainLine.setAttribute("stroke", strokeCol);
                            mainLine.setAttribute("stroke-width", strokeWidth);
                            svg.appendChild(mainLine);

                            // 4. Arrowheads at Both Ends
                            var poly1 = document.createElementNS("http://www.w3.org/2000/svg", "polygon");
                            poly1.setAttribute("points", px1 + "," + py1 + " " + (px1 + ux * 8 + nx * 2.5) + "," + (py1 + uy * 8 + ny * 2.5) + " " + (px1 + ux * 8 - nx * 2.5) + "," + (py1 + uy * 8 - ny * 2.5));
                            poly1.setAttribute("fill", strokeCol);
                            svg.appendChild(poly1);

                            var poly2 = document.createElementNS("http://www.w3.org/2000/svg", "polygon");
                            poly2.setAttribute("points", px2 + "," + py2 + " " + (px2 - ux * 8 + nx * 2.5) + "," + (py2 - uy * 8 + ny * 2.5) + " " + (px2 - ux * 8 - nx * 2.5) + "," + (py2 - uy * 8 - ny * 2.5));
                            poly2.setAttribute("fill", strokeCol);
                            svg.appendChild(poly2);

                            // 5. SVG Engineering Text (aligned with dimension line)
                            var midLinX = (px1 + px2) / 2;
                            var midLinY = (py1 + py2) / 2;
                            var angLin = Math.atan2(dy, dx) * 180 / Math.PI;
                            if (angLin > 90) angLin -= 180; else if (angLin < -90) angLin += 180;
                            var tLinX = midLinX + nx * 7;
                            var tLinY = midLinY + ny * 7;

                            var txtLin = document.createElementNS("http://www.w3.org/2000/svg", "text");
                            txtLin.setAttribute("x", tLinX);
                            txtLin.setAttribute("y", tLinY);
                            txtLin.setAttribute("transform", "rotate(" + angLin + ", " + tLinX + ", " + tLinY + ")");
                            txtLin.setAttribute("fill", CAD_TEXT_RED);
                            txtLin.setAttribute("font-family", "Consolas, 'Roboto Mono', 'SF Mono', monospace");
                            txtLin.setAttribute("font-size", isSelected ? "12" : "11");
                            txtLin.setAttribute("font-weight", "700");
                            txtLin.setAttribute("text-anchor", "middle");
                            txtLin.setAttribute("dominant-baseline", "central");
                            txtLin.style.paintOrder = "stroke fill";
                            txtLin.style.stroke = "#ffffff";
                            txtLin.style.strokeWidth = "2.5px";
                            txtLin.style.strokeLinejoin = "round";
                            txtLin.style.cursor = "pointer";
                            txtLin.style.pointerEvents = "auto";
                            txtLin.textContent = targetVal + " mm";
                            txtLin.addEventListener("click", function(e) {
                                e.stopPropagation();
                                if (isModal) selectMmInplace(idx); else selectGenStep(idx);
                            });
                            svg.appendChild(txtLin);

                            // Precision AutoCAD Osnap Snap Grips
                            var h1 = null;
                            if (genDrag && genDrag.idx === idx && genDrag.pointNum === 1 && genDrag.handle && layer.contains(genDrag.handle)) {
                                h1 = genDrag.handle;
                                h1.style.left = clampPct(x1) + "%";
                                h1.style.top = clampPct(y1) + "%";
                            } else {
                                h1 = buildCadSnapNode(idx, 1, x1, y1, isSelected, isModal, strokeCol);
                                layer.appendChild(h1);
                                attachVernierHandleDrag(h1, idx, 1, isModal);
                            }

                            var h2 = null;
                            if (genDrag && genDrag.idx === idx && genDrag.pointNum === 2 && genDrag.handle && layer.contains(genDrag.handle)) {
                                h2 = genDrag.handle;
                                h2.style.left = clampPct(x2) + "%";
                                h2.style.top = clampPct(y2) + "%";
                            } else {
                                h2 = buildCadSnapNode(idx, 2, x2, y2, isSelected, isModal, strokeCol);
                                layer.appendChild(h2);
                                attachVernierHandleDrag(h2, idx, 2, isModal);
                            }

                        } else if (x1 != null && y1 != null) {
                            var h1Pending = null;
                            if (genDrag && genDrag.idx === idx && genDrag.pointNum === 1 && genDrag.handle && layer.contains(genDrag.handle)) {
                                h1Pending = genDrag.handle;
                                h1Pending.style.left = clampPct(x1) + "%";
                                h1Pending.style.top = clampPct(y1) + "%";
                            } else {
                                h1Pending = buildCadSnapNode(idx, 1, x1, y1, true, isModal, CAD_GREEN);
                                h1Pending.classList.add("cad-pulse-node");
                                h1Pending.title = stepName + " (Point 1 set, click image to set Point 2)";
                                layer.appendChild(h1Pending);
                                attachVernierHandleDrag(h1Pending, idx, 1, isModal);
                            }
                        }
                    }
                }
            });
        }

        function renderGenMarkers() {
            if (!isVernierMode() && !isMixedMode()) {
                if (!genMarkerLayer) return;
                syncGenMarkerLayer();
                genMarkerLayer.querySelectorAll(".gen-marker-dot, .cad-snap-node, .cad-dim-badge, .gen-marker-svg").forEach(function (d) { d.remove(); });
                var img = genComponentImage;
                if (!img || !img.src || !genImagePath.value) return;

                var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : [];
                rows.forEach(function (tr, idx) {
                    var inputX = tr.querySelector(".step-marker-x");
                    var inputY = tr.querySelector(".step-marker-y");
                    if (inputX && inputY && inputX.value && inputY.value && !isNaN(parseFloat(inputX.value)) && !isNaN(parseFloat(inputY.value))) {
                        var x = clampPct(parseFloat(inputX.value));
                        var y = clampPct(parseFloat(inputY.value));
                        var dot = buildGenMarkerDot(idx, getRowMarkerSize(tr));
                        dot.style.left = x + "%";
                        dot.style.top = y + "%";
                        dot.textContent = idx + 1;
                        genMarkerLayer.appendChild(dot);
                        attachGenMarkerDot(dot, idx);
                    }
                });
                return;
            }
            renderMarkerSurface(false);
        }

        // Unified canvas click handler for placing markers
        function handleCanvasClick(x, y, isModal) {
            var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : [];
            if (!rows.length) return;

            if (currentSelectedStep < 0 || currentSelectedStep >= rows.length) {
                currentSelectedStep = 0;
            }

            var targetRow = rows[currentSelectedStep];
            if (!targetRow) return;

            var isVernier = isStepVernier(targetRow);

            if (!isVernier) {
                updateRowMarker(currentSelectedStep, x, y, "", "");
                renderGenMarkers();
                if (isModal) renderMmMarkers();

                for (var i = 0; i < rows.length; i++) {
                    if (!isRowMarkerPlaced(rows[i])) {
                        if (isModal) selectMmInplace(i); else selectGenStep(i);
                        break;
                    }
                }
            } else {
                var dimType = getStepDimType("", targetRow);
                if (dimType === "radius") {
                    updateRowMarker(currentSelectedStep, x, y, "", "");
                    updateStepInstructionPrompt(currentSelectedStep);
                    renderGenMarkers();
                    if (isModal) renderMmMarkers();

                    autoAdvanceToNextUnplacedStep(isModal);
                    return;
                }

                var inpX1 = targetRow.querySelector(".step-marker-x");
                var inpY1 = targetRow.querySelector(".step-marker-y");
                var inpX2 = targetRow.querySelector(".step-marker-x2");
                var inpY2 = targetRow.querySelector(".step-marker-y2");

                var hasP1 = inpX1 && inpY1 && inpX1.value && inpY1.value;
                var hasP2 = inpX2 && inpY2 && inpX2.value && inpY2.value;

                if (!hasP1 || (hasP1 && hasP2)) {
                    updateRowMarker(currentSelectedStep, x, y, "", "");
                    updateStepInstructionPrompt(currentSelectedStep);
                    renderGenMarkers();
                    if (isModal) renderMmMarkers();
                } else if (hasP1 && !hasP2) {
                    updateRowMarker(currentSelectedStep, undefined, undefined, x, y);
                    updateStepInstructionPrompt(currentSelectedStep);
                    renderGenMarkers();
                    if (isModal) renderMmMarkers();

                    autoAdvanceToNextUnplacedStep(isModal);
                }
            }
        }

        // Click on image container to place marker
        if (genImageContainer) {
            genImageContainer.addEventListener("click", function (event) {
                if (isTemplateViewOnly) return;
                if (genSuppressClick) { genSuppressClick = false; return; }
                var img = genComponentImage;
                if (!img || !img.src || !genImagePath.value) return;

                var rect = img.getBoundingClientRect();
                if (rect.width === 0 || rect.height === 0) return;

                var clientX = event.clientX;
                var clientY = event.clientY;
                if (clientX < rect.left || clientX > rect.right || clientY < rect.top || clientY > rect.bottom) {
                    return;
                }

                var x = ((clientX - rect.left) / rect.width * 100).toFixed(3);
                var y = ((clientY - rect.top) / rect.height * 100).toFixed(3);

                var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : [];
                if (!rows.length) return;

                if (!isVernierMode() && !isMixedMode()) {
                    if (currentSelectedStep < 0 || currentSelectedStep >= rows.length) {
                        currentSelectedStep = 0;
                    }

                    var targetRow = rows[currentSelectedStep];
                    if (!targetRow) return;

                    var inputX = targetRow.querySelector(".step-marker-x");
                    var inputY = targetRow.querySelector(".step-marker-y");
                    if (inputX) inputX.value = x;
                    if (inputY) inputY.value = y;
                    var sizeInput = targetRow.querySelector(".step-marker-size");
                    if (sizeInput) sizeInput.value = currentMarkerSize;

                    var pinIcon = targetRow.querySelector(".btn-marker-pin i");
                    if (pinIcon) {
                        pinIcon.className = "bi bi-geo-alt-fill text-success";
                    }
                    targetRow.classList.remove("table-danger");

                    renderGenMarkers();

                    // Auto-advance to next step if unplaced
                    for (var i = 0; i < rows.length; i++) {
                        var rX = rows[i].querySelector(".step-marker-x");
                        if (!rX || !rX.value) {
                            selectGenStep(i);
                            break;
                        }
                    }
                    return;
                }

                // Vernier mode near-miss selection: clicking close to an existing dimension selects it
                for (var vn = 0; vn < rows.length; vn++) {
                    var vx1 = rows[vn].querySelector(".step-marker-x");
                    var vy1 = rows[vn].querySelector(".step-marker-y");
                    var vx2 = rows[vn].querySelector(".step-marker-x2");
                    var vy2 = rows[vn].querySelector(".step-marker-y2");
                    if (!vx1 || !vy1 || !vx1.value || !vy1.value) continue;
                    var vpx1 = rect.left + parseFloat(vx1.value) / 100 * rect.width;
                    var vpy1 = rect.top + parseFloat(vy1.value) / 100 * rect.height;
                    if (Math.hypot(clientX - vpx1, clientY - vpy1) <= 18 || Math.hypot(clientX - vpx1, clientY - (vpy1 - 13)) <= 18) {
                        selectGenStep(vn);
                        return;
                    }
                    if (vx2 && vy2 && vx2.value && vy2.value) {
                        var vpx2 = rect.left + parseFloat(vx2.value) / 100 * rect.width;
                        var vpy2 = rect.top + parseFloat(vy2.value) / 100 * rect.height;
                        if (Math.hypot(clientX - vpx2, clientY - vpy2) <= 18 || Math.hypot(clientX - (vpx1 + vpx2)/2, clientY - (vpy1 + vpy2)/2) <= 18) {
                            selectGenStep(vn);
                            return;
                        }
                    }
                }

                handleCanvasClick(x, y, false);
            });
        }

        // -------------------------------------------------------------
        // Large-Scale Marker Placement Modal
        // -------------------------------------------------------------
        var mmModalEl = document.getElementById("marker-large-modal");
        var mmModal = mmModalEl && window.bootstrap ? new window.bootstrap.Modal(mmModalEl) : null;
        var mmImgWrap = document.getElementById("mm-img-wrap");
        var mmImage = document.getElementById("mm-component-image");
        var mmLayer = document.getElementById("mm-marker-layer");
        var mmSizeSlider = document.getElementById("mm-marker-size-slider");
        var mmSizeValue = document.getElementById("mm-marker-size-value");
        var mmSelLabel = document.getElementById("mm-selected-step-label");
        var mmDrag = null; // active modal drag: { idx, dot }
        var mmSuppressClick = false; // swallow the canvas click after a dot rebuild
        var mmBtnLarge = document.getElementById("btn-gen-large-view");

        function mmRow(idx) {
            var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : [];
            return rows[idx] || null;
        }

        function mmStatus(idx) {
            var row = mmRow(idx);
            if (!row) return { x: null, y: null, x2: null, y2: null, placed: false };
            var ix = row.querySelector(".step-marker-x");
            var iy = row.querySelector(".step-marker-y");
            var ix2 = row.querySelector(".step-marker-x2");
            var iy2 = row.querySelector(".step-marker-y2");
            var x = ix && ix.value ? parseFloat(ix.value) : null;
            var y = iy && iy.value ? parseFloat(iy.value) : null;
            var x2 = ix2 && ix2.value ? parseFloat(ix2.value) : null;
            var y2 = iy2 && iy2.value ? parseFloat(iy2.value) : null;
            return { x: x, y: y, x2: x2, y2: y2, placed: isRowMarkerPlaced(row) };
        }

        function selectMmInplace(idx, skipMarkerRender) {
            currentSelectedStep = idx;
            var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : [];
            rows.forEach(function (r, i2) {
                r.classList.toggle("table-primary", i2 === idx);
            });
            updateStepInstructionPrompt(idx);
            var row = rows[idx];
            var st = mmStatus(idx);
            syncMarkerSizeControls(st.placed ? getRowMarkerSize(row) : currentMarkerSize);
            if (!skipMarkerRender) {
                renderMmMarkers();
                renderGenMarkers();
            }
        }

        function syncMmMarkerLayer() {
            if (!mmLayer || !mmImage || !mmImgWrap) return;
            var rect = mmImage.getBoundingClientRect();
            if (!rect.width || !rect.height) return;
            var wrapRect = mmImgWrap.getBoundingClientRect();
            mmLayer.style.position = "absolute";
            mmLayer.style.left = (rect.left - wrapRect.left) + "px";
            mmLayer.style.top = (rect.top - wrapRect.top) + "px";
            mmLayer.style.width = rect.width + "px";
            mmLayer.style.height = rect.height + "px";
        }

        function renderMmMarkers() {
            if (!isVernierMode() && !isMixedMode()) {
                if (!mmLayer) return;
                syncMmMarkerLayer();
                mmLayer.querySelectorAll(".gen-marker-dot, .cad-snap-node, .cad-dim-badge, .gen-marker-svg").forEach(function (d) { d.remove(); });
                if (!genImagePath.value || !mmImage || !mmImage.src) return;

                var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : [];
                rows.forEach(function (tr, idx) {
                    var inputX = tr.querySelector(".step-marker-x");
                    var inputY = tr.querySelector(".step-marker-y");
                    if (inputX && inputY && inputX.value && inputY.value && !isNaN(parseFloat(inputX.value)) && !isNaN(parseFloat(inputY.value))) {
                        var dot = buildGenMarkerDot(idx, getRowMarkerSize(tr));
                        dot.style.left = clampPct(parseFloat(inputX.value)) + "%";
                        dot.style.top = clampPct(parseFloat(inputY.value)) + "%";
                        dot.textContent = idx + 1;
                        mmLayer.appendChild(dot);
                        attachMmDot(dot, idx);
                    }
                });
                return;
            }
            renderMarkerSurface(true);
        }

        function attachMmDot(dot, idx) {
            dot.addEventListener("pointerdown", function (e) {
                if (!genImagePath.value) return;
                if (e.button !== undefined && e.button !== 0) return;
                e.preventDefault();
                e.stopPropagation();
                selectMmInplace(idx);
                mmDrag = { idx: idx, dot: dot };
                dot.style.cursor = "grabbing";
                try { dot.setPointerCapture(e.pointerId); } catch (err) { }
            });
            dot.addEventListener("pointermove", function (e) {
                if (!mmDrag || mmDrag.dot !== dot) return;
                var rect = mmImage.getBoundingClientRect();
                if (!rect.width || !rect.height) return;
                var x = clampPct((e.clientX - rect.left) / rect.width * 100);
                var y = clampPct((e.clientY - rect.top) / rect.height * 100);
                updateRowMarker(idx, x.toFixed(3), y.toFixed(3));
                dot.style.left = x + "%";
                dot.style.top = y + "%";
            });
            var finishDrag = function () {
                if (!mmDrag || mmDrag.dot !== dot) return;
                mmDrag = null;
                dot.style.cursor = "grab";
                // Rebuilding the dots detaches the one under the cursor, so the
                // browser's follow-up click falls through to the canvas: without
                // this guard it would re-place the pin and auto-advance, which
                // stole the selection right before the user reached the size
                // slider. Swallow that one click.
                mmSuppressClick = true;
                setTimeout(function () { mmSuppressClick = false; }, 150);
                renderMmMarkers();
                renderGenMarkers();
            };
            dot.addEventListener("pointerup", finishDrag);
            dot.addEventListener("pointercancel", finishDrag);
            dot.addEventListener("click", function (e) {
                // pointerdown already selects; block the wrap's click-to-place
                e.stopPropagation();
            });
        }

        // Click inside the modal canvas places a pin/marker for the selected step.
        if (mmImgWrap) {
            mmImgWrap.addEventListener("click", function (event) {
                if (mmSuppressClick) { mmSuppressClick = false; return; }
                if (!genImagePath.value) return;
                var img = mmImage;
                if (!img || !img.src) return;
                var rect = img.getBoundingClientRect();
                if (!rect.width || !rect.height) return;
                if (event.clientX < rect.left || event.clientX > rect.right || event.clientY < rect.top || event.clientY > rect.bottom) return;
                if (!img.contains(event.target) && !mmLayer.contains(event.target)) return;

                var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : [];
                if (!rows.length) return;

                if (!isVernierMode() && !isMixedMode()) {
                    // Near-miss tolerance: a click that lands within a placed
                    // marker's radius selects that step instead of placing a new
                    // pin — tapping close to a dot must never hijack the selection.
                    for (var n = 0; n < rows.length; n++) {
                        var st = mmStatus(n);
                        if (!st.placed) continue;
                        var nx = rect.left + st.x / 100 * rect.width;
                        var ny = rect.top + st.y / 100 * rect.height;
                        var rad = Math.max(12, getRowMarkerSize(rows[n])) / 2 + 3;
                        var ddx = event.clientX - nx;
                        var ddy = event.clientY - ny;
                        if (ddx * ddx + ddy * ddy <= rad * rad) {
                            selectMmInplace(n);
                            renderMmMarkers();
                            renderGenMarkers();
                            return;
                        }
                    }

                    if (currentSelectedStep < 0 || currentSelectedStep >= rows.length) currentSelectedStep = 0;
                    var targetRow = rows[currentSelectedStep];
                    if (!targetRow) return;

                    var x = ((event.clientX - rect.left) / rect.width * 100).toFixed(3);
                    var y = ((event.clientY - rect.top) / rect.height * 100).toFixed(3);
                    var inputX = targetRow.querySelector(".step-marker-x");
                    var inputY = targetRow.querySelector(".step-marker-y");
                    if (inputX) inputX.value = x;
                    if (inputY) inputY.value = y;
                    var sizeInput = targetRow.querySelector(".step-marker-size");
                    if (sizeInput) sizeInput.value = currentMarkerSize;
                    var pinIcon = targetRow.querySelector(".btn-marker-pin i");
                    if (pinIcon) pinIcon.className = "bi bi-geo-alt-fill text-success";
                    targetRow.classList.remove("table-danger");

                    renderMmMarkers();
                    renderGenMarkers();

                    // Auto-advance: pick the first unplaced step, or stay on this one
                    for (var i = 0; i < rows.length; i++) {
                        var rX = rows[i].querySelector(".step-marker-x");
                        if (!rX || !rX.value) {
                            selectMmInplace(i);
                            break;
                        }
                    }
                    return;
                }

                // Vernier mode near-miss selection inside modal:
                for (var vn2 = 0; vn2 < rows.length; vn2++) {
                    var mx1 = rows[vn2].querySelector(".step-marker-x");
                    var my1 = rows[vn2].querySelector(".step-marker-y");
                    var mx2 = rows[vn2].querySelector(".step-marker-x2");
                    var my2 = rows[vn2].querySelector(".step-marker-y2");
                    if (!mx1 || !my1 || !mx1.value || !my1.value) continue;
                    var mpx1 = rect.left + parseFloat(mx1.value) / 100 * rect.width;
                    var mpy1 = rect.top + parseFloat(my1.value) / 100 * rect.height;
                    if (Math.hypot(event.clientX - mpx1, event.clientY - mpy1) <= 18 || Math.hypot(event.clientX - mpx1, event.clientY - (mpy1 - 13)) <= 18) {
                        selectMmInplace(vn2);
                        renderMmMarkers();
                        renderGenMarkers();
                        return;
                    }
                    if (mx2 && my2 && mx2.value && my2.value) {
                        var mpx2 = rect.left + parseFloat(mx2.value) / 100 * rect.width;
                        var mpy2 = rect.top + parseFloat(my2.value) / 100 * rect.height;
                        if (Math.hypot(event.clientX - mpx2, event.clientY - mpy2) <= 18 || Math.hypot(event.clientX - (mpx1 + mpx2)/2, event.clientY - (mpy1 + mpy2)/2) <= 18) {
                            selectMmInplace(vn2);
                            renderMmMarkers();
                            renderGenMarkers();
                            return;
                        }
                    }
                }

                var x = ((event.clientX - rect.left) / rect.width * 100).toFixed(3);
                var y = ((event.clientY - rect.top) / rect.height * 100).toFixed(3);

                handleCanvasClick(x, y, true);
            });
        }

        // Wire Vernier Measure Option buttons (Line Distance vs Radius)
        document.querySelectorAll("#vernier-type-btn-group button, #mm-vernier-toolbar button").forEach(function (btn) {
            btn.addEventListener("click", function (e) {
                e.stopPropagation();
                var t = this.dataset.vernierType || this.dataset.type;
                if (t) setVernierStepType(t);
            });
        });

        if (mmImage) {
            mmImage.addEventListener("load", function () {
                syncMmMarkerLayer();
                renderMmMarkers();
            });
        }

        window.addEventListener("resize", function () {
            if (mmModalEl && mmModalEl.classList.contains("show")) syncMmMarkerLayer();
        });

        document.querySelectorAll(".mm-size-preset").forEach(function (btn) {
            btn.addEventListener("click", function () { setMarkerSize(this.dataset.size); });
        });
        if (mmSizeSlider) {
            mmSizeSlider.addEventListener("input", function () { setMarkerSize(this.value); });
        }

        function mmRefreshSelLabel() {
            if (!mmSelLabel) return;
            var row = mmRow(currentSelectedStep);
            var lbl = row ? row.querySelector(".step-label") : null;
            var isVernier = isVernierMode();
            var stepType = isVernier ? getStepDimType(lbl ? lbl.value : "", row) : "torque";
            var defaultPrefix = isVernier ? (stepType === "radius" ? "Radius " : "Distance ") : "Bolt ";
            var text = (lbl && lbl.value.trim()) ? lbl.value.trim() : (defaultPrefix + (currentSelectedStep + 1));
            if (isVernier) {
                text = text.replace(/^bolt\s*/i, stepType === "radius" ? "Radius " : "Distance ");
                if (lbl && /^bolt\s*\d+$/i.test(lbl.value.trim())) {
                    lbl.value = text;
                }
            }
            mmSelLabel.textContent = text;
        }

        function openMarkerModal(idx) {
            if (!genImagePath.value) {
                // Reuse the generator's error notice for a step-scoped message
                var err = document.getElementById("gen-upload-error");
                if (err) {
                    err.textContent = "Upload a component image before marking steps.";
                    err.classList.remove("d-none");
                    setTimeout(function () { err.classList.add("d-none"); }, 4000);
                }
                return;
            }
            if (!mmModal || !mmImage || !mmLayer) return;
            var target = Math.max(0, Math.min(idx, (genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row").length : 0) - 1));
            selectGenStep(isNaN(target) ? 0 : target);
            mmRefreshSelLabel();
            mmImage.src = genComponentImage && genComponentImage.src ? genComponentImage.src : ("/" + genImagePath.value);
            renderMmMarkers();
            mmModal.show();
            setTimeout(function () {
                syncMmMarkerLayer();
                renderMmMarkers();
            }, 60);
            setTimeout(function () {
                syncMmMarkerLayer();
                renderMmMarkers();
            }, 250);
        }

        if (mmBtnLarge) {
            mmBtnLarge.addEventListener("click", function (e) {
                e.stopPropagation();
                openMarkerModal(currentSelectedStep);
            });
        }

        if (mmModalEl) {
            mmModalEl.addEventListener("shown.bs.modal", function () {
                syncMmMarkerLayer();
                renderMmMarkers();
            });
            mmModalEl.addEventListener("hidden.bs.modal", function () {
                renderGenMarkers();
            });
        }

        // -------------------------------------------------------------
        // Cropper.js Integration for Component Image (shared editor)
        // -------------------------------------------------------------
        var _cropper = null;
        var _blobUrl = null;
        var _cropZoomLevel = 1.0;
        var _flipH = false;
        var _flipV = false;
        var cropTarget = "gen"; // "gen": template component image, "product": product image
        var btnUseOriginal = document.getElementById("btn-use-original");

        var cropModalEl = document.getElementById("cropModal");
        var cropModal = cropModalEl && window.bootstrap ? new window.bootstrap.Modal(cropModalEl) : null;
        var cropImg = document.getElementById("crop-img");
        var btnApplyCrop = document.getElementById("btn-apply-crop");

        function openCropModal(src, target) {
            cropTarget = target || "gen";
            var subject = document.getElementById("crop-modal-subject");
            if (subject) subject.textContent = cropTarget === "product" ? "Product" : "Component";

            _flipH = false;
            _flipV = false;
            _cropZoomLevel = 1.0;

            var zoomSlider = document.getElementById("crop-zoom-slider");
            if (zoomSlider) zoomSlider.value = 1;
            var zoomPct = document.getElementById("crop-zoom-pct");
            if (zoomPct) zoomPct.textContent = "100%";

            var flipHBtn = document.getElementById("btn-crop-fliph");
            if (flipHBtn) flipHBtn.classList.remove("active");
            var flipVBtn = document.getElementById("btn-crop-flipv");
            if (flipVBtn) flipVBtn.classList.remove("active");

            setCropAspectActive(NaN);

            if (_cropper) {
                _cropper.destroy();
                _cropper = null;
            }

            if (cropImg) {
                cropImg.style.opacity = "0";
                cropImg.src = src;
            }

            if (cropModal) cropModal.show();
        }

        if (cropModalEl) {
            cropModalEl.addEventListener("shown.bs.modal", function () {
                if (!cropImg) return;
                cropImg.style.opacity = "1";
                if (_cropper) { _cropper.destroy(); _cropper = null; }

                if (typeof Cropper !== "undefined") {
                    _cropper = new Cropper(cropImg, {
                        viewMode: 1,
                        dragMode: "move",
                        autoCropArea: 0.88,
                        responsive: true,
                        restore: false,
                        guides: true,
                        center: true,
                        highlight: false,
                        cropBoxMovable: true,
                        cropBoxResizable: true,
                        toggleDragModeOnDblclick: true,
                        ready: function () {
                            _cropZoomLevel = 1.0;
                            var zs = document.getElementById("crop-zoom-slider");
                            if (zs) zs.value = 1;
                            var zp = document.getElementById("crop-zoom-pct");
                            if (zp) zp.textContent = "100%";
                        },
                        zoom: function (e) {
                            _cropZoomLevel = e.detail.ratio;
                            var pct = Math.round(_cropZoomLevel * 100);
                            var zp = document.getElementById("crop-zoom-pct");
                            if (zp) zp.textContent = pct + "%";
                            var sl = document.getElementById("crop-zoom-slider");
                            if (sl) {
                                sl.value = Math.min(Math.max(_cropZoomLevel, parseFloat(sl.min)), parseFloat(sl.max));
                            }
                        },
                        crop: function (e) {
                            var d = e.detail;
                            var cw = document.getElementById("info-cw");
                            var ch = document.getElementById("info-ch");
                            if (cw) cw.textContent = Math.round(d.width);
                            if (ch) ch.textContent = Math.round(d.height);
                            if (_cropper) {
                                var imgData = _cropper.getImageData();
                                var ratio = imgData.naturalWidth / imgData.width;
                                var ow = document.getElementById("info-ow");
                                var oh = document.getElementById("info-oh");
                                if (ow) ow.textContent = Math.round(d.width * ratio);
                                if (oh) oh.textContent = Math.round(d.height * ratio);
                            }
                        }
                    });
                }
            });

            cropModalEl.addEventListener("hidden.bs.modal", function () {
                if (_blobUrl) { URL.revokeObjectURL(_blobUrl); _blobUrl = null; }
                if (_cropper) { _cropper.destroy(); _cropper = null; }
                if (cropImg) cropImg.src = "";
                if (cropTarget === "product") {
                    if (!productCropConsumed) productQueue = []; // bailed on this file: abandon the rest of the batch
                    productCropConsumed = false;
                    productCropFile = null;
                    productHideSettled = true;
                    maybeAdvanceProductQueue();
                }
                cropTarget = "gen";
            });
        }

        function setCropAspectActive(ratio) {
            var map = { "ar-free": NaN, "ar-11": 1, "ar-43": 4/3, "ar-169": 16/9, "ar-32": 1.5 };
            Object.keys(map).forEach(function (id) {
                var el = document.getElementById(id);
                if (!el) return;
                var match = isNaN(ratio) ? isNaN(map[id]) : Math.abs(ratio - map[id]) < 0.001;
                el.classList.toggle("active", match);
            });
        }

        // Crop Toolbar listeners
        document.querySelectorAll(".crop-ar-btn").forEach(function (btn) {
            btn.addEventListener("click", function () {
                var r = parseFloat(this.dataset.ratio);
                if (_cropper) _cropper.setAspectRatio(isNaN(r) ? NaN : r);
                setCropAspectActive(r);
            });
        });

        var btnRotLeft = document.getElementById("btn-crop-rot-left");
        if (btnRotLeft) {
            btnRotLeft.addEventListener("click", function () {
                if (_cropper) _cropper.rotate(-90);
            });
        }

        var btnRotRight = document.getElementById("btn-crop-rot-right");
        if (btnRotRight) {
            btnRotRight.addEventListener("click", function () {
                if (_cropper) _cropper.rotate(90);
            });
        }

        var btnFlipH = document.getElementById("btn-crop-fliph");
        if (btnFlipH) {
            btnFlipH.addEventListener("click", function () {
                if (!_cropper) return;
                _flipH = !_flipH;
                _cropper.scaleX(_flipH ? -1 : 1);
                this.classList.toggle("active", _flipH);
            });
        }

        var btnFlipV = document.getElementById("btn-crop-flipv");
        if (btnFlipV) {
            btnFlipV.addEventListener("click", function () {
                if (!_cropper) return;
                _flipV = !_flipV;
                _cropper.scaleY(_flipV ? -1 : 1);
                this.classList.toggle("active", _flipV);
            });
        }

        var btnZoomIn = document.getElementById("btn-crop-zoom-in");
        if (btnZoomIn) {
            btnZoomIn.addEventListener("click", function () {
                if (_cropper) _cropper.zoom(0.1);
            });
        }

        var btnZoomOut = document.getElementById("btn-crop-zoom-out");
        if (btnZoomOut) {
            btnZoomOut.addEventListener("click", function () {
                if (_cropper) _cropper.zoom(-0.1);
            });
        }

        var cropZoomSlider = document.getElementById("crop-zoom-slider");
        if (cropZoomSlider) {
            cropZoomSlider.addEventListener("input", function () {
                if (_cropper) _cropper.zoomTo(parseFloat(this.value));
            });
        }

        var btnCropReset = document.getElementById("btn-crop-reset");
        if (btnCropReset) {
            btnCropReset.addEventListener("click", function () {
                if (!_cropper) return;
                _cropper.reset();
                _flipH = false;
                _flipV = false;
                _cropZoomLevel = 1.0;
                if (cropZoomSlider) cropZoomSlider.value = 1;
                var zp = document.getElementById("crop-zoom-pct");
                if (zp) zp.textContent = "100%";
                if (btnFlipH) btnFlipH.classList.remove("active");
                if (btnFlipV) btnFlipV.classList.remove("active");
                setCropAspectActive(NaN);
            });
        }

        // Apply crop and upload to /Template/UploadImage
        if (btnApplyCrop) {
            btnApplyCrop.addEventListener("click", function () {
                if (!_cropper) return;
                btnApplyCrop.disabled = true;
                btnApplyCrop.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Processing…';

                var qualityEl = document.getElementById("crop-quality");
                var quality = qualityEl ? parseFloat(qualityEl.value) : 0.95;

                var canvas = _cropper.getCroppedCanvas({
                    maxWidth: 4096, maxHeight: 4096,
                    imageSmoothingEnabled: true,
                    imageSmoothingQuality: "high"
                });

                if (!canvas) {
                    btnApplyCrop.disabled = false;
                    btnApplyCrop.innerHTML = '<i class="bi bi-check2 me-1"></i>Crop & Use Image';
                    alert("Could not generate cropped canvas.");
                    return;
                }

                canvas.toBlob(function (blob) {
                    if (!blob) {
                        btnApplyCrop.disabled = false;
                        btnApplyCrop.innerHTML = '<i class="bi bi-check2 me-1"></i>Crop & Use Image';
                        alert("Blob conversion failed.");
                        return;
                    }

                    if (cropModal) cropModal.hide();
                    btnApplyCrop.disabled = false;
                    btnApplyCrop.innerHTML = '<i class="bi bi-check2 me-1"></i>Crop & Use Image';

                    if (cropTarget === "product") {
                        uploadProductBlob(blob, "product.jpg");
                        return;
                    }
                    uploadTemplateBlob(blob);
                }, "image/jpeg", quality);
            });
        }

        function uploadTemplateBlob(blob) {
            if (genUploadError) genUploadError.classList.add("d-none");
            if (genUploadStatus) genUploadStatus.classList.remove("d-none");

            var fd = new FormData();
            fd.append("file", blob, "component.jpg");
            var antiforgery = form.querySelector('[name="__RequestVerificationToken"]');

            fetch("/Template/UploadImage", {
                method: "POST",
                headers: antiforgery ? { "RequestVerificationToken": antiforgery.value } : {},
                body: fd
            })
            .then(function (r) {
                return r.json().then(function (data) { return { ok: r.ok, data: data }; });
            })
            .then(function (res) {
                if (genUploadStatus) genUploadStatus.classList.add("d-none");
                if (!res.ok) throw new Error(res.data.error || "Upload failed.");

                // Set image path and display preview
                genImagePath.value = res.data.path;
                var displayUrl = res.data.url + "?t=" + Date.now();

                if (genImageRow) genImageRow.classList.remove("d-none");
                if (genImagePlaceholder) genImagePlaceholder.classList.add("d-none");

                // Update marker surface
                if (genComponentImage) {
                    genComponentImage.src = displayUrl;
                }
                if (genMarkerPanel) genMarkerPanel.classList.remove("d-none");

                // Update action buttons
                if (genUploadBtnText) genUploadBtnText.textContent = "Replace Image";
                if (btnGenEditImage) btnGenEditImage.classList.remove("d-none");
                if (btnGenRemoveImage) btnGenRemoveImage.classList.remove("d-none");

                renderGenMarkers();
            })
            .catch(function (err) {
                if (genUploadStatus) genUploadStatus.classList.add("d-none");
                if (genUploadError) {
                    genUploadError.textContent = err.message;
                    genUploadError.classList.remove("d-none");
                }
            });
        }

        // File input selection triggers crop modal
        if (genFileInput) {
            genFileInput.addEventListener("change", function () {
                var file = this.files[0];
                if (!file) return;
                this.value = ""; // allow re-selection
                var maxBytes = 5 * 1024 * 1024;
                var genErrEl = document.getElementById("gen-upload-error");
                if (file.size > maxBytes) {
                    if (genErrEl) { genErrEl.textContent = "File too large (" + (file.size / 1024 / 1024).toFixed(1) + " MB). Maximum allowed size is 5 MB."; genErrEl.classList.remove("d-none"); }
                    return;
                }
                if (genErrEl) genErrEl.classList.add("d-none");
                if (_blobUrl) URL.revokeObjectURL(_blobUrl);
                _blobUrl = URL.createObjectURL(file);
                openCropModal(_blobUrl);
            });
        }

        // Use the picked file untouched (product flow only).
        if (btnUseOriginal) {
            btnUseOriginal.addEventListener("click", function () {
                var isProduct = cropTarget === "product";
                var file = productCropFile;
                if (cropModal) cropModal.hide();
                if (isProduct && file) {
                    btnUseOriginal.disabled = true;
                    uploadProductBlob(file, file.name);
                    btnUseOriginal.disabled = false;
                }
            });
        }

        // Edit current image button
        if (btnGenEditImage) {
            btnGenEditImage.addEventListener("click", function () {
                if (genImagePath.value) {
                    var src = genComponentImage.src || ("/" + genImagePath.value);
                    openCropModal(src);
                }
            });
        }

        // Remove image button
        if (btnGenRemoveImage) {
            btnGenRemoveImage.addEventListener("click", function () {
                genImagePath.value = "";
                if (genImageRow) genImageRow.classList.add("d-none");
                if (genImagePlaceholder) genImagePlaceholder.classList.remove("d-none");
                if (genMarkerPanel) genMarkerPanel.classList.add("d-none");
                if (genComponentImage) genComponentImage.src = "";
                if (genUploadBtnText) genUploadBtnText.textContent = "Choose Component Image";
                if (btnGenEditImage) btnGenEditImage.classList.add("d-none");
                if (btnGenRemoveImage) btnGenRemoveImage.classList.add("d-none");

                // Clear markers from rows
                var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : [];
                rows.forEach(function (r) {
                    var mx = r.querySelector(".step-marker-x");
                    var my = r.querySelector(".step-marker-y");
                    var mx2 = r.querySelector(".step-marker-x2");
                    var my2 = r.querySelector(".step-marker-y2");
                    var ms = r.querySelector(".step-marker-size");
                    if (mx) mx.value = "";
                    if (my) my.value = "";
                    if (mx2) mx2.value = "";
                    if (my2) my2.value = "";
                    if (ms) ms.value = "";
                    var pin = r.querySelector(".btn-marker-pin i");
                    if (pin) pin.className = "bi bi-geo-alt text-secondary opacity-50";
                    r.classList.add("table-danger");
                });
                renderGenMarkers();
            });
        }

        // -------------------------------------------------------------
        // Similarity Modal & Side-by-Side Comparison
        // -------------------------------------------------------------
        function renderModalMarkerLayer(containerId, steps, markerSize) {
            var layer = document.getElementById(containerId);
            if (!layer) return;
            var wrap = layer.parentElement;
            var img = wrap ? wrap.querySelector("img") : null;
            layer.innerHTML = "";

            // The wrap letterboxes the centered image; position the layer over the
            // exact img box or the pins drift with the empty side margins.
            if (!img || !img.src || !img.complete || !img.naturalWidth) return;
            markerSize = markerSize || 18;
            var rect = img.getBoundingClientRect();
            var wrapRect = wrap.getBoundingClientRect();
            layer.style.position = "absolute";
            layer.style.left = (rect.left - wrapRect.left) + "px";
            layer.style.top = (rect.top - wrapRect.top) + "px";
            layer.style.width = rect.width + "px";
            layer.style.height = rect.height + "px";

            (steps || []).forEach(function (s, idx) {
                if (s.markerX != null && s.markerY != null && !isNaN(parseFloat(s.markerX)) && !isNaN(parseFloat(s.markerY))) {
                    var sSize = parseInt(s.markerSize, 10);
                    var perStep = (!isNaN(sSize) && sSize > 0) ? Math.max(12, Math.min(56, sSize)) : markerSize;
                    var dot = document.createElement("div");
                    dot.style.position = "absolute";
                    dot.style.width = perStep + "px";
                    dot.style.height = perStep + "px";
                    dot.style.borderRadius = "50%";
                    dot.style.left = s.markerX + "%";
                    dot.style.top = s.markerY + "%";
                    dot.style.transform = "translate(-50%, -50%)";
                    dot.style.display = "flex";
                    dot.style.alignItems = "center";
                    dot.style.justifyContent = "center";
                    dot.style.fontSize = Math.max(8, Math.round(perStep * 0.45)) + "px";
                    dot.style.fontWeight = "700";
                    dot.style.color = "#fff";
                    dot.style.background = "#0d6efd";
                    dot.style.border = "2px solid #fff";
                    dot.style.boxShadow = "0 2px 4px rgba(0,0,0,0.4)";
                    dot.textContent = s.sequenceNo || (idx + 1);
                    layer.appendChild(dot);
                }
            });
        }

        function populateModalSideBySide(data) {
            // Existing match
            currentExistingMatch = data.existing;
            document.getElementById("modal-existing-name").textContent = data.existing.name;
            document.getElementById("modal-existing-code").textContent = data.existing.templateCode;
            document.getElementById("modal-existing-version").textContent = "v" + data.existing.version;
            document.getElementById("modal-existing-stepcount").textContent = data.existing.stepCount;

            // Existing image preview
            var existImgWrap = document.getElementById("modal-existing-img-wrap");
            var existImg = document.getElementById("modal-existing-img");
            if (data.existing.imagePath) {
                existImg.src = data.existing.imagePath.startsWith("/") ? data.existing.imagePath : ("/" + data.existing.imagePath);
                existImg.onload = function () {
                    renderModalMarkerLayer("modal-existing-markers-layer", data.existing.steps, 18);
                };
                existImgWrap.classList.remove("d-none");
                renderModalMarkerLayer("modal-existing-markers-layer", data.existing.steps, 18);
            } else {
                existImgWrap.classList.add("d-none");
            }

            var existTbody = document.getElementById("modal-existing-steps-tbody");
            existTbody.innerHTML = "";
            (data.existing.steps || []).forEach(function (s) {
                var tolDisplay = "±" + s.tolerancePlus + " " + (s.toleranceMode === "Percent" ? "%" : s.unit);
                var pinIcon = (s.markerX != null && s.markerY != null) ? '<i class="bi bi-geo-alt-fill text-success ms-1"></i>' : '';
                existTbody.innerHTML += '<tr><td class="fw-bold text-center">' + s.sequenceNo + '</td>' +
                    '<td>' + s.label + pinIcon + '</td>' +
                    '<td>' + s.target + ' ' + s.unit + '</td>' +
                    '<td>' + tolDisplay + '</td></tr>';
            });

            // New Specification
            document.getElementById("modal-new-name").textContent = data.newSpec.name;
            document.getElementById("modal-new-code").textContent = data.newSpec.generatedCode;
            document.getElementById("modal-new-stepcount").textContent = data.newSpec.stepCount;

            // New image preview
            var newImgWrap = document.getElementById("modal-new-img-wrap");
            var newImg = document.getElementById("modal-new-img");
            if (data.newSpec.imagePath) {
                newImg.src = data.newSpec.imagePath.startsWith("/") ? data.newSpec.imagePath : ("/" + data.newSpec.imagePath);
                newImg.onload = function () {
                    renderModalMarkerLayer("modal-new-markers-layer", data.newSpec.steps, 18);
                };
                newImgWrap.classList.remove("d-none");
                renderModalMarkerLayer("modal-new-markers-layer", data.newSpec.steps, 18);
            } else {
                newImgWrap.classList.add("d-none");
            }

            var newTbody = document.getElementById("modal-new-steps-tbody");
            newTbody.innerHTML = "";
            (data.newSpec.steps || []).forEach(function (s) {
                var tolDisplay = "±" + s.tolerancePlus + " " + (s.toleranceMode === "Percent" ? "%" : s.unit);
                var pinIcon = (s.markerX != null && s.markerY != null) ? '<i class="bi bi-geo-alt-fill text-success ms-1"></i>' : '';
                newTbody.innerHTML += '<tr><td class="fw-bold text-center">' + s.sequenceNo + '</td>' +
                    '<td>' + s.label + pinIcon + '</td>' +
                    '<td>' + s.target + ' ' + s.unit + '</td>' +
                    '<td>' + tolDisplay + '</td></tr>';
            });
        }

        // -------------------------------------------------------------
        // Catalogue Template Preview Modal (Inspect before add / move)
        // -------------------------------------------------------------
        var currentPreviewTpl = null;

        function openTemplatePreviewModal(templateId) {
            if (!templateId || templateId <= 0) return;

            var modalEl = document.getElementById("template-preview-modal");
            if (!modalEl || !window.bootstrap || !window.bootstrap.Modal) {
                addRow(templateId);
                return;
            }

            var modal = bootstrap.Modal.getInstance(modalEl) || new bootstrap.Modal(modalEl);

            // Reset modal UI to loading state
            var nameEl = document.getElementById("tpl-preview-name");
            var codeEl = document.getElementById("tpl-preview-code");
            var verEl = document.getElementById("tpl-preview-version");
            var modeEl = document.getElementById("tpl-preview-mode");
            var tolEl = document.getElementById("tpl-preview-tolmode");
            var countEl = document.getElementById("tpl-preview-stepcount");
            var descEl = document.getElementById("tpl-preview-desc");
            var tbody = document.getElementById("tpl-preview-steps-tbody");
            var imgWrap = document.getElementById("tpl-preview-img-wrap");
            var noImg = document.getElementById("tpl-preview-no-img");
            var btnAdd = document.getElementById("btn-preview-add-continue");
            var btnMove = document.getElementById("btn-preview-move-edit");

            if (nameEl) nameEl.textContent = "Loading template specifications...";
            if (codeEl) codeEl.textContent = "—";
            if (verEl) verEl.textContent = "v—";
            if (modeEl) modeEl.innerHTML = '<i class="bi bi-hourglass-split me-1"></i>Loading...';
            if (tolEl) tolEl.textContent = "—";
            if (countEl) countEl.textContent = "...";
            if (descEl) descEl.classList.add("d-none");
            if (tbody) tbody.innerHTML = '<tr><td colspan="5" class="text-center py-4 text-muted"><span class="spinner-border spinner-border-sm text-primary me-2"></span>Loading step specifications...</td></tr>';
            if (imgWrap) imgWrap.classList.add("d-none");
            if (noImg) noImg.classList.add("d-none");
            if (btnAdd) btnAdd.disabled = true;
            if (btnMove) btnMove.disabled = true;

            modal.show();

            fetch("/Product/GetTemplateDetails?templateId=" + encodeURIComponent(templateId))
                .then(function (r) {
                    if (!r.ok) throw new Error("Could not load template details.");
                    return r.json();
                })
                .then(function (tpl) {
                    currentPreviewTpl = tpl;
                    if (nameEl) nameEl.textContent = tpl.name || "Untitled Template";
                    if (codeEl) codeEl.textContent = tpl.templateCode || "—";
                    if (verEl) verEl.textContent = "v" + (tpl.version || 1);

                    var isVernier = (tpl.mode === "Vernier");
                    var unit = isVernier ? "mm" : "N·m";
                    if (modeEl) {
                        modeEl.innerHTML = isVernier
                            ? '<i class="bi bi-rulers me-1"></i>Distance (mm) — Vernier'
                            : '<i class="bi bi-wrench me-1"></i>Torque (N·m) — Wrench';
                    }

                    if (tolEl) {
                        tolEl.textContent = (tpl.toleranceMode || "Absolute") + (tpl.toleranceMode === "Percentage" ? " (±%)" : (" (±" + unit + ")"));
                    }
                    if (countEl) {
                        countEl.textContent = (tpl.steps ? tpl.steps.length : 0) + (isVernier ? " Measurement Points" : " Fastening Steps");
                    }

                    if (descEl) {
                        if (tpl.description && tpl.description.trim()) {
                            descEl.textContent = tpl.description;
                            descEl.classList.remove("d-none");
                        } else {
                            descEl.classList.add("d-none");
                        }
                    }

                    // Image & Markers
                    var imgEl = document.getElementById("tpl-preview-img");
                    var markersLayer = document.getElementById("tpl-preview-markers-layer");
                    if (markersLayer) markersLayer.innerHTML = "";

                    if (tpl.imagePath && imgEl) {
                        if (imgWrap) imgWrap.classList.remove("d-none");
                        if (noImg) noImg.classList.add("d-none");
                        var displayUrl = (tpl.imageUrl || ("/" + tpl.imagePath)) + "?t=" + Date.now();
                        imgEl.src = displayUrl;

                        var triggerMarkerRender = function () {
                            setTimeout(function () {
                                renderModalMarkerLayer("tpl-preview-markers-layer", tpl.steps, 22);
                            }, 60);
                        };
                        if (imgEl.complete && imgEl.naturalWidth > 0) {
                            triggerMarkerRender();
                        } else {
                            imgEl.onload = triggerMarkerRender;
                        }
                    } else {
                        if (imgWrap) imgWrap.classList.add("d-none");
                        if (noImg) noImg.classList.remove("d-none");
                    }

                    // Steps Table
                    if (tbody) {
                        tbody.innerHTML = "";
                        if (!tpl.steps || tpl.steps.length === 0) {
                            tbody.innerHTML = '<tr><td colspan="5" class="text-center py-3 text-muted small">No steps defined in this template.</td></tr>';
                        } else {
                            tpl.steps.forEach(function (s, idx) {
                                var tr = document.createElement("tr");
                                var hasPin = (s.markerX != null && s.markerY != null && !isNaN(parseFloat(s.markerX)));
                                var pinBadge = hasPin
                                    ? '<span class="badge bg-success-subtle text-success border border-success-subtle"><i class="bi bi-geo-alt-fill me-1"></i>Pin placed</span>'
                                    : '<span class="badge bg-light text-muted border">No pin</span>';

                                var tolDisplay = "±" + (s.tolerancePlus != null ? s.tolerancePlus : (s.toleranceMinus != null ? s.toleranceMinus : "0")) + (tpl.toleranceMode === "Percentage" ? "%" : (" " + unit));

                                tr.innerHTML = '<td class="fw-bold text-muted text-center" style="width:45px">' + (s.sequenceNo || (idx + 1)) + '</td>' +
                                    '<td class="fw-semibold text-dark">' + (s.label || (isVernier ? ("Distance " + (idx + 1)) : ("Bolt " + (idx + 1)))) + '</td>' +
                                    '<td class="font-monospace fw-bold text-primary">' + (s.target != null ? s.target : "—") + ' ' + unit + '</td>' +
                                    '<td class="font-monospace text-secondary">' + tolDisplay + '</td>' +
                                    '<td class="text-center">' + pinBadge + '</td>';
                                tbody.appendChild(tr);
                            });
                        }
                    }

                    if (btnAdd) btnAdd.disabled = false;
                    if (btnMove) btnMove.disabled = false;
                })
                .catch(function (err) {
                    if (nameEl) nameEl.textContent = "Error loading template specifications";
                    if (tbody) tbody.innerHTML = '<tr><td colspan="5" class="text-center py-3 text-danger small"><i class="bi bi-exclamation-triangle-fill me-1"></i>' + err.message + '</td></tr>';
                });
        }

        // Preview modal: Add to Product & Continue
        var btnPreviewAdd = document.getElementById("btn-preview-add-continue");
        if (btnPreviewAdd) {
            btnPreviewAdd.addEventListener("click", function () {
                if (!currentPreviewTpl) return;
                var modalEl = document.getElementById("template-preview-modal");
                if (modalEl && window.bootstrap && window.bootstrap.Modal) {
                    var modal = bootstrap.Modal.getInstance(modalEl);
                    if (modal) modal.hide();
                }

                addRow(
                    currentPreviewTpl.templateId,
                    currentPreviewTpl.name,
                    currentPreviewTpl.templateCode,
                    currentPreviewTpl.steps ? currentPreviewTpl.steps.length : 0
                );
                showAlert("Template '" + currentPreviewTpl.name + "' (" + (currentPreviewTpl.templateCode || "") + ") attached to product.", false, 2);
            });
        }

        // Preview modal: Move to Edit Window
        var btnPreviewMove = document.getElementById("btn-preview-move-edit");
        if (btnPreviewMove) {
            btnPreviewMove.addEventListener("click", function () {
                if (!currentPreviewTpl) return;
                var modalEl = document.getElementById("template-preview-modal");
                if (modalEl && window.bootstrap && window.bootstrap.Modal) {
                    var modal = bootstrap.Modal.getInstance(modalEl);
                    if (modal) modal.hide();
                }

                // If not already in product components, also attach it so it appears in Added to Product
                var currentRows = visibleRows();
                var alreadyAdded = false;
                var targetRowIdx = null;
                for (var i = 0; i < currentRows.length; i++) {
                    var sel = currentRows[i].querySelector(".row-template-select");
                    if (sel && parseInt(sel.value, 10) === currentPreviewTpl.templateId) {
                        alreadyAdded = true;
                        targetRowIdx = i;
                        break;
                    }
                }

                if (!alreadyAdded) {
                    addRow(
                        currentPreviewTpl.templateId,
                        currentPreviewTpl.name,
                        currentPreviewTpl.templateCode,
                        currentPreviewTpl.steps ? currentPreviewTpl.steps.length : 0
                    );
                    var updatedRows = visibleRows();
                    targetRowIdx = updatedRows.length - 1;
                }

                loadTemplateIntoEditor(currentPreviewTpl.templateId, targetRowIdx);

                // Smoothly scroll the left editor column into view
                var col = document.getElementById("template-generator-col");
                if (col) {
                    col.scrollIntoView({ behavior: "smooth", block: "start" });
                }
                showAlert("Template '" + currentPreviewTpl.name + "' loaded into edit window.", false, 2);
            });
        }

        // Re-align marker pins on shown.bs.modal
        var previewModalEl = document.getElementById("template-preview-modal");
        if (previewModalEl) {
            previewModalEl.addEventListener("shown.bs.modal", function () {
                if (currentPreviewTpl && currentPreviewTpl.steps && currentPreviewTpl.imagePath) {
                    setTimeout(function () {
                        renderModalMarkerLayer("tpl-preview-markers-layer", currentPreviewTpl.steps, 22);
                    }, 60);
                }
            });
        }

        // Generate & Add Click
        if (btnGenerateTemplate) {
            btnGenerateTemplate.addEventListener("click", function () {
                if (isTemplateViewOnly) {
                    if (viewingTemplateId) {
                        loadTemplateIntoEditor(viewingTemplateId, viewingComponentRowIdx, false);
                    }
                    return;
                }
                if (genError) genError.classList.add("d-none");
                var name = genTplName ? genTplName.value.trim() : "";
                if (!name) {
                    if (genTplName) genTplName.classList.add("is-invalid");
                    var errEl = document.getElementById("gen-tpl-name-err");
                    if (errEl) errEl.classList.remove("d-none");
                    return;
                }
                if (genTplName) genTplName.classList.remove("is-invalid");

                var steps = collectGenSteps();
                if (steps.length === 0) {
                    if (genError) {
                        genError.textContent = isVernierMode()
                            ? "Please add at least one measurement step."
                            : "Please add at least one bolt step.";
                        genError.classList.remove("d-none");
                    }
                    return;
                }

                // Marker assignment is compulsory: no pin, no template (when drawing uploaded).
                var unmarked = firstUnmarkedGenRow();
                if (unmarked) {
                    focusUnplacedMarker(unmarked);
                    var isVernier = isVernierMode();
                    var stepLabel = unmarked.row.querySelector(".step-label");
                    var dimType = isVernier ? getStepDimType(stepLabel ? stepLabel.value : "", unmarked.row) : "torque";
                    var defaultPrefix = isVernier ? (dimType === "radius" ? "Radius " : "Distance ") : "Bolt ";
                    var stepName = (stepLabel && stepLabel.value.trim()) || (defaultPrefix + (unmarked.index + 1));
                    if (isVernier) stepName = stepName.replace(/^bolt\s*/i, dimType === "radius" ? "Radius " : "Distance ");
                    var markerMsg = isVernier
                        ? (dimType === "radius"
                            ? "Measurement assignment is compulsory: mark point on circle for '" + stepName + "' before saving."
                            : "Measurement assignment is compulsory: select two points on the component image for '" + stepName + "' before saving.")
                        : "Marker assignment is compulsory: place a pin on the component image for '" + stepName + "' before saving.";
                    showAlert(markerMsg, true, 2);
                    return;
                }

                // If currently editing an existing template:
                if (editingTemplateId) {
                    var updatePayload = {
                        templateId: editingTemplateId,
                        name: name,
                        mode: genTplMode ? genTplMode.value : "Torque",
                        imagePath: genImagePath && genImagePath.value ? genImagePath.value : null,
                        steps: steps
                    };

                    pendingUpdatePayload = updatePayload;

                    var confirmModalEl = document.getElementById("save-template-confirm-modal");
                    if (confirmModalEl && window.bootstrap && window.bootstrap.Modal) {
                        var modalTplName = document.getElementById("confirm-modal-template-name");
                        var modalTplCode = document.getElementById("confirm-modal-template-code");
                        var currentEditName = document.getElementById("gen-editing-name");
                        if (modalTplName) modalTplName.textContent = name;
                        if (modalTplCode && currentEditName) modalTplCode.textContent = currentEditName.textContent || "";

                        var saveConfirmModal = bootstrap.Modal.getInstance(confirmModalEl) || new bootstrap.Modal(confirmModalEl);
                        saveConfirmModal.show();
                        return;
                    }

                    // Fallback if modal element is not in DOM
                    executeSaveTemplate(updatePayload, true);
                    return;
                }

                var deptEl = document.getElementById("DepartmentId") || document.querySelector("[name='DepartmentId']");
                var currentDeptId = deptEl && deptEl.value ? parseInt(deptEl.value, 10) : null;

                currentNewRequest = {
                    name: name,
                    mode: genTplMode ? genTplMode.value : "Torque",
                    imagePath: genImagePath && genImagePath.value ? genImagePath.value : null,
                    departmentId: currentDeptId,
                    steps: steps
                };

                btnGenerateTemplate.disabled = true;
                btnGenerateTemplate.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Checking catalogue...';

                // Check similarity with catalogue
                fetch("/Product/CheckTemplateSimilarity", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(currentNewRequest)
                })
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    if (data.isSimilarFound) {
                        // Show side-by-side modal
                        populateModalSideBySide(data);
                        if (similarityModal) similarityModal.show();
                    } else {
                        // Create directly and add
                        createAndAddInline(currentNewRequest);
                    }
                })
                .catch(function (err) {
                    if (genError) {
                        genError.textContent = "Error checking catalogue: " + err.message;
                        genError.classList.remove("d-none");
                    }
                })
                .finally(function () {
                    btnGenerateTemplate.disabled = false;
                    btnGenerateTemplate.innerHTML = '<i class="bi bi-magic me-1"></i>Generate Template &amp; Attach to Product';
                });
            });
        }

        function createAndAddInline(req, onComplete) {
            fetch("/Product/CreateInlineTemplate", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(req)
            })
            .then(function (r) {
                if (!r.ok) {
                    return r.text().then(function (text) {
                        var msg = "Failed to create template specification.";
                        try {
                            var json = JSON.parse(text);
                            msg = json.error || json.title || (json.errors ? Object.values(json.errors).flat().join(" ") : null) || msg;
                        } catch (_) {
                            if (text && text.length < 200) msg = text;
                        }
                        throw new Error(msg);
                    });
                }
                return r.json();
            })
            .then(function (created) {
                appendTemplateToCatalogue(created);
                addRow(created.templateId, created.name);
                showAlert("Template '" + created.name + "' (" + created.templateCode + ") successfully registered and added to product components.");
                if (genTplName) genTplName.value = "";
                if (onComplete) onComplete();
            })
            .catch(function (err) {
                showAlert(err.message, true, 2);
                if (onComplete) onComplete();
            });
        }
        // Modal Action: Choose Existing
        if (btnChooseExisting) {
            btnChooseExisting.addEventListener("click", function () {
                if (!currentExistingMatch) return;
                addRow(currentExistingMatch.templateId, currentExistingMatch.name);
                if (similarityModal) similarityModal.hide();
                showAlert("Existing catalogue template '" + currentExistingMatch.name + "' (" + currentExistingMatch.templateCode + ") added to product.");
                if (genTplName) genTplName.value = "";
            });
        }

        // Modal Action: Choose New
        if (btnChooseNew) {
            btnChooseNew.addEventListener("click", function () {
                if (!currentNewRequest) return;
                btnChooseNew.disabled = true;
                btnChooseNew.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Saving new template...';
                createAndAddInline(currentNewRequest, function () {
                    btnChooseNew.disabled = false;
                    btnChooseNew.innerHTML = '<i class="bi bi-plus-circle me-1"></i>Save as New Template & Use';
                    if (similarityModal) similarityModal.hide();
                });
            });
        }

                // ================================================================
        // STEPPER FLOW: New Product -> New Template -> Product Created
        // ================================================================
        var currentStep = 1;
        var pCodeInput = document.getElementById("ProductCode");
        var pNameInput = document.getElementById("ProductName");
        var pDescInput = document.getElementById("Description");
        var pBadge = document.getElementById("step2-product-badge");
        var step2AttachedCount = document.getElementById("step2-attached-count");

        function updateStep2Badge() {
            var rows = visibleRows();
            if (step2AttachedCount) step2AttachedCount.textContent = rows.length;
            renderStep2AddedTemplates();
        }

        function refreshStep3Summary() {
            var sName = document.getElementById("summary-product-name");
            var sCode = document.getElementById("summary-product-code");
            var sDesc = document.getElementById("summary-product-desc");
            var sImg = document.getElementById("summary-image-preview");
            var sPlaceholder = document.getElementById("summary-image-placeholder");
            var sViewBtn = document.getElementById("btn-summary-view-image");

            var nameVal = pNameInput ? pNameInput.value.trim() : "";
            var codeVal = pCodeInput ? pCodeInput.value.trim() : "";
            var descVal = pDescInput ? pDescInput.value.trim() : "";

            if (sName) sName.textContent = nameVal || "Untitled Product";
            if (sCode) sCode.textContent = codeVal || "NO-CODE";
            if (sDesc) sDesc.textContent = descVal || "No description provided.";

            var firstImagePath = productImages[0];
            if (firstImagePath && sImg) {
                sImg.src = "/" + firstImagePath + "?t=" + Date.now();
                sImg.style.display = "block";
                if (sPlaceholder) sPlaceholder.style.display = "none";
                if (sViewBtn) sViewBtn.style.display = "";
            } else {
                if (sImg) sImg.style.display = "none";
                if (sPlaceholder) sPlaceholder.style.display = "block";
                if (sViewBtn) sViewBtn.style.display = "none";
            }
        }

        // ---------------------------------------------------------------
        // Item code availability (Step 1 guard)
        // ---------------------------------------------------------------
        var codeFeedback = document.getElementById("product-code-feedback");
        var codeCheckToken = 0;
        var codeCheckTimer = null;

        function showCodeFeedback(message, isError) {
            if (!codeFeedback) return;
            codeFeedback.textContent = message;
            codeFeedback.className = isError ? "text-danger small mt-1" : "text-success small mt-1";
            codeFeedback.style.display = "block";
        }

        function hideCodeFeedback() {
            if (!codeFeedback) return;
            codeFeedback.textContent = "";
            codeFeedback.style.display = "none";
        }

        function excludeProductId() {
            var hidden = form ? form.querySelector('input[name="EditingProductId"]') : null;
            var value = hidden ? parseInt(hidden.value, 10) : NaN;
            return isNaN(value) ? null : value;
        }

        function checkCodeAvailable(code, excludeId) {
            return fetch("/Product/CheckCode", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ code: code, excludeProductId: excludeId })
            })
                .then(function (r) {
                    if (!r.ok) throw new Error("Item code check failed.");
                    return r.json();
                });
        }

        function codeUnchangedSince(code) {
            return pCodeInput && pCodeInput.value.trim() === code;
        }

        function markCodeInvalid(invalid) {
            if (!pCodeInput) return;
            if (invalid) pCodeInput.classList.add("is-invalid");
            else pCodeInput.classList.remove("is-invalid");
        }

        function renderCodeCheck(result, onVerified) {
            var available = typeof result === "boolean" ? result : !!(result && result.available);
            var message = (result && result.message) ? result.message : "This code is already used by another product.";
            markCodeInvalid(!available);
            if (!available) {
                showCodeFeedback(message, true);
                return false;
            }
            showCodeFeedback("Item code is available.", false);
            if (onVerified) onVerified();
            return true;
        }

        function cancelPendingCodeCheck() {
            if (codeCheckTimer) { clearTimeout(codeCheckTimer); codeCheckTimer = null; }
        }

        // Live feedback while typing (debounced) or on blur.
        function onCodeEdited(immediate) {
            cancelPendingCodeCheck();
            if (!immediate) {
                codeCheckTimer = setTimeout(function () { onCodeEdited(true); }, 400);
                return;
            }

            var code = pCodeInput ? pCodeInput.value.trim() : "";
            if (!code) {
                codeCheckToken++;
                markCodeInvalid(false);
                hideCodeFeedback();
                return;
            }

            var token = ++codeCheckToken;
            showCodeFeedback("Checking item code availability...", false);
            checkCodeAvailable(code, excludeProductId())
                .then(function (available) {
                    if (token !== codeCheckToken) return;
                    if (!codeUnchangedSince(code)) return;
                    renderCodeCheck(available);
                })
                .catch(function () {
                    if (token !== codeCheckToken) return;
                    hideCodeFeedback();
                });
        }

        // Runs the uniqueness check, then continues to the target step.
        function verifyCodeThenContinue(targetStep) {
            cancelPendingCodeCheck();

            var code = pCodeInput ? pCodeInput.value.trim() : "";
            if (!code) return;
            markCodeInvalid(false);

            var token = ++codeCheckToken;
            showCodeFeedback("Checking item code availability...", false);
            checkCodeAvailable(code, excludeProductId())
                .then(function (available) {
                    if (token !== codeCheckToken) return;
                    if (!codeUnchangedSince(code)) {
                        verifyCodeThenContinue(targetStep);
                        return;
                    }
                    renderCodeCheck(available, function () { applyStep(targetStep); });
                })
                .catch(function () {
                    if (token !== codeCheckToken) return;
                    hideCodeFeedback();
                    applyStep(targetStep);
                });
        }

        // Last client-side guard before the form POST: the server re-checks anyway.
        function formSubmitGuard(code) {
            cancelPendingCodeCheck();
            markCodeInvalid(false);

            var token = ++codeCheckToken;
            showCodeFeedback("Checking item code availability...", false);
            checkCodeAvailable(code, excludeProductId())
                .then(function (available) {
                    if (token !== codeCheckToken) return;
                    if (!codeUnchangedSince(code)) {
                        var fresh = pCodeInput ? pCodeInput.value.trim() : "";
                        if (fresh) formSubmitGuard(fresh);
                        return;
                    }
                    if (!available) {
                        renderCodeCheck(false);
                        applyStep(1);
                        return;
                    }
                    hideCodeFeedback();
                    if (form) form.submit();
                })
                .catch(function () {
                    if (token !== codeCheckToken) return;
                    if (form) form.submit();
                });
        }

        function goToStep(step) {
            // Validation before entering Step 2 or Step 3
            if (step === 2 || step === 3) {
                var cVal = pCodeInput ? pCodeInput.value.trim() : "";
                var nVal = pNameInput ? pNameInput.value.trim() : "";
                if (!cVal || !nVal) {
                    codeCheckToken++;
                    if (pCodeInput && !cVal) pCodeInput.classList.add("is-invalid");
                    if (pNameInput && !nVal) pNameInput.classList.add("is-invalid");
                    showAlert("Please specify Item Code and Product Name before continuing.", true, 1);
                    return false;
                }
                if (pCodeInput) pCodeInput.classList.remove("is-invalid");
                if (pNameInput) pNameInput.classList.remove("is-invalid");

                if (step === 3 && genStepsTbody && genStepsTbody.querySelectorAll(".gen-step-row").length) {
                    // Marker assignment is compulsory: every bolt step needs a
                    // placed pin before the flow can move into Step 3.
                    var unmarked = firstUnmarkedGenRow();
                    if (unmarked) {
                        applyStep(2);
                        focusUnplacedMarker(unmarked);
                        var isVernier = isVernierMode();
                        var stepLabel2 = unmarked.row.querySelector(".step-label");
                        var dimType2 = isVernier ? getStepDimType(stepLabel2 ? stepLabel2.value : "", unmarked.row) : "torque";
                        var defaultPrefix2 = isVernier ? (dimType2 === "radius" ? "Radius " : "Distance ") : "Bolt ";
                        var stepName2 = (stepLabel2 && stepLabel2.value.trim()) || (defaultPrefix2 + (unmarked.index + 1));
                        if (isVernier) stepName2 = stepName2.replace(/^bolt\s*/i, dimType2 === "radius" ? "Radius " : "Distance ");
                        var markerMsg2 = isVernier
                            ? (dimType2 === "radius"
                                ? "Measurement assignment is compulsory: mark point on circle for '" + stepName2 + "' before continuing."
                                : "Measurement assignment is compulsory: select two points on the component image for '" + stepName2 + "' before continuing.")
                            : "Marker assignment is compulsory: place a pin on the component image for '" + stepName2 + "' before continuing.";
                        showAlert(markerMsg2, true, 2);
                        return false;
                    }
                }

                hideAlert();
                verifyCodeThenContinue(step);
                return true;
            }

            applyStep(step);
            return true;
        }
        function applyStep(step) {
            // Banner alerts belong to the step that raised them: drop them when
            // the user moves to a different step so they never linger.
            if (alertOwnerStep !== null && alertOwnerStep !== step) hideAlert();

            if (step === 2 || step === 3) {
                if (pBadge) pBadge.textContent = pNameInput ? pNameInput.value.trim() : "";

                // Pre-fill template name if currently empty
                var genNameEl = document.getElementById("gen-tpl-name");
                if (genNameEl && !genNameEl.value.trim()) {
                    genNameEl.value = (pNameInput ? pNameInput.value.trim() : "") + " Fastening";
                }
            }

            currentStep = step;

            // Panel visibility
            var panel1 = document.getElementById("flow-step-1");
            var panel2 = document.getElementById("flow-step-2");
            var panel3 = document.getElementById("flow-step-3");
            if (panel1) panel1.classList.toggle("d-none", step !== 1);
            if (panel2) panel2.classList.toggle("d-none", step !== 2);
            if (panel3) panel3.classList.toggle("d-none", step !== 3);

            // Update Stepper indicators
            [1, 2, 3].forEach(function (s) {
                var navBtn = document.getElementById("step-nav-" + s);
                var numBadge = navBtn ? navBtn.querySelector(".step-num") : null;
                if (!navBtn || !numBadge) return;

                if (s === step) {
                    navBtn.classList.add("active");
                    numBadge.className = "step-num rounded-circle d-flex align-items-center justify-content-center fw-bold bg-primary text-white";
                    numBadge.textContent = s;
                } else if (s < step) {
                    navBtn.classList.remove("active");
                    numBadge.className = "step-num rounded-circle d-flex align-items-center justify-content-center fw-bold bg-success text-white";
                    numBadge.innerHTML = '<i class="bi bi-check-lg"></i>';
                } else {
                    navBtn.classList.remove("active");
                    numBadge.className = "step-num rounded-circle d-flex align-items-center justify-content-center fw-bold bg-light text-secondary border";
                    numBadge.textContent = s;
                }
            });

            if (step === 2) {
                updateStep2Badge();
            }
            if (step === 3) {
                refreshStep3Summary();
            }

            window.scrollTo({ top: 0, behavior: "smooth" });
            return true;
        }

        // Stepper Navigation Listeners
        document.querySelectorAll("[data-step]").forEach(function (btn) {
            btn.addEventListener("click", function () {
                var targetStep = parseInt(btn.dataset.step, 10);
                if (targetStep) goToStep(targetStep);
            });
        });

        var btnGoto2 = document.getElementById("btn-goto-step-2");
        if (btnGoto2) btnGoto2.addEventListener("click", function () { goToStep(2); });

        var btnBack1 = document.getElementById("btn-back-to-step-1");
        if (btnBack1) btnBack1.addEventListener("click", function () { goToStep(1); });

        var btnGoto3 = document.getElementById("btn-goto-step-3");
        if (btnGoto3) btnGoto3.addEventListener("click", function () { goToStep(3); });

        var btnBack2 = document.getElementById("btn-back-to-step-2");
        if (btnBack2) btnBack2.addEventListener("click", function () { goToStep(2); });

        var btnAddAnother = document.getElementById("btn-add-another-template");
        if (btnAddAnother) {
            btnAddAnother.addEventListener("click", function () {
                goToStep(2);
                var genNameEl = document.getElementById("gen-tpl-name");
                if (genNameEl) {
                    genNameEl.value = "";
                    genNameEl.focus();
                }
            });
        }

        var btnEmptyGoto2 = document.getElementById("btn-empty-goto-step-2");
        if (btnEmptyGoto2) btnEmptyGoto2.addEventListener("click", function () { goToStep(2); });

        var btnQuickEdit1 = document.getElementById("btn-quick-edit-step1");
        if (btnQuickEdit1) btnQuickEdit1.addEventListener("click", function () { goToStep(1); });

        // Item code availability checks while the user edits Step 1
        if (pCodeInput) {
            pCodeInput.addEventListener("input", function () { onCodeEdited(false); });
            pCodeInput.addEventListener("blur", function () { onCodeEdited(true); });
        }

        // Final client-side guard before the form POST; the server re-checks anyway.
        if (form) {
            form.addEventListener("submit", function (ev) {
                var code = pCodeInput ? pCodeInput.value.trim() : "";
                if (!code) return; // server validates the required code
                ev.preventDefault();
                formSubmitGuard(code);
            });
        }

        // Side Panel Toggle (Step 2)
        var btnToggleCatalogue = document.getElementById("btn-toggle-catalogue-view");
        var catalogueCol = document.getElementById("template-catalogue-col");
        var generatorCol = document.getElementById("template-generator-col");
        if (btnToggleCatalogue && catalogueCol && generatorCol) {
            btnToggleCatalogue.addEventListener("click", function () {
                var isHidden = catalogueCol.classList.contains("d-none");
                if (isHidden) {
                    catalogueCol.classList.remove("d-none");
                    generatorCol.className = "col-lg-8";
                    btnToggleCatalogue.innerHTML = '<i class="bi bi-layout-sidebar-reverse me-1" id="icon-toggle-side-panel"></i><span id="btn-toggle-catalogue-label">Minimize Side Panel</span>';
                } else {
                    catalogueCol.classList.add("d-none");
                    generatorCol.className = "col-lg-12";
                    var catBadge = document.getElementById("catalogue-badge-secondary");
                    var count = catBadge ? catBadge.textContent : "";
                    btnToggleCatalogue.innerHTML = '<i class="bi bi-layout-sidebar me-1" id="icon-toggle-side-panel"></i><span id="btn-toggle-catalogue-label">Show Catalogue &amp; Added (' + count + ')</span>';
                }
            });
        }

        // Section Minimize / Expand Toggles (Added Templates & Catalogue Library)
        var headerAdded = document.getElementById("header-added-templates");
        var btnToggleAdded = document.getElementById("btn-toggle-added-section");
        var bodyAdded = document.getElementById("collapse-added-templates");
        var iconAdded = document.getElementById("icon-toggle-added");

        function toggleAddedSection() {
            if (!bodyAdded) return;
            var isCollapsed = bodyAdded.classList.contains("d-none");
            if (isCollapsed) {
                bodyAdded.classList.remove("d-none");
                if (iconAdded) { iconAdded.className = "bi bi-chevron-down"; }
            } else {
                bodyAdded.classList.add("d-none");
                if (iconAdded) { iconAdded.className = "bi bi-chevron-right"; }
            }
        }
        if (headerAdded) headerAdded.addEventListener("click", function () { toggleAddedSection(); });
        if (btnToggleAdded) btnToggleAdded.addEventListener("click", function (e) {
            e.stopPropagation();
            toggleAddedSection();
        });

        var headerCatalogue = document.getElementById("header-catalogue-library");
        var btnToggleCatalogueSec = document.getElementById("btn-toggle-catalogue-section");
        var bodyCatalogue = document.getElementById("collapse-catalogue-library");
        var iconCatalogue = document.getElementById("icon-toggle-catalogue");

        function toggleCatalogueSection() {
            if (!bodyCatalogue) return;
            var isCollapsed = bodyCatalogue.classList.contains("d-none");
            if (isCollapsed) {
                bodyCatalogue.classList.remove("d-none");
                if (iconCatalogue) { iconCatalogue.className = "bi bi-chevron-down"; }
            } else {
                bodyCatalogue.classList.add("d-none");
                if (iconCatalogue) { iconCatalogue.className = "bi bi-chevron-right"; }
            }
        }
        if (headerCatalogue) headerCatalogue.addEventListener("click", function () { toggleCatalogueSection(); });
        if (btnToggleCatalogueSec) btnToggleCatalogueSec.addEventListener("click", function (e) {
            e.stopPropagation();
            toggleCatalogueSection();
        });
        // Initial setup
        syncModeTexts();
        syncMarkerSizeControls(currentMarkerSize);
        renderProductImages();
        refreshStep3Summary();
        // Seed the draft generator whenever the steps table is empty so it never shows a blank table
        if (genStepsTbody && genStepsTbody.querySelectorAll("tr").length === 0) {
            populateQuickSteps(4, 25.0, 2.0);
        }
        updateAutoGeneratedCodePreview();
        visibleRows().forEach(function (r) { r.draggable = false; });
        reindex();
    });
})();
