# SmartTorque ESP32 Hardware Gateway Firmware (Phase 6)

Production-ready firmware for the SmartTorque wireless instrument transmitter gateway, supporting both digital Torque Wrenches and digital Vernier Calipers.

---

## 1. Hardware Architecture

- **Microcontroller**: ESP32-S3-WROOM-1 (or ESP32-S2) with native USB-OTG Host support.
- **Instrument Interface**: USB Type-A Receptacle connected to native USB D+/D- pins (GPIO 19 / GPIO 20 on ESP32-S3).
- **Power**: 3.7V LiPo battery with onboard TP4056 charging IC and 5V boost regulator for USB VBUS power.
- **Wireless**: 2.4 GHz 802.11 b/g/n Wi-Fi with WPA2/WPA3 enterprise and personal encryption.

### Pinout (ESP32-S3 DevKitC-1)
| Signal | ESP32-S3 Pin | Instrument / Hardware Connection |
|---|---|---|
| USB D+ | GPIO 20 | USB Type-A Pin 3 (Green) |
| USB D- | GPIO 19 | USB Type-A Pin 2 (White) |
| VBUS (5V Out) | 5V / VBUS | USB Type-A Pin 1 (Red) |
| GND | GND | USB Type-A Pin 4 (Black) |
| Battery ADC | GPIO 4 | Battery voltage divider (100k / 100k) |
| Status LED | GPIO 2 | Blue indicator (Heartbeat / Wi-Fi status) |

---

## 2. Key Features (Phase 6)

1. **Dual-Instrument Parsing**:
   - Parses CDC virtual COM and HID keyboard streams from both Torque Wrenches and Vernier Calipers.
   - Automatically formats units (`Nm` for torque, `mm` for calipers) before transmission.
2. **Rate-Governed Live Streaming**:
   - Transmits non-stable measurement updates at 5–10 Hz (`isStable: false`) to feed the real-time SVG gauge and digital readout.
3. **Peak & Stable Hold Capture**:
   - Detects tool peak torque release or caliper data/hold button press (`isStable: true`).
4. **Persistent Sequence Counter (`seq`)**:
   - Maintained in ESP32 Non-Volatile Storage (Preferences / NVS) across power cycles and reboots to guarantee strict idempotency.
5. **Offline Ring Buffer & Ordered Replay (PRD Feature 10)**:
   - When plant Wi-Fi connectivity drops or HTTP requests fail, confirmed stable readings are buffered in flash storage (up to 500 readings).
   - Once Wi-Fi reconnects, all buffered readings are replayed in chronological order with original sequence numbers and timestamps.
   - Live samples are discarded during outages; confirmed measurements are guaranteed never lost.
6. **Hardware Heartbeat**:
   - Sends keep-alive ping every 10 seconds with battery percentage and Wi-Fi RSSI signal strength to update Admin portal live status.

---

## 3. Configuration & Flashing

### Using PlatformIO (VS Code / CLI)
1. Navigate to `firmware/esp32-gateway`.
2. Edit `include/config.h` with your local network SSID, password, backend URL, and device API key generated from `/Device`.
3. Build and flash:
```bash
pio run -t upload
pio device monitor -b 115200
```
