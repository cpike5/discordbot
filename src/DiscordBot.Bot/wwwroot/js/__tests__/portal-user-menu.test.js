const test = require('node:test');
const assert = require('node:assert/strict');
const PortalUserMenu = require('../portal-user-menu.js');

// Just enough DOM: a container holding a button and a menu with items.
function fakeMenu(itemCount) {
    const focused = { el: null };
    function element(name) {
        return {
            name,
            attrs: {},
            listeners: {},
            classes: new Set(),
            setAttribute(k, v) { this.attrs[k] = v; },
            addEventListener(type, fn) { this.listeners[type] = fn; },
            focus() { focused.el = this; }
        };
    }
    const button = element('button');
    const menu = element('menu');
    menu.classList = {
        add: c => menu.classes.add(c),
        remove: c => menu.classes.delete(c),
        contains: c => menu.classes.has(c)
    };
    const items = Array.from({ length: itemCount }, (_, i) => element('item' + i));
    menu.querySelectorAll = sel => (sel === '[role="menuitem"]' ? items : []);
    const inside = new Set([button, menu, ...items]);
    const container = element('container');
    container.contains = el => inside.has(el) || el === container;
    container.querySelector = sel => (sel === '#portalUserMenuButton' ? button : sel === '#portalUserMenu' ? menu : null);
    const doc = { listeners: {}, addEventListener(type, fn) { this.listeners[type] = fn; } };
    // A key pressed on the menu comes from whichever item has focus, as in a browser
    const key = (target, k) => {
        let prevented = false;
        const origin = target === menu && focused.el ? focused.el : target;
        target.listeners.keydown({ key: k, target: origin, preventDefault() { prevented = true; } });
        return prevented;
    };
    return { button, menu, items, container, doc, focused, key, outside: element('outside') };
}

test('clicking the button opens and closes the menu and keeps aria-expanded in step', () => {
    const f = fakeMenu(3);
    const menu = PortalUserMenu.wire(f.container, f.doc);
    f.button.listeners.click();
    assert.equal(menu.isOpen(), true);
    assert.equal(f.button.attrs['aria-expanded'], 'true');
    f.button.listeners.click();
    assert.equal(menu.isOpen(), false);
    assert.equal(f.button.attrs['aria-expanded'], 'false');
});

test('Escape closes the menu and returns focus to the button', () => {
    const f = fakeMenu(3);
    const menu = PortalUserMenu.wire(f.container, f.doc);
    menu.open(0);
    assert.equal(f.focused.el, f.items[0]);
    assert.equal(f.key(f.menu, 'Escape'), true);
    assert.equal(menu.isOpen(), false);
    assert.equal(f.focused.el, f.button);
});

test('a click outside closes the menu, a click inside does not', () => {
    const f = fakeMenu(2);
    const menu = PortalUserMenu.wire(f.container, f.doc);
    menu.open();
    f.doc.listeners.click({ target: f.items[0] });
    assert.equal(menu.isOpen(), true);
    f.doc.listeners.click({ target: f.outside });
    assert.equal(menu.isOpen(), false);
});

test('arrow keys move between the items and wrap around', () => {
    const f = fakeMenu(3);
    PortalUserMenu.wire(f.container, f.doc);
    f.key(f.button, 'ArrowDown');
    assert.equal(f.focused.el, f.items[0]);
    f.key(f.menu, 'ArrowDown');
    assert.equal(f.focused.el, f.items[1]);
    f.key(f.menu, 'ArrowUp');
    f.key(f.menu, 'ArrowUp');
    assert.equal(f.focused.el, f.items[2], 'up from the first item wraps to the last');
    f.key(f.menu, 'Home');
    assert.equal(f.focused.el, f.items[0]);
    f.key(f.menu, 'End');
    assert.equal(f.focused.el, f.items[2]);
});

test('focus leaving the menu closes it, focus moving inside does not', () => {
    const f = fakeMenu(2);
    const menu = PortalUserMenu.wire(f.container, f.doc);
    menu.open();
    f.container.listeners.focusout({ relatedTarget: f.items[1] });
    assert.equal(menu.isOpen(), true);
    f.container.listeners.focusout({ relatedTarget: f.outside });
    assert.equal(menu.isOpen(), false);
});

test('wire does nothing for a container without the button and menu', () => {
    const container = { querySelector: () => null };
    assert.equal(PortalUserMenu.wire(container, {}), null);
});
