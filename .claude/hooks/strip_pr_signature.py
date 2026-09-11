import re
import sys

SIGNATURE_LINE_PATTERNS = [
    r'Generated (with|by) \[?Claude Code\]?',
    r'^Co-Authored-By:\s*Claude\b',
    r'^Claude-Session:\s*https://claude\.ai/code/\S+',
    r'claude\.ai/code/session_\S+',
]


def is_signature_line(line: str) -> bool:
    stripped = line.strip()
    if not stripped:
        return False
    return any(re.search(pat, stripped, re.IGNORECASE) for pat in SIGNATURE_LINE_PATTERNS)


def strip_signature(body: str) -> str:
    lines = body.splitlines()
    out = [line for line in lines if not is_signature_line(line)]

    while out and out[-1].strip() == '':
        out.pop()

    while out and out[-1].strip() == '---':
        out.pop()
        while out and out[-1].strip() == '':
            out.pop()

    collapsed = []
    prev_blank = False
    for line in out:
        blank = line.strip() == ''
        if blank and prev_blank:
            continue
        collapsed.append(line)
        prev_blank = blank

    return '\n'.join(collapsed) + '\n'


if __name__ == '__main__':
    sys.stdout.write(strip_signature(sys.stdin.read()))
