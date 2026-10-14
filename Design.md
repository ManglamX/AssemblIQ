# Design — Visual Language, Typography and Screens

| Item | Value |
|---|---|
| Document | UI / UX specification |
| Read before | Any view, CSS or front-end JS work |
| Theme | **White `#FFFFFF`** and **Purple-Magenta `#BD1362`** |
| Framework | Bootstrap 5 (vendored locally) themed by `wwwroot/css/theme.css` |
| Two audiences | **Admin:** dense, desktop-first, sidebar app. **Worker:** sparse, tablet-first, huge live value, workshop use. |

The rules from `Rules.md` section 7 apply: tokens only (no hex in views/JS), colour is never the only signal, no CDN, large touch targets for Worker.

---

## 1. Design Principles

1. **Clarity over decoration.** White surfaces, one brand colour, lots of space.
2. **The live value is the hero** on the Worker screen: biggest element, highest contrast.
3. **Never rely on colour alone.** PASS/FAIL carry an icon and a text label; priority badges carry a text label and a shape.
4. **Brand ≠ status.** `#BD1362` is close to red, so brand colour is used for actions and navigation, never to mean "fail" or "critical".
5. **Zero thinking for the worker.** One obvious next action per screen; no instrument selector; automatic step advance.
6. **Works offline.** No external fonts, icons, scripts or images.

---

## 2. Design Tokens (`theme.css`)

```css
:root {
  /* Brand */
  --brand:        #BD1362;   /* primary buttons, headers, active nav, links */
  --brand-dark:   #8F0E4A;   /* hover / pressed */
  --brand-light:  #F9E6EF;   /* row hover, subtle highlights, selected */
  --on-brand:     #FFFFFF;   /* text on brand */

  /* Surfaces and text */
  --bg:           #FFFFFF;   /* page and card background */
  --bg-subtle:    #FAF7F9;   /* page background behind cards (optional), sidebar hover */
  --text:         #222222;   /* body */
  --text-muted:   #666666;   /* secondary text */
  --border:       #E5E5E5;   /* dividers, input borders */
  --focus-ring:   #BD1362;   /* 3px outline, offset 2px */

  /* Status (functional, separate from brand) */
  --pass:         #2E9E5B;   /* fills, bars, icons */
  --pass-text:    #1E7A43;   /* green text on white (AA) */
  --pass-bg:      #E8F5EE;
  --fail:         #D32F2F;
  --fail-bg:      #FDECEC;
  --warn:         #B26A00;   /* amber text on white */
  --warn-bg:      #FFF4E0;
  --info:         #1F5FAF;
  --info-bg:      #E8F0FB;

  /* Priority badges (always shown with text label) */
  --pri-black:    #111111;   /* BLACK  / 1 Critical  */
  --pri-red:      #C62828;   /* RED    / 2 Very High */
  --pri-yellow:   #F2B705;   /* YELLOW / 3 Medium (dark text) */
  --pri-green:    #3C8D40;   /* GREEN  / 4 Low */
  --pri-white:    #FFFFFF;   /* WHITE  / 5 Lowest (outlined) */

  /* Shape and elevation */
  --radius-sm: 6px;
  --radius:    10px;
  --radius-lg: 16px;
  --shadow-1:  0 1px 2px rgba(0,0,0,.06), 0 1px 3px rgba(0,0,0,.08);
  --shadow-2:  0 4px 12px rgba(0,0,0,.10);

  /* Spacing scale (4px base) */
  --sp-1: 4px;  --sp-2: 8px;  --sp-3: 12px; --sp-4: 16px;
  --sp-5: 24px; --sp-6: 32px; --sp-7: 48px; --sp-8: 64px;

  /* Touch */
  --touch-min: 48px;         /* Worker minimum target; primary Worker buttons 56px */
}
```

Bootstrap mapping: override `--bs-primary` to `--brand`, link colour to `--brand`, focus ring to `--focus-ring`, and customise `.btn-primary`, `.page-link`, `.form-check-input:checked`, `.nav-pills .active` accordingly. Do not use Bootstrap's default blue anywhere.

### Contrast (approximate; verify with a contrast checker)
| Pair | Ratio | Use |
|---|---|---|
| `--brand` on white / white on `--brand` | ~6:1 | AA for all text |
| `--text` on white | > 14:1 | body |
| `--pass-text` on white | ~5:1 | PASS text |
| `--pass` on white | ~3.4:1 | large text, icons, bars only |
| `--fail` on white | ~5:1 | FAIL text |
| `--text` on `--pri-yellow` | > 8:1 | YELLOW badge uses dark text |

