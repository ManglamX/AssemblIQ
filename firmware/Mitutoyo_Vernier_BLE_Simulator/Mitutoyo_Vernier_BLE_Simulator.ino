/*
  =============================================================================
  AssemblIQ - Mitutoyo U-WAVE Vernier Caliper BLE & HTTP Webpage Simulator
  =============================================================================
  This firmware emulates a Mitutoyo U-WAVE Digital Vernier Caliper over
  Bluetooth Low Energy (BLE GATT) AND hosts a built-in HTTP Web Page.

  NO SERIAL MONITOR REQUIRED!
  Open http://<esp32-ip>/ or http://192.168.4.1/ in any browser on your phone,
  tablet, or PC, and click buttons to trigger real-time mm measurements.

  Matches AssemblIQ's built-in "Mitutoyo U-WAVE (DT1 ASCII / COM & BLE)" profile:
    - Service UUID : 0000ffe0-0000-1000-8000-00805f9b34fb
    - TX Notify UUID: 0000ffe1-0000-1000-8000-00805f9b34fb
    - Frame Format : DT10102+00000025.00M\r\n  (Value at positions 8-19)
  =============================================================================
*/

#include <WiFi.h>
#include <WebServer.h>
#include <BLEDevice.h>
#include <BLEServer.h>
#include <BLEUtils.h>
#include <BLE2902.h>

// =============================================================================
// User Configuration
// =============================================================================
const char* WIFI_SSID     = "";      // e.g. "Factory_WiFi" (blank = SoftAP)
const char* WIFI_PASSWORD = "";

// SoftAP Hotspot Settings (Used if WiFi fails or credentials are blank)
const char* AP_SSID       = "AssemblIQ-Vernier";
const char* AP_PASSWORD   = "12345678";

#define RECEIVER_ID       "01"
#define TOOL_SERIAL_ID    "02"
#define BLE_DEVICE_NAME   "U-WAVE_VERNIER_" TOOL_SERIAL_ID
#define BOOT_BUTTON_PIN   0          // Physical BOOT button (active LOW)
#define STATUS_LED_PIN    2          // Built-in status LED

// BLE GATT UUIDs matching AssemblIQ Vernier / Mitutoyo U-WAVE profile
#define SERVICE_UUID      "0000ffe0-0000-1000-8000-00805f9b34fb"
#define TX_CHAR_UUID      "0000ffe1-0000-1000-8000-00805f9b34fb" // Notify (Caliper -> Tablet)
#define RX_CHAR_UUID      "0000ffe2-0000-1000-8000-00805f9b34fb" // Optional Write (Tablet -> Caliper)

// =============================================================================
// Global State
// =============================================================================
WebServer webServer(80);
BLEServer* pServer = nullptr;
BLECharacteristic* pTxCharacteristic = nullptr;
BLECharacteristic* pRxCharacteristic = nullptr;

volatile bool deviceConnected = false;
bool oldDeviceConnected = false;

// Caliper Parameters (mm)
float targetMm = 25.00f;
float lowerLimitMm = 23.00f;
float upperLimitMm = 27.00f;
uint16_t measurementCounter = 1;
String lastTransmittedFrame = "None";
String clientIpAddress = "192.168.4.1";

// Forward Declarations
void sendMitutoyoDt1Frame(float valueMm);
void simulateMeasure(char mode, float customVal = 0.0f);
void parseIncomingCommand(const String& cmd);

// =============================================================================
// BLE Server Callbacks
// =============================================================================
class ServerCallbacks : public BLEServerCallbacks {
    void onConnect(BLEServer* pServer) override {
        deviceConnected = true;
        digitalWrite(STATUS_LED_PIN, HIGH);
        Serial.println("\n[BLE] *** Tablet Connected to Vernier Caliper! ***");
    }

    void onDisconnect(BLEServer* pServer) override {
        deviceConnected = false;
        digitalWrite(STATUS_LED_PIN, LOW);
        pServer->startAdvertising();
        Serial.println("\n[BLE] *** Tablet Disconnected. Advertising restarted... ***");
    }
};

