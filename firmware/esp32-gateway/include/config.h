#pragma once

#include <Arduino.h>

// ============================================================================
// SmartTorque ESP32 Gateway Hardware Configuration
// ============================================================================

// Default Wi-Fi Credentials (override via Serial CLI or NVS)
#define DEFAULT_WIFI_SSID       "SmartTorque_Plant_LAN"
#define DEFAULT_WIFI_PASS       "PlantSecure2026"

// Default Backend Endpoint
#define DEFAULT_SERVER_URL      "http://192.168.1.100:5084"

// Device Credentials (Generated in Admin UI -> /Device)
#define DEFAULT_TRANSMITTER_ID  "ESP32-TW-001"
#define DEFAULT_API_KEY         "st_secret_api_key_replace_with_actual"

// Instrument Type: 1 = Torque Wrench (N·m), 2 = Vernier Caliper (mm)
#define INSTRUMENT_TYPE_TORQUE  1
#define INSTRUMENT_TYPE_VERNIER 2
#define DEFAULT_INSTRUMENT_TYPE INSTRUMENT_TYPE_TORQUE

// Gateway Timers & Settings
#define FIRMWARE_VERSION        "v1.0.0-p6"
#define HEARTBEAT_INTERVAL_MS   10000    // 10 seconds
#define LIVE_SAMPLE_INTERVAL_MS 150      // ~6.6 Hz live streaming
#define STABLE_HOLD_MS          800      // Required stable duration before confirmed accept
#define RECONNECT_INTERVAL_MS   5000     // Wi-Fi retry interval

// Ring Buffer Size for Offline Storage (PRD Feature 10)
#define OFFLINE_BUFFER_CAPACITY 500      // Holds up to 500 confirmed measurements offline
