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

        // Choosing a page in the menu closes the phone menu at once, and the box that opens beside the page (the flyout) too: it stays hidden until the pointer
        // leaves that page of the menu, so it does not stay over the page that was just opened.
        if (target.closest('.sidebar a')) {
            closeMenus();
            const item = target.closest('.item');
            if (item) {
                item.classList.add('fly-hidden');
                if (document.activeElement && document.activeElement.blur) { document.activeElement.blur(); }
                item.addEventListener('mouseleave', function () { item.classList.remove('fly-hidden'); }, { once: true });
            }
        }
    });

    // The phone menu is a drawer over the page. A press anywhere outside it (on the page, not on the menu or its button) folds it up, and the press still reaches
    // what was pressed, so the page can be used at once.
    document.addEventListener('pointerdown', function (event) {
        const toggle = document.getElementById('nav-toggle');
        if (!toggle || !toggle.checked) { return; }
        const target = event.target;
        if (target instanceof Element && !target.closest('.sidebar, .menu-button, label[for="nav-toggle"]')) {
            toggle.checked = false;
        }
    }, true);

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

// The box that opens beside a page of the menu (NavItem). The CSS shows and hides it; this only puts it at the height of the page it belongs to
// (without it the box would be where it sits in the menu, which also works), and keeps it on the screen.
(function () {
    function place(item) {
        const flyout = item.querySelector('.flyout');
        if (!flyout) { return; }
        const rect = item.getBoundingClientRect();
        const header = document.querySelector('.header');
        const top = header ? header.getBoundingClientRect().bottom : 0;
        const height = flyout.offsetHeight || 0;
        let y = rect.top - 6;
        y = Math.min(y, window.innerHeight - height - 8);
        y = Math.max(y, top + 4);
        flyout.style.setProperty('--fly-top', y + 'px');
    }

    function onEnter(event) {
        const target = event.target;
        if (!(target instanceof Element)) { return; }
        const item = target.closest('.item[data-flyout]');
        if (item) { place(item); }
    }

    document.addEventListener('mouseover', onEnter);
    document.addEventListener('focusin', onEnter);

    // Going from a page to a link of its box in a straight line crosses the pages below it, and their boxes would open over the one the person is heading to.
    // While the pointer is inside the triangle between where it left the page and the near edge of the open box, that box stays and the others wait (data-aim, data-aiming).
    let last = { x: 0, y: 0 };
    let aim = null;

    function inTriangle(p, a, b, c) {
        const side = function (p1, p2, p3) { return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y); };
        const d1 = side(p, a, b);
        const d2 = side(p, b, c);
        const d3 = side(p, c, a);
        return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
    }

    function stopAim() {
        if (aim) {
            aim.item.removeAttribute('data-aim');
            document.documentElement.removeAttribute('data-aiming');
            clearTimeout(aim.timer);
            aim = null;
        }
    }

    document.addEventListener('mousemove', function (event) {
        const p = { x: event.clientX, y: event.clientY };
        if (aim) {
            const box = aim.item.querySelector('.flyout').getBoundingClientRect();
            const inside = p.x >= box.left && p.x <= box.right && p.y >= box.top && p.y <= box.bottom;
            if (inside || !inTriangle(p, aim.from, { x: box.left, y: box.top }, { x: box.left, y: box.bottom })) { stopAim(); }
        }
        last = p;
    });

    document.addEventListener('mouseout', function (event) {
        const item = event.target instanceof Element ? event.target.closest('.item[data-flyout]') : null;
        if (!item || aim || (event.relatedTarget instanceof Node && item.contains(event.relatedTarget))) { return; }
        const flyout = item.querySelector('.flyout');
        if (!flyout) { return; }
        const style = getComputedStyle(flyout);
        const box = flyout.getBoundingClientRect();
        // Only a box that is open, to the right of the pointer, and a pointer that is moving towards it.
        if (style.visibility !== 'visible' || Number(style.opacity) < 0.5 || event.clientX < last.x || last.x >= box.left) { return; }
        aim = { item: item, from: last, timer: setTimeout(stopAim, 700) };
        item.setAttribute('data-aim', '');
        document.documentElement.setAttribute('data-aiming', '');
    });

    // A page that opens can change what is under a pointer that does not move (the menu of the school opens from its icons when a lesson is left, and then a page of it is under the
    // pointer): the boxes beside the menu stay shut until the pointer really moves again (data-nomove, NavItem.razor.css).
    function sleep() { document.documentElement.setAttribute('data-nomove', ''); }
    document.addEventListener('mousemove', function (event) {
        if ((event.movementX || event.movementY) && document.documentElement.hasAttribute('data-nomove')) { document.documentElement.removeAttribute('data-nomove'); }
    }, { passive: true });
    if (window.Blazor && typeof window.Blazor.addEventListener === 'function') { window.Blazor.addEventListener('enhancednavigationend', sleep); }

    // A click on a link of the menu closes its box until the pointer leaves the page's item (the pointer is still over it, and the box would cover the page).
    document.addEventListener('click', function (event) {
        const target = event.target;
        if (!(target instanceof Element)) { return; }
        const item = target.closest('.item[data-flyout]');
        if (item && target.closest('a')) { item.setAttribute('data-rest', ''); }
    });
    document.addEventListener('mouseover', function (event) {
        const target = event.target;
        document.querySelectorAll('.item[data-rest]').forEach(function (item) {
            if (!(target instanceof Node) || !item.contains(target)) { item.removeAttribute('data-rest'); }
        });
    });
})();