// =============================================================================
// BLE Characteristic Rx Callbacks (Optional Limit Sync from AssemblIQ)
// =============================================================================
class RxCallbacks : public BLECharacteristicCallbacks {
    void onWrite(BLECharacteristic* pCharacteristic) override {
        String rxValue = pCharacteristic->getValue().c_str();
        rxValue.trim();
        if (rxValue.length() > 0) {
            Serial.printf("[BLE RX] Command received from Tablet: \"%s\"\n", rxValue.c_str());
            parseIncomingCommand(rxValue);
        }
    }
};

void parseIncomingCommand(const String& cmd) {
    if (cmd.startsWith("AT037")) {
        int firstComma = cmd.indexOf(',');
        int secondComma = cmd.indexOf(',', firstComma + 1);
        if (firstComma != -1 && secondComma != -1) {
            upperLimitMm = cmd.substring(firstComma + 1, secondComma).toFloat();
            lowerLimitMm = cmd.substring(secondComma + 1).toFloat();
            targetMm = (upperLimitMm + lowerLimitMm) / 2.0f;
            Serial.printf("[CMD] Limits Synced: Target=%.2f mm (Lo=%.2f, Hi=%.2f)\n",
                          targetMm, lowerLimitMm, upperLimitMm);
        }
    }
}

// =============================================================================
// Send Mitutoyo U-WAVE DT1 Telemetry Frame over BLE
// Format: DT10102+00000025.00M\r\n  (Positions 8-19 = value)
// =============================================================================
void sendMitutoyoDt1Frame(float valueMm) {
    if (!deviceConnected) {
        Serial.println("[WARN] Telemetry not sent: Tablet not connected via BLE.");
        lastTransmittedFrame = "[Failed: Tablet not connected via BLE]";
        return;
    }

    char frameBuffer[40];
    snprintf(frameBuffer, sizeof(frameBuffer),
             "DT1%s%s%+012.2fM\r\n",
             RECEIVER_ID,
             TOOL_SERIAL_ID,
             valueMm);

    measurementCounter++;
    lastTransmittedFrame = String(frameBuffer);
    lastTransmittedFrame.trim();

    pTxCharacteristic->setValue(frameBuffer);
    pTxCharacteristic->notify();
    delay(40);

    Serial.printf("[BLE TX] Sent DT1 Frame: %s", frameBuffer);
}

// =============================================================================
// Simulation Logic
// =============================================================================
void simulateMeasure(char mode, float customVal) {
    float val = targetMm;

    if (mode == 't') {
        // Pass with natural variance (+/- 0.05 mm)
        float jitter = ((random(0, 11) - 5) * 0.01f);
        val = targetMm + jitter;
    }
    else if (mode == 'h') {
        // Over-Size (NG High)
        val = upperLimitMm + 2.50f;
    }
    else if (mode == 'l') {
        // Under-Size (NG Low)
        val = lowerLimitMm - 2.50f;
    }
    else if (mode == 'c') {
        val = customVal;
    }

    sendMitutoyoDt1Frame(val);
}

