/**
 * AssemblIQ - Tohnichi CEM3-G-BT Web Bluetooth (BLE) Driver
 * =========================================================================
 * Provides native browser-to-wrench wireless BLE communication using the
 * Web Bluetooth GATT standard. Works directly with physical Tohnichi CEM3-G-BT
 * wrenches (in BLE mode) as well as the AssemblIQ ESP32 BLE Simulator.
 */

class TohnichiBleDriver {
    constructor() {
        this.SERVICE_UUID = '55df0001-a9b0-11e3-a5e2-000190f08f1e';
        this.TX_CHAR_UUID = '55df0002-a9b0-11e3-a5e2-000190f08f1e'; // Tool -> Browser (Notify)
        this.RX_CHAR_UUID = '55df0003-a9b0-11e3-a5e2-000190f08f1e'; // Browser -> Tool (Write)
        this.protocols = [];
        this.deviceMappings = [];
        this.activeProtocol = null;
        this._rxBuffer = '';
        this._rxFlushTimer = null;
        this.serialPort = null;
        this.serialReader = null;
        this.isSerialConnected = false;
        // Preload dynamic BLE protocols in background so connect() keeps the user gesture intact
        this._protocolsLoaded = this.loadProtocols();

        this.device = null;
        this.server = null;
        this.service = null;
        this.txChar = null;
        this.rxChar = null;
        this.isConnected = false;
        this.toolSerial = null;
        this.isUserInitiatedDisconnect = false;
        this.reconnectTimer = null;
        this._isReconnecting = false;

        // Callback hooks
        this.onReadingReceived = null;
        this.onStatusChange = null;
        this.onConnected = null;
    }

    /**
     * Check if Web Bluetooth is available in this browser
     */
    isSupported() {
        return !!(window.isSecureContext && navigator && navigator.bluetooth);
    }

    /**
     * Attach gattserverdisconnected event listener once
     */
    bindDisconnectListener(dev) {
        if (!dev || dev._disconnectBound) return;
        dev._disconnectBound = true;
        dev.addEventListener('gattserverdisconnected', () => this.handleDisconnect());
    }

    /**
     * Handle unexpected GATT disconnect (wrench idle, sleep, or transient radio drop)
     */
    handleDisconnect() {
        this.isConnected = false;
        console.warn("[Tohnichi BLE] Wrench disconnected.");

        if (this.isUserInitiatedDisconnect) {
            this.updateStatus("Disconnected", "secondary");
            return;
        }

        // Automatic background reconnection: wrench sleep / idle between bolts
        this.updateStatus("Reconnecting...", "warning");
        this.scheduleAutoReconnect(500);
    }

    /**
     * Schedule a background reconnection attempt
     */
    scheduleAutoReconnect(delayMs = 1000) {
        if (this.isUserInitiatedDisconnect || this.isConnected) return;
        if (this.reconnectTimer) clearTimeout(this.reconnectTimer);

        this.reconnectTimer = setTimeout(async () => {
            if (this.isUserInitiatedDisconnect || this.isConnected) return;
            try {
                const reconnected = await this.autoReconnect();
                if (reconnected) {
                    console.log("[Tohnichi BLE] Background auto-reconnect succeeded!");
                    return;
                }
            } catch (err) {
                // Ignore transient errors
            }

            // Retry continuously every 1.5s while the operator is on the job
            if (!this.isUserInitiatedDisconnect && !this.isConnected) {
                this.scheduleAutoReconnect(1500);
            }
        }, delayMs);
    }

