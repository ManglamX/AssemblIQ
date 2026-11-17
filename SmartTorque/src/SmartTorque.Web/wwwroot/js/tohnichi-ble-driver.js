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

        // Callback hooks
        this.onReadingReceived = null;
        this.onStatusChange = null;
    }

    /**
     * Check if Web Bluetooth is available in this browser
     */
    isSupported() {
        return !!(window.isSecureContext && navigator && navigator.bluetooth);
    }

    /**
     * Connect to the Tohnichi CEM3-G-BT wrench or ESP32 BLE Simulator
     */
    async connect() {
        if (!navigator.bluetooth) {
            throw new Error("Web Bluetooth API is not available in this browser. Please use Google Chrome or Microsoft Edge.");
        }

        try {
            this.updateStatus("Connecting...", "warning");

            // Request nearby Bluetooth devices advertising CEM3 prefix or matching Service UUID
            this.device = await navigator.bluetooth.requestDevice({
                filters: [
                    { namePrefix: 'CEM3' }
                ],
                optionalServices: [this.SERVICE_UUID]
            });

            this.toolSerial = this.device.name ? this.device.name.replace('CEM3-G-BT_', '').replace('_BLE', '') : '704620P';

            this.device.addEventListener('gattserverdisconnected', () => {
                this.isConnected = false;
                this.updateStatus("Disconnected", "secondary");
                console.warn("[Tohnichi BLE] Wrench disconnected.");
            });

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
            this.updateStatus(`Connected: ${this.device.name}`, "success");
            console.log(`[Tohnichi BLE] Successfully connected to ${this.device.name}!`);

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
        if (!this.isSupported() || !navigator.bluetooth.getDevices) return false;
        try {
            const devices = await navigator.bluetooth.getDevices();
            const targetName = localStorage.getItem('assembl_active_ble_tool');
            const targetSerial = localStorage.getItem('assembl_active_ble_serial');
            let cemDevice = devices.find(d => 
                (targetName && d.name === targetName) ||
                (targetSerial && d.name && d.name.includes(targetSerial)) ||
                (d.name && (d.name.startsWith('CEM3') || d.name.includes('BLE') || d.name.toLowerCase().includes('tohnichi')))
            );

            if (!cemDevice && devices.length > 0) {
                cemDevice = devices[0];
            }
            if (!cemDevice) return false;

            console.log("[Tohnichi BLE] Auto-reconnecting to authorized device:", cemDevice.name);
            this.device = cemDevice;
            this.toolSerial = this.device.name ? this.device.name.replace('CEM3-G-BT_', '').replace('_BLE', '') : '704620P';

            this.device.addEventListener('gattserverdisconnected', () => {
                this.isConnected = false;
                this.updateStatus("Disconnected", "secondary");
                console.warn("[Tohnichi BLE] Wrench disconnected.");
            });

            // Allow brief grace period for OS BLE stack to release previous page GATT handle
            for (let attempt = 1; attempt <= 3; attempt++) {
                try {
                    this.server = await this.device.gatt.connect();
                    break;
                } catch (connErr) {
                    if (attempt === 3) throw connErr;
                    console.log(`[Tohnichi BLE] Auto-reconnect attempt ${attempt} failed, retrying in 400ms...`);
                    await new Promise(r => setTimeout(r, 400));
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
            return true;
        } catch (err) {
            console.warn("[Tohnichi BLE] Auto-reconnect failed (device will require manual click if needed):", err);
            return false;
        }
    }

    /**
     * Disconnect cleanly from the tool
     */
    disconnect() {
        if (this.device && this.device.gatt.connected) {
            this.device.gatt.disconnect();
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

// Global Singleton Instance
window.tohnichiBle = new TohnichiBleDriver();
