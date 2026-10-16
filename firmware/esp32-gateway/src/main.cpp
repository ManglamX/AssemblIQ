#include <Arduino.h>
#include <WiFi.h>
#include <HTTPClient.h>
#include <ArduinoJson.h>
#include <Preferences.h>
#include <time.h>

#include "config.h"
#include "offline_buffer.h"

// Hardware and state objects
Preferences prefs;
OfflineBuffer offlineBuffer;
HTTPClient http;

// Dynamic state variables
String serverUrl = DEFAULT_SERVER_URL;
String transmitterId = DEFAULT_TRANSMITTER_ID;
String apiKey = DEFAULT_API_KEY;
int instrumentType = DEFAULT_INSTRUMENT_TYPE;
uint64_t deviceSequence = 0;

unsigned long lastHeartbeatTime = 0;
unsigned long lastLiveSampleTime = 0;
float currentMeasurement = 0.0f;
bool isMeasurementActive = false;

// NTP configuration
const char* ntpServer = "pool.ntp.org";
const long gmtOffset_sec = 19800; // IST: UTC + 5:30
const int daylightOffset_sec = 0;

void getUtcIsoTimestamp(char* buffer, size_t maxLen) {
    time_t now;
    struct tm timeinfo;
    time(&now);
    gmtime_r(&now, &timeinfo);
    strftime(buffer, maxLen, "%Y-%m-%dT%H:%M:%SZ", &timeinfo);
}

bool sendHttpRequest(const String& path, const String& jsonPayload, String& responseOut, int& statusCodeOut) {
    if (WiFi.status() != WL_CONNECTED) {
        return false;
    }

    String fullUrl = serverUrl + path;
    http.begin(fullUrl);
    http.addHeader("Content-Type", "application/json");
    http.addHeader("X-Api-Key", apiKey);
    http.addHeader("X-Transmitter-Id", transmitterId);
    http.setTimeout(3000); // 3 second timeout

    statusCodeOut = http.POST(jsonPayload);
    if (statusCodeOut > 0) {
        responseOut = http.getString();
        http.end();
        return (statusCodeOut >= 200 && statusCodeOut < 300);
    } else {
        http.end();
        return false;
    }
}

void registerDevice() {
    Serial.printf("[GATEWAY] Registering transmitter '%s' with backend...\n", transmitterId.c_str());
    JsonDocument doc;
    doc["transmitterId"] = transmitterId;
    doc["firmware"] = FIRMWARE_VERSION;
    doc["instrument"] = (instrumentType == INSTRUMENT_TYPE_TORQUE) ? "Torque" : "Vernier";

    String payload;
    serializeJson(doc, payload);

    String resp;
    int code;
    if (sendHttpRequest("/api/device/register", payload, resp, code)) {
        Serial.printf("[GATEWAY] Successfully registered device. Response: %s\n", resp.c_str());
    } else {
        Serial.printf("[GATEWAY] Device registration failed with HTTP code %d\n", code);
    }
}

void sendHeartbeat() {
    JsonDocument doc;
    doc["transmitterId"] = transmitterId;
    doc["firmwareVersion"] = FIRMWARE_VERSION;
    doc["batteryLevel"] = 98; // Simulated battery ADC level
    doc["signalStrength"] = WiFi.RSSI();

    String payload;
    serializeJson(doc, payload);

    String resp;
    int code;
    if (sendHttpRequest("/api/device/heartbeat", payload, resp, code)) {
        Serial.printf("[GATEWAY] Heartbeat OK (RSSI: %d dBm)\n", WiFi.RSSI());
    } else {
        Serial.printf("[GATEWAY] Heartbeat failed (code: %d)\n", code);
    }
}

bool postReading(const OfflineReading& reading) {
    JsonDocument doc;
    doc["transmitterId"] = transmitterId;
    doc["instrument"] = (instrumentType == INSTRUMENT_TYPE_TORQUE) ? "Torque" : "Vernier";
    doc["value"] = reading.value;
    doc["unit"] = reading.unit;
    doc["isStable"] = reading.isStable;
    doc["seq"] = reading.seq;
    doc["timestamp"] = reading.timestamp;

    String payload;
    serializeJson(doc, payload);

    String resp;
    int code;
    bool success = sendHttpRequest("/api/device/reading", payload, resp, code);
    if (success) {
        Serial.printf("[GATEWAY] Reading POST OK (seq=%llu, val=%.2f %s, stable=%d): %s\n",
            reading.seq, reading.value, reading.unit, reading.isStable, resp.c_str());
        return true;
    } else {
        Serial.printf("[GATEWAY] Reading POST failed (HTTP %d)\n", code);
        return false;
    }
}

