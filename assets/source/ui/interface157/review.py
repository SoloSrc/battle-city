"""Reproduce this art kit's Godot evidence in an isolated temporary project."""
from pathlib import Path
import argparse
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[4]
SOURCE = Path(__file__).resolve().parent


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--godot', required=True, help='Godot 4.7 executable')
    args = parser.parse_args()
    if not (ROOT / 'assets/ui/fonts/local/Outfit.ttf').exists():
        parser.error('Run install_font.py before review.')
    evidence = ROOT / 'docs/requests/evidence/interface157'
    evidence.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='astra157-') as tmp:
        project = Path(tmp)
        shutil.copytree(ROOT / 'assets/ui', project / 'assets/ui')
        shutil.copy(SOURCE / 'capture.gd', project / 'capture.gd')
        (project / 'project.godot').write_text('''config_version=5
[application]
config/name="Astra interface review"
[display]
window/size/viewport_width=1920
window/size/viewport_height=1080
[rendering]
renderer/rendering_method="mobile"
''')
        base = [args.godot, '--path', str(project)]
        subprocess.run(base + ['--headless', '--editor', '--import', '--quit'], check=True, timeout=120)
        subprocess.run(base + ['--rendering-method', 'mobile', '--rendering-driver', 'vulkan',
                              '--script', str(project / 'capture.gd'), '--', str(evidence)], check=True, timeout=120)
        for sidecar in (project / 'assets/ui').rglob('*.png.import'):
            shutil.copy(sidecar, ROOT / sidecar.relative_to(project))
    print('Saved Godot evidence and texture import settings.')


if __name__ == '__main__':
    main()