    /**
     * Connect to the Tohnichi CEM3-G-BT wrench or ESP32 BLE Simulator via pairing chooser
     */
    async connect() {
        if (!window.isSecureContext) {
            throw new Error("Web Bluetooth requires a Secure Context (HTTPS or localhost). Over LAN HTTP, please enable chrome://flags/#unsafely-treat-insecure-origin-as-secure or use Gateway/Manual Entry.");
        }
        if (!navigator.bluetooth) {
            throw new Error("Web Bluetooth API is not available in this browser. Please use Google Chrome or Microsoft Edge.");
        }

        this.isUserInitiatedDisconnect = false;
        if (this.reconnectTimer) clearTimeout(this.reconnectTimer);

        try {
            this.updateStatus("Connecting...", "primary");

            // IMPORTANT: Do NOT await before requestDevice() or the browser drops the user-gesture activation!
            const prefixSet = new Set(['CEM3', 'Tohnichi', 'ESP', 'esp', 'AssemblIQ', 'Vernier', 'Caliper', 'Mitutoyo', 'U-WAVE']);
            const serviceSet = new Set([this.SERVICE_UUID]);
            for (const p of this.protocols) {
                if (p.serviceUuid) serviceSet.add(p.serviceUuid.toLowerCase());
                if (p.deviceNamePrefixes) {
                    p.deviceNamePrefixes.split(',').map(s => s.trim()).filter(Boolean).forEach(px => prefixSet.add(px));
                }
            }

            const filters = [
                ...Array.from(prefixSet).map(namePrefix => ({ namePrefix })),
                ...Array.from(serviceSet).map(uuid => ({ services: [uuid] }))
            ];

            // Request nearby Bluetooth devices matching any configured protocol prefix or Service UUID
            this.device = await navigator.bluetooth.requestDevice({
                filters,
                optionalServices: Array.from(serviceSet)
            });

            if (this.protocols.length === 0) {
                await this.loadProtocols();
            }

            this.toolSerial = this.device.name ? this.device.name.replace('CEM3-G-BT_', '').replace('_BLE', '') : '704620P';
            this.bindDisconnectListener(this.device);

            // Connect to GATT Server and bind mapped BLE protocol
            this.server = await this.device.gatt.connect();
            const bound = await this.bindProtocolGatt(this.device, this.server);
            this.service = bound.service;
            this.txChar = bound.txChar;
            this.rxChar = bound.rxChar;
            await this.txChar.startNotifications();
            this.txChar.addEventListener('characteristicvaluechanged', (e) => this.handleNotification(e));

            this.isConnected = true;

            // PERSISTENT PAIRING: Remember paired wrench in localStorage so it stays connected across sessions & pages
            localStorage.setItem('assembl_ble_paired', 'true');
            localStorage.setItem('assembl_ble_auto_connect', 'true');
            localStorage.setItem('assembl_active_ble_tool', this.device.name || 'CEM3-G-BT');
            localStorage.setItem('assembl_active_ble_serial', this.toolSerial || '704620P');
            if (this.device.id) localStorage.setItem('assembl_active_ble_id', this.device.id);

            this.updateStatus(`Connected: ${this.device.name}`, "success");
            console.log(`[Tohnichi BLE] Successfully connected to ${this.device.name}!`);

            if (typeof this.onConnected === 'function') {
                try { this.onConnected(); } catch (e) { }
            }

            return {
                name: this.device.name,
                serial: this.toolSerial
            };

        } catch (error) {
            this.isConnected = false;
            this.updateStatus("Not Connected", "secondary");
            console.error("[Tohnichi BLE] Connection error:", error);
            throw error;
        }
    }