// =============================================================================
// HTTP Webpage Interface (Mobile-Friendly Dark Industrial UI)
// =============================================================================
const char INDEX_HTML[] PROGMEM = R"rawliteral(
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Mitutoyo Vernier BLE Simulator</title>
    <style>
        :root {
            --bg-dark: #0f172a;
            --card-bg: #1e293b;
            --accent: #0284c7;
            --text-main: #f8fafc;
            --text-sub: #94a3b8;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; }
        body { background: var(--bg-dark); color: var(--text-main); display: flex; justify-content: center; min-height: 100vh; padding: 16px; }
        .container { width: 100%; max-width: 480px; }
        .header { text-align: center; margin-bottom: 20px; padding: 12px; background: var(--card-bg); border-radius: 12px; border: 1px solid #334155; }
        .title { font-size: 1.25rem; font-weight: 700; color: #38bdf8; letter-spacing: 0.5px; }
        .subtitle { font-size: 0.8rem; color: var(--text-sub); margin-top: 4px; }
        .status-card { background: var(--card-bg); border-radius: 12px; padding: 16px; margin-bottom: 16px; border: 1px solid #334155; }
        .badge { display: inline-block; padding: 4px 10px; border-radius: 9999px; font-size: 0.75rem; font-weight: 700; text-transform: uppercase; }
        .badge-connected { background: rgba(16, 185, 129, 0.2); color: #34d399; border: 1px solid #10b981; }
        .badge-disconnected { background: rgba(245, 158, 11, 0.2); color: #fbbf24; border: 1px solid #f59e0b; }
        .limits-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px; margin-top: 14px; text-align: center; }
        .limit-box { background: #0f172a; padding: 10px 4px; border-radius: 8px; border: 1px solid #334155; }
        .limit-label { font-size: 0.7rem; color: var(--text-sub); text-transform: uppercase; }
        .limit-val { font-size: 1.1rem; font-weight: 700; margin-top: 2px; }
        .val-target { color: #38bdf8; }
        .val-lo { color: #34d399; }
        .val-hi { color: #f87171; }
        .btn-grid { display: grid; grid-template-columns: 1fr; gap: 12px; margin-bottom: 16px; }
        .btn { border: none; padding: 14px 16px; border-radius: 10px; font-size: 1rem; font-weight: 700; cursor: pointer; display: flex; align-items: center; justify-content: space-between; transition: transform 0.1s, opacity 0.2s; -webkit-tap-highlight-color: transparent; }
        .btn:active { transform: scale(0.98); opacity: 0.9; }
        .btn-pass { background: linear-gradient(135deg, #059669 0%, #10b981 100%); color: white; }
        .btn-over { background: linear-gradient(135deg, #dc2626 0%, #ef4444 100%); color: white; }
        .btn-under { background: linear-gradient(135deg, #d97706 0%, #f59e0b 100%); color: white; }
        .custom-card { background: var(--card-bg); border-radius: 12px; padding: 16px; border: 1px solid #334155; margin-bottom: 16px; }
        .custom-form { display: flex; gap: 8px; margin-top: 8px; }
        .custom-input { flex: 1; padding: 12px; border-radius: 8px; border: 1px solid #475569; background: #0f172a; color: white; font-size: 1.1rem; font-weight: bold; text-align: center; }
        .btn-custom { background: var(--accent); color: white; padding: 0 20px; font-size: 0.95rem; }
        .log-box { background: #0f172a; border-radius: 8px; padding: 10px; font-family: monospace; font-size: 0.75rem; color: #a5f3fc; border: 1px solid #334155; word-break: break-all; min-height: 48px; }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <div class="title">📏 MITUTOYO U-WAVE VERNIER</div>
            <div class="subtitle">Wireless Caliper Webpage Controller (mm)</div>
        </div>

        <div class="status-card">
            <div style="display: flex; justify-content: space-between; align-items: center;">
                <span style="font-size: 0.85rem; color: var(--text-sub);">BLE Connection:</span>
                <span id="bleStatus" class="badge badge-disconnected">Checking...</span>
            </div>
            <div class="limits-grid">
                <div class="limit-box">
                    <div class="limit-label">Lower Limit</div>
                    <div class="limit-val val-lo" id="valLo">--.--</div>
                </div>
                <div class="limit-box">
                    <div class="limit-label">Target</div>
                    <div class="limit-val val-target" id="valTarget">--.--</div>
                </div>
                <div class="limit-box">
                    <div class="limit-label">Upper Limit</div>
                    <div class="limit-val val-hi" id="valHi">--.--</div>
                </div>
            </div>
        </div>

        <div class="btn-grid">
            <button class="btn btn-pass" onclick="sendMeasure('t')">
                <span>🟢 PASS Measurement</span>
                <span id="btnPassMm" style="font-size: 0.85rem; opacity: 0.85;">Target mm</span>
            </button>
            <button class="btn btn-over" onclick="sendMeasure('h')">
                <span>🔴 Over-Size (NG High)</span>
                <span style="font-size: 0.85rem; opacity: 0.85;">+2.50 mm</span>
            </button>
            <button class="btn btn-under" onclick="sendMeasure('l')">
                <span>🟡 Under-Size (NG Low)</span>
                <span style="font-size: 0.85rem; opacity: 0.85;">-2.50 mm</span>
            </button>
        </div>

        <div class="custom-card">
            <div style="font-size: 0.85rem; font-weight: 600; color: var(--text-sub);">Send Exact Measurement (mm):</div>
            <div class="custom-form">
                <input type="number" step="0.01" id="customInput" class="custom-input" placeholder="e.g. 25.00">
                <button class="btn btn-custom" onclick="sendCustom()">Send</button>
            </div>
        </div>

        <div class="status-card">
            <div style="font-size: 0.8rem; color: var(--text-sub); margin-bottom: 6px;">Last Transmitted DT1 Frame:</div>
            <div class="log-box" id="logBox">Ready for measurement...</div>
        </div>
    </div>

    <script>
        async function fetchStatus() {
            try {
                const res = await fetch('/status');
                const data = await res.json();
                const badge = document.getElementById('bleStatus');
                if (data.connected) {
                    badge.className = 'badge badge-connected';
                    badge.textContent = 'TABLET CONNECTED';
                } else {
                    badge.className = 'badge badge-disconnected';
                    badge.textContent = 'READY TO PAIR (BLE)';
                }
                document.getElementById('valTarget').textContent = data.target.toFixed(2) + ' mm';
                document.getElementById('valLo').textContent = data.lower.toFixed(2) + ' mm';
                document.getElementById('valHi').textContent = data.upper.toFixed(2) + ' mm';
                document.getElementById('btnPassMm').textContent = data.target.toFixed(2) + ' mm';
                if (data.lastFrame && data.lastFrame !== 'None') {
                    document.getElementById('logBox').textContent = data.lastFrame;
                }
            } catch (e) { }
        }

        async function sendMeasure(mode) {
            try {
                const res = await fetch('/measure?mode=' + mode);
                const data = await res.json();
                document.getElementById('logBox').textContent = data.frame || 'Reading sent!';
                fetchStatus();
            } catch (e) {
                alert('Error sending command to ESP32');
            }
        }

        async function sendCustom() {
            const val = parseFloat(document.getElementById('customInput').value);
            if (isNaN(val)) {
                alert('Please enter a valid mm value');
                return;
            }
            try {
                const res = await fetch('/measure?mode=c&val=' + val);
                const data = await res.json();
                document.getElementById('logBox').textContent = data.frame || 'Custom reading sent!';
                fetchStatus();
            } catch (e) {
                alert('Error sending custom value');
            }
        }

        setInterval(fetchStatus, 1500);
        fetchStatus();
    </script>
</body>
</html>
)rawliteral";

// =============================================================================
// Web Server Route Handlers
// =============================================================================
void handleRoot() {
    webServer.send_P(200, "text/html", INDEX_HTML);
}

void handleStatus() {
    String json = "{";
    json += "\"connected\":" + String(deviceConnected ? "true" : "false") + ",";
    json += "\"target\":" + String(targetMm, 2) + ",";
    json += "\"lower\":" + String(lowerLimitMm, 2) + ",";
    json += "\"upper\":" + String(upperLimitMm, 2) + ",";
    json += "\"counter\":" + String(measurementCounter) + ",";
    json += "\"lastFrame\":\"" + lastTransmittedFrame + "\"";
    json += "}";
    webServer.send(200, "application/json", json);
}

void handleMeasure() {
    if (!webServer.hasArg("mode")) {
        webServer.send(400, "application/json", "{\"error\":\"Missing mode param\"}");
        return;
    }

    String modeStr = webServer.arg("mode");
    char mode = modeStr.charAt(0);
    float customVal = 0.0f;
    if (webServer.hasArg("val")) {
        customVal = webServer.arg("val").toFloat();
    }

    simulateMeasure(mode, customVal);

    String json = "{";
    json += "\"success\":true,";
    json += "\"frame\":\"" + lastTransmittedFrame + "\"";
    json += "}";
    webServer.send(200, "application/json", json);
}

// =============================================================================
// Arduino Setup
// =============================================================================
void setup() {
    Serial.begin(115200);
    delay(500);

    pinMode(BOOT_BUTTON_PIN, INPUT_PULLUP);
    pinMode(STATUS_LED_PIN, OUTPUT);
    digitalWrite(STATUS_LED_PIN, LOW);

    Serial.println("\n=======================================================");
    Serial.println("  AssemblIQ - Mitutoyo Vernier BLE & Webpage Simulator");
    Serial.println("=======================================================");

    bool wifiConnected = false;
    if (strlen(WIFI_SSID) > 0) {
        Serial.printf("[WIFI] Connecting to '%s'...\n", WIFI_SSID);
        WiFi.mode(WIFI_STA);
        WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
        unsigned long startAttempt = millis();
        while (WiFi.status() != WL_CONNECTED && millis() - startAttempt < 8000) {
            delay(300);
            Serial.print(".");
        }
        if (WiFi.status() == WL_CONNECTED) {
            wifiConnected = true;
            clientIpAddress = WiFi.localIP().toString();
            Serial.printf("\n[WIFI] Connected! Webpage Simulator IP: http://%s/\n", clientIpAddress.c_str());
        }
    }

    if (!wifiConnected) {
        WiFi.mode(WIFI_AP);
        WiFi.softAP(AP_SSID, AP_PASSWORD);
        clientIpAddress = WiFi.softAPIP().toString();
        Serial.printf("\n[WIFI] SoftAP Active! Connect to '%s' (PW: %s)\n", AP_SSID, AP_PASSWORD);
        Serial.printf("[WIFI] Webpage Simulator URL: http://%s/\n", clientIpAddress.c_str());
    }

    webServer.on("/", HTTP_GET, handleRoot);
    webServer.on("/status", HTTP_GET, handleStatus);
    webServer.on("/measure", HTTP_GET, handleMeasure);
    webServer.begin();
    Serial.println("[HTTP] Web Server started on port 80");

    BLEDevice::init(BLE_DEVICE_NAME);
    BLEDevice::setMTU(517);

    pServer = BLEDevice::createServer();
    pServer->setCallbacks(new ServerCallbacks());

    BLEService* pService = pServer->createService(SERVICE_UUID);

    pTxCharacteristic = pService->createCharacteristic(
        TX_CHAR_UUID,
        BLECharacteristic::PROPERTY_NOTIFY | BLECharacteristic::PROPERTY_READ
    );
    pTxCharacteristic->addDescriptor(new BLE2902());

    pRxCharacteristic = pService->createCharacteristic(
        RX_CHAR_UUID,
        BLECharacteristic::PROPERTY_WRITE | BLECharacteristic::PROPERTY_WRITE_NR
    );
    pRxCharacteristic->setCallbacks(new RxCallbacks());

    pService->start();

    BLEAdvertising* pAdvertising = BLEDevice::getAdvertising();
    pAdvertising->addServiceUUID(SERVICE_UUID);
    pAdvertising->setScanResponse(true);
    pAdvertising->setMinPreferred(0x06);
    pAdvertising->setMaxPreferred(0x12);
    BLEDevice::startAdvertising();

    Serial.printf("[BLE] Advertising '%s'...\n", BLE_DEVICE_NAME);
    Serial.println("=======================================================\n");
}

// =============================================================================
// Main Loop
// =============================================================================
void loop() {
    webServer.handleClient();

    if (!deviceConnected && oldDeviceConnected) {
        delay(300);
        pServer->startAdvertising();
        Serial.println("[BLE] Resumed advertising for AssemblIQ...");
        oldDeviceConnected = deviceConnected;
    }
    if (deviceConnected && !oldDeviceConnected) {
        oldDeviceConnected = deviceConnected;
    }

    static bool lastBtnState = HIGH;
    bool currentBtnState = digitalRead(BOOT_BUTTON_PIN);
    if (lastBtnState == HIGH && currentBtnState == LOW) {
        delay(50);
        if (digitalRead(BOOT_BUTTON_PIN) == LOW) {
            simulateMeasure('t');
        }
    }
    lastBtnState = currentBtnState;

    delay(5);
}