---

## 3. Typography

No web fonts are downloaded. Use the system stack; optionally self-host **Inter** in `wwwroot/lib/fonts` and put it first.

```css
--font-sans: "Inter", system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
--font-mono: ui-monospace, "Cascadia Mono", Consolas, "Liberation Mono", monospace;
```

| Role | Admin | Worker | Weight |
|---|---|---|---|
| Display / live value | n/a | **96–140 px** (responsive `clamp(88px, 14vw, 140px)`) | 700 |
| Page title (H1) | 28 px | 32 px | 700 |
| Section title (H2) | 20 px | 24 px | 600 |
| Card title (H3) | 16 px | 20 px | 600 |
| Step/component label | 16 px | **28 px** | 600 |
| Body | 14 px | 18 px | 400 |
| Table cell | 14 px | 18 px | 400 |
| Caption / helper | 12 px | 14 px | 400 |
| Button text | 14 px | 20 px | 600 |

- Line height 1.5 for body, 1.2 for headings and the live value.
- Numbers that change (live value, targets, limits, timers): `font-variant-numeric: tabular-nums;` so digits do not jitter.
- Units (`N·m`, `mm`) are smaller (40% of the value size), muted, aligned to the baseline.
- Use sentence case, not ALL CAPS, except priority badges and PASS/FAIL labels.

---

## 4. Iconography

- **Bootstrap Icons**, vendored as SVG sprite or font. 20 px in Admin, 28 to 40 px in Worker feedback.
- Required semantic icons (always paired with text): PASS = check-circle, FAIL = x-octagon, warning = exclamation-triangle, device online = wifi, offline = wifi-off, torque = wrench, Vernier = rulers, lock = lock-fill, job = clipboard-check.
- Component image markers: a pulsing ring in `--brand` with a numbered dot; non-current markers are small grey dots.

---

## 5. Components

### 5.1 Buttons
| Type | Style |
|---|---|
| Primary | Fill `--brand`, text white, hover `--brand-dark`, pressed `--brand-dark` with inset shadow |
| Secondary | White fill, 1px `--brand` border, `--brand` text, hover `--brand-light` fill |
| Tertiary / link | `--brand` text, underline on hover |
| Danger | White fill, `--fail` border and text; filled only inside a confirm dialog |
| Disabled | 40% opacity, no hover, `aria-disabled` |
Sizes: Admin 36 px height; Worker 56 px height minimum, full width on phones, radius `--radius`. Visible focus ring (3 px `--focus-ring`, 2 px offset) on every interactive element.

### 5.2 Forms
- Label above field, required marker `*`, helper text below in `--text-muted`.
- Inputs 40 px (Admin) / 52 px (Worker), 1px `--border`, radius `--radius-sm`; focus border `--brand` plus ring.
- Inline validation under the field in `--fail` with an icon; summary at the top for long forms; never clear entered values on error.
- Numeric inputs for target/tolerance use `inputmode="decimal"` and step `0.001`; unit shown as a suffix that changes with the instrument.

### 5.3 Tables (Admin)
- White rows, 1px `--border` separators, header row `--bg-subtle` with `--text-muted` 12 px uppercase labels.
- Row hover `--brand-light`. Sortable headers show a caret. Sticky header on long lists.
- Row actions in a kebab menu on narrow screens.
- Pagination at the bottom, page-size selector.

### 5.4 Cards
White, `--radius`, `--shadow-1`, 1px `--border`, padding `--sp-5`. Dashboard KPI card: label (muted), big number (32 px, 700), small trend/subtext.

### 5.5 Badges
**Priority badge (always with label and icon/shape):**
| Priority | Label | Look |
|---|---|---|
| 1 | `BLACK` + "Critical" | `--pri-black` fill, white text, ● solid circle icon, bold |
| 2 | `RED` | `--pri-red` fill, white text, ▲ icon |
| 3 | `YELLOW` | `--pri-yellow` fill, dark text, ◆ icon |
| 4 | `GREEN` | `--pri-green` fill, white text, ■ icon |
| 5 | `WHITE` | white fill, 1px `--text-muted` border, dark text, ○ icon |
Black jobs also get a 4 px left border in `--pri-black` and a "Critical" tag, and are pinned to the top.

