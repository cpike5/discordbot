const test = require('node:test');
const assert = require('node:assert/strict');
const NotificationHistory = require('../notification-history.js');

test('a list with no read filter keeps a row whatever its new state', () => {
    assert.equal(NotificationHistory.staysInList('', true), true);
    assert.equal(NotificationHistory.staysInList('', false), true);
    assert.equal(NotificationHistory.staysInList(undefined, true), true);
});

test('an Unread list drops a row once it is read, and keeps it while it is unread', () => {
    assert.equal(NotificationHistory.staysInList('false', true), false);
    assert.equal(NotificationHistory.staysInList('false', false), true);
});

test('a Read list drops a row once it is marked unread, and keeps it while it is read', () => {
    assert.equal(NotificationHistory.staysInList('true', false), false);
    assert.equal(NotificationHistory.staysInList('true', true), true);
});

test('the delete all message names the scope and the count and says it cannot be undone', () => {
    const message = NotificationHistory.deleteAllMessage(12, 'the notifications that match the current filters');
    assert.match(message, /the notifications that match the current filters/);
    assert.match(message, /12 notifications/);
    assert.match(message, /cannot be undone/);
});

test('the delete all message uses the singular for one notification', () => {
    assert.match(NotificationHistory.deleteAllMessage(1, 'every notification'), /1 notification\b(?!s)/);
});

test('the summary line gives the range and the total', () => {
    assert.equal(NotificationHistory.summaryText(1, 25, 60), 'Showing 1 to 25 of 60 notifications');
    assert.equal(NotificationHistory.summaryText(1, 1, 1), 'Showing 1 to 1 of 1 notification');
});