// The menu of a course is a strip that scrolls sideways on a phone or a tablet: the page that is open is brought into view (it can be the 6th of the tabs).
(function () {
    function reveal() {
        document.querySelectorAll('.course-menu').forEach(function (menu) {
            const on = menu.querySelector('.cm-item.on');
            if (!on || menu.scrollWidth <= menu.clientWidth + 2) { return; }
            const left = on.getBoundingClientRect().left - menu.getBoundingClientRect().left + menu.scrollLeft;
            menu.scrollLeft = Math.max(0, left - (menu.clientWidth - on.offsetWidth) / 2);
        });
    }

    document.addEventListener('DOMContentLoaded', function () { reveal(); setTimeout(reveal, 500); });
    if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
        window.Blazor.addEventListener('enhancednavigationend', function () { reveal(); setTimeout(reveal, 300); });
    }
})();

// The picture of the person in the card at the bottom of the menu changes when they upload or remove it in the settings menu (the card is drawn
// by the page, not by the settings menu, so the settings menu tells it).
(function () {
    window.seaitt = window.seaitt || {};
    window.seaitt.setMyPicture = function (url) {
        document.querySelectorAll('.sidebar-user .avatar').forEach(function (avatar) {
            let img = avatar.querySelector('img');
            if (url) {
                if (!img) {
                    avatar.textContent = '';
                    img = document.createElement('img');
                    img.alt = '';
                    avatar.appendChild(img);
                }
                img.src = url;
            } else {
                if (img) { img.remove(); }
                avatar.textContent = avatar.getAttribute('data-initials') || '';
            }
        });
    };
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

// The main menu on the pages where a lesson is read (a page asks for it with an element that has data-side-rail): it is folded to its icons so the lesson has the room.
// The button at the top of the menu (data-rail-toggle) shows the names again, or folds them again. Leaving the page puts the menu back.
(function () {
    var root = document.documentElement;
    var state = null;   // null: the page does not ask for it; "on": folded; "shown": the person showed it again

    function sync() {
        var wanted = !!document.querySelector('[data-side-rail]');
        if (wanted) {
            if (state === null) { state = 'on'; }
            if (root.getAttribute('data-rail') !== state) { root.setAttribute('data-rail', state); }
        } else if (state !== null) {
            state = null;
            root.removeAttribute('data-rail');
        }
    }

    document.addEventListener('click', function (event) {
        var target = event.target;
        if (target instanceof Element && target.closest('[data-rail-toggle]') && state !== null) {
            state = state === 'on' ? 'shown' : 'on';
            sync();
        }
    });

    new MutationObserver(sync).observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['data-rail'] });
    sync();
    if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
        window.Blazor.addEventListener('enhancednavigationend', sync);
    }
})();

// The way the courses (Cards or List) and the calendar (Month or Week) are looked at is remembered in a cookie (ViewPreference.cs reads it), so a page that
// is opened without a view in its address - a tab of the courses, the Courses or Calendar link of the menu - shows it as the person chose last.
(function () {
    var pages = { subjects: ['cards', 'list'], calendar: ['month', 'week'] };

    function remember(address) {
        try {
            var url = new URL(address, document.baseURI);
            var view = url.searchParams.get('view');
            var page = url.pathname.split('/').filter(Boolean).pop();
            if (view && pages[page] && pages[page].indexOf(view) >= 0) {
                document.cookie = 'seaitt-view-' + page + '=' + view + '; path=/; samesite=lax; max-age=31536000';
            }
        } catch (error) { }
    }

    document.addEventListener('click', function (event) {
        var target = event.target;
        var link = target instanceof Element ? target.closest('a[href]') : null;
        if (link) { remember(link.href); }
    }, true);

    remember(location.href);
    if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
        window.Blazor.addEventListener('enhancednavigationend', function () { remember(location.href); });
    }
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
window.seaitt = Object.assign(window.seaitt || {}, {
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
    // window, tries to copy, cut or paste, or takes a screenshot (the Print Screen key, Windows + Shift + S, Command + Shift + 3, 4 or 5)
    // or prints the page. Copying, cutting, pasting and the right-click menu are blocked while it is on. A photo taken with another device
    // can't be seen by a web page, so the page also shows the student's name over the questions: a photo of it leads back to the student.
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

        // The Print Screen key often only gives a key-up. Whatever was copied to the clipboard is replaced, so a screenshot is not left there.
        function screenshot(what) {
            report(what);
            try { if (navigator.clipboard && navigator.clipboard.writeText) { navigator.clipboard.writeText(' '); } } catch (clipError) { /* the browser did not allow it */ }
        }

        function isScreenshotKey(event) {
            var key = (event.key || '').toLowerCase();
            if (key === 'printscreen' || event.code === 'PrintScreen') { return true; }
            // Windows: Windows + Shift + S. Mac: Command + Shift + 3, 4 or 5.
            if (event.metaKey && event.shiftKey && (key === 's' || key === '3' || key === '4' || key === '5' || event.code === 'Digit3' || event.code === 'Digit4' || event.code === 'Digit5')) { return true; }
            return false;
        }

        var handlers = {
            visibilitychange: function () { if (document.hidden) { report('Left the quiz tab'); } },
            blur: function () { report('Switched to another window'); },
            keydown: function (event) {
                if (isScreenshotKey(event)) { event.preventDefault(); screenshot('Tried to take a screenshot'); }
                else if ((event.ctrlKey || event.metaKey) && (event.key || '').toLowerCase() === 'p') { event.preventDefault(); screenshot('Tried to print the quiz'); }
            },
            keyup: function (event) { if (isScreenshotKey(event)) { screenshot('Tried to take a screenshot'); } },
            beforeprint: function () { screenshot('Tried to print the quiz'); },
            copy: function (event) { event.preventDefault(); report('Tried to copy'); },
            cut: function (event) { event.preventDefault(); report('Tried to cut'); },
            paste: function (event) { event.preventDefault(); report('Tried to paste'); },
            contextmenu: function (event) { event.preventDefault(); },
        };

        Object.keys(handlers).forEach(function (name) {
            (name === 'visibilitychange' ? document : name === 'blur' || name === 'beforeprint' ? window : document).addEventListener(name, handlers[name]);
        });
        window.seaitt.quizGuard = handlers;
    },
    stopQuizGuard: function () {
        var handlers = window.seaitt.quizGuard;
        if (!handlers) { return; }
        Object.keys(handlers).forEach(function (name) {
            (name === 'visibilitychange' ? document : name === 'blur' || name === 'beforeprint' ? window : document).removeEventListener(name, handlers[name]);
        });
        window.seaitt.quizGuard = null;
    },
});

