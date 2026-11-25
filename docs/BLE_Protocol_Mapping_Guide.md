# AssemblIQ Universal BLE Protocol Mapping Guide

This document describes every field in **Devices → BLE Protocol Mapping**, how incoming Bluetooth Low Energy (BLE) frames are normalized for the Worker Execution Cockpit, and how to configure new tools such as Mitutoyo U-WAVE Bluetooth (`E12000.pdf`).

---

## 1. Architecture Flow

1. **Admin Configuration (`/Device` → BLE Protocol Mapping tab):**  
   Define a `BleProtocol` profile specifying GATT UUIDs, frame format, and field mappings, and test sample frames in the **Live Frame Mapping Tester**.
2. **Device Registration (`/Device/Create` or `/Device/Edit`):**  
   Select the **BLE Protocol Mapping** for any *Torque Wrench (BLE)* or *Vernier Caliper (BLE)*. The `BLE UUID` auto-populates from the protocol's `ServiceUuid`.
3. **Worker Cockpit (`Execute.cshtml` + `tohnichi-ble-driver.js`):**  
   On connect/auto-reconnect, the browser fetches active profiles from `GET /Device/ProtocolsJson`, subscribes to `TxCharUuid`, parses incoming frames into the normalized cockpit `reading` object, and posts to `POST /Worker/JobBoard/SubmitReading`.

---

## 2. Protocol Mapping Fields Reference

### A. Identity & Discovery

| UI Field | Backend Property | Required | Description & Example |
| :--- | :--- | :---: | :--- |
| **Protocol Name** | `Name` | Yes | Display name shown in the Devices table and Device Registration dropdown (e.g., `Tohnichi CEM3-G-BT (DF-3)`). |
| **Instrument Scope** | `SupportedInstrumentType` | No | `Universal` (`null`, works for both Torque & Vernier), `Torque Wrench (1)`, or `Vernier Caliper (2)`. |
| **BLE Advertised Name Prefixes** | `DeviceNamePrefixes` | No | Comma-separated BLE name prefixes passed to `navigator.bluetooth.requestDevice` filters (e.g., `CEM3,Tohnichi,ESP,U-WAVE,Vernier`). |
| **Description** | `Description` | No | Short note describing the hardware model and frame structure. |

### B. GATT Service & Characteristics

| UI Field | Backend Property | Required | Description & Example |
| :--- | :--- | :---: | :--- |
| **Service UUID** | `ServiceUuid` | Yes | Primary BLE GATT Service UUID (lowercase 128-bit or 16-bit expanded UUID, e.g., `55df0001-a9b0-11e3-a5e2-000190f08f1e`). |
| **TX Notify Char UUID** | `TxCharUuid` | Yes | Characteristic UUID that sends **Tool → Browser** notifications containing measurement frames. |
| **RX Write Char UUID** | `RxCharUuid` | No | Optional characteristic UUID for **Browser → Tool** commands (e.g., pushing target limits). Leave blank for read-only tools like Vernier calipers. |

### C. Frame Format & Filtering

| UI Field | Backend Property | Required | Description & Example |
| :--- | :--- | :---: | :--- |
| **Frame Format** | `FrameFormat` | Yes | Parser mode: `Delimited`, `Regex`, `Json`, or `HexBytes`. |
| **Delimiter** | `Delimiter` | If `Delimited` | Separator character/string (default `,`). Tokens are indexed starting at `0`. |
| **Regex Pattern** | `RegexPattern` | If `Regex` | JavaScript regular expression with capture groups. Groups are indexed starting at `1`. |
| **Frame Start Filter** | `FrameStartFilter` | No | Only parse frames starting with this prefix (e.g., `RE,`). Non-matching frames are skipped. |
| **Ignore Prefix (ACKs)** | `IgnorePrefix` | No | Silently ignore status/ACK frames starting with this prefix (e.g., `RE003`). |

### D. Cockpit Field Mapping

| UI Field | Backend Property | Maps To Cockpit / API | Required | How to Specify |
| :--- | :--- | :--- | :---: | :--- |
| **Reading Value Field** | `ValueField` | `reading.torque` → `ProcessReadingCommand.Value` | **Yes** | Token index (`2`), regex group (`1`), JSON path (`data.val`), or byte index (`0`). |
| **Multiplier** | `ValueMultiplier` | Multiplies parsed value | No (default `1`) | Use `1` for direct `N·m`/`mm`, or e.g. `0.01` if tool sends integer hundredths. |
| **Sequence / Counter** | `CounterField` | `reading.counter` → `DeviceSeq` | No (default `1`) | Index/group/key for the monotonic reading counter. |
| **Tool Serial / Transmitter** | `ToolSerialField` | `reading.toolSerial` → `TransmitterId` | No | Index/group/key for tool serial. Falls back to BLE `device.name`. |
| **Unit** | `UnitField` | `reading.unit` | No | Index/group/key for unit string (`nm`, `mm`, `in`). |
| **Angle** | `AngleField` | `reading.angle` | No | Index/group/key for tightening angle (used in double-tightening warning). |
| **Judgment** | `JudgmentField` | `reading.judgment` | No | Index/group/key for tool's pass/fail judgment code (e.g., `OO`, `OD`). |
| **Pass Token** | `PassValue` | `reading.isPass` | No | Exact judgment string meaning Pass (e.g., `OO`). |
| **Double-Hit Token** | `DoubleTighteningValue` | `reading.isDoubleTightening` | No | Substring in judgment triggering Poka-Yoke double-tightening block (e.g., `D`). |
| **Limit Sync Command** | `SyncLimitsCommandTemplate` | Written to `RxCharUuid` | No | Command template with `{HIGH}`, `{LOW}`, `{TARGET}` placeholders (e.g., `AT037,{HIGH},{LOW}\r\n`). |

