// Small touches for the menus. The pages work without this file, it only makes the menus nicer:
// it closes the account menu and the phone menu when you click somewhere else, press Escape or open a page.
(function () {
    function closeUserMenu() {
        document.querySelectorAll('details.pop[open]').forEach(function (menu) { menu.open = false; });
    }

    function closeMenus() {
        closeUserMenu();
        const toggle = document.getElementById('nav-toggle');
        if (toggle) {
            toggle.checked = false;
        }
    }

    document.addEventListener('click', function (event) {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        // A click anywhere outside an open pop-up menu of the header (notifications, settings) closes it.
        document.querySelectorAll('details.pop[open]').forEach(function (menu) {
            if (!menu.contains(target)) {
                menu.open = false;
            }
        });

        // Choosing a page in the phone menu closes it.
        if (target.closest('.sidebar a')) {
            closeMenus();
        }
    });

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape') {
            closeMenus();
        }
    });

    // Blazor changes the page without loading it again, so a menu that was open would stay open.
    if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
        window.Blazor.addEventListener('enhancednavigationend', closeMenus);
    }
})();

// The settings menu: light / dark / automatic, and animations on or off. The choice is kept in this browser (localStorage)
// and in a cookie. The cookie is what lets the server draw the right look on every page, also the ones Blazor swaps in
// when a link is followed (App.razor reads it).
(function () {
    var root = document.documentElement;

    function save(key, value) {
        try {
            if (value === null) { localStorage.removeItem(key); } else { localStorage.setItem(key, value); }
        } catch (error) { }

        try {
            document.cookie = key + '=' + (value === null ? '' : value) + '; path=/; samesite=lax; max-age=' + (value === null ? 0 : 31536000);
        } catch (error) { }
    }

    function setTheme(choice) {
        if (choice === 'light' || choice === 'dark') {
            root.setAttribute('data-theme', choice);
            root.setAttribute('data-theme-pref', choice);
            save('seaitt-theme', choice);
        } else {
            root.removeAttribute('data-theme');
            root.setAttribute('data-theme-pref', 'auto');
            save('seaitt-theme', null);
        }
    }

    function setMotion(on) {
        if (on) { root.removeAttribute('data-motion'); save('seaitt-motion', null); }
        else { root.setAttribute('data-motion', 'off'); save('seaitt-motion', 'off'); }
    }

    document.addEventListener('click', function (event) {
        var target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        var theme = target.closest('[data-theme-choice]');
        if (theme) {
            setTheme(theme.getAttribute('data-theme-choice'));
            return;
        }

        if (target.closest('[data-motion-toggle]')) {
            setMotion(root.getAttribute('data-motion') === 'off');
        }
    });
})();