// The fields of the academic setup are always on the page. Edit on a row fills them in; this brings them into view and puts the cursor in the first one.
window.seaitt = Object.assign(window.seaitt || {}, {
    goToId: function (id) {
        var box = document.getElementById(id);
        if (box) { box.scrollIntoView({ behavior: 'smooth', block: 'start' }); }
    },
    showEntry: function (id) {
        var box = document.getElementById(id);
        if (!box) { return; }
        box.scrollIntoView({ behavior: 'smooth', block: 'center' });
        box.classList.remove('flash');
        void box.offsetWidth;
        box.classList.add('flash');
        var field = box.querySelector('input:not([type=checkbox]):not([type=radio]):not([disabled]), select:not([disabled]), textarea');
        if (field) { field.focus({ preventScroll: true }); }
    },
});

// The steps line of the academic setup: a click scrolls to the step (the address is still a real link).
document.addEventListener('click', function (event) {
    var link = event.target instanceof Element ? event.target.closest('a[data-goto]') : null;
    if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey) { return; }
    var box = document.getElementById(link.getAttribute('data-goto'));
    if (!box) { return; }
    event.preventDefault();
    event.stopPropagation(); // before Blazor's own handling of links, which would load the address again
    box.scrollIntoView({ behavior: 'smooth', block: 'start' });
}, true);

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

// Files: a link marked data-file="Name" (data-show="1" when the browser can show that kind of file) opens in the box in the middle of the
// screen (Components/Shared/FileViewer.razor) instead of saving the file or leaving the page. Download saves it, Open in a new tab opens it.
// With Ctrl, Shift or the middle button the link works as a normal link.
(function () {
    function viewer() { return document.getElementById('file-viewer'); }

    function kindOf(name) {
        var ext = (name.split('.').pop() || '').toLowerCase();
        if (ext === 'pdf') { return 'frame'; }
        if (['png', 'jpg', 'jpeg', 'gif', 'webp'].indexOf(ext) >= 0) { return 'image'; }
        if (['mp3', 'wav', 'ogg'].indexOf(ext) >= 0) { return 'audio'; }
        if (['mp4', 'webm'].indexOf(ext) >= 0) { return 'video'; }
        return 'frame';
    }

    function open(url, name, canShow) {
        var box = viewer();
        if (!box) { return false; }

        var base = url.split('?')[0];
        box.querySelector('.viewer-name').textContent = name;
        var tab = box.querySelector('[data-viewer="tab"]');
        var save = box.querySelector('[data-viewer="save"]');
        tab.href = base;
        tab.style.display = canShow ? '' : 'none';
        save.href = base + '?download=1';
        save.setAttribute('download', name);

        var body = box.querySelector('.viewer-body');
        body.textContent = '';
        var shown;
        if (!canShow) {
            shown = document.createElement('div');
            shown.className = 'viewer-none';
            var title = document.createElement('strong');
            title.textContent = 'This file cannot be shown here';
            var hint = document.createElement('p');
            hint.textContent = 'Press Download to save it to your computer, then open it there.';
            shown.append(title, hint);
        } else {
            var kind = kindOf(name);
            shown = document.createElement(kind === 'frame' ? 'iframe' : kind === 'image' ? 'img' : kind);
            if (kind === 'frame') { shown.title = name; }
            if (kind === 'image') { shown.alt = name; }
            if (kind === 'audio' || kind === 'video') { shown.controls = true; shown.autoplay = false; }
            shown.setAttribute('src', base);
        }
        body.appendChild(shown);

        if (!box.open) { box.showModal(); }
        return true;
    }

    document.addEventListener('click', function (event) {
        var link = event.target instanceof Element ? event.target.closest('a[data-file]') : null;
        if (link) {
            if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) { return; }
            if (open(link.getAttribute('href') || '', link.getAttribute('data-file') || 'File', link.getAttribute('data-show') === '1')) {
                event.preventDefault();
            }
            return;
        }

        var box = viewer();
        if (!box || !box.open || !(event.target instanceof Element)) { return; }
        // The close button, or a click on the dark area around the box.
        if (event.target.closest('.viewer-close') || event.target === box) { box.close(); }
    });

    // Closing the box stops a sound or a video.
    document.addEventListener('close', function (event) {
        if (event.target && event.target.id === 'file-viewer') {
            event.target.querySelector('.viewer-body').textContent = '';
        }
    }, true);
})();

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
    setInterval(tick, 5000);
})();

