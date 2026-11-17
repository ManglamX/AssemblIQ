/*
  =============================================================================
  AssemblIQ - Tohnichi CEM3-G-BT BLE & HTTP Webpage Torque Wrench Simulator
  =============================================================================
  This firmware emulates an authentic Tohnichi CEM3-G-BT wireless digital torque
  wrench over Bluetooth Low Energy (BLE GATT) AND hosts a built-in HTTP Web Page.

  NO SERIAL MONITOR REQUIRED!
  Open http://<esp32-ip>/ or http://192.168.4.1/ in any browser on your phone,
  tablet, or PC, and click buttons to trigger real-time tightening readings.

  Features:
  1. BLE GATT Server matching physical Tohnichi CEM3-G-BT specifications.
  2. Built-in HTTP Web Server: Trigger Pass, High, Low, Double-Hit & Custom torque.
  3. WiFi Station + Fallback SoftAP Hotspot ("AssemblIQ-Wrench" / "12345678").
  4. Real-time AT037 command synchronization: AssemblIQ syncs target limits live!
  5. Enhanced BLE stability with MTU 517 & zero-disconnect connection parameters.
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
// Enter your plant / home WiFi credentials (or leave blank to use ESP32 SoftAP Hotspot)
const char* WIFI_SSID     = "";      // e.g. "Factory_WiFi"
const char* WIFI_PASSWORD = "";      // e.g. "Factory@123"

// SoftAP Hotspot Settings (Used if WiFi fails or credentials are blank)
const char* AP_SSID       = "AssemblIQ-Wrench";
const char* AP_PASSWORD   = "12345678";

#define TOOL_SERIAL_NO    "704620P"
#define BLE_DEVICE_NAME   "CEM3-G-BT_" TOOL_SERIAL_NO "_BLE"
#define BOOT_BUTTON_PIN   0          // Physical BOOT button (active LOW)
#define STATUS_LED_PIN    2          // Built-in status LED

// Tohnichi CEM3-G-BT BLE GATT UUIDs
#define SERVICE_UUID      "55df0001-a9b0-11e3-a5e2-000190f08f1e"
#define TX_CHAR_UUID      "55df0002-a9b0-11e3-a5e2-000190f08f1e" // Notify (Tool -> Host)
#define RX_CHAR_UUID      "55df0003-a9b0-11e3-a5e2-000190f08f1e" // Write (Host -> Tool)

// =============================================================================
// Global State
// =============================================================================
WebServer webServer(80);
BLEServer* pServer = nullptr;
BLECharacteristic* pTxCharacteristic = nullptr;
BLECharacteristic* pRxCharacteristic = nullptr;

volatile bool deviceConnected = false;
bool oldDeviceConnected = false;

// Tool Parameters (Updated dynamically by AssemblIQ via AT037 commands)
float targetTorque = 25.0f;
float lowerLimitTorque = 23.0f;
float upperLimitTorque = 27.0f;
uint16_t measurementCounter = 1;
String lastTransmittedFrame = "None";
String clientIpAddress = "192.168.4.1";

// Forward Declarations
void sendTohnichiDf3Frame(float torque, float angle, const char* judgment);
void simulateTighten(char mode, float customVal = 0.0f);
void parseIncomingCommand(const String& cmd);

// =============================================================================
// BLE Server Callbacks
// =============================================================================
class ServerCallbacks : public BLEServerCallbacks {
    void onConnect(BLEServer* pServer) override {
        deviceConnected = true;
        digitalWrite(STATUS_LED_PIN, HIGH);
        Serial.println("\n[BLE] *** Tablet Connected to CEM3-G-BT Wrench! ***");
    }

    void onDisconnect(BLEServer* pServer) override {
        deviceConnected = false;
        digitalWrite(STATUS_LED_PIN, LOW);
        pServer->startAdvertising();
        Serial.println("\n[BLE] *** Tablet Disconnected. Immediate advertising restarted... ***");
    }
};

// =============================================================================
// BLE Characteristic Rx Callbacks (Commands from AssemblIQ)
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

// =============================================================================
// Tohnichi AT Command Parser
// =============================================================================
void parseIncomingCommand(const String& cmd) {
    // AT037,UPPER,LOWER (Set torque limits from AssemblIQ)
    if (cmd.startsWith("AT037")) {
        int firstComma = cmd.indexOf(',');
        int secondComma = cmd.indexOf(',', firstComma + 1);
        if (firstComma != -1 && secondComma != -1) {
            String upperStr = cmd.substring(firstComma + 1, secondComma);
            String lowerStr = cmd.substring(secondComma + 1);
            upperLimitTorque = upperStr.toFloat();
            lowerLimitTorque = lowerStr.toFloat();
            targetTorque = (upperLimitTorque + lowerLimitTorque) / 2.0f;

            Serial.printf("[CMD] Limits Synchronized: Target=%.1f Nm (Lo=%.1f, Hi=%.1f)\n",
                          targetTorque, lowerLimitTorque, upperLimitTorque);

            String response = "RE003,OK\r\n";
            pTxCharacteristic->setValue(response.c_str());
            pTxCharacteristic->notify();
            delay(30);
            return;
        }
    }

    // Default acknowledgement for AT commands
    String response = "RE003,OK\r\n";
    pTxCharacteristic->setValue(response.c_str());
    pTxCharacteristic->notify();
    delay(30);
}

// =============================================================================
// Send Tohnichi DF-3 Telemetry Frame over BLE
// =============================================================================
void sendTohnichiDf3Frame(float torque, float angle, const char* judgment) {
    if (!deviceConnected) {
        Serial.println("[WARN] Telemetry not sent: Tablet not connected via BLE.");
        lastTransmittedFrame = "[Failed: Tablet not connected via BLE]";
        return;
    }

    char frameBuffer[80];
    snprintf(frameBuffer, sizeof(frameBuffer),
             "RE,%03d,%+06.1f,nm,%+04d,deg,%s,%s,26/11/17,12:00:00\r\n",
             measurementCounter % 1000,
             torque,
             (int)angle,
             judgment,
             TOOL_SERIAL_NO);

    measurementCounter++;
    lastTransmittedFrame = String(frameBuffer);
    lastTransmittedFrame.trim();

    pTxCharacteristic->setValue(frameBuffer);
    pTxCharacteristic->notify();
    delay(40); // Allow BLE radio buffer to flush cleanly without disconnection

    Serial.printf("[BLE TX] Sent DF-3 Frame: %s", frameBuffer);
}

// =============================================================================
// Simulation Logic
// =============================================================================
void simulateTighten(char mode, float customVal) {
    float torque = targetTorque;
    float angle = 90.0f;
    const char* judgment = "OO"; // Default Pass

    if (mode == 't') {
        // Pass with natural variance (+/- 0.2 Nm)
        float jitter = ((random(0, 5) - 2) * 0.1f);
        torque = targetTorque + jitter;
        angle = (float)random(88, 93);
        judgment = "OO";
    }
    else if (mode == 'h') {
        // High / Over-Torque
        torque = upperLimitTorque + 3.2f;
        angle = 120.0f;
        judgment = "HO";
    }
    else if (mode == 'l') {
        // Low / Under-Torque
        torque = lowerLimitTorque - 3.8f;
        angle = 45.0f;
        judgment = "LO";
    }
    else if (mode == 'd') {
        // Double-Tightening Poka-Yoke NG
        torque = targetTorque + 0.8f;
        angle = 2.5f; // Small rotation (< 5 deg)
        judgment = "OD";
    }
    else if (mode == 'c') {
        torque = customVal;
        angle = 90.0f;
        if (torque >= lowerLimitTorque && torque <= upperLimitTorque) {
            judgment = "OO";
        } else if (torque > upperLimitTorque) {
            judgment = "HO";
        } else {
            judgment = "LO";
        }
    }

    sendTohnichiDf3Frame(torque, angle, judgment);
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
    <title>Tohnichi CEM3-G-BT Wrench Simulator</title>
    <style>
        :root {
            --bg-dark: #0f172a;
            --card-bg: #1e293b;
            --accent: #0284c7;
            --success: #10b981;
            --warning: #f59e0b;
            --danger: #ef4444;
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
        .btn-double { background: linear-gradient(135deg, #7c3aed 0%, #8b5cf6 100%); color: white; }
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
            <div class="title">🔧 TOHNICHI CEM3-G-BT</div>
            <div class="subtitle">Wireless Torque Wrench Webpage Controller</div>
        </div>

        <div class="status-card">
            <div style="display: flex; justify-content: space-between; align-items: center;">
                <span style="font-size: 0.85rem; color: var(--text-sub);">BLE Connection:</span>
                <span id="bleStatus" class="badge badge-disconnected">Checking...</span>
            </div>
            <div class="limits-grid">
                <div class="limit-box">
                    <div class="limit-label">Lower Limit</div>
                    <div class="limit-val val-lo" id="valLo">--.-</div>
                </div>
                <div class="limit-box">
                    <div class="limit-label">Target Torque</div>
                    <div class="limit-val val-target" id="valTarget">--.-</div>
                </div>
                <div class="limit-box">
                    <div class="limit-label">Upper Limit</div>
                    <div class="limit-val val-hi" id="valHi">--.-</div>
                </div>
            </div>
        </div>

        <div class="btn-grid">
            <button class="btn btn-pass" onclick="sendTighten('t')">
                <span>🟢 PASS Tightening</span>
                <span id="btnPassNm" style="font-size: 0.85rem; opacity: 0.85;">Target Nm</span>
            </button>
            <button class="btn btn-over" onclick="sendTighten('h')">
                <span>🔴 Over-Torque (NG)</span>
                <span style="font-size: 0.85rem; opacity: 0.85;">High HO</span>
            </button>
            <button class="btn btn-under" onclick="sendTighten('l')">
                <span>🟡 Under-Torque (NG)</span>
                <span style="font-size: 0.85rem; opacity: 0.85;">Low LO</span>
            </button>
            <button class="btn btn-double" onclick="sendTighten('d')">
                <span>⚠️ Double-Hit Poka-Yoke</span>
                <span style="font-size: 0.85rem; opacity: 0.85;">Angle &lt; 5° (OD)</span>
            </button>
        </div>

        <div class="custom-card">
            <div style="font-size: 0.85rem; font-weight: 600; color: var(--text-sub);">Send Exact Torque Value (N·m):</div>
            <div class="custom-form">
                <input type="number" step="0.1" id="customInput" class="custom-input" placeholder="e.g. 25.0">
                <button class="btn btn-custom" onclick="sendCustom()">Send</button>
            </div>
        </div>

        <div class="status-card">
            <div style="font-size: 0.8rem; color: var(--text-sub); margin-bottom: 6px;">Last Transmitted DF-3 Frame:</div>
            <div class="log-box" id="logBox">Ready for tightening...</div>
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
                document.getElementById('valTarget').textContent = data.target.toFixed(1) + ' N·m';
                document.getElementById('valLo').textContent = data.lower.toFixed(1) + ' N·m';
                document.getElementById('valHi').textContent = data.upper.toFixed(1) + ' N·m';
                document.getElementById('btnPassNm').textContent = data.target.toFixed(1) + ' N·m';
                if (data.lastFrame && data.lastFrame !== 'None') {
                    document.getElementById('logBox').textContent = data.lastFrame;
                }
            } catch (e) { }
        }

        async function sendTighten(mode) {
            try {
                const res = await fetch('/tighten?mode=' + mode);
                const data = await res.json();
                document.getElementById('logBox').textContent = data.frame || 'Reading sent!';
                fetchStatus();
            } catch (e) {
                alert('Error sending command to ESP32');
            }
        }

        async function sendCustom() {
            const val = parseFloat(document.getElementById('customInput').value);
            if (isNaN(val) || val <= 0) {
                alert('Please enter a valid torque value');
                return;
            }
            try {
                const res = await fetch('/tighten?mode=c&val=' + val);
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
    json += "\"target\":" + String(targetTorque, 1) + ",";
    json += "\"lower\":" + String(lowerLimitTorque, 1) + ",";
    json += "\"upper\":" + String(upperLimitTorque, 1) + ",";
    json += "\"counter\":" + String(measurementCounter) + ",";
    json += "\"lastFrame\":\"" + lastTransmittedFrame + "\"";
    json += "}";
    webServer.send(200, "application/json", json);
}

void handleTighten() {
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

    simulateTighten(mode, customVal);

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
    Serial.println("  AssemblIQ - Tohnichi CEM3-G-BT BLE & Webpage Simulator");
    Serial.println("=======================================================");

    // 1. Initialize WiFi (Connect to AP or spin up SoftAP)
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

    // 2. Setup HTTP Web Server
    webServer.on("/", HTTP_GET, handleRoot);
    webServer.on("/status", HTTP_GET, handleStatus);
    webServer.on("/tighten", HTTP_GET, handleTighten);
    webServer.begin();
    Serial.println("[HTTP] Web Server started on port 80");

    // 3. Initialize BLE with High MTU (prevents buffer truncation / disconnects)
    BLEDevice::init(BLE_DEVICE_NAME);
    BLEDevice::setMTU(517); // Support full 80-byte DF-3 frames without truncation

    pServer = BLEDevice::createServer();
    pServer->setCallbacks(new ServerCallbacks());

    // Create Primary Service
    BLEService* pService = pServer->createService(SERVICE_UUID);

    // Create Transmit Characteristic (Tool -> Tablet / Notify)
    pTxCharacteristic = pService->createCharacteristic(
        TX_CHAR_UUID,
        BLECharacteristic::PROPERTY_NOTIFY | BLECharacteristic::PROPERTY_READ
    );
    pTxCharacteristic->addDescriptor(new BLE2902());

    // Create Receive Characteristic (Tablet -> Tool / Write)
    pRxCharacteristic = pService->createCharacteristic(
        RX_CHAR_UUID,
        BLECharacteristic::PROPERTY_WRITE | BLECharacteristic::PROPERTY_WRITE_NR
    );
    pRxCharacteristic->setCallbacks(new RxCallbacks());

    // Start Service
    pService->start();

    // Start Advertising with robust connection intervals
    BLEAdvertising* pAdvertising = BLEDevice::getAdvertising();
    pAdvertising->addServiceUUID(SERVICE_UUID);
    pAdvertising->setScanResponse(true);
    pAdvertising->setMinPreferred(0x06); // 7.5ms
    pAdvertising->setMaxPreferred(0x12); // 22.5ms
    BLEDevice::startAdvertising();

    Serial.printf("[BLE] Advertising '%s'...\n", BLE_DEVICE_NAME);
    Serial.println("=======================================================\n");
}

// =============================================================================
// Main Loop
// =============================================================================
void loop() {
    // 1. Handle HTTP Web Server Requests (Instant browser clicks!)
    webServer.handleClient();

    // 2. Handle BLE reconnection after disconnection
    if (!deviceConnected && oldDeviceConnected) {
        delay(300);
        pServer->startAdvertising();
        Serial.println("[BLE] Resumed advertising for AssemblIQ...");
        oldDeviceConnected = deviceConnected;
    }
    if (deviceConnected && !oldDeviceConnected) {
        oldDeviceConnected = deviceConnected;
    }

    // 3. Physical BOOT Button Press (active LOW with debounce)
    static bool lastBtnState = HIGH;
    bool currentBtnState = digitalRead(BOOT_BUTTON_PIN);
    if (lastBtnState == HIGH && currentBtnState == LOW) {
        delay(50); // Debounce
        if (digitalRead(BOOT_BUTTON_PIN) == LOW) {
            simulateTighten('t'); // Default normal pass tightening
        }
    }
    lastBtnState = currentBtnState;

    delay(5);
}
