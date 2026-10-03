const test = require('node:test');
const assert = require('node:assert/strict');
const { render, escapeHtml } = require('../discord-markdown.js');

test.describe('escaping comes first', () => {
    test('markup in the message is shown as text', () => {
        assert.equal(render('<script>alert(1)</script>'), '&lt;script&gt;alert(1)&lt;/script&gt;');
        assert.ok(!render('<img src=x onerror=alert(1)>').includes('<img'));
    });

    test('quotes are escaped so a value cannot leave an attribute', () => {
        assert.equal(render('say "hi" & \'bye\''), 'say &quot;hi&quot; &amp; &#39;bye&#39;');
    });

    test('a forged placeholder in the message is not restored', () => {
        const out = render('\uE0000\uE001 text');
        assert.ok(!out.includes('\uE000'));
    });

    test('empty and nullish input render nothing', () => {
        assert.equal(render(''), '');
        assert.equal(render(null), '');
        assert.equal(render(undefined), '');
        assert.equal(escapeHtml(null), '');
    });
});

test.describe('inline markdown', () => {
    test('bold, italic, underline, strikethrough', () => {
        assert.equal(render('**b**'), '<strong>b</strong>');
        assert.equal(render('*i*'), '<em>i</em>');
        assert.equal(render('_i_'), '<em>i</em>');
        assert.equal(render('__u__'), '<u>u</u>');
        assert.equal(render('~~s~~'), '<s>s</s>');
        assert.equal(render('***bi***'), '<strong><em>bi</em></strong>');
    });

    test('snake_case words are not italicised', () => {
        assert.equal(render('use snake_case_names here'), 'use snake_case_names here');
    });

    test('inline code is literal', () => {
        assert.equal(render('run `**not bold**` now'), 'run <code class="discord-md-code">**not bold**</code> now');
    });

    test('code blocks are literal and keep their newlines', () => {
        const out = render('```js\nlet a = *1*;\n```');
        assert.equal(out, '<pre class="discord-md-codeblock"><code>let a = *1*;</code></pre>');
    });

    test('markup inside a code span stays escaped', () => {
        const out = render('`<b>x</b>`');
        assert.equal(out, '<code class="discord-md-code">&lt;b&gt;x&lt;/b&gt;</code>');
    });

    test('spoilers are focusable so they can be revealed by keyboard', () => {
        assert.ok(render('||secret||').includes('class="discord-md-spoiler" tabindex="0"'));
    });
});

test.describe('mentions and links', () => {
    test('user and channel mentions become chips without the id', () => {
        assert.equal(render('<@123456789012345678>'), '<span class="discord-mention">@user</span>');
        assert.equal(render('<#123456789012345678>'), '<span class="discord-mention">#channel</span>');
    });

    test('@everyone and @here are highlighted', () => {
        assert.equal(render('@everyone'), '<span class="discord-mention">@everyone</span>');
    });

    test('a URL is wrapped but not linked, and trailing punctuation stays outside', () => {
        assert.equal(render('see https://example.com/a?b=1.'), 'see <span class="discord-md-link">https://example.com/a?b=1</span>.');
    });

    test('underscores inside a URL are not italic', () => {
        const out = render('https://example.com/a_b_c');
        assert.ok(!out.includes('<em>'));
    });
});

test.describe('blocks and line breaks', () => {
    test('newlines become <br>', () => {
        assert.equal(render('a\nb'), 'a<br>b');
        assert.equal(render('a\n\nb'), 'a<br><br>b');
    });

    test('quote lines are blocks with no stray breaks', () => {
        assert.equal(render('> quoted\nafter'), '<div class="discord-md-quote">quoted</div>after');
    });

    test('headings', () => {
        assert.equal(render('## Title'), '<div class="discord-md-h2">Title</div>');
    });
});

test.describe('tokens', () => {
    const tokens = {
        '{user}': { text: '@NewMember', mention: true },
        '{server}': 'My <Server>',
        '{memberCount}': '1,234'
    };

    test('a token previews as sample data, escaped', () => {
        assert.equal(render('Welcome to {server}!', { tokens }), 'Welcome to <strong>My &lt;Server&gt;</strong>!');
    });

    test('a mention token becomes a chip', () => {
        assert.equal(render('Hi {user}', { tokens }), 'Hi <span class="discord-mention">@NewMember</span>');
    });

    test('markdown can wrap a token', () => {
        assert.equal(render('*{memberCount}*', { tokens }), '<em><strong>1,234</strong></em>');
    });

    test('a dollar sequence in the sample data stays literal', () => {
        const out = render('{server}', { tokens: { '{server}': "$& $1 $'" } });
        assert.equal(out, '<strong>$&amp; $1 $&#39;</strong>');
    });

    test('an unknown token is left as text', () => {
        assert.equal(render('{nope}', { tokens }), '{nope}');
    });
});