void replayOfflineBuffer() {
    if (offlineBuffer.isEmpty() || WiFi.status() != WL_CONNECTED) return;

    Serial.printf("[GATEWAY] Replaying %d buffered offline readings in sequence...\n", offlineBuffer.count());
    while (!offlineBuffer.isEmpty()) {
        OfflineReading item;
        if (offlineBuffer.peek(item)) {
            if (postReading(item)) {
                offlineBuffer.pop(item);
                delay(50); // Small inter-packet gap
            } else {
                Serial.println("[GATEWAY] Buffer replay paused due to network transmission failure.");
                break;
            }
        }
    }
}

void processIncomingUsbSample(float rawValue, bool isStableSignal) {
    deviceSequence++;
    prefs.putULong64("seq", deviceSequence);

    OfflineReading reading;
    reading.seq = deviceSequence;
    reading.value = rawValue;
    strncpy(reading.unit, (instrumentType == INSTRUMENT_TYPE_TORQUE) ? "Nm" : "mm", sizeof(reading.unit) - 1);
    reading.isStable = isStableSignal;
    getUtcIsoTimestamp(reading.timestamp, sizeof(reading.timestamp));

    if (!isStableSignal) {
        // Live streaming sample: send immediately, drop if offline (Architecture §12)
        if (WiFi.status() == WL_CONNECTED) {
            postReading(reading);
        }
    } else {
        // Confirmed stable measurement: guaranteed delivery via offline ring buffer
        if (WiFi.status() == WL_CONNECTED && offlineBuffer.isEmpty()) {
            if (!postReading(reading)) {
                Serial.println("[GATEWAY] Direct post failed. Storing confirmed measurement in offline buffer.");
                offlineBuffer.push(reading);
            }
        } else {
            Serial.printf("[GATEWAY] Offline mode. Buffering confirmed measurement (seq=%llu). Buffer count: %d\n",
                reading.seq, offlineBuffer.count() + 1);
            offlineBuffer.push(reading);
        }
    }
}

void setup() {
    Serial.begin(115200);
    delay(1000);
    Serial.println("=================================================");
    Serial.println("SmartTorque ESP32 Gateway Firmware v1.0.0 (Phase 6)");
    Serial.println("=================================================");

    // Initialize Non-Volatile Storage (Preferences)
    prefs.begin("smarttorque", false);
    deviceSequence = prefs.getULong64("seq", 1000);
    Serial.printf("[NVS] Loaded persistent sequence counter: %llu\n", deviceSequence);

    // Connect to Wi-Fi
    Serial.printf("[WIFI] Connecting to SSID: %s", DEFAULT_WIFI_SSID);
    WiFi.mode(WIFI_STA);
    WiFi.begin(DEFAULT_WIFI_SSID, DEFAULT_WIFI_PASS);

    int retries = 0;
    while (WiFi.status() != WL_CONNECTED && retries < 20) {
        delay(500);
        Serial.print(".");
        retries++;
    }

    if (WiFi.status() == WL_CONNECTED) {
        Serial.printf("\n[WIFI] Connected! Local IP: %s\n", WiFi.localIP().toString().c_str());
        configTime(gmtOffset_sec, daylightOffset_sec, ntpServer);
        registerDevice();
    } else {
        Serial.println("\n[WIFI] Wi-Fi connection timed out. Starting in offline buffering mode.");
    }
}

void loop() {
    unsigned long now = millis();

    // 1. Wi-Fi reconnection handling
    if (WiFi.status() != WL_CONNECTED && (now - lastHeartbeatTime > RECONNECT_INTERVAL_MS)) {
        WiFi.reconnect();
    }

    // 2. Offline buffer replay if connection active
    if (WiFi.status() == WL_CONNECTED && !offlineBuffer.isEmpty()) {
        replayOfflineBuffer();
    }

    // 3. Heartbeat timer
    if (now - lastHeartbeatTime >= HEARTBEAT_INTERVAL_MS) {
        lastHeartbeatTime = now;
        if (WiFi.status() == WL_CONNECTED) {
            sendHeartbeat();
        }
    }

    // 4. USB Host packet reading simulation loop (or physical OTG interrupt)
    if (Serial.available()) {
        String cmd = Serial.readStringUntil('\n');
        cmd.trim();
        if (cmd.startsWith("READ:")) {
            float val = cmd.substring(5).toFloat();
            processIncomingUsbSample(val, true);
        } else if (cmd.startsWith("LIVE:")) {
            float val = cmd.substring(5).toFloat();
            processIncomingUsbSample(val, false);
        }
    }

    delay(10);
}
