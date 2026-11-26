# Execute.cshtml Tablet-First Redesign - Implementation Guide

## Status: PARTIALLY COMPLETE

### ✅ COMPLETED (Tasks 3 & 4)
1. **Header restructured** into 2 rows with compact chips
2. **Hold/Resume consolidated** into single toggle button (removed redundant banner button)

### 🔄 IN PROGRESS (Task 6)
3. Started responsive grid layout conversion

### ⏳ REMAINING TASKS

## Task 2: Remove Manual Entry Modal (Lines 850-892, 533-541, 1404-1484)

### Delete Manual Entry Modal HTML (around line 850)
```
DELETE ENTIRE SECTION:
<div class="modal fade" id="manualEntryModal" ... to ... </div>
```

### Delete Manual Entry Button (around line 533-541)
```
DELETE:
<button type="button" class="btn btn-outline-primary btn-sm fw-semibold px-3 py-1 shadow-sm d-flex align-items-center gap-1.5 @(initConnected && !isManualToolMode ? "d-none" : "")" id="btn-cockpit-manual-entry" onclick="showManualEntryModal()">
    <i class="bi bi-pencil-square"></i>
    <span>Manual Entry</span>
</button>
```

### Delete Inline Manual Entry JavaScript Functions (around lines 1404-1484)
```
DELETE FUNCTIONS:
- showInlineManualInput()
- submitInlineManualReading()
- cancelInlineManualInput()
```

## Task 6: Complete Responsive Grid Layout

### Find the closing div for left column (around line 846)
```
CHANGE:
</div>

<!-- Right Column: Blueprint Viewer -->
<div class="col-12 col-lg-7 col-xl-8 d-flex flex-column" style="min-height: 0;">

TO:
</div>

<!-- Right Column: Blueprint Viewer -->
<div class="execute-right">
```

### Find the closing div for the main grid (around line 846+)
```
CHANGE final closing:
        </div>
    </div>

TO:
        </div>
    </div> <!-- end execute-grid -->
```

## Task 1: Add Touch Gesture Support

### Add after blueprint initialization (around line 2888, search for "bpApplyTransform")

```javascript
// Touch gesture support for blueprint canvas
(function initBlueprintTouchGestures() {
    const bpViewport = document.getElementById('bp-viewport');
    if (!bpViewport) return;

    let touchStartDist = 0;
    let touchStartScale = 1;
    let touchStartX = 0;
    let touchStartY = 0;

    bpViewport.addEventListener('touchstart', (e) => {
        if (e.touches.length === 2) {
            // Pinch zoom start
            e.preventDefault();
            const touch1 = e.touches[0];
            const touch2 = e.touches[1];
            touchStartDist = Math.hypot(
                touch2.clientX - touch1.clientX,
                touch2.clientY - touch1.clientY
            );
            touchStartScale = bpState.scale;
        } else if (e.touches.length === 1) {
            // Pan start
            const touch = e.touches[0];
            bpState.isDragging = true;
            touchStartX = touch.clientX - bpState.translateX;
            touchStartY = touch.clientY - bpState.translateY;
        }
    }, { passive: false });

    bpViewport.addEventListener('touchmove', (e) => {
        if (e.touches.length === 2) {
            // Pinch zoom
            e.preventDefault();
            const touch1 = e.touches[0];
            const touch2 = e.touches[1];
            const currentDist = Math.hypot(
                touch2.clientX - touch1.clientX,
                touch2.clientY - touch1.clientY
            );
            const scale = touchStartScale * (currentDist / touchStartDist);
            bpState.scale = Math.max(0.5, Math.min(5, scale));
            bpApplyTransform();
            const zoomLabel = document.getElementById("bp-zoom-label");
            if (zoomLabel) zoomLabel.textContent = Math.round(bpState.scale * 100) + "%";
        } else if (e.touches.length === 1 && bpState.isDragging) {
            // Pan
            e.preventDefault();
            const touch = e.touches[0];
            bpState.translateX = touch.clientX - touchStartX;
            bpState.translateY = touch.clientY - touchStartY;
            bpApplyTransform();
        }
    }, { passive: false });

    bpViewport.addEventListener('touchend', (e) => {
        if (e.touches.length < 2) {
            touchStartDist = 0;
        }
        if (e.touches.length === 0) {
            bpState.isDragging = false;
        }
    });

    // Prevent browser behaviors
    bpViewport.style.touchAction = 'none';
    bpViewport.style.overscrollBehavior = 'contain';
})();
```

### Update "Drag to pan" hint text (around line 639)
```
FIND:
<small class="text-white-50 mt-2 user-select-none" style="font-size: 0.72rem;">
    <i class="bi bi-hand-index"></i> Drag to pan • Scroll to zoom
</small>

CHANGE TO:
<small class="text-white-50 mt-2 user-select-none" style="font-size: 0.72rem;">
    <i class="bi bi-hand-index"></i> Pinch to zoom • Drag to pan
</small>
```

## Task 1b: Update Hold/Resume UI Logic (around line 3317-3345)

### Find the `updateHoldUi()` function and update it:

