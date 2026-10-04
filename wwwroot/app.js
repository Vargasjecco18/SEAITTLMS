// Small touches for the menus. The pages work without this file, it only makes the menus nicer:
// it closes the account menu and the phone menu when you click somewhere else, press Escape or open a page.
(function () {
    function closeUserMenu() {
        const menu = document.querySelector('details.user-menu');
        if (menu) {
            menu.open = false;
        }
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

        // A click anywhere outside the account menu closes it.
        const menu = document.querySelector('details.user-menu');
        if (menu && menu.open && !menu.contains(target)) {
            menu.open = false;
        }

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
    print: function () {
        window.print();
    },
};