    /**
     * Attempt automatic reconnection to a previously authorized BLE wrench without user gesture
     */
    async autoReconnect() {
        if (!this.isSupported()) return false;
        if (this.isUserInitiatedDisconnect) return false;
        if (this.isConnected && this.device && this.device.gatt && this.device.gatt.connected) return true;
        if (this._isReconnecting) return false;

        // Only auto-reconnect if the tool was previously paired by the operator
        const isPaired = localStorage.getItem('assembl_ble_paired') === 'true' || localStorage.getItem('assembl_ble_auto_connect') === 'true';
        if (!isPaired) return false;

        this._isReconnecting = true;
        this.updateStatus("Connecting...", "primary");

        try {
            let cemDevice = this.device;
            if (!cemDevice && navigator.bluetooth && navigator.bluetooth.getDevices) {
                try {
                    const devices = await navigator.bluetooth.getDevices();
                    const targetName = localStorage.getItem('assembl_active_ble_tool');
                    const targetSerial = localStorage.getItem('assembl_active_ble_serial');
                    const targetId = localStorage.getItem('assembl_active_ble_id');

                    cemDevice = devices.find(d => 
                        (targetId && d.id === targetId) ||
                        (targetName && d.name === targetName) ||
                        (targetSerial && d.name && d.name.includes(targetSerial)) ||
                        (d.name && (d.name.startsWith('CEM3') || d.name.toLowerCase().includes('esp') || d.name.toLowerCase().includes('tohnichi') || d.name.includes('BLE')))
                    ) || (devices.length > 0 ? devices[0] : null);
                } catch (devErr) {
                    console.warn("[Tohnichi BLE] getDevices error:", devErr);
                }
            }

            if (!cemDevice) {
                this._isReconnecting = false;
                console.log("[Tohnichi BLE] No authorized Bluetooth device found in background cache. User gesture required to reconnect.");
                return false;
            }

            console.log("[Tohnichi BLE] Auto-reconnecting to authorized device:", cemDevice.name);
            this.device = cemDevice;
            this.toolSerial = this.device.name ? this.device.name.replace('CEM3-G-BT_', '').replace('_BLE', '') : (localStorage.getItem('assembl_active_ble_serial') || '704620P');
            this.bindDisconnectListener(this.device);

            // Connect GATT with progressive retry for OS BLE stack release after navigation
            for (let attempt = 1; attempt <= 6; attempt++) {
                try {
                    this.server = await this.device.gatt.connect();
                    if (this.server && this.server.connected) break;
                } catch (connErr) {
                    if (attempt === 6) throw connErr;
                    await new Promise(r => setTimeout(r, 350 * attempt));
                }
            }

            await this.loadProtocols();
            const bound = await this.bindProtocolGatt(this.device, this.server);
            this.service = bound.service;
            this.txChar = bound.txChar;
            this.rxChar = bound.rxChar;
            await this.txChar.startNotifications();
            this.txChar.addEventListener('characteristicvaluechanged', (e) => this.handleNotification(e));

            this.isConnected = true;
            this.updateStatus(`Connected: ${this.device.name}`, "success");
            console.log(`[Tohnichi BLE] Auto-reconnected to ${this.device.name}!`);

            if (typeof this.onConnected === 'function') {
                try { this.onConnected(); } catch (e) { }
            }

            this._isReconnecting = false;
            return true;
        } catch (err) {
            console.warn("[Tohnichi BLE] Auto-reconnect attempt failed, scheduling next retry:", err);
            this._isReconnecting = false;
            if (!this.isUserInitiatedDisconnect) {
                this.scheduleAutoReconnect(2000);
            }
            return false;
        }
    }

    /**
     * Disconnect cleanly from the tool (operator initiated or logout)
     */
    disconnect(isManual = true) {
        if (isManual) {
            this.isUserInitiatedDisconnect = true;
            localStorage.removeItem('assembl_ble_paired');
            localStorage.removeItem('assembl_ble_auto_connect');
            localStorage.removeItem('assembl_active_ble_tool');
            localStorage.removeItem('assembl_active_ble_serial');
            localStorage.removeItem('assembl_active_ble_id');
        }
        if (this.reconnectTimer) clearTimeout(this.reconnectTimer);
        if (this.device && this.device.gatt && this.device.gatt.connected) {
            try { this.device.gatt.disconnect(); } catch (e) {}
        }
        this.isConnected = false;
        this.updateStatus("Disconnected", "secondary");
    }

    /**
     * Send target torque limits to the wrench using Tohnichi AT037 command
     * Format: AT037,HIGH,LOW\r\n (5 chars with decimal point)
     */
    async syncTorqueLimits(upperLimitNm, lowerLimitNm) {
        if (!this.isConnected || !this.rxChar) {
            console.warn("[Tohnichi BLE] Cannot send limits: Wrench not connected or write char missing.");
            return false;
        }

        try {
            // Format to 5 chars with decimal (e.g. 027.0, 023.0)
            const hiStr = Number(upperLimitNm).toFixed(1).padStart(5, '0');
            const loStr = Number(lowerLimitNm).toFixed(1).padStart(5, '0');
            const targetStr = ((Number(upperLimitNm) + Number(lowerLimitNm)) / 2).toFixed(1).padStart(5, '0');
            const tpl = this.activeProtocol?.syncLimitsCommandTemplate;
            if (this.activeProtocol && !tpl) return; // Protocol is read-only (e.g. Vernier)
            const cmd = tpl
                ? tpl.replace('{HIGH}', hiStr).replace('{LOW}', loStr).replace('{TARGET}', targetStr)
                : `AT037,${hiStr},${loStr}\r\n`;

            const encoder = new TextEncoder();
            await this.rxChar.writeValueWithResponse(encoder.encode(cmd));
            console.log(`[Tohnichi BLE] Limits sent to wrench: ${cmd.trim()}`);
            return true;
        } catch (err) {
            console.error("[Tohnichi BLE] Failed to write limits to wrench:", err);
            return false;
        }
    }

