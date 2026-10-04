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

// The settings menu: light / dark / automatic, and animations on or off. The choice is kept in this browser.
// (App.razor applies it again before the next page is drawn.)
(function () {
    var root = document.documentElement;

    function save(key, value) {
        try {
            if (value === null) { localStorage.removeItem(key); } else { localStorage.setItem(key, value); }
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
};