// The editor of a student's work (the "RichTextEditor" component draws the bar of tools, this file makes them work).
// It is a box the browser lets the person edit ("contenteditable"), and the tools call the browser's own editing commands
// (document.execCommand), the way a word processor on a web page does. What is written is cleaned again on the server.
(function () {
    var saved = null;           // the last place the cursor or selection was inside a box, kept while a list or a color box has the focus
    var pendingSize = {};       // the size (in pt) chosen with nothing selected, for what is typed next, by box

    function boxOf(node) {
        var element = node && (node.nodeType === 1 ? node : node.parentElement);
        return element && element.closest ? element.closest('.rte-editor') : null;
    }

    function wrapOf(editor) {
        return editor.closest('[data-rte]');
    }

    function isEmpty(editor) {
        return editor.innerText.trim() === '' && !editor.querySelector('table, hr');
    }

    function words(text) {
        var found = text.trim().match(/\S+/g);
        return found ? found.length : 0;
    }

    function updateStatus(editor) {
        var wrap = wrapOf(editor);
        var text = editor.innerText.replace(/ /g, ' ').trim();
        editor.setAttribute('data-empty', isEmpty(editor) ? 'true' : 'false');
        var counter = wrap && wrap.querySelector('[data-rte-count]');
        if (counter) {
            var max = parseInt(editor.getAttribute('data-max') || '0', 10);
            var count = text.length;
            counter.textContent = words(text) + ' ' + (words(text) === 1 ? 'word' : 'words') + ' · ' + count.toLocaleString('en-US') + (max ? ' / ' + max.toLocaleString('en-US') : '') + ' characters';
            counter.classList.toggle('over', max > 0 && count > max);
        }
    }

    // The browser writes a size chosen in pt as <font size="7">; turn those into a span with the size, and let the new size win over the old ones inside.
    // Taking a node out of the page empties the selection, so the selection (or the cursor) is put back on the new span.
    function applySizes(editor) {
        var pt = pendingSize[editor.id];
        var fonts = editor.querySelectorAll('font[size="7"]');
        if (!pt || fonts.length === 0) { return; }

        var selection = document.getSelection();
        var inside = selection && selection.rangeCount > 0 && editor.contains(selection.anchorNode);
        var hadRange = inside && !selection.isCollapsed;
        var anchor = inside ? (selection.anchorNode.nodeType === 1 ? selection.anchorNode : selection.anchorNode.parentElement) : null;
        var anchorFont = anchor && anchor.closest ? anchor.closest('font[size="7"]') : null;

        var spans = [];
        var anchorSpan = null;
        fonts.forEach(function (font) {
            var span = document.createElement('span');
            span.style.fontSize = pt + 'pt';
            while (font.firstChild) { span.appendChild(font.firstChild); }
            span.querySelectorAll('[style]').forEach(function (inner) { inner.style.removeProperty('font-size'); });
            font.replaceWith(span);
            spans.push(span);
            if (font === anchorFont) { anchorSpan = span; }
        });

        if (inside && spans.length > 0) {
            var range = document.createRange();
            if (hadRange) {
                range.setStartBefore(spans[0]);
                range.setEndAfter(spans[spans.length - 1]);
            } else if (anchorSpan) {
                range.selectNodeContents(anchorSpan);
                range.collapse(false);
            } else {
                return;
            }

            selection.removeAllRanges();
            selection.addRange(range);
        }
    }

    function cleanUp(editor) {
        applySizes(editor);
        // Nothing but a lonely <br> or empty spans left behind by the commands.
        editor.querySelectorAll('span:empty, font:empty').forEach(function (span) { span.remove(); });
    }

    // The selection changed by a command is remembered right away (the browser reports it a little later, and the next tool must not use the old one).
    function remember(editor) {
        var selection = document.getSelection();
        if (selection && selection.rangeCount > 0 && editor.contains(selection.anchorNode)) {
            saved = { editor: editor, range: selection.getRangeAt(0).cloneRange() };
        }
    }

    function afterChange(editor) {
        cleanUp(editor);
        remember(editor);
        updateStatus(editor);
        showState(wrapOf(editor));
    }

    function restore(editor) {
        editor.focus();
        if (saved && saved.editor === editor) {
            var selection = document.getSelection();
            selection.removeAllRanges();
            selection.addRange(saved.range);
        }
    }

    function run(editor, command, value) {
        restore(editor);
        document.execCommand('styleWithCSS', false, true);
        document.execCommand(command, false, value === undefined ? null : value);
        afterChange(editor);
    }

    function blocksInSelection(editor) {
        var selection = document.getSelection();
        if (!selection || selection.rangeCount === 0) { return []; }
        var range = selection.getRangeAt(0);
        return Array.prototype.filter.call(editor.querySelectorAll('p, div, li, h1, h2, h3, h4, blockquote, pre, td, th'), function (block) {
            return range.intersectsNode(block) && !block.querySelector('p, div, li, h1, h2, h3, h4, blockquote, pre, td, th');
        });
    }

    function setLineHeight(editor, value) {
        restore(editor);
        var blocks = blocksInSelection(editor);
        if (blocks.length === 0) {
            document.execCommand('formatBlock', false, 'p');
            blocks = blocksInSelection(editor);
        }
        blocks.forEach(function (block) { block.style.lineHeight = value; });
        afterChange(editor);
    }

    function setSize(editor, pt) {
        restore(editor);
        pendingSize[editor.id] = pt;
        document.execCommand('styleWithCSS', false, false);
        document.execCommand('fontSize', false, '7');
        document.execCommand('styleWithCSS', false, true);
        afterChange(editor);
    }

    function insertTable(editor) {
        var cell = '<td><br></td>';
        var row = '<tr>' + cell + cell + cell + '</tr>';
        run(editor, 'insertHTML', '<table><tbody>' + row + row + row + '</tbody></table><p><br></p>');
    }

    function insertLink(editor) {
        restore(editor);
        var selection = document.getSelection();
        var address = window.prompt('Link address (it starts with https://)', 'https://');
        if (!address || !/^https?:\/\//i.test(address.trim())) { return; }
        address = address.trim();
        restore(editor);
        if (selection && selection.isCollapsed) {
            var link = document.createElement('a');
            link.href = address;
            link.textContent = address;
            document.execCommand('insertHTML', false, link.outerHTML);
        } else {
            document.execCommand('createLink', false, address);
        }
        afterChange(editor);
    }

    // Shows which tools are on where the cursor is (bold is pressed in bold text), and the font and size in the lists.
    function showState(wrap) {
        if (!wrap) { return; }
        var editor = wrap.querySelector('.rte-editor');
        if (document.activeElement !== editor && !(saved && saved.editor === editor)) { return; }
        wrap.querySelectorAll('button[data-state]').forEach(function (button) {
            var on = false;
            try { on = document.queryCommandState(button.getAttribute('data-cmd')); } catch (error) { on = false; }
            button.setAttribute('aria-pressed', on ? 'true' : 'false');
        });

        var selection = document.getSelection();
        var element = selection && selection.anchorNode ? (selection.anchorNode.nodeType === 1 ? selection.anchorNode : selection.anchorNode.parentElement) : null;
        if (!element || !editor.contains(element)) { return; }
        var style = window.getComputedStyle(element);

        var sizeBox = wrap.querySelector('select[data-select="fontSizePt"]');
        if (sizeBox && document.activeElement !== sizeBox) {
            var pt = Math.round(parseFloat(style.fontSize) * 0.75);
            if (Array.prototype.some.call(sizeBox.options, function (option) { return option.value === String(pt); })) { sizeBox.value = String(pt); }
        }

        var fontBox = wrap.querySelector('select[data-select="fontName"]');
        if (fontBox && document.activeElement !== fontBox) {
            var family = style.fontFamily.split(',')[0].replace(/["']/g, '').trim().toLowerCase();
            Array.prototype.forEach.call(fontBox.options, function (option) { if (option.value.toLowerCase() === family) { fontBox.value = option.value; } });
        }

        var blockBox = wrap.querySelector('select[data-select="formatBlock"]');
        if (blockBox && document.activeElement !== blockBox) {
            var block = element.closest('h1, h2, h3, blockquote');
            blockBox.value = block && editor.contains(block) ? block.tagName.toLowerCase() : 'p';
        }
    }

    function tabInTable(event, editor) {
        var selection = document.getSelection();
        var element = selection && selection.anchorNode ? (selection.anchorNode.nodeType === 1 ? selection.anchorNode : selection.anchorNode.parentElement) : null;
        var cell = element && element.closest('td, th');
        if (!cell || !editor.contains(cell)) { return false; }
        event.preventDefault();
        var cells = Array.prototype.slice.call(cell.closest('table').querySelectorAll('td, th'));
        var next = cells[cells.indexOf(cell) + (event.shiftKey ? -1 : 1)];
        if (!next && !event.shiftKey) {
            // After the last cell a new row is added, like in a word processor.
            var row = cell.parentElement.cloneNode(true);
            row.querySelectorAll('td, th').forEach(function (copy) { copy.innerHTML = '<br>'; });
            cell.parentElement.after(row);
            next = row.querySelector('td, th');
        }

        if (next) {
            var range = document.createRange();
            range.selectNodeContents(next);
            range.collapse(true);
            selection.removeAllRanges();
            selection.addRange(range);
        }

        return true;
    }

    // ---- the tools: one listener for the whole page, so the bar keeps working when the page redraws it
    document.addEventListener('mousedown', function (event) {
        // A click on a tool must not take the cursor out of the text (a list or a color box needs it to take the focus).
        var tool = event.target.closest && event.target.closest('[data-rte] .bar button');
        if (tool) { event.preventDefault(); }
    });

    document.addEventListener('click', function (event) {
        var button = event.target.closest && event.target.closest('[data-rte] .bar button');
        if (!button) { return; }
        var wrap = button.closest('[data-rte]');
        var editor = wrap.querySelector('.rte-editor');
        var action = button.getAttribute('data-action');
        if (action === 'table') { insertTable(editor); }
        else if (action === 'link') { insertLink(editor); }
        else if (button.getAttribute('data-cmd')) { run(editor, button.getAttribute('data-cmd')); }
    });

    document.addEventListener('change', function (event) {
        var box = event.target.closest && event.target.closest('[data-rte] .bar select');
        if (!box) { return; }
        var editor = box.closest('[data-rte]').querySelector('.rte-editor');
        var kind = box.getAttribute('data-select');
        if (kind === 'fontName') { run(editor, 'fontName', box.value); }
        else if (kind === 'fontSizePt') { setSize(editor, box.value); }
        else if (kind === 'lineHeight') { setLineHeight(editor, box.value); }
        else if (kind === 'formatBlock') { run(editor, 'formatBlock', box.value === 'p' ? 'p' : box.value); }
    });

    document.addEventListener('input', function (event) {
        var color = event.target.closest && event.target.closest('[data-rte] .bar input[type="color"]');
        if (color) {
            var editor = color.closest('[data-rte]').querySelector('.rte-editor');
            color.parentElement.style.setProperty('--swatch', color.value);
            run(editor, color.getAttribute('data-color'), color.value);
            return;
        }

        var typed = boxOf(event.target);
        if (typed) { afterChange(typed); }
    });

    document.addEventListener('selectionchange', function () {
        var selection = document.getSelection();
        if (!selection || selection.rangeCount === 0) { return; }
        var editor = boxOf(selection.anchorNode);
        if (editor) {
            saved = { editor: editor, range: selection.getRangeAt(0).cloneRange() };
            showState(wrapOf(editor));
        }
    });

    document.addEventListener('keydown', function (event) {
        var editor = boxOf(event.target);
        if (editor && event.key === 'Tab') { tabInTable(event, editor); }
    });

    // Pasting keeps the words and the paragraphs only (what comes from another page or a document would bring its own fonts, sizes and pictures).
    document.addEventListener('paste', function (event) {
        var editor = boxOf(event.target);
        if (!editor) { return; }
        event.preventDefault();
        var text = (event.clipboardData || window.clipboardData).getData('text/plain');
        if (text) { document.execCommand('insertText', false, text.replace(/\r\n?/g, '\n')); }
        afterChange(editor);
    });

    // A file or a picture dropped on the box would make the browser leave the page: only text is taken.
    document.addEventListener('dragover', function (event) {
        if (boxOf(event.target) && event.dataTransfer && Array.prototype.indexOf.call(event.dataTransfer.types, 'Files') >= 0) { event.preventDefault(); }
    });
    document.addEventListener('drop', function (event) {
        var editor = boxOf(event.target);
        if (editor && event.dataTransfer && event.dataTransfer.files && event.dataTransfer.files.length > 0) { event.preventDefault(); }
    });

    window.seaittEditor = {
        // Called by the component when it appears: fills the box and makes new paragraphs <p>.
        start: function (id, html) {
            var editor = document.getElementById(id);
            if (!editor) { return; }
            if (html && html.trim() !== '') { editor.innerHTML = html; }
            else if (editor.innerText.trim() === '') { editor.innerHTML = '<p><br></p>'; }
            try { document.execCommand('defaultParagraphSeparator', false, 'p'); } catch (error) { /* an old browser keeps its own */ }
            updateStatus(editor);
        },
        setHtml: function (id, html) {
            var editor = document.getElementById(id);
            if (!editor) { return; }
            editor.innerHTML = html && html.trim() !== '' ? html : '<p><br></p>';
            updateStatus(editor);
        },
        // What was written, or "" when the box is empty.
        getHtml: function (id) {
            var editor = document.getElementById(id);
            if (!editor || isEmpty(editor)) { return ''; }
            return editor.innerHTML;
        },
    };
})();

// The timer of a locked account on the sign-in page. The server writes how many seconds are left in "data-lock-seconds" (it is the server that keeps
// the lock); this counts them down on the screen and says when the time is over. It starts again when the page is patched after a wrong try.
(function () {
    var timer = null;

    function pad(n) { return (n < 10 ? '0' : '') + n; }

    function start() {
        if (timer) { clearInterval(timer); timer = null; }
        var box = document.querySelector('.lock-timer[data-lock-seconds]');
        if (!box) { return; }
        var clock = box.querySelector('.lock-clock');
        var end = performance.now() + parseInt(box.getAttribute('data-lock-seconds'), 10) * 1000;

        function tick() {
            var left = Math.max(0, Math.ceil((end - performance.now()) / 1000));
            if (clock) { clock.textContent = pad(Math.floor(left / 60)) + ':' + pad(left % 60); }
            if (left <= 0) {
                clearInterval(timer);
                timer = null;
                box.classList.add('done');
                var title = box.querySelector('strong');
                if (title) { title.textContent = 'The lock is over'; }
                var wait = box.querySelector('.lock-wait');
                var done = box.querySelector('.lock-done');
                if (wait) { wait.hidden = true; }
                if (done) { done.hidden = false; }
            }
        }

        tick();
        if (!timer && !box.classList.contains('done')) { timer = setInterval(tick, 1000); }
    }

    document.addEventListener('enhancedload', start);
    start();
})();

// The menu of a course (CourseLayout) can be hidden and shown again with its button (data-cm-toggle). The choice is the attribute data-course-menu on the page
// (so Blazor never draws over it) and is kept on this computer, so it is the same on every course.
(function () {
    var root = document.documentElement;
    var key = 'seaitt-course-menu';

    try { if (localStorage.getItem(key) === 'hidden') { root.setAttribute('data-course-menu', 'hidden'); } } catch (error) { }

    function sync() {
        var hidden = root.getAttribute('data-course-menu') === 'hidden';
        document.querySelectorAll('[data-cm-toggle]').forEach(function (button) {
            button.setAttribute('aria-expanded', hidden ? 'false' : 'true');
            button.title = hidden ? 'Show the course menu' : 'Hide the course menu';
        });
    }

    document.addEventListener('click', function (event) {
        var button = event.target.closest && event.target.closest('[data-cm-toggle]');
        if (!button) { return; }
        var hide = root.getAttribute('data-course-menu') !== 'hidden';
        if (hide) { root.setAttribute('data-course-menu', 'hidden'); } else { root.removeAttribute('data-course-menu'); }
        try { if (hide) { localStorage.setItem(key, 'hidden'); } else { localStorage.removeItem(key); } } catch (error) { }
        sync();
    });

    // The pages are drawn again by Blazor (and a live page replaces what the server drew), so the buttons are kept in step.
    var waiting = false;
    new MutationObserver(function () {
        if (waiting) { return; }
        waiting = true;
        requestAnimationFrame(function () { waiting = false; sync(); });
    }).observe(document.documentElement, { childList: true, subtree: true });
    document.addEventListener('DOMContentLoaded', sync);
    sync();
})();

// The strands of the first page: a button with data-drop-toggle="id" drops the panel with that id down under it (and folds the others up); Escape, a second press
// or a click outside folds it up. A panel that would leave the screen on the right is aligned to the right edge of its card.
(function () {
    function fold(except) {
        document.querySelectorAll('[data-drop-toggle][aria-expanded="true"]').forEach(function (button) {
            if (button === except) { return; }
            button.setAttribute('aria-expanded', 'false');
            var panel = document.getElementById(button.getAttribute('data-drop-toggle'));
            if (panel) { panel.hidden = true; }
        });
    }

    document.addEventListener('click', function (e) {
        var toggle = e.target.closest && e.target.closest('[data-drop-toggle]');
        if (toggle) {
            var panel = document.getElementById(toggle.getAttribute('data-drop-toggle'));
            if (!panel) { return; }
            var open = toggle.getAttribute('aria-expanded') === 'true';
            fold(open ? null : toggle);
            toggle.setAttribute('aria-expanded', open ? 'false' : 'true');
            panel.hidden = open;
            if (!open) {
                panel.classList.remove('right');
                var r = panel.getBoundingClientRect();
                if (r.right > window.innerWidth - 8) { panel.classList.add('right'); }
            }
            return;
        }
        if (!(e.target.closest && e.target.closest('.strand-drop'))) { fold(null); }
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') { fold(null); }
    });
})();

// The message at the top center (TopAlert): the X closes it, and ?alert= is taken off the address so a refresh does not show it again.
(function () {
    document.addEventListener('click', function (e) {
        var close = e.target.closest && e.target.closest('[data-close-top-alert]');
        if (close) {
            var box = close.closest('.top-alert');
            if (box) { box.hidden = true; }
        }
    });

    try {
        var url = new URL(window.location.href);
        if (url.searchParams.has('alert')) {
            url.searchParams.delete('alert');
            window.history.replaceState(window.history.state, '', url.pathname + url.search + url.hash);
        }
    } catch (err) { /* an old browser: the message just stays in the address */ }
})();

// The sound of a new message or notification (HeaderTools calls seaitt.playNotificationSound(choice, url, force)). The person chooses it in Settings: one of the sounds the browser makes
// by itself (two tones, chime, bell, pop, knock), the sound of the school or a file they added (the page gives its address in url). With no choice it is the file
// wwwroot/sounds/notification.mp3 when there is one, otherwise the two tones. A browser keeps sound off until the person has clicked or typed on the page once: when a sound can't
// play for that reason a small button says so, and one click turns the sound on. Settings has a switch (data-sound-toggle) and a button that plays the sound (data-sound-test).
(function () {
    var root = document.documentElement;
    var context = null;

    try { if (localStorage.getItem('seaitt-sound') === 'off') { root.setAttribute('data-sound', 'off'); } } catch (error) { }

    function audio() {
        if (context) { return context; }
        var Maker = window.AudioContext || window.webkitAudioContext;
        if (!Maker) { return null; }
        try { context = new Maker(); } catch (error) { context = null; }
        return context;
    }

    function removeHint() {
        var hint = document.getElementById('sound-hint');
        if (hint) { hint.remove(); }
    }

    function unlock() {
        var made = audio();
        if (made && made.state === 'suspended') { made.resume(); }
        removeHint();
    }

    ['pointerdown', 'keydown', 'touchstart'].forEach(function (name) { document.addEventListener(name, unlock, { passive: true }); });

    // The browser did not let the sound play (nobody has clicked on this page yet): a button to click, which is the click the browser wants.
    function showHint() {
        if (document.getElementById('sound-hint')) { return; }
        var hint = document.createElement('button');
        hint.id = 'sound-hint';
        hint.type = 'button';
        hint.textContent = 'Click here to turn on the notification sound';
        hint.setAttribute('style', 'position:fixed;right:1rem;bottom:max(1rem,env(safe-area-inset-bottom));z-index:300;max-width:calc(100vw - 2rem);padding:.5rem .9rem;border:1px solid var(--border);border-radius:999px;background:var(--surface);color:var(--text);box-shadow:var(--shadow-lg);font:inherit;font-size:.88rem;font-weight:600;line-height:1.3;cursor:pointer');
        hint.addEventListener('click', function () { unlock(); playBuiltIn('tones', true); hint.remove(); });
        document.body.appendChild(hint);
        // It stays until the person clicks (any click on the page also does it): the sound can't play before that, and a button that goes away by itself is easy to miss.
    }

    // Runs fn(context) when the browser lets the page make sound; otherwise shows the hint.
    function withSound(fn, quiet) {
        var made = audio();
        if (!made) { return; }
        if (made.state === 'running') { fn(made); return; }
        var activated = !navigator.userActivation || navigator.userActivation.hasBeenActive;
        if (!activated) { if (!quiet) { showHint(); } return; }
        made.resume().then(function () { if (made.state === 'running') { fn(made); } else if (!quiet) { showHint(); } }).catch(function () { if (!quiet) { showHint(); } });
    }

    function tone(made, frequency, start, length, peak) {
        var oscillator = made.createOscillator();
        var volume = made.createGain();
        oscillator.type = 'sine';
        oscillator.frequency.value = frequency;
        volume.gain.setValueAtTime(0.0001, start);
        volume.gain.exponentialRampToValueAtTime(peak || 0.25, start + 0.02);
        volume.gain.exponentialRampToValueAtTime(0.0001, start + length);
        oscillator.connect(volume);
        volume.connect(made.destination);
        oscillator.start(start);
        oscillator.stop(start + length + 0.05);
    }

    // A short "pop": the pitch falls quickly.
    function blip(made, from, to, start, length, peak) {
        var oscillator = made.createOscillator();
        var volume = made.createGain();
        oscillator.type = 'sine';
        oscillator.frequency.setValueAtTime(from, start);
        oscillator.frequency.exponentialRampToValueAtTime(to, start + length);
        volume.gain.setValueAtTime(0.0001, start);
        volume.gain.exponentialRampToValueAtTime(peak, start + 0.01);
        volume.gain.exponentialRampToValueAtTime(0.0001, start + length);
        oscillator.connect(volume);
        volume.connect(made.destination);
        oscillator.start(start);
        oscillator.stop(start + length + 0.05);
    }

    // The sounds the browser makes by itself.
    var builtIn = {
        tones: function (made, now) { tone(made, 880, now, 0.28); tone(made, 1318.5, now + 0.14, 0.4); },
        chime: function (made, now) { tone(made, 1046.5, now, 0.4, 0.22); tone(made, 1318.5, now + 0.13, 0.4, 0.22); tone(made, 1568, now + 0.26, 0.6, 0.22); },
        bell: function (made, now) { tone(made, 1174.7, now, 1.3, 0.28); tone(made, 2349.3, now, 0.8, 0.12); tone(made, 3520, now, 0.4, 0.06); },
        pop: function (made, now) { blip(made, 700, 160, now, 0.16, 0.5); },
        knock: function (made, now) { blip(made, 260, 90, now, 0.14, 0.7); blip(made, 260, 90, now + 0.18, 0.14, 0.7); },
    };

    function playBuiltIn(name, quiet) {
        var make = builtIn[name] || builtIn.tones;
        withSound(function (made) { make(made, made.currentTime); }, quiet);
    }

    function playFile(address, quiet) {
        try {
            var player = new Audio(address);
            player.volume = 0.9;
            var started = player.play();
            if (started && started.catch) {
                started.catch(function (error) {
                    if (error && error.name === 'NotAllowedError') { if (!quiet) { showHint(); } } else { playBuiltIn('tones', quiet); }
                });
            }
        } catch (error) { playBuiltIn('tones', quiet); }
    }

    // The file of the school put in wwwroot/sounds (notification.mp3, .ogg, .wav or .m4a): the first time it is needed the page asks which of them exists and keeps the answer.
    var soundFile = undefined;   // undefined: not asked yet; null: there is none; otherwise the address

    function findSoundFile() {
        if (soundFile !== undefined) { return Promise.resolve(soundFile); }
        var names = ['notification.mp3', 'notification.ogg', 'notification.wav', 'notification.m4a'];
        var base = document.baseURI;
        return names.reduce(function (chain, name) {
            return chain.then(function (found) {
                if (found) { return found; }
                var address = new URL('sounds/' + name, base).href;
                return fetch(address, { method: 'HEAD' }).then(function (response) {
                    var type = response.headers.get('content-type') || '';
                    return response.ok && type.indexOf('text/html') === -1 ? address : null;
                }).catch(function () { return null; });
            });
        }, Promise.resolve(null)).then(function (found) { soundFile = found; return found; });
    }

    // choice: tones, chime, bell, pop, knock, school, custom (the last two come with the address of the file in url), or empty for the usual sound. force: play even if the switch is off (the Play button).
    function play(choice, url, force) {
        if (!force && root.getAttribute('data-sound') === 'off') { return; }
        var kind = choice || '';
        if ((kind === 'custom' || kind === 'school') && url) { playFile(new URL(url, document.baseURI).href, false); return; }
        if (builtIn[kind]) { playBuiltIn(kind, false); return; }
        findSoundFile().then(function (address) {
            if (address) { playFile(address, false); } else { playBuiltIn('tones', false); }
        });
    }

    window.seaitt = window.seaitt || {};
    window.seaitt.playNotificationSound = play;

    // The header calls this when a page opens (a person who is signed in): when nobody has clicked on the page yet, the browser will not let a sound play when a message
    // comes in, so the button that asks for the click is shown now, not only when a sound has failed.
    window.seaitt.prepareSound = function () {
        if (root.getAttribute('data-sound') === 'off') { return; }
        if (navigator.userActivation && !navigator.userActivation.hasBeenActive) { showHint(); }
    };

    document.addEventListener('click', function (event) {
        var target = event.target;
        if (!(target instanceof Element)) { return; }

        if (target.closest('[data-sound-toggle]')) {
            var off = root.getAttribute('data-sound') !== 'off';
            if (off) { root.setAttribute('data-sound', 'off'); } else { root.removeAttribute('data-sound'); }
            try { if (off) { localStorage.setItem('seaitt-sound', 'off'); } else { localStorage.removeItem('seaitt-sound'); } } catch (error) { }
            // (Also in a cookie: the server writes it onto <html>, which is how it survives when Blazor swaps the page. See App.razor.)
            try { document.cookie = 'seaitt-sound=' + (off ? 'off' : '') + '; path=/; samesite=lax; max-age=' + (off ? 31536000 : 0); } catch (error) { }
            if (!off) { unlock(); play('', '', false); }
            return;
        }

        var test = target.closest('[data-sound-test]');
        if (test) {
            unlock();
            play(test.getAttribute('data-sound-choice') || '', test.getAttribute('data-sound-url') || '', true);
        }
    });
})();

// Saves a file the page made in memory (Services/FileDownloads.cs): the bytes come as a stream, and the browser is asked to save them under the given name.
(function () {
    window.seaitt = window.seaitt || {};
    window.seaitt.downloadFile = async function (fileName, contentType, stream) {
        const buffer = await stream.arrayBuffer();
        const url = URL.createObjectURL(new Blob([buffer], { type: contentType }));
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(function () { URL.revokeObjectURL(url); }, 4000);
    };
})();
