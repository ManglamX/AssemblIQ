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

            if (countBadge) countBadge.textContent = rows.length; var step2Count = document.getElementById("step2-attached-count"); if (step2Count) step2Count.textContent = rows.length;
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

            var btn = document.createElement("button");
            btn.type = "button";
            btn.className = "list-group-item list-group-item-action d-flex justify-content-between align-items-center border-0 border-bottom px-2 py-2";
            btn.dataset.templateId = created.templateId;
            btn.dataset.steps = created.stepCount;
            btn.dataset.label = created.label;
            btn.dataset.name = created.name;
            btn.dataset.search = (created.templateCode + " " + created.name).toLowerCase();
            btn.innerHTML = '<span><span class="d-block fw-semibold text-dark small">' + created.name + '</span>' +
                '<span class="d-block text-muted" style="font-size:0.75rem">' + created.templateCode + ' · v' + created.version + ' · ' + created.stepCount + ' steps</span></span>' +
                '<span class="btn btn-sm btn-outline-primary py-0 px-2" title="Add as component"><i class="bi bi-plus"></i> Add</span>';

            btn.addEventListener("click", function () {
                addRow(created.templateId, created.name);
            });

            if (picker) {
                picker.insertBefore(btn, picker.firstChild);
            }

            var catBadge = document.getElementById("catalogue-badge");
            if (catBadge) {
                var c = parseInt(catBadge.textContent || "0", 10) + 1;
                catBadge.textContent = c;
            }
        }

        // Template picker click listener (initial items)
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

        // -------------------------------------------------------------
        // Step Rows Management
        // -------------------------------------------------------------
        function updateGenStepCount() {
            if (!genStepsTbody) return;
            var rows = genStepsTbody.querySelectorAll("tr");
            if (genStepsCount) genStepsCount.textContent = rows.length;
        }

        function createGenStepRow(seq, label, target, tol, markerX, markerY, markerSize) {
            var tr = document.createElement("tr");
            tr.className = "gen-step-row";
            tr.dataset.index = seq - 1;
            tr.style.cursor = "pointer";

            var hasMarker = markerX != null && markerY != null && !isNaN(parseFloat(markerX)) && !isNaN(parseFloat(markerY));
            var pinClass = hasMarker ? "bi-geo-alt-fill text-success" : "bi-geo-alt text-secondary opacity-50";

            tr.innerHTML = '<td class="fw-bold text-center text-muted seq-cell">' + seq + '</td>' +
                '<td><input type="text" class="form-control form-control-sm py-0 step-label" value="' + label + '" /></td>' +
                '<td><input type="number" step="0.1" class="form-control form-control-sm py-0 step-target" value="' + target + '" /></td>' +
                '<td><input type="number" step="0.1" class="form-control form-control-sm py-0 step-tol" value="' + tol + '" /></td>' +
                '<td class="text-center align-middle">' +
                    '<button type="button" class="btn btn-sm btn-link p-0 text-decoration-none btn-marker-pin" title="Open the large marking view for this step">' +
                        '<i class="bi ' + pinClass + '"></i>' +
                    '</button>' +
                    '<input type="hidden" class="step-marker-x" value="' + (hasMarker ? markerX : '') + '" />' +
                    '<input type="hidden" class="step-marker-y" value="' + (hasMarker ? markerY : '') + '" />' +
                    '<input type="hidden" class="step-marker-size" value="' + (hasMarker && markerSize ? markerSize : '') + '" />' +
                '</td>' +
                '<td class="text-end"><button type="button" class="btn btn-sm btn-link text-danger p-0 step-del" title="Remove step"><i class="bi bi-x-circle"></i></button></td>';

            tr.addEventListener("click", function (e) {
                if (e.target.closest(".step-del")) return;
                var pinBtn = e.target.closest(".btn-marker-pin");
                if (pinBtn) {
                    // The pin button is the shortcut into the large marking view
                    openMarkerModal(parseInt(tr.dataset.index, 10));
                    return;
                }
                selectGenStep(parseInt(tr.dataset.index, 10));
            });

            return tr;
        }

        function selectGenStep(index, skipMarkerRender) {
            currentSelectedStep = index;
            var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : [];
            rows.forEach(function (r, idx) {
                if (idx === index) {
                    r.classList.add("table-primary");
                    var lbl = r.querySelector(".step-label");
                    if (genSelectedStepLabel) {
                        genSelectedStepLabel.textContent = (lbl && lbl.value.trim()) ? lbl.value.trim() : ("Bolt " + (idx + 1));
                    }
                } else {
                    r.classList.remove("table-primary");
                }
            });
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

        function populateQuickSteps(count, target, tol) {
            if (!genStepsTbody) return;
            genStepsTbody.innerHTML = "";
            count = Math.max(1, parseInt(count, 10) || 4);
            target = parseFloat(target) || 25.0;
            tol = parseFloat(tol) || 2.0;

            for (var i = 1; i <= count; i++) {
                genStepsTbody.appendChild(createGenStepRow(i, "Bolt " + i, target, tol));
            }
            updateGenStepCount();
            selectGenStep(0);
            renderGenMarkers();
        }

        // Mode switch changes unit label
        if (genTplMode) {
            genTplMode.addEventListener("change", function () {
                var unitLabels = document.querySelectorAll(".unit-label");
                var u = genTplMode.value === "Vernier" ? "mm" : "N·m";
                unitLabels.forEach(function (l) { l.textContent = u; });
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
                var row = createGenStepRow(nextSeq, "Bolt " + nextSeq, defTarget, defTol);
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
            var instrument = mode === "Vernier" ? "Vernier" : "Torque";
            var tolMode = genTplTolMode ? genTplTolMode.value : "Absolute";

            rows.forEach(function (tr, idx) {
                var label = tr.querySelector(".step-label").value.trim() || ("Bolt " + (idx + 1));
                var target = parseFloat(tr.querySelector(".step-target").value) || 0;
                var tol = parseFloat(tr.querySelector(".step-tol").value) || 0;
                var inputX = tr.querySelector(".step-marker-x");
                var inputY = tr.querySelector(".step-marker-y");
                var markerX = inputX && inputX.value ? parseFloat(inputX.value) : null;
                var markerY = inputY && inputY.value ? parseFloat(inputY.value) : null;
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
                    markerSize: markerSize,
                    maxRetries: 999
                });
            });
            return steps;
        }

        // -------------------------------------------------------------
        // Compulsory marker assignment: a bolt step without a placed pin
        // blocks template generation and Step-3 navigation.
        // -------------------------------------------------------------
        function firstUnmarkedGenRow() {
            var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : null;
            if (!rows) return null;
            for (var i = 0; i < rows.length; i++) {
                var inputX = rows[i].querySelector(".step-marker-x");
                var inputY = rows[i].querySelector(".step-marker-y");
                if (!inputX || !inputY || !inputX.value || !inputY.value) {
                    return { row: rows[i], index: i };
                }
            }
            return null;
        }

        function refreshUnplacedMarkers() {
            var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : [];
            rows.forEach(function (r) {
                var inputX = r.querySelector(".step-marker-x");
                var inputY = r.querySelector(".step-marker-y");
                var placed = inputX && inputY && inputX.value && inputY.value;
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

        function updateRowMarker(idx, x, y) {
            var row = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row")[idx] : null;
            if (!row) return;
            var inputX = row.querySelector(".step-marker-x");
            var inputY = row.querySelector(".step-marker-y");
            if (inputX) inputX.value = x;
            if (inputY) inputY.value = y;
            var sizeInput = row.querySelector(".step-marker-size");
            if (sizeInput && !sizeInput.value) {
                // Stamp the size at placement time so later slider changes to
                // other markers don't retro-adjust this one.
                sizeInput.value = currentMarkerSize;
            }
            var pinIcon = row.querySelector(".btn-marker-pin i");
            if (pinIcon) {
                pinIcon.className = (x && y) ? "bi bi-geo-alt-fill text-success" : "bi bi-geo-alt text-secondary opacity-50";
            }
            row.classList.toggle("table-danger", !(x && y));
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
            dot.style.touchAction = "none"; // drag must not scroll the page on touch
            dot.style.pointerEvents = "auto"; // the layer itself is click-through
            dot.textContent = idx + 1;
            return dot;
        }

        // Pointer dragging + tap-to-select for one dot.
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
                // Rebuilding the dots detaches the one under the cursor, so the
                // browser's follow-up click would fall through to the canvas and
                // trigger place + auto-advance, stealing the selection. Swallow
                // that one click.
                genSuppressClick = true;
                setTimeout(function () { genSuppressClick = false; }, 150);
                renderGenMarkers(); // snap back inside bounds, refresh selection style
            };
            dot.addEventListener("pointerup", finishDrag);
            dot.addEventListener("pointercancel", finishDrag);

            dot.addEventListener("click", function (e) {
                // pointerdown above already selects the row; without stopping the
                // click here the container would treat the same tap as pin placement
                e.stopPropagation();
            });
        }

        function renderGenMarkers() {
            if (!genMarkerLayer) return;
            syncGenMarkerLayer();
            genMarkerLayer.querySelectorAll(".gen-marker-dot").forEach(function (d) { d.remove(); });

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
        }

        // Click on image container to place marker
        if (genImageContainer) {
            genImageContainer.addEventListener("click", function (event) {
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

                renderGenMarkers();

                // Auto-advance to next step if unplaced
                for (var i = 0; i < rows.length; i++) {
                    var rX = rows[i].querySelector(".step-marker-x");
                    if (!rX || !rX.value) {
                        selectGenStep(i);
                        break;
                    }
                }
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
            if (!row) return { x: null, y: null, placed: false };
            var ix = row.querySelector(".step-marker-x");
            var iy = row.querySelector(".step-marker-y");
            var x = ix && ix.value ? parseFloat(ix.value) : null;
            var y = iy && iy.value ? parseFloat(iy.value) : null;
            return { x: x, y: y, placed: x != null && y != null && !isNaN(x) && !isNaN(y) };
        }

        // Placement needs the same select-without-destroy dance as the inline
        // panel: pointerdown would otherwise rebuild the grabbed dot.
        function selectMmInplace(idx) {
            currentSelectedStep = idx;
            var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : [];
            rows.forEach(function (r, i2) {
                r.classList.toggle("table-primary", i2 === idx);
            });
            var row = rows[idx];
            var lbl = row ? row.querySelector(".step-label") : null;
            if (genSelectedStepLabel) genSelectedStepLabel.textContent = (lbl && lbl.value.trim()) ? lbl.value.trim() : ("Bolt " + (idx + 1));
            if (mmSelLabel) mmSelLabel.textContent = (lbl && lbl.value.trim()) ? lbl.value.trim() : ("Bolt " + (idx + 1));
            var st = mmStatus(idx);
            syncMarkerSizeControls(st.placed ? getRowMarkerSize(row) : currentMarkerSize);
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
            if (!mmLayer) return;
            syncMmMarkerLayer();
            mmLayer.querySelectorAll(".gen-marker-dot").forEach(function (d) { d.remove(); });
            if (!genImagePath.value || !mmImage || !mmImage.src) return;

            genStepsTbody.querySelectorAll(".gen-step-row").forEach(function (tr, idx) {
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

        // Click inside the modal canvas places a pin for the selected step.
        if (mmImgWrap) {
            mmImgWrap.addEventListener("click", function (event) {
                if (mmSuppressClick) { mmSuppressClick = false; return; }
                if (!genImagePath.value) return;
                var img = mmImage;
                if (!img || !img.src) return;
                var rect = img.getBoundingClientRect();
                if (!rect.width || !rect.height) return;
                if (event.clientX < rect.left || event.clientX > rect.right || event.clientY < rect.top || event.clientY > rect.bottom) return;
                // Ignore clicks landing inside the letterbox margins when the
                // wrap is wider/taller than the image box.
                if (!img.contains(event.target) && !mmLayer.contains(event.target)) return;

                var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : [];
                if (!rows.length) return;

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
                targetRow.querySelector(".step-marker-x").value = x;
                targetRow.querySelector(".step-marker-y").value = y;
                var sizeInput = targetRow.querySelector(".step-marker-size");
                if (sizeInput) sizeInput.value = currentMarkerSize;
                var pinIcon = targetRow.querySelector(".btn-marker-pin i");
                if (pinIcon) pinIcon.className = "bi bi-geo-alt-fill text-success";

                renderMmMarkers();
                renderGenMarkers();

                // Auto-advance to the next unplaced step
                for (var i = 0; i < rows.length; i++) {
                    var rX = rows[i].querySelector(".step-marker-x");
                    if (!rX || !rX.value) {
                        selectGenStep(i);
                        break;
                    }
                }
            });
        }

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
            mmSelLabel.textContent = (lbl && lbl.value.trim()) ? lbl.value.trim() : ("Bolt " + (currentSelectedStep + 1));
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
            mmModal.show();
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
                    var ms = r.querySelector(".step-marker-size");
                    if (mx) mx.value = "";
                    if (my) my.value = "";
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

        // Generate & Add Click
        if (btnGenerateTemplate) {
            btnGenerateTemplate.addEventListener("click", function () {
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
                        genError.textContent = "Please add at least one bolt step.";
                        genError.classList.remove("d-none");
                    }
                    return;
                }

                // Marker assignment is compulsory: no pin, no template.
                var unmarked = firstUnmarkedGenRow();
                if (unmarked) {
                    focusUnplacedMarker(unmarked);
                    var stepLabel = unmarked.row.querySelector(".step-label");
                    var stepName = (stepLabel && stepLabel.value.trim()) || ("Bolt " + (unmarked.index + 1));
                    showAlert("Marker assignment is compulsory: place a pin on the component image for '" + stepName + "' before generating the template.", true, 2);
                    return;
                }

                currentNewRequest = {
                    name: name,
                    mode: genTplMode ? genTplMode.value : "Torque",
                    imagePath: genImagePath && genImagePath.value ? genImagePath.value : null,
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
                    btnGenerateTemplate.innerHTML = '<i class="bi bi-plus-lg me-1"></i>Generate & Add to Product';
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
                if (!r.ok) throw new Error("Failed to create template specification.");
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
                showAlert(err.message, true);
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
        // Product code availability (Step 1 guard)
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
                    if (!r.ok) throw new Error("Product code check failed.");
                    return r.json();
                })
                .then(function (data) { return !!data.available; });
        }

        function codeUnchangedSince(code) {
            return pCodeInput && pCodeInput.value.trim() === code;
        }

        function markCodeInvalid(invalid) {
            if (!pCodeInput) return;
            if (invalid) pCodeInput.classList.add("is-invalid");
            else pCodeInput.classList.remove("is-invalid");
        }

        function renderCodeCheck(available, onVerified) {
            markCodeInvalid(!available);
            if (!available) {
                showCodeFeedback("This code is already used by another product.", true);
                return false;
            }
            showCodeFeedback("Product code is available.", false);
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
            showCodeFeedback("Checking product code availability...", false);
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
            showCodeFeedback("Checking product code availability...", false);
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
            showCodeFeedback("Checking product code availability...", false);
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
                    showAlert("Please specify Product Code and Product Name before continuing.", true, 1);
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
                    var stepLabel2 = unmarked.row.querySelector(".step-label");
                    var stepName2 = (stepLabel2 && stepLabel2.value.trim()) || ("Bolt " + (unmarked.index + 1));
                    showAlert("Marker assignment is compulsory: place a pin on the component image for '" + stepName2 + "' before continuing.", true, 2);
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

        // Product code availability checks while the user edits Step 1
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

        var btnToggleCatalogue = document.getElementById("btn-toggle-catalogue-view");
        var catalogueCol = document.getElementById("template-catalogue-col");
        var generatorCol = document.getElementById("template-generator-col");
        if (btnToggleCatalogue && catalogueCol && generatorCol) {
            btnToggleCatalogue.addEventListener("click", function () {
                var isHidden = catalogueCol.classList.contains("d-none");
                if (isHidden) {
                    catalogueCol.classList.remove("d-none");
                    generatorCol.className = "col-lg-8";
                } else {
                    catalogueCol.classList.add("d-none");
                    generatorCol.className = "col-lg-12";
                }
            });
        }

        // Initial setup
        syncMarkerSizeControls(currentMarkerSize);
        renderProductImages();
        refreshStep3Summary();
        // Seed the draft generator only for products without attached
        // templates yet; edit mode with components must stay freely navigable.
        if (visibleRows().length === 0) {
            populateQuickSteps(4, 25.0, 2.0);
        }
        visibleRows().forEach(function (r) { r.draggable = false; });
        reindex();
    });
})();