// The teacher's calendar: an "unassigned activity" (an element with data-unassigned-item, in the widgets at the right) is dragged onto a day
// (an element with data-day, in the month or week view). Dropping it opens the calendar again with ?edit=<item>&on=<day>, which shows the box
// that sets its due date. (The "Set date" link next to every activity does the same without dragging.)
(function () {
    var dragging = null;

    function itemOf(target) {
        return target instanceof Element ? target.closest('[data-unassigned-item]') : null;
    }

    function dayOf(target) {
        return target instanceof Element ? target.closest('[data-day]') : null;
    }

    function clearMarks() {
        document.querySelectorAll('.drop-over').forEach(function (el) { el.classList.remove('drop-over'); });
        document.querySelectorAll('.dragging').forEach(function (el) { el.classList.remove('dragging'); });
        document.documentElement.classList.remove('dragging-item');
    }

    document.addEventListener('dragstart', function (event) {
        var item = itemOf(event.target);
        if (!item) {
            return;
        }

        dragging = item.getAttribute('data-unassigned-item');
        event.dataTransfer.effectAllowed = 'move';
        event.dataTransfer.setData('text/plain', dragging);
        item.classList.add('dragging');
        // The days that can take it are drawn as targets while it is held.
        document.documentElement.classList.add('dragging-item');
    });

    document.addEventListener('dragover', function (event) {
        var day = dayOf(event.target);
        if (!dragging || !day) {
            return;
        }

        event.preventDefault();
        event.dataTransfer.dropEffect = 'move';
        document.querySelectorAll('.drop-over').forEach(function (el) { if (el !== day) { el.classList.remove('drop-over'); } });
        day.classList.add('drop-over');
    });

    document.addEventListener('dragleave', function (event) {
        var day = dayOf(event.target);
        if (day && !day.contains(event.relatedTarget)) {
            day.classList.remove('drop-over');
        }
    });

    document.addEventListener('drop', function (event) {
        var day = dayOf(event.target);
        if (!dragging || !day) {
            return;
        }

        event.preventDefault();
        var item = dragging;
        dragging = null;
        clearMarks();

        var url = new URL(window.location.href);
        url.searchParams.set('edit', item);
        url.searchParams.set('on', day.getAttribute('data-day'));
        var target = url.pathname.replace(/^\//, '') + url.search;
        if (window.Blazor && typeof window.Blazor.navigateTo === 'function') {
            window.Blazor.navigateTo(target);
        } else {
            window.location.assign(url.toString());
        }
    });

    document.addEventListener('dragend', function () {
        dragging = null;
        clearMarks();
    });
})();

// Used by the pages that show a login to hand over (called from C# as "seaitt.copyText" and "seaitt.print").
window.seaitt = {
    copyText: async function (text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch (error) {
            // The clipboard needs a secure page (https or localhost). Elsewhere, select a hidden box and copy from it.
            const box = document.createElement('textarea');
            box.value = text;
            box.setAttribute('readonly', '');
            box.style.position = 'fixed';
            box.style.opacity = '0';
            document.body.appendChild(box);
            box.select();
            try {
                return document.execCommand('copy');
            } catch (copyError) {
                return false;
            } finally {
                document.body.removeChild(box);
            }
        }
    },
    // Shows the end of a scrolling box (the newest message of a conversation).
    scrollToEnd: function (element) {
        if (element) {
            element.scrollTop = element.scrollHeight;
        }
    },
    print: function () {
        window.print();
    },
    // The anti-cheating watch of a quiz. It tells the quiz page (a C# object, "reference") when the student leaves the tab or the
    // window, or tries to copy, cut or paste. Copying, cutting, pasting and the right-click menu are blocked while it is on.
    startQuizGuard: function (reference) {
        window.seaitt.stopQuizGuard();
        var last = 0;

        function report(what) {
            var now = Date.now();
            // Leaving the tab fires "hidden" and "blur" together: count it once.
            if (now - last < 1500) { return; }
            last = now;
            reference.invokeMethodAsync('OnSuspicious', what);
        }

        var handlers = {
            visibilitychange: function () { if (document.hidden) { report('Left the quiz tab'); } },
            blur: function () { report('Switched to another window'); },
            copy: function (event) { event.preventDefault(); report('Tried to copy'); },
            cut: function (event) { event.preventDefault(); report('Tried to cut'); },
            paste: function (event) { event.preventDefault(); report('Tried to paste'); },
            contextmenu: function (event) { event.preventDefault(); },
        };

        Object.keys(handlers).forEach(function (name) {
            (name === 'visibilitychange' ? document : name === 'blur' ? window : document).addEventListener(name, handlers[name]);
        });
        window.seaitt.quizGuard = handlers;
    },
    stopQuizGuard: function () {
        var handlers = window.seaitt.quizGuard;
        if (!handlers) { return; }
        Object.keys(handlers).forEach(function (name) {
            (name === 'visibilitychange' ? document : name === 'blur' ? window : document).removeEventListener(name, handlers[name]);
        });
        window.seaitt.quizGuard = null;
    },
};

// The pop-up of the courses of an enrollment (EnrollmentTable.razor) opens next to the button that opened it, below it when there is
// room and above it otherwise, and never outside the screen. Without this script it simply stays in the middle of the screen.
document.addEventListener('toggle', function (event) {
    var pop = event.target;
    if (!(pop instanceof HTMLElement) || !pop.classList.contains('courses-pop') || event.newState !== 'open') { return; }

    var button = document.querySelector('.courses-button[popovertarget="' + pop.id + '"]');
    if (!button) { return; }

    var anchor = button.getBoundingClientRect();
    var box = pop.getBoundingClientRect();
    var gap = 6;
    var margin = 8;
    var below = anchor.bottom + gap;
    var top = below + box.height <= window.innerHeight - margin ? below : Math.max(margin, anchor.top - gap - box.height);
    var left = Math.min(Math.max(margin, anchor.left), Math.max(margin, window.innerWidth - box.width - margin));

    pop.style.inset = 'auto';
    pop.style.margin = '0';
    pop.style.top = top + 'px';
    pop.style.left = left + 'px';
}, true);

// Ctrl+K (Cmd+K on a Mac) opens the search pop-up of the header (Components/Layout/SearchBox.razor).
document.addEventListener('keydown', function (event) {
    if ((event.ctrlKey || event.metaKey) && !event.shiftKey && !event.altKey && event.key.toLowerCase() === 'k') {
        var trigger = document.querySelector('[data-search-trigger]');
        if (trigger) {
            event.preventDefault();
            trigger.click();
        }
    }
});

// Auto refresh: a page with <AutoRefresh /> (Components/Shared/AutoRefresh.razor) loads itself again every few seconds with Blazor's
// enhanced navigation, which only changes what is different. It does not do it while somebody is typing, a pop-up or box is open, a
// text is selected or the tab is in the background, and it keeps the scroll position.
(function () {
    var last = Date.now();
    var busy = false;

    function marker() { return document.querySelector('[data-auto-refresh]'); }

    function somethingIsOpen() {
        var active = document.activeElement;
        if (active && (active.tagName === 'INPUT' || active.tagName === 'TEXTAREA' || active.tagName === 'SELECT' || active.isContentEditable)) { return true; }
        if (document.querySelector('[role="dialog"], .nav-toggle:checked, :popover-open, details.pop[open]')) { return true; }
        var selection = window.getSelection && window.getSelection();
        return !!(selection && !selection.isCollapsed && String(selection).length > 0);
    }

    function tick() {
        var m = marker();
        if (!m || busy || document.hidden || !window.Blazor || typeof Blazor.navigateTo !== 'function') { return; }
        var seconds = parseInt(m.getAttribute('data-auto-refresh'), 10) || 30;
        if (Date.now() - last < seconds * 1000 || somethingIsOpen()) { return; }

        busy = true;
        var x = window.scrollX, y = window.scrollY;
        last = Date.now();
        Blazor.navigateTo(location.pathname + location.search, { forceLoad: false, replaceHistory: true });
        // Back to where the person was after the page has been patched.
        setTimeout(function () { window.scrollTo(x, y); busy = false; }, 600);
    }

    // Going to another page starts the wait again.
    document.addEventListener('enhancedload', function () { last = Date.now(); });
    setInterval(tick, 2000);
})();
