/**
 * AssemblIQ Auto-Dismiss System
 * Automatically fades out flash alert banners and popup notifications after 5 seconds.
 */
(function () {
    'use strict';

    function dismissAlert(alertEl) {
        if (!alertEl || alertEl.dataset.dismissing === 'true') return;
        // Do not auto-dismiss persistent workbook state warnings (like Partially Imported Workbook)
        if (alertEl.classList.contains('warning') && alertEl.innerText.includes('Partially Imported')) return;
        if (alertEl.classList.contains('stay-open')) return;

        alertEl.dataset.dismissing = 'true';
        alertEl.classList.add('auto-dismiss-fade');
        alertEl.title = 'Click to dismiss';
        alertEl.style.maxHeight = (alertEl.scrollHeight + 60) + 'px';

        // Clicking the alert dismisses it immediately
        alertEl.addEventListener('click', function () {
            alertEl.classList.add('fade-out');
            setTimeout(function () {
                if (alertEl.parentNode) alertEl.style.display = 'none';
            }, 400);
        });

        // Auto-dismiss after 5 seconds
        setTimeout(function () {
            alertEl.classList.add('fade-out');
            setTimeout(function () {
                if (alertEl.parentNode) {
                    alertEl.style.display = 'none';
                }
            }, 550);
        }, 5000);
    }

    function initAutoDismiss() {
        var selector = '.alert-inline.success, .alert-inline.error, .alert-inline.info, .alert.alert-success, .alert.alert-danger, .alert.alert-info, .alert-dismissible:not(.stay-open)';
        document.querySelectorAll(selector).forEach(function (el) {
            dismissAlert(el);
        });

        // Watch for dynamically inserted alerts
        if (window.MutationObserver) {
            var observer = new MutationObserver(function (mutations) {
                mutations.forEach(function (mutation) {
                    mutation.addedNodes.forEach(function (node) {
                        if (node.nodeType === 1) { // Element node
                            if (node.matches && node.matches(selector)) {
                                dismissAlert(node);
                            }
                            if (node.querySelectorAll) {
                                node.querySelectorAll(selector).forEach(function (child) {
                                    dismissAlert(child);
                                });
                            }
                        }
                    });
                });
            });
            observer.observe(document.body, { childList: true, subtree: true });
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAutoDismiss);
    } else {
        initAutoDismiss();
    }
})();
