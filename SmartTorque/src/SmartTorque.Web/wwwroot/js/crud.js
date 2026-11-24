/**
 * Global Confirmation Modal and Notification System
 * AssemblIQ / SmartTorque
 * Provides consistent confirmation dialogs and notifications for CRUD operations
 */

(function () {
    'use strict';

    // Notification System
    const notifications = {
        show: function (message, type = 'success') {
            const notification = document.createElement('div');
            notification.className = `app-notification app-notification-${type}`;
            notification.innerHTML = `
                <div class="app-notification-content">
                    <i class="bi bi-${this.getIcon(type)} me-2"></i>
                    <span>${message}</span>
                </div>
                <button type="button" class="app-notification-close" aria-label="Close">
                    <i class="bi bi-x"></i>
                </button>
            `;

            document.body.appendChild(notification);

            // Show notification
            setTimeout(() => notification.classList.add('show'), 10);

            // Close button
            const closeBtn = notification.querySelector('.app-notification-close');
            closeBtn.addEventListener('click', () => this.hide(notification));

            // Auto-hide after 5 seconds
            setTimeout(() => this.hide(notification), 5000);
        },

        hide: function (notification) {
            notification.classList.remove('show');
            setTimeout(() => {
                if (notification.parentNode) {
                    notification.parentNode.removeChild(notification);
                }
            }, 300);
        },

        getIcon: function (type) {
            const icons = {
                success: 'check-circle-fill',
                error: 'exclamation-triangle-fill',
                warning: 'exclamation-circle-fill',
                info: 'info-circle-fill'
            };
            return icons[type] || 'info-circle-fill';
        }
    };

    // Confirmation Modal
    let confirmModal = null;

    const confirmation = {
        show: function (options) {
            const defaults = {
                title: 'Confirm Action',
                message: 'Are you sure you want to proceed?',
                confirmText: 'Confirm',
                cancelText: 'Cancel',
                confirmClass: 'btn-danger',
                onConfirm: null,
                onCancel: null
            };

            const config = { ...defaults, ...options };

            // Create modal if it doesn't exist
            if (!confirmModal) {
                this.createModal();
            }

            // Update modal content
            const modalTitle = confirmModal.querySelector('.modal-title');
            const modalBody = confirmModal.querySelector('.modal-body');
            const confirmBtn = confirmModal.querySelector('.btn-confirm');
            const cancelBtn = confirmModal.querySelector('.btn-cancel');

            modalTitle.textContent = config.title;
            modalBody.textContent = config.message;
            confirmBtn.textContent = config.confirmText;
            cancelBtn.textContent = config.cancelText;

            // Update button class
            confirmBtn.className = `btn ${config.confirmClass} btn-confirm`;

            // Set up event handlers
            const handleConfirm = () => {
                if (config.onConfirm) config.onConfirm();
                this.hide();
            };

            const handleCancel = () => {
                if (config.onCancel) config.onCancel();
                this.hide();
            };

            // Remove old event listeners by cloning
            const newConfirmBtn = confirmBtn.cloneNode(true);
            const newCancelBtn = cancelBtn.cloneNode(true);
            confirmBtn.parentNode.replaceChild(newConfirmBtn, confirmBtn);
            cancelBtn.parentNode.replaceChild(newCancelBtn, cancelBtn);

            // Add new event listeners
            newConfirmBtn.addEventListener('click', handleConfirm);
            newCancelBtn.addEventListener('click', handleCancel);

            // Show modal
            const bsModal = new bootstrap.Modal(confirmModal);
            bsModal.show();
        },

        createModal: function () {
            confirmModal = document.createElement('div');
            confirmModal.className = 'modal fade';
            confirmModal.id = 'appConfirmModal';
            confirmModal.setAttribute('tabindex', '-1');
            confirmModal.setAttribute('aria-hidden', 'true');
            confirmModal.innerHTML = `
                <div class="modal-dialog modal-dialog-centered">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title">Confirm Action</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            Are you sure you want to proceed?
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary btn-cancel" data-bs-dismiss="modal">Cancel</button>
                            <button type="button" class="btn btn-danger btn-confirm">Confirm</button>
                        </div>
                    </div>
                </div>
            `;
            document.body.appendChild(confirmModal);
        },

        hide: function () {
            if (confirmModal) {
                const bsModal = bootstrap.Modal.getInstance(confirmModal);
                if (bsModal) bsModal.hide();
            }
        }
    };

    // Delete Confirmation Helper
    function confirmDelete(options) {
        const defaults = {
            entityType: 'record',
            entityName: '',
            dependencies: null,
            onConfirm: null
        };

        const config = { ...defaults, ...options };

        let message = `Are you sure you want to delete this ${config.entityType}?`;

        if (config.entityName) {
            message = `Are you sure you want to delete "${config.entityName}"?`;
        }

        if (config.dependencies) {
            message += `\n\n${config.dependencies}`;
        }

        message += '\n\nThis action cannot be undone.';

        confirmation.show({
            title: `Delete ${config.entityType}`,
            message: message,
            confirmText: 'Delete',
            cancelText: 'Cancel',
            confirmClass: 'btn-danger',
            onConfirm: config.onConfirm
        });
    }

    // AJAX Helper for CRUD operations
    function ajaxRequest(url, method, data, options = {}) {
        const defaults = {
            onSuccess: null,
            onError: null,
            successMessage: 'Operation completed successfully',
            errorMessage: 'Operation failed'
        };

        const config = { ...defaults, ...options };

        // Show loading state if button provided
        const button = config.button;
        let originalHtml;
        if (button) {
            originalHtml = button.innerHTML;
            button.disabled = true;
            button.innerHTML = '<i class="bi bi-hourglass-split"></i> Processing...';
        }

        const requestOptions = {
            method: method,
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            }
        };

        if (data) {
            requestOptions.body = JSON.stringify(data);
        }

        // Add anti-forgery token for POST/PUT/DELETE
        if (method !== 'GET') {
            const token = document.querySelector('input[name="__RequestVerificationToken"]');
            if (token) {
                requestOptions.headers['RequestVerificationToken'] = token.value;
            }
        }

        fetch(url, requestOptions)
            .then(response => {
                if (!response.ok) {
                    return response.json().then(err => Promise.reject(err));
                }
                return response.json();
            })
            .then(result => {
                if (button) {
                    button.disabled = false;
                    button.innerHTML = originalHtml;
                }

                if (result.success) {
                    notifications.show(result.message || config.successMessage, 'success');
                    if (config.onSuccess) config.onSuccess(result);
                } else {
                    notifications.show(result.message || config.errorMessage, 'error');
                    if (config.onError) config.onError(result);
                }
            })
            .catch(error => {
                if (button) {
                    button.disabled = false;
                    button.innerHTML = originalHtml;
                }

                const errorMsg = error.message || config.errorMessage;
                notifications.show(errorMsg, 'error');
                if (config.onError) config.onError(error);
                console.error('AJAX Error:', error);
            });
    }

    // Expose global API
    window.AppCRUD = {
        notify: notifications.show.bind(notifications),
        confirm: confirmation.show.bind(confirmation),
        confirmDelete: confirmDelete,
        ajax: ajaxRequest
    };

    // Initialize from TempData
    document.addEventListener('DOMContentLoaded', () => {
        // Check for TempData messages
        const successMsg = document.querySelector('meta[name="app-success"]');
        const errorMsg = document.querySelector('meta[name="app-error"]');
        const warningMsg = document.querySelector('meta[name="app-warning"]');
        const infoMsg = document.querySelector('meta[name="app-info"]');

        if (successMsg) notifications.show(successMsg.content, 'success');
        if (errorMsg) notifications.show(errorMsg.content, 'error');
        if (warningMsg) notifications.show(warningMsg.content, 'warning');
        if (infoMsg) notifications.show(infoMsg.content, 'info');
    });

})();