**Status badge (job/session):** soft background with text: Pending (grey), Assigned (info), In progress (brand-light/brand text), Completed (pass-bg/pass-text), On hold (warn), Rework (fail-bg/fail), Cancelled (grey, strikethrough optional).

**Result chip:** `✔ PASS` on `--pass-bg` with `--pass-text`; `✖ FAIL` on `--fail-bg` with `--fail`. Icon plus text, always.

### 5.6 Alerts and toasts
Inline alert with left stripe and icon (info/success/warn/error). Toasts bottom-right for confirmations (Admin); Worker feedback uses a full-width banner inside the execute screen, not toasts, so it cannot be missed.

### 5.7 Stepper / wizard (template creation)
Three numbered steps (Basic Info → Image & Steps → Values & Tolerances). Current step filled `--brand`, completed steps `--brand` outline with check, future grey. Back/Next at the bottom; Save on the last step.

### 5.8 Modal
Centered, `--radius-lg`, focus trapped, Esc closes (not for destructive confirms in progress). Confirm dialogs name the object being changed.

### 5.9 Empty, loading, error
- Empty state: simple line illustration (SVG, local), one sentence, one primary action.
- Loading: skeleton rows for tables; spinner only for short actions.
- Error page: friendly message, request ID, "Back to home".

---

## 6. Layout

### 6.1 Admin shell
```
┌─────────────────────────────────────────────────────────────────────────┐
│ ▣ Smart Torque              Search…                   🔔  Priya ▾ Logout │  ← top bar, white, bottom border
├───────────────┬─────────────────────────────────────────────────────────┤
│ Dashboard     │  Page title                          [ + Primary action ]│
│ Templates     │  ─────────────────────────────────────────────────────  │
│ Products      │  filters / tabs                                         │
│ BM & Workload │  content (cards / table / form)                         │
│ Reports       │                                                         │
│ Devices       │                                                         │
│ Users         │                                                         │
│ Settings      │                                                         │
└───────────────┴─────────────────────────────────────────────────────────┘
```
- Sidebar 240 px, white, active item has `--brand-light` background, 3 px `--brand` left bar, `--brand` text. Collapses to icons under 992 px and to an off-canvas drawer under 768 px.
- Content max width 1400 px, 24 px gutters.

### 6.2 Worker shell
Minimal top bar (logo, worker name, device status chips, logout). No sidebar. Content full width with 24 px gutters. Designed for 10" tablet landscape (1280×800) and works on portrait and phone.

### 6.3 Breakpoints
`sm 576`, `md 768`, `lg 992`, `xl 1200`, `xxl 1400`. Worker execute screen switches from two columns (md+) to stacked (below md).

---

## 7. Screen Specifications

### 7.1 Login (shared)
Centered card 400 px on white, logo, username, password, primary button full width, error banner. Brand colour accents only on the button and link. Redirects by role.

### 7.2 Admin Dashboard
KPI row: Jobs by status (stacked), Today's pass rate, Online devices, **Black jobs waiting** (brand-outlined card with number). Below: recent sessions table and a device online list.

### 7.3 Template list
Toolbar: search, filters (mode, instrument, active), `+ Create Template`. Table columns: Code, Name, Mode, Steps (torque/vernier counts), Version, Active, Updated, Actions. Mode shown as chips with icons.

### 7.4 Template wizard (3 stages)
```
 ① Basic Info ──── ② Image & Steps ──── ③ Values & Tolerances

 Stage 1:  Name*  Code*  Description   Mode ( ) Torque ( ) Vernier ( ) Mixed
 Stage 2:  Number of steps [ 8 ]   Image upload (drop zone)   Preview with click-to-place markers
           Step rows are generated here; click on image to place the marker for the selected step.
 Stage 3:  Table of steps:
           # | Instrument [Torque▾] | Label | Target | Tol + | Tol − | Mode [abs|%] | Seq | Retries | Marker
           Computed band per row: "Accept 23.50 – 26.50 N·m" (live, from the evaluator)
           Image on the right with the selected row's marker highlighted.
 Footer:   Back   Save as draft?   Preview   Save
```
Unit column derived from the instrument. Mixed mode lets each row's instrument be changed; Torque-only/Vernier-only pre-fill.

