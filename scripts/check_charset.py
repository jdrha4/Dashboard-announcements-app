#!/usr/bin/env python3
from collections.abc import Iterator
from pathlib import Path
import sys


def find_disallowed_chars(path: Path) -> "Iterator[tuple[str, int, int]]":
    """Yield any disallowed characters and their line/column positions.

    Allowed characters are those in the:
    - Unicode Basic Latin
    - Latin-1 Supplement
    - Latin Extended-A blocks
    -> Code points below U+024F
    """
    try:
        with path.open("r", encoding="utf-8") as f:
            for lineno, line in enumerate(f, start=1):
                for colno, ch in enumerate(line, start=1):
                    if ord(ch) > 0x024F:
                        yield ch, lineno, colno
    except Exception as exc:
        print(f"Could not open file: {path} - {exc!r}, skipping", file=sys.stderr)


def main() -> None:
    paths = [Path(path) for path in sys.argv[1:]]

    has_bad_files = False
    for file in paths:
        if not file.is_file():
            print(f"Path {file} is not a file")
            sys.exit(1)

        for amt, (ch, lineno, colno) in enumerate(find_disallowed_chars(file), start=1):
            has_bad_files = True
            print(f"File {file} contains characters beyond the allowed charset:")
            print(f"  Line {lineno}, Column {colno}: {repr(ch)} (U+{ord(ch):04X})")

            if amt >= 10:
                print("  (Only the first 10 disallowed characters reported)")
                break

    if has_bad_files:
        sys.exit(1)


if __name__ == "__main__":
    main()
