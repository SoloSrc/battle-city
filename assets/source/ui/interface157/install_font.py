"""Fetch the pinned OFL font locally; third-party font files stay out of Git."""
from pathlib import Path
from urllib.request import urlopen
import hashlib

ROOT = Path(__file__).resolve().parents[4]
DEST = ROOT / "assets/ui/fonts/local"
BASE = "https://raw.githubusercontent.com/google/fonts/e44c4b011a820c2cbe2fd2cfa8052037d7edb571/ofl/outfit/"
FILES = {
    "Outfit.ttf": ("Outfit%5Bwght%5D.ttf", "fc7287273e66929776e2ba54f144fe699080bec29f61bf649d70d871468aeade"),
    "OFL.txt": ("OFL.txt", "c676351bf8576b9aba743cd5eaa8c0e7ee0d51f805d720447b4df4ddb6a2e416"),
}

if __name__ == "__main__":
    # Verify both responses before writing either file.
    downloaded = {}
    for name, (remote, digest) in FILES.items():
        data = urlopen(BASE + remote, timeout=30).read()
        if hashlib.sha256(data).hexdigest() != digest:
            raise SystemExit(f"Checksum mismatch: {name}")
        downloaded[name] = data
    DEST.mkdir(parents=True, exist_ok=True)
    for name, data in downloaded.items():
        (DEST / name).write_bytes(data)
    print(f"Installed Outfit and its OFL license in {DEST}")
