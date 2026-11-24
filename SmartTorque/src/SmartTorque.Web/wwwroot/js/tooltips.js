/**
 * Global Tooltip System
 * AssemblIQ / SmartTorque
 * Initializes and manages hover-based tooltips for page descriptions
 */

(function () {
    'use strict';

    let activeTooltip = null;
    let hideTimeout = null;
    let isTouchDevice = false;

    // Detect touch device
    if ('ontouchstart' in window || navigator.maxTouchPoints > 0) {
        isTouchDevice = true;
    }

    /**
     * Create tooltip element
     */
    function createTooltip(text, position) {
        const tooltip = document.createElement('div');
        tooltip.className = `app-tooltip ${position}`;
        tooltip.textContent = text;
        tooltip.setAttribute('role', 'tooltip');
        return tooltip;
    }

    /**
     * Calculate best position for tooltip
     */
    function calculatePosition(trigger) {
        const rect = trigger.getBoundingClientRect();
        const viewportWidth = window.innerWidth;
        const viewportHeight = window.innerHeight;

        // Estimate tooltip size (we'll adjust after rendering)
        const estimatedTooltipHeight = 80;
        const estimatedTooltipWidth = 320;

        let position = 'bottom'; // Default to bottom to avoid clipping at top

        // Check if there's enough space above (at least 100px from top)
        if (rect.top > 150) {
            position = 'top';
        } else if (rect.bottom < viewportHeight - 100) {
            // Prefer bottom if there's space
            position = 'bottom';
        }

        // Check horizontal space for left/right positioning
        if (rect.right > viewportWidth - 150 && rect.left > 150) {
            position = 'left';
        } else if (rect.left < 150 && rect.right < viewportWidth - 150) {
            position = 'right';
        }

        return position;
    }

    /**
     * Show tooltip
     */
    function showTooltip(trigger) {
        if (activeTooltip) {
            hideTooltip();
        }

        const text = trigger.getAttribute('data-tooltip');
        if (!text) return;

        const position = calculatePosition(trigger);
        const tooltip = createTooltip(text, position);

        trigger.style.position = 'relative';
        trigger.appendChild(tooltip);

        // Force reflow for transition
        tooltip.offsetHeight;

        // Check actual tooltip dimensions and reposition if needed
        setTimeout(() => {
            const tooltipRect = tooltip.getBoundingClientRect();
            const triggerRect = trigger.getBoundingClientRect();

            // Check if tooltip is clipped at top
            if (tooltipRect.top < 10) {
                // Switch to bottom position
                tooltip.classList.remove('top');
                tooltip.classList.add('bottom');
            }

            // Check if tooltip is clipped at bottom
            if (tooltipRect.bottom > window.innerHeight - 10) {
                // Switch to top position
                tooltip.classList.remove('bottom');
                tooltip.classList.add('top');
            }

            // Check if tooltip is clipped on right
            if (tooltipRect.right > window.innerWidth - 10) {
                tooltip.style.left = 'auto';
                tooltip.style.right = '0';
            }

            // Check if tooltip is clipped on left
            if (tooltipRect.left < 10) {
                tooltip.style.left = '0';
                tooltip.style.right = 'auto';
            }
        }, 10);

        // Show tooltip
        requestAnimationFrame(() => {
            tooltip.classList.add('show');
        });

        activeTooltip = {
            element: tooltip,
            trigger: trigger
        };
    }

    /**
     * Hide tooltip
     */
    function hideTooltip() {
        if (!activeTooltip) return;

        const { element, trigger } = activeTooltip;

        element.classList.remove('show');

        setTimeout(() => {
            if (element.parentNode === trigger) {
                trigger.removeChild(element);
            }
        }, 200);

        activeTooltip = null;
    }

    /**
     * Handle mouse enter
     */
    function handleMouseEnter(event) {
        const trigger = event.currentTarget;

        clearTimeout(hideTimeout);

        // Small delay before showing (prevent flickering on quick mouse movements)
        hideTimeout = setTimeout(() => {
            showTooltip(trigger);
        }, 300);
    }

    /**
     * Handle mouse leave
     */
    function handleMouseLeave() {
        clearTimeout(hideTimeout);

        // Small delay before hiding (allow moving cursor to tooltip area)
        hideTimeout = setTimeout(() => {
            hideTooltip();
        }, 100);
    }

    /**
     * Handle touch/click on mobile or desktop click
     */
    function handleTouch(event) {
        const trigger = event.currentTarget;

        if (activeTooltip && activeTooltip.trigger === trigger) {
            hideTooltip();
        } else {
            event.preventDefault();
            event.stopPropagation();
            showTooltip(trigger);

            // Auto-hide after 5 seconds on touch devices
            setTimeout(() => {
                if (activeTooltip && activeTooltip.trigger === trigger) {
                    hideTooltip();
                }
            }, 5000);
        }
    }

    /**
     * Initialize tooltips
     */
    function initTooltips(container) {
        const root = container || document;
        const triggers = root.querySelectorAll('[data-tooltip]');

        triggers.forEach(trigger => {
            // Skip if already initialized
            if (trigger.dataset.tooltipInitialized === 'true') return;

            trigger.dataset.tooltipInitialized = 'true';

            // Add accessibility attributes
            trigger.setAttribute('aria-label', trigger.getAttribute('data-tooltip'));

            // Support both hover on desktop and tap/click on all devices
            trigger.addEventListener('mouseenter', handleMouseEnter);
            trigger.addEventListener('mouseleave', handleMouseLeave);
            trigger.addEventListener('click', handleTouch);
        });
    }

    /**
     * Close tooltip when clicking outside
     */
    document.addEventListener('click', (event) => {
        if (activeTooltip && !activeTooltip.trigger.contains(event.target)) {
            hideTooltip();
        }
    });

    /**
     * Close tooltip on scroll
     */
    window.addEventListener('scroll', () => {
        if (activeTooltip) {
            hideTooltip();
        }
    }, { passive: true });

    /**
     * Close tooltip on window resize
     */
    window.addEventListener('resize', () => {
        if (activeTooltip) {
            hideTooltip();
        }
    });

    /**
     * Initialize on DOM ready
     */
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => initTooltips());
    } else {
        initTooltips();
    }

    /**
     * Reinitialize when new content is added (for AJAX/dynamic content)
     */
    const observer = new MutationObserver((mutations) => {
        mutations.forEach((mutation) => {
            mutation.addedNodes.forEach((node) => {
                if (node.nodeType === 1) {
                    if (node.hasAttribute && node.hasAttribute('data-tooltip')) {
                        initTooltips(node.parentElement);
                    } else if (node.querySelectorAll) {
                        initTooltips(node);
                    }
                }
            });
        });
    });

    observer.observe(document.documentElement, { childList: true, subtree: true });

    // Expose global API
    window.AppTooltips = {
        init: initTooltips,
        show: showTooltip,
        hide: hideTooltip
    };

})();
