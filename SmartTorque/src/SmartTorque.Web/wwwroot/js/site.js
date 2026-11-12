// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Global form submission loading states
document.addEventListener('DOMContentLoaded', function () {
    // Handle all form submissions
    document.querySelectorAll('form').forEach(function (form) {
        form.addEventListener('submit', function (e) {
            // Skip forms with data-no-loading attribute
            if (form.hasAttribute('data-no-loading')) {
                return;
            }

            // Find the submit button that was clicked
            var submitBtn = form.querySelector('button[type="submit"]');
            if (!submitBtn) {
                submitBtn = form.querySelector('input[type="submit"]');
            }

            if (submitBtn && !submitBtn.disabled) {
                // Store original button content
                var originalContent = submitBtn.innerHTML;
                submitBtn.setAttribute('data-original-content', originalContent);

                // Disable button and show spinner
                submitBtn.disabled = true;
                submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>Processing...';

                // Re-enable after 30 seconds as a safety fallback
                setTimeout(function () {
                    if (submitBtn.disabled) {
                        submitBtn.disabled = false;
                        var original = submitBtn.getAttribute('data-original-content');
                        if (original) {
                            submitBtn.innerHTML = original;
                        }
                    }
                }, 30000);
            }
        });
    });

    // Handle AJAX form submissions (if using fetch/XMLHttpRequest)
    // This allows programmatic re-enabling of buttons
    window.enableFormSubmit = function (formOrButton) {
        var btn = formOrButton.tagName === 'BUTTON' ? formOrButton : formOrButton.querySelector('button[type="submit"]');
        if (btn) {
            btn.disabled = false;
            var original = btn.getAttribute('data-original-content');
            if (original) {
                btn.innerHTML = original;
            }
        }
    };
});
