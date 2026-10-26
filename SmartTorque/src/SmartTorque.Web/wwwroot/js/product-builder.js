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

        function showAlert(message, isError) {
            if (!alertBanner || !alertMsg) return;
            alertMsg.textContent = message;
            alertBanner.className = isError
                ? "alert alert-danger alert-dismissible fade show mb-4"
                : "alert alert-success alert-dismissible fade show mb-4";
            alertBanner.classList.remove("d-none");
            alertBanner.scrollIntoView({ behavior: "smooth", block: "nearest" });
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

        // Product Image Upload (top-level product image)
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
        var genImagePreview = document.getElementById("gen-image-preview");
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

        function createGenStepRow(seq, label, target, tol, markerX, markerY) {
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
                    '<button type="button" class="btn btn-sm btn-link p-0 text-decoration-none btn-marker-pin" title="Click to select for marker placement">' +
                        '<i class="bi ' + pinClass + '"></i>' +
                    '</button>' +
                    '<input type="hidden" class="step-marker-x" value="' + (hasMarker ? markerX : '') + '" />' +
                    '<input type="hidden" class="step-marker-y" value="' + (hasMarker ? markerY : '') + '" />' +
                '</td>' +
                '<td class="text-end"><button type="button" class="btn btn-sm btn-link text-danger p-0 step-del" title="Remove step"><i class="bi bi-x-circle"></i></button></td>';

            tr.addEventListener("click", function (e) {
                if (e.target.closest(".step-del")) return;
                selectGenStep(parseInt(tr.dataset.index, 10));
            });

            return tr;
        }

        function selectGenStep(index) {
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
            renderGenMarkers();
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
                    maxRetries: 999
                });
            });
            return steps;
        }

        // -------------------------------------------------------------
        // Marker Size & Rendering on Image
        // -------------------------------------------------------------
        function setMarkerSize(size) {
            size = Math.max(14, Math.min(42, parseInt(size, 10) || 24));
            currentMarkerSize = size;
            localStorage.setItem("smarttorque_marker_size", size);

            document.querySelectorAll(".gen-size-preset").forEach(function (btn) {
                var pSize = parseInt(btn.dataset.size || "0", 10);
                btn.classList.toggle("active", pSize === size);
            });

            renderGenMarkers();
        }

        document.querySelectorAll(".gen-size-preset").forEach(function (btn) {
            btn.addEventListener("click", function () {
                setMarkerSize(this.dataset.size);
            });
        });

        function renderGenMarkers() {
            if (!genImageContainer) return;
            // Remove previous marker dots
            genImageContainer.querySelectorAll(".gen-marker-dot").forEach(function (d) { d.remove(); });

            var rows = genStepsTbody ? genStepsTbody.querySelectorAll(".gen-step-row") : [];
            rows.forEach(function (tr, idx) {
                var inputX = tr.querySelector(".step-marker-x");
                var inputY = tr.querySelector(".step-marker-y");
                if (inputX && inputY && inputX.value && inputY.value) {
                    var x = parseFloat(inputX.value);
                    var y = parseFloat(inputY.value);
                    var isSelected = idx === currentSelectedStep;

                    var dot = document.createElement("div");
                    dot.className = "gen-marker-dot shadow-sm" + (isSelected ? " active-marker" : "");
                    dot.style.position = "absolute";
                    dot.style.width = currentMarkerSize + "px";
                    dot.style.height = currentMarkerSize + "px";
                    dot.style.borderRadius = "50%";
                    dot.style.left = x + "%";
                    dot.style.top = y + "%";
                    dot.style.transform = "translate(-50%, -50%)";
                    dot.style.display = "flex";
                    dot.style.alignItems = "center";
                    dot.style.justifyContent = "center";
                    dot.style.fontSize = Math.max(9, Math.round(currentMarkerSize * 0.48)) + "px";
                    dot.style.fontWeight = "700";
                    dot.style.color = "#fff";
                    dot.style.cursor = "pointer";
                    dot.style.background = isSelected ? "var(--bs-primary, #0d6efd)" : "var(--bs-success, #198754)";
                    dot.style.border = isSelected ? "2px solid #fff" : "2px solid rgba(255,255,255,0.85)";
                    dot.style.boxShadow = isSelected ? "0 0 0 3px rgba(13,110,253,0.5), 0 2px 4px rgba(0,0,0,0.4)" : "0 2px 4px rgba(0,0,0,0.3)";
                    dot.style.transition = "width 0.15s, height 0.15s, transform 0.1s";
                    dot.style.zIndex = isSelected ? "10" : "5";
                    dot.textContent = idx + 1;

                    dot.addEventListener("click", function (e) {
                        e.stopPropagation();
                        selectGenStep(idx);
                    });

                    genImageContainer.appendChild(dot);
                }
            });
        }

        // Click on image container to place marker
        if (genImageContainer) {
            genImageContainer.addEventListener("click", function (event) {
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
        // Cropper.js Integration for Component Image
        // -------------------------------------------------------------
        var _cropper = null;
        var _blobUrl = null;
        var _cropZoomLevel = 1.0;
        var _flipH = false;
        var _flipV = false;

        var cropModalEl = document.getElementById("cropModal");
        var cropModal = cropModalEl && window.bootstrap ? new window.bootstrap.Modal(cropModalEl) : null;
        var cropImg = document.getElementById("crop-img");
        var btnApplyCrop = document.getElementById("btn-apply-crop");

        function openCropModal(src) {
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

                if (genImagePreview) {
                    genImagePreview.src = displayUrl;
                    genImagePreview.classList.remove("d-none");
                }
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
                if (genImagePreview) {
                    genImagePreview.src = "";
                    genImagePreview.classList.add("d-none");
                }
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
                    if (mx) mx.value = "";
                    if (my) my.value = "";
                    var pin = r.querySelector(".btn-marker-pin i");
                    if (pin) pin.className = "bi bi-geo-alt text-secondary opacity-50";
                });
                renderGenMarkers();
            });
        }

        // -------------------------------------------------------------
        // Similarity Modal & Side-by-Side Comparison
        // -------------------------------------------------------------
        function renderModalMarkerLayer(containerId, steps, markerSize) {
            var container = document.getElementById(containerId);
            if (!container) return;
            container.innerHTML = "";
            markerSize = markerSize || 18;

            (steps || []).forEach(function (s, idx) {
                if (s.markerX != null && s.markerY != null && !isNaN(parseFloat(s.markerX)) && !isNaN(parseFloat(s.markerY))) {
                    var dot = document.createElement("div");
                    dot.style.position = "absolute";
                    dot.style.width = markerSize + "px";
                    dot.style.height = markerSize + "px";
                    dot.style.borderRadius = "50%";
                    dot.style.left = s.markerX + "%";
                    dot.style.top = s.markerY + "%";
                    dot.style.transform = "translate(-50%, -50%)";
                    dot.style.display = "flex";
                    dot.style.alignItems = "center";
                    dot.style.justifyContent = "center";
                    dot.style.fontSize = Math.max(8, Math.round(markerSize * 0.45)) + "px";
                    dot.style.fontWeight = "700";
                    dot.style.color = "#fff";
                    dot.style.background = "#0d6efd";
                    dot.style.border = "2px solid #fff";
                    dot.style.boxShadow = "0 2px 4px rgba(0,0,0,0.4)";
                    dot.textContent = s.sequenceNo || (idx + 1);
                    container.appendChild(dot);
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

        // Initial setup
        populateQuickSteps(4, 25.0, 2.0);
        visibleRows().forEach(function (r) { r.draggable = false; });
        reindex();
    });
})();
