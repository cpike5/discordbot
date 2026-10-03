const test = require('node:test');
const assert = require('node:assert/strict');
const CurrencyManage = require('../currency/currency-manage.js');

const form = (overrides) => Object.assign({
    name: 'Coins', symbol: '🪙', isTransferable: true, allowNegative: false, debtFloor: ''
}, overrides);

test('a name and a symbol are required', () => {
    assert.equal(CurrencyManage.validateCurrencyForm(form({ name: '  ' })).field, 'currency-name');
    assert.equal(CurrencyManage.validateCurrencyForm(form({ symbol: '' })).field, 'currency-symbol');
});

test('a valid form builds the request with the debt floor left null when debt is off', () => {
    const result = CurrencyManage.validateCurrencyForm(form({ name: ' Coins ', debtFloor: '-50' }));
    assert.equal(result.error, null);
    assert.deepEqual(result.payload, { name: 'Coins', symbol: '🪙', isTransferable: true, allowNegative: false, debtFloor: null });
});

test('with debt on, the floor must be a whole number below zero', () => {
    const ok = CurrencyManage.validateCurrencyForm(form({ allowNegative: true, debtFloor: '-100' }));
    assert.equal(ok.error, null);
    assert.equal(ok.payload.debtFloor, -100);

    for (const bad of ['', '0', '5', '-1.5', 'abc', '-1e3', '--4']) {
        const result = CurrencyManage.validateCurrencyForm(form({ allowNegative: true, debtFloor: bad }));
        assert.ok(result.error, 'expected an error for ' + JSON.stringify(bad));
        assert.equal(result.field, 'currency-debt-floor');
    }
});

test('a debt floor too large for a safe integer is refused', () => {
    const result = CurrencyManage.validateCurrencyForm(form({ allowNegative: true, debtFloor: '-99999999999999999999' }));
    assert.ok(result.error);
});

test('a user grant needs a picked user and keeps the ID a string', () => {
    assert.equal(CurrencyManage.buildGrant(0, '', '').field, 'authority-user-search');
    assert.equal(CurrencyManage.buildGrant(0, 'alice', '').error !== null, true);
    const grant = CurrencyManage.buildGrant(0, '987654321098765432', '');
    assert.deepEqual(grant.body, { principalType: 0, principalId: '987654321098765432' });
});

test('a role grant needs a chosen role', () => {
    assert.equal(CurrencyManage.buildGrant(1, '111', '').field, 'authority-role');
    assert.deepEqual(CurrencyManage.buildGrant(1, '', '555').body, { principalType: 1, principalId: '555' });
});

test('a system grant names no principal', () => {
    assert.deepEqual(CurrencyManage.buildGrant(2, '', '').body, { principalType: 2, principalId: null });
});

test('rules text matches the server-rendered one', () => {
    assert.equal(CurrencyManage.rulesText({ isTransferable: true, allowNegative: false }), 'Transferable');
    assert.equal(CurrencyManage.rulesText({ isTransferable: false, allowNegative: true, debtFloor: -100 }), 'Not transferable • debt to -100');
});

test('a grant is labelled with its name, or with what it is when the name is unknown', () => {
    assert.equal(CurrencyManage.authorityLabel({ principalName: 'alice', principalType: 0 }), 'alice');
    assert.equal(CurrencyManage.authorityLabel({ principalName: null, principalType: 0 }), 'Unknown user');
    assert.equal(CurrencyManage.authorityLabel({ principalName: null, principalType: 1 }), 'Unknown role');
    assert.equal(CurrencyManage.authorityLabel({ principalName: null, principalType: 2 }), 'System');
});
