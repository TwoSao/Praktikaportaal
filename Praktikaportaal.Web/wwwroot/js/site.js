// Praktikaportaal site script: small helpers shared by all pages.

(function () {
    'use strict';

    // Show a Bootstrap toast. type: success | info | warning | danger
    window.showToast = function (title, message, type) {
        var container = document.getElementById('toastContainer');
        if (!container || !window.bootstrap) { return; }

        var el = document.createElement('div');
        el.className = 'toast align-items-center border-0 text-bg-' + (type || 'primary');
        el.setAttribute('role', 'alert');
        el.innerHTML =
            '<div class="d-flex"><div class="toast-body"><strong></strong><div class="small"></div></div>' +
            '<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Sulge"></button></div>';
        // textContent (not innerHTML) so server data can never inject markup
        el.querySelector('strong').textContent = title;
        el.querySelector('.small').textContent = message || '';
        container.appendChild(el);

        var toast = new bootstrap.Toast(el, { delay: 8000 });
        el.addEventListener('hidden.bs.toast', function () { el.remove(); });
        toast.show();
    };

    // Confirm dialogs for dangerous buttons: <button data-confirm="Kas oled kindel?">
    document.addEventListener('click', function (e) {
        var target = e.target.closest('[data-confirm]');
        if (target && !window.confirm(target.getAttribute('data-confirm'))) {
            e.preventDefault();
        }
    });

    // Disable a submit button after the first click so a slow response is not submitted twice.
    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (form.hasAttribute('data-no-lock') || (window.jQuery && window.jQuery(form).data('validator') && !window.jQuery(form).valid())) { return; }
        var button = form.querySelector('button[type=submit]:not([data-no-lock])');
        if (button) {
            setTimeout(function () { button.disabled = true; }, 0);
        }
    });
})();
