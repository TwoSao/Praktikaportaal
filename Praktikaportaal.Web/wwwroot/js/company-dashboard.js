// Company dashboard: when a new application arrives (see live-applications.js) bump the counter of
// the internship and add the application to the "Uued avaldused" feed - no page refresh needed.

(function () {
    'use strict';

    var feed = document.getElementById('liveFeed');
    if (!feed) { return; }

    var empty = document.getElementById('liveFeedEmpty');
    var applicantsUrlBase = feed.getAttribute('data-applicants-url');   // e.g. /Company/Applicants

    document.addEventListener('application-received', function (event) {
        var m = event.detail;

        // 1) counters
        var counter = document.querySelector('[data-applications-count="' + m.internshipId + '"]');
        if (counter) {
            counter.textContent = (parseInt(counter.textContent, 10) || 0) + 1;
            var row = counter.closest('tr');
            if (row) { row.classList.remove('row-new'); void row.offsetWidth; row.classList.add('row-new'); }
        }
        var total = document.getElementById('totalApplications');
        if (total) { total.textContent = (parseInt(total.textContent, 10) || 0) + 1; }

        // 2) feed entry (textContent only: names and titles come from users)
        if (empty) { empty.remove(); empty = null; }
        var item = document.createElement('li');
        item.className = 'list-group-item row-new';
        item.setAttribute('data-application-id', m.applicationId);

        var who = document.createElement('strong');
        who.textContent = m.studentName || 'Õppur';
        var link = document.createElement('a');
        link.href = applicantsUrlBase + '/' + encodeURIComponent(m.internshipId);
        link.textContent = m.internshipTitle || 'praktikakoht';
        var time = document.createElement('div');
        time.className = 'small text-muted';
        time.textContent = new Date(m.createdAt || Date.now()).toLocaleString('et-EE');

        item.appendChild(who);
        item.appendChild(document.createTextNode(' avaldas kohale '));
        item.appendChild(link);
        item.appendChild(time);
        feed.insertBefore(item, feed.firstChild);

        while (feed.children.length > 20) { feed.removeChild(feed.lastChild); }
    });
})();