### 7.5 Product builder
Left: searchable list of latest templates (with version). Right: ordered component list (drag handles), each row: component name input, template name + version chip, quantity stepper, remove. Top: product name, code, description, image. Primary `Save product`.

### 7.6 BM upload and preview
Upload drop zone with a colour legend panel (Black → Critical … White → Lowest). Preview table:
| Row | Product code | Qty | Due date | Colour (swatch + hex) | Priority (badge) | Status | Message |
Error rows have a `--fail-bg` row with an icon; Admin can change the priority (dropdown) or Skip. Summary strip: `42 rows · 38 OK · 3 warnings · 1 error`. Primary `Confirm import`, secondary `Cancel`.

### 7.7 Workload board
Filter bar: priority (multi, with badges), due date range, worker, status. Table sorted by the ordering rule; Black rows pinned with the left border. Columns: Priority badge, Job no, Product, Qty, Due, Assigned to, Status, Actions (Assign, Hold, Cancel). Assign opens a modal with a worker select and "Leave open".

### 7.8 Device list
Columns: Name, Transmitter ID, Instrument icon, Location, Online (dot + label "Online/Offline"), Last seen, Calibration due (warn if soon / fail if overdue, with text), Active. `+ Register device` shows the API key once in a copy-to-clipboard modal with a warning.

### 7.9 Reports
Tabs: Sessions, Traceability, Pass/Fail, Workers, Devices. Shared filter panel on top; results table; `Export Excel` and `Export PDF` buttons top right. Session detail: header (job, product, worker, devices, times, final result chip) and a step table with target, band, actual, attempt, result chip, device, time.

### 7.10 Worker Job Board
```
┌─────────────────────────────────────────────────────────────────────────┐
│  Jobs for today                                    ⟳ Refresh   Filter ▾ │
│ ┌─────────────────────────────────────────────────────────────────────┐ │
│ │▌● BLACK  Critical     JOB-20261012-0007    Locker 6-Panel   Qty 2   │ │
│ │▌                      Due 12 Oct                          [  Start  ]│ │
│ └─────────────────────────────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────────────────────────────┐ │
│ │ ▲ RED                 JOB-20261012-0003    Locker 4-Panel   Qty 5   │ │
│ │                       Due 13 Oct                          [  Start  ]│ │
│ └─────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
```
Cards (not a dense table) with large `Start` button (56 px). Black cards are visually heavier (black left border, bold). A toast/banner appears when a new Black job arrives. A job taken by someone else shows a lock icon and is disabled.

