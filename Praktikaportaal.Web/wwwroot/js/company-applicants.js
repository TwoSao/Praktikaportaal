// Applicants page: when a new application for THIS internship arrives, reload the list in place
// (a fresh copy of the page is fetched and only the list is swapped; filters and page stay as they are).

(function () {
    'use strict';

    var list = document.getElementById('applicantsList');
    if (!list) { return; }

    var internshipId = list.getAttribute('data-internship-id');
    var refreshing = false;

    document.addEventListener('application-received', function (event) {
        if (event.detail.internshipId !== internshipId || refreshing) { return; }
        refreshing = true;

        fetch(window.location.href, { credentials: 'same-origin', headers: { 'X-Requested-With': 'fetch' } })
            .then(function (response) { return response.ok ? response.text() : Promise.reject(response.status); })
            .then(function (html) {
                var doc = new DOMParser().parseFromString(html, 'text/html');
                var fresh = doc.getElementById('applicantsList');
                if (!fresh) { return; }

                var known = {};
                list.querySelectorAll('[data-application-id]').forEach(function (el) { known[el.getAttribute('data-application-id')] = true; });

                list.innerHTML = fresh.innerHTML;
                list.querySelectorAll('[data-application-id]').forEach(function (el) {
                    if (!known[el.getAttribute('data-application-id')]) { el.classList.add('row-new'); }
                });

                var count = doc.getElementById('applicantsTotal');
                var current = document.getElementById('applicantsTotal');
                if (count && current) { current.textContent = count.textContent; }
            })
            .catch(function () { /* the toast already told the user; a manual refresh still works */ })
            .finally(function () { refreshing = false; });
    });
})();
