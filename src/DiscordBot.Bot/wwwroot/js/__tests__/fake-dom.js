// A just-enough DOM for tests of scripts that build markup (empty-state.js, skeleton.js).
// Not a spec-compliant DOM: elements, text nodes, attributes, classList, listeners.

class FakeNode {
    constructor(tagName, isText = false, text = '') {
        this.tagName = tagName ? tagName.toUpperCase() : '';
        this.isText = isText;
        this._text = text;
        this.children = [];
        this.parentNode = null;
        this.attributes = {};
        this.listeners = {};
        this.className = '';
        this.id = '';
        const self = this;
        this.classList = {
            add(name) { self.className = [...new Set(self.className.split(/\s+/).filter(Boolean).concat(name))].join(' '); },
            remove(name) { self.className = self.className.split(/\s+/).filter((c) => c && c !== name).join(' '); },
            contains(name) { return self.className.split(/\s+/).includes(name); }
        };
    }

    get textContent() {
        if (this.isText) return this._text;
        return this.children.map((c) => c.textContent).join('');
    }

    set textContent(value) {
        this.children.forEach((c) => { c.parentNode = null; });
        this.children = [];
        if (value !== '' && value !== null && value !== undefined) {
            this.appendChild(new FakeNode('', true, String(value)));
        }
    }

    appendChild(node) {
        if (node.parentNode) node.parentNode.removeChild(node);
        node.parentNode = this;
        this.children.push(node);
        return node;
    }

    removeChild(node) {
        const index = this.children.indexOf(node);
        if (index >= 0) this.children.splice(index, 1);
        node.parentNode = null;
        return node;
    }

    setAttribute(name, value) {
        if (name === 'class') this.className = String(value);
        else if (name === 'id') this.id = String(value);
        this.attributes[name] = String(value);
    }

    getAttribute(name) {
        if (name === 'class') return this.className || null;
        if (name === 'id') return this.id || null;
        return name in this.attributes ? this.attributes[name] : null;
    }

    removeAttribute(name) {
        delete this.attributes[name];
    }

    hasAttribute(name) {
        return name in this.attributes;
    }

    addEventListener(type, handler) {
        (this.listeners[type] = this.listeners[type] || []).push(handler);
    }

    click() {
        (this.listeners.click || []).forEach((h) => h({ target: this }));
    }

    find(predicate) {
        const found = [];
        const walk = (node) => {
            node.children.forEach((child) => {
                if (!child.isText && predicate(child)) found.push(child);
                walk(child);
            });
        };
        walk(this);
        return found;
    }

    findByTag(tag) {
        return this.find((n) => n.tagName === tag.toUpperCase());
    }
}

function installFakeDocument() {
    global.document = {
        createElement: (tag) => new FakeNode(tag),
        createElementNS: (ns, tag) => new FakeNode(tag),
        createTextNode: (text) => new FakeNode('', true, text)
    };
}

function removeFakeDocument() {
    delete global.document;
}

module.exports = { FakeNode, installFakeDocument, removeFakeDocument };
