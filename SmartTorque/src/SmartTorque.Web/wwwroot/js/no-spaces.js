/**
 * Prevents spaces / whitespace in code and identifier input fields.
 * Attach via `data-no-spaces="true"` or class `no-spaces`.
 */
(function () {
    'use strict';

    const SELECTOR = 'input[data-no-spaces="true"], input.no-spaces';

    function stripWhitespace(el) {
        if (!el || typeof el.value !== 'string') return;
        if (!/\s/.test(el.value)) return;
        const start = el.selectionStart;
        const end = el.selectionEnd;
        const before = el.value.slice(0, start ?? el.value.length);
        const removedBefore = (before.match(/\s/g) || []).length;
        el.value = el.value.replace(/\s+/g, '');
        if (typeof start === 'number' && typeof end === 'number') {
            const nextPos = Math.max(0, start - removedBefore);
            try { el.setSelectionRange(nextPos, nextPos); } catch (_) { }
        }
    }

    document.addEventListener('keydown', function (e) {
        const el = e.target;
        if (!el || !el.matches || !el.matches(SELECTOR)) return;
        if (e.key === ' ' || e.code === 'Space') {
            e.preventDefault();
        }
    });

    document.addEventListener('input', function (e) {
        const el = e.target;
        if (!el || !el.matches || !el.matches(SELECTOR)) return;
        stripWhitespace(el);
    });

    document.addEventListener('paste', function (e) {
        const el = e.target;
        if (!el || !el.matches || !el.matches(SELECTOR)) return;
        setTimeout(function () { stripWhitespace(el); }, 0);
    });
})();
