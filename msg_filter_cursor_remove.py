import sys
for line in sys.stdin:
    if line.strip() == "Co-authored-by: Cursor <cursoragent@cursor.com>":
        continue
    sys.stdout.write(line)