```javascript
function updateHoldUi() {
    const isBleConnected = !!(window.tohnichiBle && window.tohnichiBle.isConnected);
    
    // Update toolbar hold/resume toggle button
    const holdBtn = document.getElementById("btn-hold-toggle");
    const holdBtnIcon = document.getElementById("hold-btn-icon");
    const holdBtnText = document.getElementById("hold-btn-text");
    
    if (holdBtn && holdBtnIcon && holdBtnText) {
        if (isJobOnHold) {
            // Show Resume state (green, play icon)
            holdBtn.className = "btn btn-success fw-semibold shadow-sm d-flex align-items-center gap-2";
            holdBtn.style.minHeight = "48px";
            holdBtn.style.borderRadius = "6px";
            holdBtn.style.touchAction = "manipulation";
            holdBtnIcon.className = "bi bi-play-circle-fill";
            holdBtnText.textContent = "Resume";
        } else {
            // Show Hold Job state (amber, pause icon)
            holdBtn.className = "btn btn-warning fw-semibold shadow-sm d-flex align-items-center gap-2";
            holdBtn.style.minHeight = "48px";
            holdBtn.style.borderRadius = "6px";
            holdBtn.style.touchAction = "manipulation";
            holdBtnIcon.className = "bi bi-pause-circle";
            holdBtnText.textContent = "Hold Job";
        }
    }

    // Disable/enable controls based on hold state
    const manualBtn = document.getElementById("btn-send-reading");
    const bleConnectBtn = document.getElementById("btn-ble-connect");
    const inlineManualInput = document.getElementById("inline-manual-input");
    
    if (manualBtn) manualBtn.disabled = isJobOnHold;
    if (bleConnectBtn) bleConnectBtn.disabled = isJobOnHold;
    if (inlineManualInput) inlineManualInput.classList.toggle("d-none", isJobOnHold);
    
    // Update timer icon color
    const timerIcon = document.getElementById("timer-icon");
    if (timerIcon) {
        timerIcon.className = isJobOnHold ? "bi bi-pause-circle text-warning" : "bi bi-stopwatch text-warning";
    }
}
```

## CSS Changes (worker.css)

### Add Responsive Grid Styles

```css
/* Tablet-First Responsive Grid */
.execute-grid {
    display: flex;
    flex-direction: column;
    gap: 1rem;
}

.execute-left,
.execute-right {
    width: 100%;
}

/* Landscape Tablet: 1024px+ */
@media (min-width: 1024px) {
    .execute-grid {
        display: grid;
        grid-template-columns: 35% 65%;
        gap: 1.5rem;
        height: calc(100dvh - 150px);
    }
    
    .execute-left {
        overflow-y: auto;
    }
    
    .execute-right {
        display: flex;
        flex-direction: column;
        min-height: 0;
    }
}

/* Portrait Tablet: 768px-1023px */
@media (min-width: 768px) and (max-width: 1023px) {
    .execute-grid {
        display: flex;
        flex-direction: column;
    }
    
    .execute-right {
        height: 45vh;
        min-height: 320px;
    }
    
    .execute-left {
        overflow-y: auto;
        max-height: calc(55vh - 150px);
    }
}

/* Desktop: 1366px+ */
@media (min-width: 1366px) {
    .execute-grid {
        grid-template-columns: 32% 68%;
        max-width: 1600px;
        margin: 0 auto;
    }
}

/* Touch Target Sizing */
button, .btn {
    touch-action: manipulation;
    min-height: 40px;
}

.btn-lg, 
.execute-header button {
    min-height: 48px;
}

/* Ensure 8px spacing between touch targets */
.execute-header .d-flex {
    gap: 8px;
}

/* Header Styles */
.execute-header {
    background: white;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

.meta-chip {
    display: inline-flex;
    flex-direction: column;
    padding: 4px 12px;
    border: 1px solid #E5E5E5;
    border-radius: 6px;
    gap: 2px;
}

.meta-chip-label {
    font-size: 0.65rem;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    color: #666;
    font-weight: 600;
}

.meta-chip-val {
    font-size: 0.85rem;
    color: #222;
    font-weight: 600;
}
```

## Testing Checklist

### Resolutions to Test:
- [ ] 1280x800 (landscape tablet - primary)
- [ ] 1024x768 (landscape tablet - minimum)
- [ ] 768x1024 (portrait tablet)
- [ ] 1366x768 (desktop)

### Functional Tests:
- [ ] Hold button shows "Hold Job" (amber) when running
- [ ] Hold button shows "Resume" (green) when on hold
- [ ] Timer pauses when on hold
- [ ] Reload while on hold shows correct button state
- [ ] Pinch zoom works on blueprint (two fingers)
- [ ] Drag to pan works on blueprint
- [ ] All buttons are ≥48px height
- [ ] No manual entry button/modal visible
- [ ] Blueprint uses full height on right side
- [ ] Left column scrolls independently
- [ ] Header stays sticky on scroll

## Implementation Steps

1. **Complete remaining grid layout changes** (see Task 6 above)
2. **Remove manual entry** (see Task 2 above)  
3. **Add touch gestures** (see Task 1 above)
4. **Update CSS** in worker.css (add all styles from CSS section above)
5. **Update `updateHoldUi()` function** (see Task 1b above)
6. **Test on all breakpoints** (use browser dev tools responsive mode)

## Notes

- All API endpoints and state management preserved
- SignalR handlers unchanged
- Timer logic unchanged
- Blueprint marker rendering unchanged
- Only layout and UI styling modified
- Touch gestures added without breaking mouse/trackpad interaction
