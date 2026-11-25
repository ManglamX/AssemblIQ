/**
 * Universal Searchable Combobox
 * AssemblIQ / SmartTorque
 * Automatically converts HTML <select> elements into searchable, keyboard-navigable comboboxes.
 * Fixes scroll/wheel handling so scrolling never prematurely closes menus or activates typing mode.
 */

(function () {
    'use strict';

    let activeCombobox = null;

    // Helper: Escape HTML
    function escapeHtml(str) {
        if (!str) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    // Enhance a single select element
    function enhanceSelect(select) {
        if (!select || select.nodeName !== 'SELECT') return null;
        if (select.dataset.comboboxInitialized === 'true') return null;
        if (select.multiple || select.hasAttribute('multiple')) return null;
        if (select.dataset.noCombobox === 'true') return null;

        select.dataset.comboboxInitialized = 'true';

        // Read attributes and classes
        const selectClasses = Array.from(select.classList);
        const isSmall = selectClasses.includes('form-select-sm') || select.classList.contains('form-control-sm');
        const isLarge = selectClasses.includes('form-select-lg') || select.classList.contains('form-control-lg');

        // Create container wrapper
        const wrapper = document.createElement('div');
        wrapper.className = 'combobox-wrapper';
        if (select.classList.contains('w-auto')) wrapper.classList.add('combobox-inline');
        if (select.style.width) wrapper.style.width = select.style.width;
        if (select.style.minWidth) wrapper.style.minWidth = select.style.minWidth;
        if (select.style.maxWidth) wrapper.style.maxWidth = select.style.maxWidth;

        // Create input wrap
        const inputWrap = document.createElement('div');
        inputWrap.className = 'combobox-input-wrap';

        // Create visible search/display input (starts readOnly to prevent accidental typing on scroll)
        const input = document.createElement('input');
        input.type = 'text';
        input.readOnly = true;
        input.className = 'form-control combobox-input' + (isSmall ? ' form-control-sm' : '') + (isLarge ? ' form-control-lg' : '');
        input.autocomplete = 'off';
        input.spellcheck = false;
        input.placeholder = select.getAttribute('placeholder') || '— Select or type to search —';
        if (select.title) input.title = select.title;
        if (select.disabled) input.disabled = true;

        if (select.style.fontSize) input.style.fontSize = select.style.fontSize;

        // Chevron arrow
        const arrow = document.createElement('span');
        arrow.className = 'combobox-arrow';
        arrow.innerHTML = '<i class="bi bi-chevron-down"></i>';

        inputWrap.appendChild(input);
        inputWrap.appendChild(arrow);
        wrapper.appendChild(inputWrap);

        // Create floating dropdown menu (appended to document.body)
        const menu = document.createElement('ul');
        menu.className = 'combobox-menu';
        document.body.appendChild(menu);

        // Hide original select visually while keeping it validatable by HTML5
        select.setAttribute('data-combobox-hidden', 'true');
        select.tabIndex = -1;

        // Insert wrapper right after select in DOM
        select.parentNode.insertBefore(wrapper, select.nextSibling);

        let optionsData = [];
        let highlightedIndex = -1;
        let isMenuOpen = false;
        let isTyping = false;

        // Extract options data from <select>
        function readOptions() {
            optionsData = [];
            const opts = select.options;
            for (let i = 0; i < opts.length; i++) {
                const opt = opts[i];
                optionsData.push({
                    index: i,
                    text: opt.text || '',
                    value: opt.value || '',
                    selected: opt.selected,
                    disabled: opt.disabled
                });
            }
        }

        function isPlaceholderOption(opt) {
            if (!opt) return true;
            const v = (opt.value || '').trim();
            return v === '' || v === '0';
        }

        // Sync input text with currently selected option in <select>
        function syncFromSelect() {
            readOptions();
            const selectedOpt = select.selectedOptions[0] || select.options[select.selectedIndex];
            if (isPlaceholderOption(selectedOpt)) {
                input.placeholder = selectedOpt ? selectedOpt.text.trim() : (select.getAttribute('placeholder') || '— Select or type to search —');
                input.value = '';
            } else {
                input.value = selectedOpt.text.trim();
            }
            input.disabled = select.disabled;
            if (select.value) {
                input.classList.remove('is-invalid');
            }
        }

        // Position the menu right below (or above) the input
        function positionMenu() {
            if (!isMenuOpen) return;
            const rect = input.getBoundingClientRect();
            if (rect.width === 0 && rect.height === 0) return; // not visible

            const menuHeight = Math.min(260, menu.scrollHeight || 200);
            const spaceBelow = window.innerHeight - rect.bottom;
            const spaceAbove = rect.top;

            menu.style.position = 'fixed';
            menu.style.left = Math.max(8, rect.left) + 'px';
            menu.style.width = Math.max(rect.width, 160) + 'px';

            if (spaceBelow < menuHeight && spaceAbove > spaceBelow) {
                menu.style.top = Math.max(8, rect.top - menuHeight - 2) + 'px';
            } else {
                menu.style.top = (rect.bottom + 2) + 'px';
            }
        }

        // Render filtered options in the dropdown menu
        function renderMenu(filterText) {
            readOptions();
            menu.innerHTML = '';
            const query = (filterText || '').trim().toLowerCase();

            let matched = optionsData.filter(function (opt) {
                if (!query) return true;
                return opt.text.toLowerCase().indexOf(query) !== -1 ||
                       opt.value.toLowerCase().indexOf(query) !== -1;
            });

            if (matched.length === 0) {
                const noResult = document.createElement('li');
                noResult.className = 'combobox-no-results';
                noResult.textContent = 'No matching options found';
                menu.appendChild(noResult);
                highlightedIndex = -1;
                return;
            }

            highlightedIndex = -1;

            matched.forEach(function (opt, idx) {
                const li = document.createElement('li');
                li.className = 'combobox-item';
                if (opt.selected) {
                    li.classList.add('is-selected');
                    if (highlightedIndex === -1) highlightedIndex = idx;
                }
                if (opt.disabled) li.classList.add('is-disabled');

                li.innerHTML = '<span>' + escapeHtml(opt.text) + '</span>';

                li.addEventListener('mousedown', function (e) {
                    e.preventDefault();
                    if (!opt.disabled) {
                        selectOption(opt);
                    }
                });

                menu.appendChild(li);
            });
        }

        // Open the menu
        function openMenu() {
            if (select.disabled || input.disabled) return;
            if (activeCombobox && activeCombobox !== comboboxInstance) {
                activeCombobox.close();
            }

            isMenuOpen = true;
            isTyping = false;
            input.readOnly = true; // remain readOnly until user explicitly types or clicks into search
            activeCombobox = comboboxInstance;
            wrapper.classList.add('is-open');

            renderMenu('');
            menu.classList.add('is-visible');
            positionMenu();

            // Scroll selected item into view inside the dropdown menu
            const selectedLi = menu.querySelector('.combobox-item.is-selected');
            if (selectedLi) {
                selectedLi.scrollIntoView({ block: 'nearest' });
            }
        }

        // Close the menu
        function closeMenu() {
            if (!isMenuOpen) return;
            isMenuOpen = false;
            isTyping = false;
            input.readOnly = true; // restore readOnly state
            wrapper.classList.remove('is-open');
            menu.classList.remove('is-visible');
            input.classList.remove('combobox-typing');

            if (activeCombobox === comboboxInstance) {
                activeCombobox = null;
            }

            // Restore display text (placeholder options stay as placeholder so user never has to delete them)
            const selectedOpt = select.selectedOptions[0] || select.options[select.selectedIndex];
            if (isPlaceholderOption(selectedOpt)) {
                input.placeholder = selectedOpt ? selectedOpt.text.trim() : (select.getAttribute('placeholder') || '— Select or type to search —');
                input.value = '';
            } else {
                input.value = selectedOpt.text.trim();
            }

            // Blur input to prevent staying in typing mode
            input.blur();
        }

        // Select an option
        function selectOption(opt) {
            select.value = opt.value;
            select.selectedIndex = opt.index;

            if (isPlaceholderOption(opt)) {
                input.placeholder = opt.text.trim();
                input.value = '';
            } else {
                input.value = opt.text.trim();
            }
            input.classList.remove('is-invalid');
            closeMenu();

            // Trigger change and input events on original select
            select.dispatchEvent(new Event('change', { bubbles: true }));
        }

        // Touch scrolling gesture detector:
        // If the user's finger drags/swipes across the dropdown, do NOT open the menu or activate typing!
        let touchStartY = 0;
        let isTouchScroll = false;
        inputWrap.addEventListener('touchstart', function (e) {
            if (e.touches && e.touches[0]) {
                touchStartY = e.touches[0].clientY;
                isTouchScroll = false;
            }
        }, { passive: true });

        inputWrap.addEventListener('touchmove', function (e) {
            if (e.touches && e.touches[0]) {
                if (Math.abs(e.touches[0].clientY - touchStartY) > 6) {
                    isTouchScroll = true;
                }
            }
        }, { passive: true });

        // Input Click: open menu and allow immediate typing
        input.addEventListener('click', function (e) {
            if (isTouchScroll) {
                isTouchScroll = false;
                return;
            }
            if (!isMenuOpen) {
                openMenu();
                input.readOnly = false;
                if (input.value) {
                    input.select();
                }
            } else {
                input.readOnly = false;
                if (input.value) {
                    input.select();
                }
            }
        });

        // Search Input: filter options in real-time when typing
        input.addEventListener('input', function (e) {
            e.stopPropagation(); // Stop propagation so live-filter form doesn't submit prematurely
            isTyping = true;
            input.classList.add('combobox-typing');
            if (!isMenuOpen) openMenu();
            renderMenu(input.value);
            positionMenu();
        });

        // Keyboard navigation & typing activation
        input.addEventListener('keydown', function (e) {
            // When user types character keys, enable search typing
            if (!e.ctrlKey && !e.altKey && !e.metaKey && e.key.length === 1) {
                if (input.readOnly) {
                    input.readOnly = false;
                    if (!isTyping) {
                        input.value = '';
                        isTyping = true;
                        input.classList.add('combobox-typing');
                    }
                }
            } else if (e.key === 'Backspace' && input.readOnly) {
                input.readOnly = false;
                input.value = '';
                isTyping = true;
                input.classList.add('combobox-typing');
                renderMenu('');
                positionMenu();
            }

            const items = Array.from(menu.querySelectorAll('.combobox-item:not(.is-disabled)'));

            if (e.key === 'ArrowDown') {
                e.preventDefault();
                if (!isMenuOpen) { openMenu(); return; }
                if (items.length) {
                    highlightedIndex = (highlightedIndex + 1) % items.length;
                    updateHighlight(items);
                }
            } else if (e.key === 'ArrowUp') {
                e.preventDefault();
                if (!isMenuOpen) { openMenu(); return; }
                if (items.length) {
                    highlightedIndex = (highlightedIndex - 1 + items.length) % items.length;
                    updateHighlight(items);
                }
            } else if (e.key === 'Enter') {
                if (isMenuOpen && highlightedIndex >= 0 && items[highlightedIndex]) {
                    e.preventDefault();
                    items[highlightedIndex].dispatchEvent(new MouseEvent('mousedown'));
                } else if (isMenuOpen && items.length === 1) {
                    e.preventDefault();
                    items[0].dispatchEvent(new MouseEvent('mousedown'));
                } else if (isMenuOpen) {
                    e.preventDefault();
                    closeMenu();
                }
            } else if (e.key === 'Escape') {
                e.preventDefault();
                closeMenu();
            } else if (e.key === 'Tab') {
                closeMenu();
            }
        });

        function updateHighlight(items) {
            items.forEach(function (el, idx) {
                if (idx === highlightedIndex) {
                    el.classList.add('is-highlighted');
                    el.scrollIntoView({ block: 'nearest' });
                } else {
                    el.classList.remove('is-highlighted');
                }
            });
        }

        // Input Blur: close menu after slight delay for click events
        input.addEventListener('blur', function () {
            setTimeout(function () {
                // If focus moved inside menu or wrapper, don't close
                if (document.activeElement && (wrapper.contains(document.activeElement) || menu.contains(document.activeElement))) {
                    return;
                }
                closeMenu();
            }, 180);
        });

        // Wheel handler on inputWrap:
        // When menu is OPEN, route mouse wheel scrolling into the dropdown menu so options scroll smoothly!
        // When menu is CLOSED, do NOT intercept so page scrolls naturally without focusing or typing.
        inputWrap.addEventListener('wheel', function (e) {
            if (isMenuOpen && menu) {
                menu.scrollTop += e.deltaY;
                e.preventDefault();
            }
        }, { passive: false });

        // Toggle via chevron arrow click
        arrow.addEventListener('click', function (e) {
            e.preventDefault();
            e.stopPropagation();
            if (isMenuOpen) {
                closeMenu();
            } else {
                input.focus();
                openMenu();
            }
        });

        // HTML5 validation integration
        select.addEventListener('invalid', function () {
            input.classList.add('is-invalid');
            input.focus();
        });

        // Form reset handler
        if (select.form) {
            select.form.addEventListener('reset', function () {
                setTimeout(syncFromSelect, 20);
            });
        }

        // Observe DOM mutations on <select> (dynamic options added/removed via JS)
        const observer = new MutationObserver(function () {
            syncFromSelect();
        });
        observer.observe(select, { childList: true, subtree: true, attributes: true, attributeFilter: ['disabled', 'class'] });

        // Intercept programmatic .value assignments on select
        const origDescriptor = Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value');
        if (origDescriptor && origDescriptor.set) {
            try {
                Object.defineProperty(select, 'value', {
                    get: function () {
                        return origDescriptor.get.call(this);
                    },
                    set: function (newVal) {
                        origDescriptor.set.call(this, newVal);
                        syncFromSelect();
                    },
                    configurable: true
                });
            } catch (err) {
                // Ignore if property redefinition is restricted
            }
        }

        // Listen for standard change events fired programmatically
        select.addEventListener('change', syncFromSelect);

        // Initial sync
        syncFromSelect();

        const comboboxInstance = {
            wrapper: wrapper,
            input: input,
            menu: menu,
            select: select,
            sync: syncFromSelect,
            open: openMenu,
            close: closeMenu,
            position: positionMenu,
            isOpen: function () { return isMenuOpen; }
        };

        return comboboxInstance;
    }

    // Global click listener to close open combobox when clicking outside
    document.addEventListener('click', function (e) {
        if (!activeCombobox) return;
        if (!activeCombobox.wrapper.contains(e.target) && !activeCombobox.menu.contains(e.target)) {
            activeCombobox.close();
        }
    });

    // Handle scroll events cleanly:
    // 1. If scrolling inside the combobox menu itself, ALLOW IT and DO NOT CLOSE!
    // 2. If scrolling the page/container outside, reposition the menu so it stays pinned to input!
    // 3. If input is scrolled out of viewport, close and blur input.
    window.addEventListener('scroll', function (e) {
        if (!activeCombobox || !activeCombobox.isOpen()) return;

        // If the scroll event originated from inside the combobox menu itself, DO NOT close!
        if (e.target === activeCombobox.menu || (activeCombobox.menu && activeCombobox.menu.contains(e.target))) {
            return;
        }

        // For outer/page scrolling, check if input is still in view
        const rect = activeCombobox.input.getBoundingClientRect();
        if (rect.bottom < 0 || rect.top > window.innerHeight) {
            activeCombobox.close();
        } else {
            activeCombobox.position();
        }
    }, true);

    // Reposition on window resize
    window.addEventListener('resize', function () {
        if (activeCombobox && activeCombobox.isOpen()) {
            activeCombobox.position();
        }
    });

    // Enhance all eligible <select> elements in a container
    function enhanceAllSelects(container) {
        const root = container || document;
        const selects = root.querySelectorAll('select.form-select, select:not([multiple]):not([data-no-combobox])');
        selects.forEach(function (sel) {
            enhanceSelect(sel);
        });
    }

    // Auto-initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            enhanceAllSelects();
        });
    } else {
        enhanceAllSelects();
    }

    // MutationObserver to automatically enhance newly added selects (modals, ajax, etc.)
    const docObserver = new MutationObserver(function (mutations) {
        mutations.forEach(function (mutation) {
            mutation.addedNodes.forEach(function (node) {
                if (node.nodeType === 1) {
                    if (node.nodeName === 'SELECT') {
                        enhanceSelect(node);
                    } else if (node.querySelectorAll) {
                        enhanceAllSelects(node);
                    }
                }
            });
        });
    });

    docObserver.observe(document.documentElement, { childList: true, subtree: true });

    // Expose global API
    window.SmartCombobox = {
        enhance: enhanceSelect,
        enhanceAll: enhanceAllSelects
    };

})();
