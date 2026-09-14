"""SOLOSRC assembly code. Generated frames; reference-derived badges: see README provenance."""
from pathlib import Path
import json,os
ROOT=Path(os.environ.get('CARDS_REPO',str(Path(__file__).resolve().parents[3])))
SRC=ROOT/'assets/source/cards';SRC.mkdir(parents=True,exist_ok=True)
for d in ['frames','icons']:(ROOT/'assets/cards'/d).mkdir(parents=True,exist_ok=True)
NS='xmlns="http://www.w3.org/2000/svg" xmlns:inkscape="http://www.inkscape.org/namespaces/inkscape"'
def svg(w,h,body):return f'<svg {NS} width="{w}" height="{h}" viewBox="0 0 {w} {h}">{body}</svg>'
def layer(name,body):return f'<g id="{name}" inkscape:groupmode="layer" inkscape:label="{name}">{body}</g>'
def save(name,w,h,body):
 p=SRC/(name+'.svg');p.write_text(svg(w,h,body));return p
DARK='#293344';IVORY='#f8f1de'
from reference_frames import write_frames
write_frames(SRC, save, layer)
# Director attachment, 2026-09-13: flat tan rim, brown field, upright dark ellipse.
# Reconstructed as editable shapes at the existing 590x860 runtime resolution.
back='<rect width="590" height="860" fill="#b7885a"/><rect x="21" y="21" width="548" height="818" rx="6" fill="#563121" stroke="#0b0000" stroke-width="2"/>'
well='<ellipse cx="295" cy="430" rx="112" ry="212" fill="#1d1d1d" stroke="#000000" stroke-width="2"/>'
save('card_back',590,860,layer('tan-rim-and-flat-brown-field',back)+layer('centered-dark-ellipse',well))
# Original tooltip glyphs; approved main badges are copied by the exporter.
from reference_badges import write_badges
write_badges(SRC, save, layer)
(SRC/'frame-layout.json').write_text(json.dumps({'revision': 5, 'size': [590, 860], 'bevel_width': 10, 'art_window': [10, 10, 570, 610], 'data_panel': [0, 630, 590, 230], 'star_size': [34, 34], 'star_step': 37, 'star_row_y': 686, 'star_row_center_x': 295, 'star_row_right_max': 467, 'attribute': [524, 686], 'attribute_size': [66, 66], 'atk': [156, 785], 'def': [434, 785], 'stat_alignment': 'center', 'stat_font_px': 80, 'stat_font_style': 'normal', 'stat_plates': [[34, 738, 244, 94], [312, 738, 244, 94]], 'spell_trap_plate': [34, 693, 522, 104], 'spell_trap_badge': [295, 745], 'spell_trap_badge_size': [72, 72], 'subtype_placement': 'tooltip_only'},indent=2)+'\n')
print('Wrote card source revision 5')
