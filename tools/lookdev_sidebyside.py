#!/usr/bin/env python3
"""Composite the look-dev captures against the approved concepts (#97).

Usage: python3 tools/lookdev_sidebyside.py <capture-dir>

Reads lookdev_world_render.png and lookdev_duel_render.png from
<capture-dir> (written by tests/scenes/LookDev.tscn with -- --capture),
puts each next to its concept — concept left, render right — and writes
lookdev_world.png and lookdev_duel.png back into <capture-dir>.
"""

import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONCEPTS = os.path.join(ROOT, "docs", "art", "concepts")
PAIRS = [
    ("world-exploration-v02.png", "lookdev_world_render.png", "lookdev_world.png"),
    ("street-duel-key-visual-v02.png", "lookdev_duel_render.png", "lookdev_duel.png"),
]


def main() -> int:
    if len(sys.argv) != 2:
        print(__doc__.strip(), file=sys.stderr)
        return 2

    directory = sys.argv[1]
    failures = 0
    for concept_name, render_name, out_name in PAIRS:
        concept_path = os.path.join(CONCEPTS, concept_name)
        render_path = os.path.join(directory, render_name)
        if not os.path.exists(render_path):
            print(f"MISSING {render_path} (run LookDev.tscn with -- --capture first)")
            failures += 1
            continue

        concept = Image.open(concept_path).convert("RGB")
        render = Image.open(render_path).convert("RGB")
        if render.height != concept.height:
            width = round(render.width * concept.height / render.height)
            render = render.resize((width, concept.height), Image.LANCZOS)

        sheet = Image.new("RGB", (concept.width + render.width, concept.height))
        sheet.paste(concept, (0, 0))
        sheet.paste(render, (concept.width, 0))
        out_path = os.path.join(directory, out_name)
        sheet.save(out_path)
        print(f"wrote {out_path}")

    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