---

## 3. Built-In Protocol Profiles

### 1. Tohnichi CEM3-G-BT (DF-3) — *Torque Wrench*
- **Sample Frame:** `RE,001,+024.8,nm,+092,deg,OO,704620P,26/11/16,22:00:00`
- **Service / TX / RX:** `55df0001-...` / `55df0002-...` / `55df0003-...`
- **Format:** `Delimited` (`,`), `FrameStartFilter`: `RE,`, `IgnorePrefix`: `RE003`
- **Mappings:** `ValueField`: `2`, `CounterField`: `1`, `UnitField`: `3`, `AngleField`: `4`, `JudgmentField`: `6`, `ToolSerialField`: `7`, `PassValue`: `OO`, `DoubleTighteningValue`: `D`
- **Sync Command:** `AT037,{HIGH},{LOW}\r\n`

### 2. Universal BLE Delimited (Torque & Vernier) — *Universal*
- **Sample Frame:** `25.42,mm,VERN-01,4` or `18.5,nm,TW-02,12`
- **Service / TX / RX:** Nordic UART (`6e400001-b5a3-f393-e0a9-e50e24dcca9e` / `6e400003-...` / `6e400002-...`)
- **Format:** `Delimited` (`,`)
- **Mappings:** `ValueField`: `0`, `UnitField`: `1`, `ToolSerialField`: `2`, `CounterField`: `3`

### 3. Generic Digital Vernier Caliper (BLE) — *Vernier Caliper*
- **Sample Frame:** `+25.42 mm`
- **Service / TX:** `0000ffe0-0000-1000-8000-00805f9b34fb` / `0000ffe1-0000-1000-8000-00805f9b34fb`
- **Format:** `Regex`, `RegexPattern`: `([+-]?\d+(?:\.\d+)?)\s*(mm|in)?`
- **Mappings:** `ValueField`: `1`, `UnitField`: `2`

---

## 4. Configuring Mitutoyo U-WAVE Bluetooth (`E12000.pdf`: U-WAVE-TCB / U-WAVE-TMB)

Mitutoyo's `E12000.pdf` covers **U-WAVE-TCB** (caliper) and **U-WAVE-TMB** (micrometer) BLE 4.2 transmitters. Mitutoyo does not publicly document the proprietary 128-bit GATT UUIDs or raw packet bytes in the brochure (officially they are handled by `U-WAVEPAK-BW` / `U-WAVEPAK-BM`).

### Option A: Direct Web BLE via Protocol Mapping
1. Power on the U-WAVE transmitter and scan it using **nRF Connect** (mobile) or `chrome://bluetooth-internals` (desktop).
2. Press the data button on the U-WAVE unit and record:
   - **Advertised Name** (e.g., `U-WAVE...`) → put prefix in **BLE Advertised Name Prefixes**.
   - **Service UUID** → put in **Service UUID**.
   - **Notify Characteristic UUID** → put in **TX Notify Char UUID** (leave **RX Write Char UUID** blank).
   - **One sample notification payload** → paste into **Live Frame Mapping Tester**.
3. Configure the parser:
   - **ASCII Digimatic payload** (e.g. `+025.42mm`):
     - `Frame Format`: `Regex`
     - `Regex Pattern`: `([+-]?\d+(?:\.\d+)?)\s*(mm|in)?`
     - `Reading Value Field`: `1`
     - `Unit`: `2`
   - **Raw binary/hex payload:** Select `HexBytes` if a single byte carries the value, or capture one sample hex packet from nRF Connect to add a multi-byte Digimatic BCD decoder in `tohnichi-ble-driver.js`.

### Option B: Mitutoyo `U-WAVEPAK-BW` (Windows HID Keyboard Mode)
If you run Mitutoyo's `U-WAVEPAK-BW` on a Windows shop-floor tablet in **Keyboard Emulation (HID)** mode, pressing the U-WAVE button types the measurement directly into the cockpit's manual measurement input field without needing GATT UUID discovery.
