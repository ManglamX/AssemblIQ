/**
 * tool-qr-scanner.js — Real-Time Hardware Tool QR Code Camera Scanner
 * Enables workers to scan physical QR code labels (API keys / Transmitter IDs)
 * on Torque Wrenches and Vernier Calipers to instantly equip them.
 */

(function () {
    let html5QrCode = null;
    let isScanning = false;
    let currentFacingMode = "environment"; // default to back camera
    let activeTarget = "pairing"; // 'pairing' (JobBoard/Index modal) or 'cockpit' (Execute modal)

    // Web Audio API beep synthesizer for audio feedback without external audio files
    function playBeep(freq = 880, duration = 0.12) {
        try {
            const AudioContext = window.AudioContext || window.webkitAudioContext;
            if (!AudioContext) return;
            const ctx = new AudioContext();
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();
            osc.type = "sine";
            osc.frequency.setValueAtTime(freq, ctx.currentTime);
            gain.gain.setValueAtTime(0.15, ctx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + duration);
            osc.connect(gain);
            gain.connect(ctx.destination);
            osc.start();
            osc.stop(ctx.currentTime + duration);
        } catch (e) {
            // Audio not supported or blocked, ignore
        }
    }

    function initScannerModal() {
        const scannerModalEl = document.getElementById('toolQrScannerModal');
        if (!scannerModalEl) return;

        scannerModalEl.addEventListener('shown.bs.modal', function () {
            startCamera();
        });

        scannerModalEl.addEventListener('hidden.bs.modal', function () {
            stopCamera();
            resetSplash();
        });

        const btnSwitchCam = document.getElementById('btn-switch-camera');
        if (btnSwitchCam) {
            btnSwitchCam.addEventListener('click', function () {
                currentFacingMode = (currentFacingMode === "environment") ? "user" : "environment";
                stopCamera().then(() => startCamera());
            });
        }

        const btnManualSubmit = document.getElementById('btn-manual-tool-submit');
        const manualInput = document.getElementById('manual-tool-key-input');
        if (btnManualSubmit && manualInput) {
            btnManualSubmit.addEventListener('click', function () {
                const code = manualInput.value.trim();
                if (code) {
                    processScannedCode(code);
                }
            });
            manualInput.addEventListener('keypress', function (e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    btnManualSubmit.click();
                }
            });
        }
    }

    function startCamera() {
        if (isScanning) return;
        const readerEl = document.getElementById('tool-qr-reader');
        if (!readerEl) return;

        setStatus('Accessing camera...', 'info');

        if (typeof Html5Qrcode === 'undefined') {
            setStatus('Camera QR scanner library loading...', 'warning');
            return;
        }

        if (!html5QrCode) {
            html5QrCode = new Html5Qrcode("tool-qr-reader");
        }

        const config = {
            fps: 15,
            qrbox: { width: 220, height: 220 },
            aspectRatio: 1.0
        };

        html5QrCode.start(
            { facingMode: currentFacingMode },
            config,
            onScanSuccess,
            onScanFailure
        ).then(() => {
            isScanning = true;
            setStatus('Point camera at tool API key QR code', 'success');
        }).catch(err => {
            console.warn('Camera start error with facingMode:', err);
            // Fallback: enumerate available cameras
            Html5Qrcode.getCameras().then(devices => {
                if (devices && devices.length > 0) {
                    const cameraId = devices[0].id;
                    return html5QrCode.start(cameraId, config, onScanSuccess, onScanFailure);
                } else {
                    throw new Error("No cameras found on device");
                }
            }).then(() => {
                isScanning = true;
                setStatus('Camera ready. Scan tool QR label.', 'success');
            }).catch(e => {
                console.warn('Full camera error:', e);
                setStatus('Camera access unavailable. Please use manual entry below or check permissions.', 'warning');
            });
        });
    }

    function stopCamera() {
        if (!html5QrCode || !isScanning) return Promise.resolve();
        return html5QrCode.stop().then(() => {
            isScanning = false;
        }).catch(err => {
            console.warn('Camera stop error:', err);
            isScanning = false;
        });
    }

    function onScanSuccess(decodedText, decodedResult) {
        if (!decodedText) return;
        playBeep(987, 0.15); // High pitch pleasant success beep
        stopCamera();
        processScannedCode(decodedText);
    }

    function onScanFailure(error) {
        // Continuous scan failure while frames have no QR code, normal
    }

    function resetSplash() {
        const splash = document.getElementById('tool-scan-success-splash');
        if (splash) splash.classList.add('d-none');
        const manualInput = document.getElementById('manual-tool-key-input');
        if (manualInput) manualInput.value = '';
    }

    function setStatus(msg, type) {
        type = type || 'secondary';
        const statusBox = document.getElementById('tool-scanner-status');
        if (!statusBox) return;
        statusBox.className = 'alert alert-' + type + ' py-1 px-2 small mb-2 text-center text-truncate';
        statusBox.textContent = msg;
        statusBox.classList.remove('d-none');
    }

    function processScannedCode(code) {
        setStatus('Verifying hardware tool with AssemblIQ...', 'info');

        fetch('/Worker/JobBoard/LookupDeviceByQr?code=' + encodeURIComponent(code))
            .then(res => res.json())
            .then(data => {
                if (data.success) {
                    showSuccessSplash(data);
                    equipDeviceInActiveModal(data);
                } else {
                    playBeep(440, 0.3); // Low error tone
                    setStatus(data.message || 'Unrecognized tool QR code', 'danger');
                    // Restart camera after 2 seconds so worker can try again
                    setTimeout(() => {
                        startCamera();
                    }, 2000);
                }
            })
            .catch(err => {
                console.error('Lookup error:', err);
                setStatus('Failed to contact server. Please retry.', 'danger');
                setTimeout(() => startCamera(), 2000);
            });
    }

    function showSuccessSplash(data) {
        const splash = document.getElementById('tool-scan-success-splash');
        const titleEl = document.getElementById('tool-scan-success-title');
        const descEl = document.getElementById('tool-scan-success-desc');
        if (splash) {
            if (titleEl) titleEl.textContent = data.name;
            if (descEl) descEl.textContent = 'Equipped: ' + data.instrumentTitle + ' (' + data.transmitterId + ')';
            splash.classList.remove('d-none');
        }
    }

    function equipDeviceInActiveModal(data) {
        setTimeout(() => {
            // Close scanner modal
            const scannerModalEl = document.getElementById('toolQrScannerModal');
            if (scannerModalEl) {
                const modal = bootstrap.Modal.getInstance(scannerModalEl);
                if (modal) modal.hide();
            }

            // Populate target modal selects
            let torqueSelect = null;
            let vernierSelect = null;
            let successBannerId = null;

            if (activeTarget === 'cockpit') {
                torqueSelect = document.getElementById('execTorqueSelect') || document.querySelector('#pairCockpitToolModal select[name="torqueDeviceId"]');
                vernierSelect = document.getElementById('execVernierSelect') || document.querySelector('#pairCockpitToolModal select[name="vernierDeviceId"]');
                successBannerId = 'cockpitScanSuccessAlert';
            } else if (activeTarget === 'prd_cw') {
                torqueSelect = document.getElementById('cwTorqueSelect');
                vernierSelect = document.getElementById('cwVernierSelect');
                successBannerId = 'cwScanSuccessAlert';
            } else {
                torqueSelect = document.getElementById('modalTorqueSelect');
                vernierSelect = document.getElementById('modalVernierSelect');
                successBannerId = 'pairingScanSuccessAlert';
            }

            if (data.instrumentType === 1) { // Torque Wrench
                if (torqueSelect) {
                    torqueSelect.value = String(data.deviceId);
                    torqueSelect.dispatchEvent(new Event('change', { bubbles: true }));
                }
                if (vernierSelect) {
                    vernierSelect.value = '';
                    vernierSelect.dispatchEvent(new Event('change', { bubbles: true }));
                }
            } else if (data.instrumentType === 2) { // Vernier Caliper
                if (vernierSelect) {
                    vernierSelect.value = String(data.deviceId);
                    vernierSelect.dispatchEvent(new Event('change', { bubbles: true }));
                }
                if (torqueSelect) {
                    torqueSelect.value = '';
                    torqueSelect.dispatchEvent(new Event('change', { bubbles: true }));
                }
            }

            // Show confirmation banner inside pairing modal
            const banner = document.getElementById(successBannerId);
            const bannerText = document.getElementById(activeTarget === 'cockpit' ? 'cockpitScanSuccessText' : (activeTarget === 'prd_cw' ? 'cwScanSuccessText' : 'pairingScanSuccessText'));
            if (banner && bannerText) {
                bannerText.innerHTML = '<strong>Tool Equipped via QR:</strong> ' + data.name + ' (' + data.transmitterId + ') &bull; <span class="badge ' + (data.instrumentType === 1 ? 'bg-primary' : 'bg-info') + '">' + data.instrumentTitle + '</span>';
                banner.classList.remove('d-none');
            }

            // If auto-start is enabled in PRD workload modal, automatically initiate guidance!
            if (activeTarget === 'prd_cw') {
                const autoStart = document.getElementById('cwAutoStartSwitch')?.checked ?? true;
                if (autoStart) {
                    setTimeout(() => {
                        if (typeof window.submitCustomWorkload === 'function') {
                            window.submitCustomWorkload();
                        }
                    }, 600);
                }
            }
        }, 1200);
    }

    // Expose trigger globally
    window.openToolQrScanner = function (target) {
        activeTarget = target || 'pairing';
        const scannerModalEl = document.getElementById('toolQrScannerModal');
        if (scannerModalEl) {
            const modal = bootstrap.Modal.getOrCreateInstance(scannerModalEl);
            modal.show();
        }
    };

    document.addEventListener('DOMContentLoaded', function () {
        initScannerModal();

        document.querySelectorAll('.btn-open-tool-scanner').forEach(btn => {
            btn.addEventListener('click', function () {
                const target = this.getAttribute('data-target-modal') || 'pairing';
                window.openToolQrScanner(target);
            });
        });
    });
})();
