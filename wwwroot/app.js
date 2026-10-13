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
});

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
    function applySizes(editor) {
        var pt = pendingSize[editor.id];
        if (!pt) { return; }
        editor.querySelectorAll('font[size="7"]').forEach(function (font) {
            var span = document.createElement('span');
            span.style.fontSize = pt + 'pt';
            while (font.firstChild) { span.appendChild(font.firstChild); }
            span.querySelectorAll('[style]').forEach(function (inner) { inner.style.removeProperty('font-size'); });
            font.replaceWith(span);
            var selection = document.getSelection();
            if (selection && selection.isCollapsed === false) {
                var range = document.createRange();
                range.selectNodeContents(span);
                selection.removeAllRanges();
                selection.addRange(range);
            }
        });
    }

    function cleanUp(editor) {
        applySizes(editor);
        // Nothing but a lonely <br> or empty spans left behind by the commands.
        editor.querySelectorAll('span:empty, font:empty').forEach(function (span) { span.remove(); });
    }

    function afterChange(editor) {
        cleanUp(editor);
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
        restore(editor);
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
