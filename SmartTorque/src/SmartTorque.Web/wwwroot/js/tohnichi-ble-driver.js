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
        if (!navigator.bluetooth) {
            throw new Error("Web Bluetooth API is not available in this browser. Please use Google Chrome or Microsoft Edge.");
        }

        this.isUserInitiatedDisconnect = false;
        if (this.reconnectTimer) clearTimeout(this.reconnectTimer);

        try {
            this.updateStatus("Connecting...", "primary");

            // Request nearby Bluetooth devices advertising CEM3 prefix, ESP, Tohnichi, or matching Service UUID
            this.device = await navigator.bluetooth.requestDevice({
                filters: [
                    { namePrefix: 'CEM3' },
                    { namePrefix: 'Tohnichi' },
                    { namePrefix: 'ESP' },
                    { namePrefix: 'esp' },
                    { namePrefix: 'AssemblIQ' },
                    { services: [this.SERVICE_UUID] }
                ],
                optionalServices: [this.SERVICE_UUID]
            });

            this.toolSerial = this.device.name ? this.device.name.replace('CEM3-G-BT_', '').replace('_BLE', '') : '704620P';
            this.bindDisconnectListener(this.device);

            // Connect to GATT Server
            this.server = await this.device.gatt.connect();
            this.service = await this.server.getPrimaryService(this.SERVICE_UUID);

            // Get Transmit Characteristic (Notifications)
            this.txChar = await this.service.getCharacteristic(this.TX_CHAR_UUID);
            await this.txChar.startNotifications();
            this.txChar.addEventListener('characteristicvaluechanged', (e) => this.handleNotification(e));

            // Get Receive Characteristic (Commands)
            try {
                this.rxChar = await this.service.getCharacteristic(this.RX_CHAR_UUID);
            } catch (err) {
                console.warn("[Tohnichi BLE] Rx Characteristic write not available on this tool:", err);
            }

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

            this.service = await this.server.getPrimaryService(this.SERVICE_UUID);

            this.txChar = await this.service.getCharacteristic(this.TX_CHAR_UUID);
            await this.txChar.startNotifications();
            this.txChar.addEventListener('characteristicvaluechanged', (e) => this.handleNotification(e));

            try {
                this.rxChar = await this.service.getCharacteristic(this.RX_CHAR_UUID);
            } catch (err) {}

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
            const cmd = `AT037,${hiStr},${loStr}\r\n`;

            const encoder = new TextEncoder();
            await this.rxChar.writeValueWithResponse(encoder.encode(cmd));
            console.log(`[Tohnichi BLE] Limits sent to wrench: ${cmd.trim()}`);
            return true;
        } catch (err) {
            console.error("[Tohnichi BLE] Failed to write limits to wrench:", err);
            return false;
        }
    }

    /**
     * Handle incoming GATT notification from the wrench
     */
    handleNotification(event) {
        const raw = new TextDecoder().decode(event.target.value);
        console.log("[Tohnichi BLE RX]:", raw.trim());

        // Ignore command confirmations (RE003,OK)
        if (raw.startsWith("RE003")) {
            console.log("[Tohnichi BLE] Tool acknowledged command: OK");
            return;
        }

        // Parse DF-3 Telemetry Frame:
        // RE,001,+024.8,nm,+092,deg,OO,704620P,26/11/16,22:00:00\r\n
        if (raw.startsWith("RE,")) {
            const parts = raw.trim().split(',');
            if (parts.length >= 7) {
                const reading = {
                    rawFrame: raw.trim(),
                    counter: parseInt(parts[1], 10),
                    torque: parseFloat(parts[2]),
                    unit: parts[3],
                    angle: parseFloat(parts[4]),
                    angleUnit: parts[5],
                    judgment: parts[6], // "OO" = Pass, "HO" = High, "LO" = Low, "OD" = Double-tightening
                    toolSerial: parts[7] || this.toolSerial || "CEM3-BLE",
                    isDoubleTightening: parts[6].includes("D"),
                    isPass: parts[6] === "OO"
                };

                if (typeof this.onReadingReceived === 'function') {
                    this.onReadingReceived(reading);
                }
            }
        }
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
