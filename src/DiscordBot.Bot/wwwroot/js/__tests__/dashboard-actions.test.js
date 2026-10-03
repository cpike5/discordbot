const test = require('node:test');
const assert = require('node:assert/strict');
const DashboardActions = require('../dashboard-actions.js');

// Just enough DOM for renderServers: a template whose clone is one row with data-field children.
function element() {
    const el = {
        textContent: '', className: '', attrs: {}, children: [], hidden: false,
        classList: { add(...names) { el.className = [el.className, ...names].filter(Boolean).join(' '); } },
        setAttribute(name, value) { el.attrs[name] = String(value); },
        appendChild(child) { el.children.push(child); return child; }
    };
    return el;
}

function fakeDocument() {
    const fields = {};
    ['avatar', 'link', 'name', 'members', 'status', 'commands', 'copy'].forEach((n) => { fields[n] = element(); });
    const row = element();
    row.querySelector = (selector) => {
        const match = selector.match(/^\[data-field="(\w+)"\]$/);
        return match ? fields[match[1]] : null;
    };
    const rows = [];
    const template = {
        content: {
            cloneNode: () => ({
                querySelector: (s) => (s === '.connected-server' ? row : null),
                _row: row
            })
        }
    };
    const list = element();
    list.appendChild = (fragment) => { rows.push(fragment._row); return fragment; };
    const head = element();
    const empty = element();
    empty.hidden = true;
    const viewAll = element();
    const byId = {
        'connected-server-template': template, 'connected-servers-list': list,
        'connected-servers-head': head, 'connected-servers-empty': empty, 'connected-servers-view-all': viewAll
    };
    return {
        fields, row, rows, list, head, empty, viewAll,
        getElementById: (id) => byId[id] || null,
        createElement: () => element()
    };
}

const format = { number: (n) => n.toLocaleString('en-US') };

test('statusStyle maps each status and treats an unknown one as Offline', () => {
    assert.equal(DashboardActions.statusStyle('Online').badge, 'badge-success');
    assert.equal(DashboardActions.statusStyle('Idle').badge, 'badge-warning');
    assert.equal(DashboardActions.statusStyle('Offline').badge, 'badge-gray');
    assert.equal(DashboardActions.statusStyle('Whatever').badge, 'badge-gray');
});

test('viewAllLabel shows the total only when the card shows some of the servers', () => {
    assert.equal(DashboardActions.viewAllLabel(5, 5), 'View all');
    assert.equal(DashboardActions.viewAllLabel(12, 5, (n) => String(n)), 'View all 12');
});

test('payload values are only used when they are safe', () => {
    assert.equal(DashboardActions.safeIconUrl('https://cdn.discordapp.com/icons/1/a.png'), 'https://cdn.discordapp.com/icons/1/a.png');
    assert.equal(DashboardActions.safeIconUrl('javascript:alert(1)'), null);
    assert.equal(DashboardActions.safeIconUrl(null), null);
    assert.equal(DashboardActions.safeAvatarClass('bg-accent-orange'), 'bg-accent-orange');
    assert.equal(DashboardActions.safeAvatarClass('x" onclick="y'), 'bg-accent-blue');
    assert.equal(DashboardActions.safeDetailUrl('/Guilds/Details/123'), '/Guilds/Details/123');
    assert.equal(DashboardActions.safeDetailUrl('//evil.example/x'), '/Guilds');
    assert.equal(DashboardActions.safeDetailUrl('javascript:alert(1)'), '/Guilds');
});

test('renderServers draws one row per server from the payload', () => {
    const doc = fakeDocument();
    const ok = DashboardActions.renderServers(doc, {
        totalServerCount: 9,
        servers: [{
            id: '123456789012345678', name: 'Test <b>Server</b>', iconUrl: null, initials: 'TS',
            avatarClass: 'bg-accent-orange', memberCount: 1234, status: 'Online', commandsToday: 7, detailUrl: '/Guilds/Details/123456789012345678'
        }]
    }, format);

    assert.equal(ok, true);
    assert.equal(doc.rows.length, 1);
    assert.equal(doc.fields.name.textContent, 'Test <b>Server</b>', 'the name goes in as text, never as markup');
    assert.equal(doc.fields.members.textContent, '1,234');
    assert.equal(doc.fields.commands.textContent, '7');
    assert.equal(doc.fields.status.textContent, 'Online');
    assert.equal(doc.fields.status.className, 'badge badge-success');
    assert.equal(doc.fields.avatar.textContent, 'TS');
    assert.match(doc.fields.avatar.className, /text-white bg-accent-orange/);
    assert.equal(doc.fields.link.attrs.href, '/Guilds/Details/123456789012345678');
    assert.equal(doc.fields.copy.attrs['data-copy-server-id'], '123456789012345678', 'the id stays a string');
    assert.equal(doc.fields.copy.attrs['aria-label'], 'Copy server ID for Test <b>Server</b>');
    assert.equal(doc.viewAll.textContent, 'View all 9');
    assert.equal(doc.list.hidden, false);
    assert.equal(doc.empty.hidden, true);
});

test('renderServers with no servers shows the empty state instead of an empty list', () => {
    const doc = fakeDocument();
    DashboardActions.renderServers(doc, { totalServerCount: 0, servers: [] }, format);

    assert.equal(doc.rows.length, 0);
    assert.equal(doc.list.hidden, true);
    assert.equal(doc.head.hidden, true);
    assert.equal(doc.empty.hidden, false);
});

test('renderServers uses the icon when there is one', () => {
    const doc = fakeDocument();
    DashboardActions.renderServers(doc, {
        totalServerCount: 1,
        servers: [{ id: '1', name: 'A', iconUrl: 'https://cdn.example/a.png', initials: 'A', avatarClass: 'bg-success', memberCount: 1, status: 'Idle', commandsToday: 0, detailUrl: '/Guilds/Details/1' }]
    }, format);

    assert.equal(doc.fields.avatar.children.length, 1);
    assert.equal(doc.fields.avatar.children[0].attrs.src, 'https://cdn.example/a.png');
    assert.equal(doc.fields.avatar.textContent, '');
});

test('renderServers does nothing without the card on the page', () => {
    const doc = { getElementById: () => null };
    assert.equal(DashboardActions.renderServers(doc, { servers: [] }, format), false);
});
