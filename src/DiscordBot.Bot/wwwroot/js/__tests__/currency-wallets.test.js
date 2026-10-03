const test = require('node:test');
const assert = require('node:assert/strict');
const Wallets = require('../currency/currency-wallets.js');

const input = (overrides) => Object.assign({ userId: '987654321098765432', amount: '10', reason: 'Event prize' }, overrides);

test('mint and fine need a picked member, an integer amount above zero and a reason', () => {
    for (const action of ['mint', 'fine']) {
        assert.equal(Wallets.validateAction(action, input({ userId: '' })).field, 'wallet-action-user-search');
        assert.equal(Wallets.validateAction(action, input({ userId: 'alice' })).field, 'wallet-action-user-search');
        assert.equal(Wallets.validateAction(action, input({ amount: '' })).field, 'wallet-action-amount');
        assert.equal(Wallets.validateAction(action, input({ amount: '1.5' })).field, 'wallet-action-amount');
        assert.equal(Wallets.validateAction(action, input({ amount: '0' })).field, 'wallet-action-amount');
        assert.equal(Wallets.validateAction(action, input({ amount: '-3' })).field, 'wallet-action-amount');
        assert.equal(Wallets.validateAction(action, input({ reason: '  ' })).field, 'wallet-action-reason');
    }
});

test('a mint request keeps the member ID a string', () => {
    const result = Wallets.validateAction('mint', input());
    assert.equal(result.error, null);
    assert.deepEqual(result.body, { userId: '987654321098765432', amount: 10, reason: 'Event prize' });
});

test('a fine can ask for a mod case', () => {
    const result = Wallets.validateAction('fine', input({ openCase: true }));
    assert.equal(result.body.openCase, true);
});

test('an adjustment is signed, names no member, and still needs a whole number and a reason', () => {
    const debit = Wallets.validateAction('adjust', { userId: '', amount: '-25', reason: 'Fix' });
    assert.equal(debit.error, null);
    assert.deepEqual(debit.body, { amount: -25, reason: 'Fix' });

    assert.equal(Wallets.validateAction('adjust', { userId: '', amount: '0', reason: 'Fix' }).field, 'wallet-action-amount');
    assert.equal(Wallets.validateAction('adjust', { userId: '', amount: '2.5', reason: 'Fix' }).field, 'wallet-action-amount');
    assert.equal(Wallets.validateAction('adjust', { userId: '', amount: '5', reason: '' }).field, 'wallet-action-reason');
});

test('an amount beyond the safe integers is refused', () => {
    assert.ok(Wallets.validateAction('mint', input({ amount: '99999999999999999999' })).error);
});

test('the holder search matches names and IDs, ignoring case', () => {
    const wallets = [
        { username: 'Alice', userId: '111' },
        { username: 'bob', userId: '222333' }
    ];
    assert.equal(Wallets.filterWallets(wallets, '').length, 2);
    assert.deepEqual(Wallets.filterWallets(wallets, 'ali').map((w) => w.username), ['Alice']);
    assert.deepEqual(Wallets.filterWallets(wallets, '2233').map((w) => w.username), ['bob']);
    assert.equal(Wallets.filterWallets(wallets, 'zzz').length, 0);
});