### 7.11 Worker Execute screen (the main screen)
Two columns on tablet landscape:

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│ Locker 6-Panel › Left Panel                 Bolt 3 of 8       ● Torque  ⌁ ESP32-TW-001 online │
├───────────────────────────────────────┬──────────────────────────────────────────┤
│                                       │  Bolt 3 – Top Left                       │
│   [ component image ]                 │                                          │
│       • 1      • 2                    │  Required      25.0 N·m                  │
│            ◉ 3  ← pulsing brand ring  │  Accept band   24.0 – 26.0 N·m           │
│       • 4      • 5 …                  │                                          │
│                                       │            24.8                          │
│                                       │            N·m           ← live value    │
│                                       │  ├──────▓▓▓▓▓▓▓▓▓▓▓▓▓──▲────────────┤     │
│                                       │   0     24.0 ──── 26.0       peak 24.8   │
│                                       │                                          │
│                                       │  ┌────────────────────────────────────┐ │
│                                       │  │ ✔ PASS  24.8 N·m  — next: Bolt 4   │ │
│                                       │  └────────────────────────────────────┘ │
├───────────────────────────────────────┴──────────────────────────────────────────┤
│ Attempt 1 of 3                                           [ Help ]  [ Hold job ]  │
└──────────────────────────────────────────────────────────────────────────────────┘
```

Rules:
- **Left (≈45%):** component image; current marker pulses in `--brand` with its number; done markers show a ✔ in `--pass`; failed markers show ✖ in `--fail`; future markers are small grey dots.
- **Right (≈55%):** step label (28 px), **Required** and **Accept band** values, then the **live value** at 96–140 px with the unit.
- **Torque view:** horizontal bar gauge from 0 to ~125% of the upper limit; the accept band is a shaded green zone with its limits labelled; a needle/marker follows the live value; **peak** value is shown and held until accepted. Live value text colour: `--text` while outside the band, `--pass-text` inside, `--fail` when the stable value was out. The state is also written as text beneath ("In range", "Too low", "Too high") with an icon.
- **Vernier view:** a large digital readout (mm, 3 decimals) with the accept band beneath as `min – max mm`; a small linear marker shows position inside the band; "Hold" or "Reading captured" label when stable.
- The view **switches automatically** when the next step has a different instrument (short 200 ms cross-fade; announce via `aria-live`). No tabs or toggles.
- **Result banner** appears below the value after each stable reading and stays until the next step starts (about 1.5 s for PASS auto-advance, until acknowledged for FAIL with retries left): `✔ PASS` or `✖ FAIL – too low (22.1 N·m). Retry (2 of 3 left)`.
- **Step counter** in the header: `Bolt 3 of 8`, plus a thin progress bar in `--brand`.
- **Device chips** show the bound devices with online dot and text; a warning banner replaces them if a bound device goes offline ("Torque wrench offline: check Wi-Fi / power").
- **Footer:** attempt counter, Help, Hold job. When all steps pass, the footer is replaced by a full-width primary **Submit** button.
- Below `md`, stack: image (collapsible), step label, live value, banner, footer.
- Keep the screen awake while a session is open (Wake Lock API where available).
- Audio feedback optional, off by default: short tone for PASS, double tone for FAIL (setting on the profile page).

### 7.12 Worker Summary
After Submit: final result chip, list of steps with actual values and chips, time taken, `Back to jobs` primary button, and for multi-component jobs "Next component" list with remaining ones.

### 7.13 Component picker
List of remaining components for the job (e.g. Left Panel ×2, Door ×1) with a progress ring per unit. A component in progress shows `Resume`.

### 7.14 My History
Simple list of the worker's sessions with date, job, component, result chip, duration. Detail opens the same step table as the Admin session detail (read only).

---

## 8. Motion and Feedback

- Motion is subtle: 150 to 200 ms ease-out for hover, modal and view cross-fade.
- The live gauge updates with CSS transitions of ~100 ms; no easing that makes the value feel laggy.
- Respect `prefers-reduced-motion`: disable pulsing, fades and animations.
- Success feedback is immediate and short; failure feedback persists until acknowledged.

---

## 9. Accessibility Checklist

- All text meets WCAG AA contrast (see section 2).
- Colour is never the only indicator: PASS/FAIL icon + label; priority label + shape; in-range text beneath the gauge; device online text.
- Every interactive element is keyboard reachable with a visible focus ring.
- Form fields have labels; errors are linked with `aria-describedby`.
- Live updates use `aria-live="polite"` (value changes) and `assertive` (FAIL, device offline).
- Touch targets ≥ 48 px on Worker screens; spacing ≥ 8 px between targets.
- Images have alt text (template name + step label).
- Works at 200% zoom without horizontal scrolling on Admin pages.

---

## 10. Assets and File Layout

```
wwwroot/
  css/theme.css            tokens above and Bootstrap overrides
  css/admin.css            sidebar, tables, wizard
  css/worker.css           job cards, execute screen, gauge
  js/worker-execute.js     SignalR client, gauge, step switching
  js/template-wizard.js    marker placement, step table
  js/product-builder.js    drag to reorder
  js/bm-preview.js         row overrides
  lib/bootstrap/           vendored
  lib/bootstrap-icons/     vendored
  lib/signalr/             vendored @microsoft/signalr
  lib/fonts/               optional self-hosted Inter
  img/logo.svg             placeholder logo (replace with the plant's logo)
  img/empty-*.svg          local empty-state illustrations
  uploads/                 template images (not in source control)
```
Rule: the only place hex colours appear is `theme.css`. The gauge, markers and badges read tokens via CSS variables (`getComputedStyle` in JS if a canvas is used).

---

## 11. Copy and Tone

- Plain, short, active voice. "Tighten Bolt 3", not "Please proceed to tighten bolt number three".
- Errors say what happened and what to do: "Torque wrench offline. Check Wi-Fi and power, then retry."
- Use the project vocabulary exactly: Template, Product, Job, Session, Step, Transmitter ID, BM.
- Units always shown with values: `25.0 N·m`, `150.250 mm`.
- Dates in the plant's local format; times 24 h.
