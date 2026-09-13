"""SOLOSRC assembly code. Reference-derived frames and badges: see README provenance."""
from pathlib import Path
import json,math,os
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
# Original flat brown black-hole motif, built from paired broad shapes.
defs='<defs><clipPath id="back-field"><rect x="28" y="28" width="534" height="804" rx="7"/></clipPath></defs>'
back=defs+'<rect width="590" height="860" rx="18" fill="#59402f"/><rect x="6" y="6" width="578" height="848" rx="14" fill="none" stroke="#2d2017" stroke-width="5"/><rect x="17" y="17" width="556" height="826" rx="7" fill="none" stroke="#a88a66" stroke-width="3"/><rect x="28" y="28" width="534" height="804" rx="7" fill="#513927" stroke="#443325" stroke-width="3"/>'
# Broad currents meet a shared elliptical inner boundary tangentially.
ribbons=[]
for k in range(3):
 sides=[]
 for sign in [1,-1]:
  pts=[]
  for j in range(101):
   t=j/100;ang=k*1.02+2.65*t
   rad=.405+.675*t
   thick=(.055+.020*k)*(math.sin(math.pi*t)**1.25)
   x=205*(rad+sign*thick)*math.cos(ang)
   y=285*(rad+sign*thick)*math.sin(ang)
   turn=-18*math.pi/180
   pts.append(f'{295+x*math.cos(turn)-y*math.sin(turn):.2f},{430+x*math.sin(turn)+y*math.cos(turn):.2f}')
  sides.append(pts)
 ribbons.append('<path d="M'+' L'.join(sides[0]+list(reversed(sides[1])))+' Z" fill="'+['#876344','#3c291d','#6b4b32'][k]+'"/>')
flow='<g clip-path="url(#back-field)"><g id="broad-currents">'+''.join(ribbons)+'</g><use href="#broad-currents" transform="rotate(180 295 430)"/></g>'
# A warm-dark transition fades inward into the same ellipse as the tips.
well='<defs><radialGradient id="inward-dark"><stop offset="0" stop-color="#050505"/><stop offset=".57" stop-color="#050505"/><stop offset=".66" stop-color="#160f0b"/><stop offset=".82" stop-color="#382619"/><stop offset="1" stop-color="#513927" stop-opacity="0"/></radialGradient></defs><ellipse cx="295" cy="430" rx="147" ry="205" transform="rotate(-18 295 430)" fill="url(#inward-dark)"/>'
save('card_back',590,860,layer('matte-brown-frame',back)+layer('inward-transition',well)+layer('broad-currents',flow))
# Exact reference badges are isolated by SVG viewports in reference_badges.py.
from reference_badges import write_badges
write_badges(SRC, save, layer)
(SRC/'frame-layout.json').write_text(json.dumps({'revision':4,'size':[590,860],'art_window':[14,14,562,616],'data_panel':[0,645,590,215],'star_size':[34,34],'star_step':36,'star_row_y':696,'star_row_center_x':295,'star_row_right_max':460,'attribute':[514,696],'attribute_size':[64,64],'atk':[158,781],'def':[430,781],'stat_alignment':'center','stat_font_px':70,'stat_font_style':'normal','stat_plates':[[46,735,224,92],[318,735,224,92]],'spell_trap_badge':[295,750],'spell_trap_badge_size':[72,72],'subtype_placement':'tooltip_only'},indent=2)+'\n')
print('Wrote card source revision 4')