    async loadProtocols() {
        try {
            const res = await fetch('/Device/ProtocolsJson');
            if (res.ok) {
                const data = await res.json();
                this.protocols = data.protocols || [];
                this.deviceMappings = data.deviceMappings || [];
            }
        } catch (err) {
            console.warn("[BLE Driver] Could not load dynamic protocols, using Tohnichi DF-3 fallback:", err);
        }
    }

    async bindProtocolGatt(device, server) {
        const devName = (device?.name || '').toLowerCase();
        const mapped = this.deviceMappings.find(m =>
            (m.name && devName && (devName === m.name.toLowerCase() || devName.includes(m.name.toLowerCase()))) ||
            (m.transmitterId && devName && devName.includes(m.transmitterId.toLowerCase()))
        );

        const candidates = [];
        if (mapped) {
            const mp = this.protocols.find(p => p.bleProtocolId === mapped.bleProtocolId);
            if (mp) candidates.push(mp);
        }
        for (const p of this.protocols) {
            if (!candidates.includes(p)) candidates.push(p);
        }

        for (const p of candidates) {
            try {
                const service = await server.getPrimaryService(p.serviceUuid.toLowerCase());
                const txChar = await service.getCharacteristic(p.txCharUuid.toLowerCase());
                let rxChar = null;
                if (p.rxCharUuid) {
                    try { rxChar = await service.getCharacteristic(p.rxCharUuid.toLowerCase()); } catch (e) { }
                }
                this.activeProtocol = p;
                console.log(`[BLE Driver] Bound protocol '${p.name}' (${p.serviceUuid})`);
                return { service, txChar, rxChar };
            } catch (e) { /* try next protocol */ }
        }

        // Fallback to built-in Tohnichi DF-3 UUIDs
        this.activeProtocol = null;
        const service = await server.getPrimaryService(this.SERVICE_UUID);
        const txChar = await service.getCharacteristic(this.TX_CHAR_UUID);
        let rxChar = null;
        try { rxChar = await service.getCharacteristic(this.RX_CHAR_UUID); } catch (e) { }
        return { service, txChar, rxChar };
    }

    /**
     * Handle incoming GATT notification from the wrench or caliper using active protocol mapping
     */
    handleNotification(event) {
        const dataView = event.target.value;

        // Binary/Hex protocol: parse immediately per packet
        if (this.activeProtocol && this.activeProtocol.frameFormat === 'HexBytes') {
            this.parseAndEmitFrame('', dataView, this.activeProtocol);
            return;
        }

        // ASCII protocols: buffer chunks so split/batched BLE notifications never drop a frame
        const chunk = new TextDecoder().decode(dataView);
        console.log("[Tohnichi BLE RX]:", chunk.trim());
        this._rxBuffer += chunk;

        if (this._rxFlushTimer) clearTimeout(this._rxFlushTimer);

        const lines = this._rxBuffer.split(/\r\n|\n|\r/);
        this._rxBuffer = lines.pop() ?? '';

        for (const line of lines) {
            const trimmed = line.trim();
            if (trimmed) this.parseAndEmitFrame(trimmed, dataView, this.activeProtocol);
        }

        // If a device sends frames without CR/LF, flush after 80ms
        if (this._rxBuffer.trim()) {
            this._rxFlushTimer = setTimeout(() => {
                const leftover = this._rxBuffer.trim();
                this._rxBuffer = '';
                if (leftover) this.parseAndEmitFrame(leftover, dataView, this.activeProtocol);
            }, 80);
        }
    }

