// Live updates for companies: a WebSocket to this site (/ws/applications), which relays the API's
// "ApplicationReceived" messages. Reconnects automatically; shows a toast and tells the page via an event.
//
// Pages mark an element  <span id="liveStatus" data-ws-path="/ws/applications">  and listen to the
// "application-received" event on document (event.detail = the message).

(function () {
    'use strict';

    var status = document.getElementById('liveStatus');
    if (!status || !('WebSocket' in window)) { return; }

    var url = (location.protocol === 'https:' ? 'wss://' : 'ws://') + location.host + (status.getAttribute('data-ws-path') || '/ws/applications');

    var socket = null;
    var failedAttempts = 0;       // connection attempts in a row that never opened
    var retryTimer = null;
    var heartbeatTimer = null;
    var watchdogTimer = null;
    var leaving = false;

    var MAX_FAILED_ATTEMPTS = 8;
    var HEARTBEAT_MS = 25000;     // send "ping" this often ...
    var SILENCE_LIMIT_MS = 70000; // ... and reconnect if nothing at all arrives for this long

    function setStatus(connected, text) {
        var dot = status.querySelector('.live-dot');
        var label = status.querySelector('.live-text');
        if (dot) { dot.classList.toggle('on', connected); }
        if (label) { label.textContent = text; }
        status.setAttribute('data-connected', connected ? 'true' : 'false');
    }

    function clearTimers() {
        clearInterval(heartbeatTimer);
        clearTimeout(watchdogTimer);
    }

    function resetWatchdog() {
        clearTimeout(watchdogTimer);
        watchdogTimer = setTimeout(function () {
            // A half-open connection never fires onclose; closing it ourselves triggers the reconnect.
            if (socket) { socket.close(); }
        }, SILENCE_LIMIT_MS);
    }

    function scheduleReconnect() {
        if (leaving) { return; }
        failedAttempts++;
        if (failedAttempts > MAX_FAILED_ATTEMPTS) {
            setStatus(false, 'Reaalajas teated ei tööta. Värskenda lehte või logi uuesti sisse.');
            return;
        }
        // exponential back-off 1, 2, 4 ... max 30 s, plus a little random jitter
        var delay = Math.min(30000, 1000 * Math.pow(2, failedAttempts - 1)) + Math.random() * 500;
        setStatus(false, 'Ühendus katkes. Proovin uuesti ' + Math.round(delay / 1000) + ' s pärast…');
        retryTimer = setTimeout(connect, delay);
    }

    function connect() {
        clearTimeout(retryTimer);
        setStatus(false, 'Ühendan…');

        try {
            socket = new WebSocket(url);
        } catch (e) {
            scheduleReconnect();
            return;
        }

        var opened = false;

        socket.onopen = function () {
            opened = true;
            failedAttempts = 0;
            setStatus(true, 'Reaalajas ühendatud');
            resetWatchdog();
            heartbeatTimer = setInterval(function () {
                if (socket && socket.readyState === WebSocket.OPEN) { socket.send('ping'); }
            }, HEARTBEAT_MS);
        };

        socket.onmessage = function (event) {
            resetWatchdog();
            var message;
            try { message = JSON.parse(event.data); } catch (e) { return; }

            if (message.type === 'ApplicationReceived') {
                handleApplication(message);
            }
        };

        socket.onclose = function (event) {
            clearTimers();
            socket = null;
            if (leaving) { return; }
            if (event && event.code === 1008) {
                // The login (JWT) expired: reconnecting cannot work until the user logs in again.
                setStatus(false, 'Sessioon aegus. Palun logi uuesti sisse.');
                return;
            }
            if (opened) { failedAttempts = 0; }
            scheduleReconnect();
        };

        socket.onerror = function () { /* onclose follows and handles the retry */ };
    }

    function handleApplication(message) {
        if (window.showToast) {
            window.showToast('Uus avaldus', (message.studentName || 'Õppur') + ' → ' + (message.internshipTitle || 'praktikakoht'), 'success');
        }
        bumpNavbarBadge();
        document.dispatchEvent(new CustomEvent('application-received', { detail: message }));
    }

    // A new application also creates a notification, so the navbar counter goes up by one.
    function bumpNavbarBadge() {
        var link = document.querySelector('a.nav-link[href$="/Notifications"]');
        if (!link) { return; }
        var badge = document.getElementById('unreadBadge');
        if (!badge) {
            badge = document.createElement('span');
            badge.id = 'unreadBadge';
            badge.className = 'badge rounded-pill bg-danger ms-1';
            badge.textContent = '0';
            link.appendChild(badge);
        }
        var n = parseInt(badge.textContent, 10);
        badge.textContent = (isNaN(n) ? 0 : n) + 1;
    }

    window.addEventListener('beforeunload', function () {
        leaving = true;
        clearTimers();
        if (socket) { socket.close(); }
    });

    connect();
})();