    parseAndEmitFrame(trimmed, dataView, protocol) {
        if (protocol) {
            const p = protocol;
            if (p.ignorePrefix && trimmed.startsWith(p.ignorePrefix)) return;
            if (p.frameStartFilter && !trimmed.startsWith(p.frameStartFilter)) return;

            let extract = () => undefined;
            try {
                if (p.frameFormat === 'Delimited') {
                    const parts = trimmed.split(p.delimiter || ',');
                    extract = (spec) => (spec !== null && spec !== undefined && spec !== '') ? parts[parseInt(spec, 10)] : undefined;
                } else if (p.frameFormat === 'FixedPosition') {
                    extract = (spec) => {
                        if (!spec) return undefined;
                        const s = String(spec).trim();
                        const dash = s.indexOf('-');
                        if (dash > 0) {
                            const start = parseInt(s.slice(0, dash), 10);
                            const end = parseInt(s.slice(dash + 1), 10);
                            return (!isNaN(start) && !isNaN(end)) ? trimmed.slice(start - 1, end).trim() : undefined;
                        }
                        const pos = parseInt(s, 10);
                        return !isNaN(pos) ? trimmed.slice(pos - 1, pos).trim() : undefined;
                    };
                } else if (p.frameFormat === 'Regex' && p.regexPattern) {
                    const match = trimmed.match(new RegExp(p.regexPattern));
                    if (!match) return;
                    extract = (spec) => (spec !== null && spec !== undefined && spec !== '') ? match[parseInt(spec, 10)] : undefined;
                } else if (p.frameFormat === 'Json') {
                    const obj = JSON.parse(trimmed);
                    extract = (spec) => (spec !== null && spec !== undefined && spec !== '') ? spec.split('.').reduce((a, k) => a?.[k], obj) : undefined;
                } else if (p.frameFormat === 'HexBytes' && dataView) {
                    extract = (spec) => {
                        if (!spec) return undefined;
                        const s = String(spec).trim().toLowerCase();
                        const parts = s.split(':');
                        if (parts.length === 2) {
                            const type = parts[0];
                            const off = parseInt(parts[1], 10);
                            if (isNaN(off)) return undefined;
                            if (type === 'u8') return dataView.getUint8(off);
                            if (type === 'i8') return dataView.getInt8(off);
                            if (type === 'u16le') return dataView.getUint16(off, true);
                            if (type === 'u16be') return dataView.getUint16(off, false);
                            if (type === 'i16le') return dataView.getInt16(off, true);
                            if (type === 'i16be') return dataView.getInt16(off, false);
                            if (type === 'f32le') return dataView.getFloat32(off, true);
                            if (type === 'f32be') return dataView.getFloat32(off, false);
                        }
                        const idx = parseInt(s, 10);
                        return (!isNaN(idx) && idx < dataView.byteLength) ? dataView.getUint8(idx) : undefined;
                    };
                }

                const rawVal = extract(p.valueField);
                const val = parseFloat(rawVal) * (p.valueMultiplier || 1.0);
                if (isNaN(val)) return;

                const judgment = extract(p.judgmentField) || '';
                const dblToken = p.doubleTighteningValue || '';
                const passToken = p.passValue || '';

                const reading = {
                    rawFrame: trimmed,
                    counter: parseInt(extract(p.counterField), 10) || 1,
                    torque: Number(val.toFixed(4)),
                    unit: extract(p.unitField) || '',
                    angle: parseFloat(extract(p.angleField)) || 0,
                    judgment: judgment,
                    toolSerial: extract(p.toolSerialField) || this.toolSerial || 'BLE-TOOL',
                    isDoubleTightening: !!(dblToken && judgment && String(judgment).includes(dblToken)),
                    isPass: !!(passToken && judgment === passToken)
                };

                if (typeof this.onReadingReceived === 'function') {
                    this.onReadingReceived(reading);
                }
                return;
            } catch (err) {
                console.warn(`[BLE Driver] Failed parsing frame with protocol '${p.name}':`, err);
                return;
            }
        }

        // Fallback Tohnichi DF-3 / generic numeric parser:
        if (trimmed.startsWith("RE003")) {
            console.log("[Tohnichi BLE] Tool acknowledged command: OK");
            return;
        }

        if (trimmed.startsWith("RE,")) {
            const parts = trimmed.split(',');
            if (parts.length >= 7) {
                const reading = {
                    rawFrame: trimmed,
                    counter: parseInt(parts[1], 10),
                    torque: parseFloat(parts[2]),
                    unit: parts[3],
                    angle: parseFloat(parts[4]),
                    angleUnit: parts[5],
                    judgment: parts[6],
                    toolSerial: parts[7] || this.toolSerial || "CEM3-BLE",
                    isDoubleTightening: parts[6].includes("D"),
                    isPass: parts[6] === "OO"
                };

                if (typeof this.onReadingReceived === 'function') {
                    this.onReadingReceived(reading);
                }
            }
            return;
        }

        // Generic ASCII numeric fallback (e.g. Mitutoyo DT1 or plain "+25.42 mm")
        const genericMatch = trimmed.match(/([+-]?\d+(?:\.\d+)?)/);
        if (genericMatch) {
            const val = parseFloat(genericMatch[1]);
            if (!isNaN(val) && typeof this.onReadingReceived === 'function') {
                this.onReadingReceived({
                    rawFrame: trimmed,
                    counter: 1,
                    torque: val,
                    unit: trimmed.toLowerCase().includes('mm') ? 'mm' : '',
                    angle: 0,
                    judgment: '',
                    toolSerial: this.toolSerial || 'UNIVERSAL-TOOL',
                    isDoubleTightening: false,
                    isPass: false
                });
            }
        }
    }

    /**
     * Connect to any Virtual COM / USB-Serial / Classic Bluetooth SPP tool via Web Serial API
     * (e.g. Mitutoyo U-WAVEPAK-BW VCP, Tohnichi BC SPP, RS-232 adapters)
     */
    async connectSerial(baudRate = 9600) {
        if (!('serial' in navigator)) {
            throw new Error("Web Serial API is not supported in this browser. Use Chrome or Edge.");
        }
        const port = await navigator.serial.requestPort();
        await port.open({ baudRate });
        this.serialPort = port;
        this.isSerialConnected = true;
        this.toolSerial = this.toolSerial || 'SERIAL-COM-TOOL';

        if (this.protocols.length === 0) await this.loadProtocols();

        const decoder = new TextDecoderStream();
        port.readable.pipeTo(decoder.writable).catch(() => {});
        this.serialReader = decoder.readable.getReader();

        let serialBuf = '';
        (async () => {
            try {
                while (this.isSerialConnected) {
                    const { value, done } = await this.serialReader.read();
                    if (done) break;
                    if (!value) continue;
                    serialBuf += value;
                    const lines = serialBuf.split(/\r\n|\n|\r/);
                    serialBuf = lines.pop() ?? '';
                    for (const line of lines) {
                        const trimmed = line.trim();
                        if (!trimmed) continue;
                        // Match first protocol whose FrameStartFilter matches, or use universal fallback
                        const matchedProto = this.protocols.find(p => p.frameStartFilter && trimmed.startsWith(p.frameStartFilter)) || null;
                        this.parseAndEmitFrame(trimmed, null, matchedProto);
                    }
                }
            } catch (err) {
                console.warn("[Serial Driver] Stream ended:", err);
            } finally {
                this.isSerialConnected = false;
            }
        })();

        return true;
    }

    async disconnectSerial() {
        this.isSerialConnected = false;
        try { await this.serialReader?.cancel(); } catch (e) {}
        try { await this.serialPort?.close(); } catch (e) {}
        this.serialPort = null;
        this.serialReader = null;
    }

    /**
     * Internal status badge updater
     */
    updateStatus(message, variant) {
        if (typeof this.onStatusChange === 'function') {
            this.onStatusChange(message, variant);
        }
    }
}

// Global Singleton Instance (Preserves live connection across page interactions)
window.tohnichiBle = window.tohnichiBle || new TohnichiBleDriver();
