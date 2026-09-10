"""SOLOSRC MIT. Original layered SVG card frames and icons, asset-list 1.5–1.9."""
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
colors={'normal':('#c8a577','#eddbc0'),'effect':('#c87841','#f4d5ad'),'fusion':('#8064b4','#dfd2ed'),'ritual':('#4a77b5','#ccdef1'),'spell':('#33948d','#cce9de'),'trap':('#b4538c','#edd0e3')}
for i,(name,(base,pale)) in enumerate(colors.items()):
 shell=f'<path fill="{base}" fill-rule="evenodd" d="M18 0 H572 Q590 0 590 18 V842 Q590 860 572 860 H18 Q0 860 0 842 V18 Q0 0 18 0 Z M35 40 V600 H555 V40 Z"/>'
 border=f'<rect x="6" y="6" width="578" height="848" rx="14" fill="none" stroke="{DARK}" stroke-width="5"/><rect x="17" y="17" width="556" height="826" rx="7" fill="none" stroke="{pale}" stroke-width="3"/><rect x="32" y="37" width="526" height="566" fill="none" stroke="{DARK}" stroke-width="6"/>'
 clouddefs='<defs><filter id="clouds" x="0" y="0" width="100%" height="100%"><feTurbulence type="fractalNoise" baseFrequency=".018 .032" numOctaves="3" seed="19"/><feColorMatrix type="matrix" values="0 0 0 0 1  0 0 0 0 1  0 0 0 0 1  1.3 0 0 0 -.35"/><feGaussianBlur stdDeviation="1.4"/></filter><clipPath id="band-clip"><rect x="28" y="608" width="534" height="224" rx="4"/></clipPath><clipPath id="frame-clip"><path clip-rule="evenodd" d="M0 0 H590 V860 H0 Z M35 40 V600 H555 V40 Z"/></clipPath></defs>'
 clouds=clouddefs+'<g clip-path="url(#frame-clip)" opacity=".48"><rect width="590" height="860" filter="url(#clouds)"/></g>'
 band=f'<rect x="28" y="608" width="534" height="224" rx="4" fill="{base}" stroke="{DARK}" stroke-width="3"/><g clip-path="url(#band-clip)"><rect x="28" y="608" width="534" height="224" fill="{pale}" opacity=".40"/><rect x="28" y="608" width="534" height="224" filter="url(#clouds)" opacity=".85"/></g>'
 if name not in ('spell','trap'):
  for x in [50,320]:
   band+=f'<path d="M{x+7} 704 H{x+203} L{x+210} 711 V799 L{x+203} 806 H{x+7} L{x} 799 V711 Z" fill="{DARK}"/><path d="M{x+9} 707 H{x+201} L{x+207} 713 V797 L{x+201} 803 H{x+9} L{x+3} 797 V713 Z" fill="{pale}"/><rect x="{x+7}" y="711" width="196" height="88" fill="{IVORY}" stroke="{DARK}" stroke-width="2"/><path d="M{x+10} 715 H{x+200} M{x+10} 795 H{x+200}" stroke="{base}" stroke-width="1.5"/>'
 # A small original type signature in the top frame rail, leaves window clear.
 marks=['M-12 0 H12','M-13 0 H-4 M4 0 H13','M-13 0 L0 -6 L13 0 L0 6 Z','M-13 0 L-6 -6 L0 0 L6 -6 L13 0 L6 6 L0 0 L-6 6 Z','M-12 5 L0 -6 L12 5','M-12 -5 L0 6 L12 -5']
 ornament=f'<path d="{marks[i]}" transform="translate(295 24)" fill="none" stroke="{DARK}" stroke-width="3" stroke-linejoin="round"/>'
 save('frame_'+name,590,860,layer('frame-base',shell)+layer('cloud-texture',clouds)+layer('border',border)+layer('information-band',band)+layer('type-signature',ornament))
# Original flat brown black-hole motif, built from paired broad shapes.
defs='<defs><clipPath id="back-field"><rect x="28" y="28" width="534" height="804" rx="7"/></clipPath></defs>'
back=defs+'<rect width="590" height="860" rx="18" fill="#59402f"/><rect x="6" y="6" width="578" height="848" rx="14" fill="none" stroke="#293344" stroke-width="5"/><rect x="17" y="17" width="556" height="826" rx="7" fill="none" stroke="#a88a66" stroke-width="3"/><rect x="28" y="28" width="534" height="804" rx="7" fill="#513927" stroke="#443325" stroke-width="3"/>'
ribbons=[]
for k in range(3):
 sides=[]
 for sign in [1,-1]:
  pts=[]
  for j in range(65):
   t=j/64;ang=k*1.02+2.1*t;rad=.48+.60*t;thick=(.065+.025*k)*math.sin(math.pi*t)
   x=295+205*(rad+sign*thick)*math.cos(ang)
   y=430+285*(rad+sign*thick)*math.sin(ang)
   pts.append(f'{x:.2f},{y:.2f}')
  sides.append(pts)
 ribbons.append('<path d="M'+' L'.join(sides[0]+list(reversed(sides[1])))+' Z" fill="'+['#876344','#3c291d','#6b4b32'][k]+'"/>')
flow='<g clip-path="url(#back-field)"><g id="broad-currents">'+''.join(ribbons)+'</g><use href="#broad-currents" transform="rotate(180 295 430)"/></g>'
hole='<ellipse cx="295" cy="430" rx="89" ry="128" transform="rotate(-18 295 430)" fill="#050505"/>'
save('card_back',590,860,layer('matte-brown-frame',back)+layer('broad-currents',flow)+layer('black-hole-centre',hole))
paths={
'dark':('M84 27 A39 39 0 1 0 91 91 A34 34 0 0 1 84 27 Z','#70639e'),
'light':('M64 21 V30 M64 98 V107 M21 64 H30 M98 64 H107 M34 34 L41 41 M87 87 L94 94 M34 94 L41 87 M87 41 L94 34','#b79235'),
'earth':('M25 87 L49 45 L64 66 L78 36 L105 87 Z M37 96 H92','#7d8550'),
'water':('M64 24 C53 42 34 61 34 77 A30 30 0 0 0 94 77 C94 60 73 39 64 24 Z','#347aab'),
'fire':('M68 23 C85 46 91 51 95 70 C102 100 47 115 33 83 C24 62 43 56 44 42 C53 51 51 59 55 64 C69 52 59 37 68 23 Z','#b8583c'),
'wind':('M25 48 H75 C98 48 95 23 78 29 M25 64 H98 M25 80 H70 C94 80 95 104 77 100','#548b79'),
'divine':('M64 23 L101 44 V84 L64 105 L27 84 V44 Z M64 40 L82 64 L64 88 L46 64 Z','#a78842')}
for name,(path,col) in paths.items():
 base=f'<circle cx="64" cy="64" r="60" fill="{col}" stroke="{DARK}" stroke-width="5"/><circle cx="64" cy="64" r="51" fill="none" stroke="{IVORY}" stroke-width="2"/>'
 fill='none' if name in ['light','wind','divine'] else IVORY
 symbol=f'<path d="{path}" fill="{fill}" stroke="{IVORY}" stroke-width="7" stroke-linecap="round" stroke-linejoin="round"/>'
 if name=='light':symbol+=f'<circle cx="64" cy="64" r="21" fill="{IVORY}"/>'
 if name=='water':symbol+=f'<path d="M46 77 Q46 91 61 94" fill="none" stroke="{col}" stroke-width="5"/>'
 save('attr_'+name,128,128,layer('badge',base)+layer('symbol',symbol))
glyphs={
'spell':'M64 22 L74 53 L106 64 L74 75 L64 106 L54 75 L22 64 L54 53 Z',
 'trap':'M29 31 H99 V70 L64 104 L29 70 Z M45 48 L83 83 M83 48 L45 83',
 'equip':'M43 25 L30 34 L34 55 L26 71 L35 103 H93 L102 71 L94 55 L98 34 L85 25 C82 48 46 48 43 25 Z M64 53 V92 M41 74 L64 82 L87 74',
 'continuous':'M63 63 C24 18 10 92 42 85 C70 79 64 30 90 41 C122 55 101 108 63 63 Z',
 'quick_play':'M73 20 L34 69 H61 L52 109 L96 56 H70 Z',
 'counter':'M35 52 L58 43 L80 52 V75 Q77 94 58 104 Q38 94 35 75 Z M24 24 L61 60 L103 23 M81 23 H103 V45',
 'field':'M64 23 L107 47 L64 71 L21 47 Z M21 66 L64 90 L107 66 M21 85 L64 109 L107 85'}
for name,path in glyphs.items():
 col='#33948d' if name=='spell' else '#b4538c' if name=='trap' else DARK
 b=f'<rect x="5" y="5" width="118" height="118" rx="24" fill="{col}" stroke="{DARK}" stroke-width="5"/>'
 symbol=f'<path d="{path}" fill="{IVORY if name in ["spell","quick_play"] else "none"}" stroke="{IVORY}" stroke-width="7" stroke-linejoin="round" stroke-linecap="round"/>'
 save('st_'+name,128,128,layer('badge',b)+layer('symbol',symbol))
pts=[]
for i in range(10):
 a=-math.pi/2+i*math.pi/5;r=23 if i%2==0 else 10
 pts.append(f'{32+r*math.cos(a):.2f},{32+r*math.sin(a):.2f}')
save('star',64,64,layer('medallion','<circle cx="32" cy="32" r="30" fill="#c88b32" stroke="#6e481e" stroke-width="3"/>')+layer('star',f'<polygon points="{" ".join(pts)}" fill="#fff0aa" stroke="#6e481e" stroke-width="2"/>'))
# Reference data mirrors systems.md. Coordinates intentionally do not decide art crop.
(SRC/'frame-layout.json').write_text(json.dumps({'size':[590,860],'art_window':[35,40,520,560],'inset_pct':6,'stars':[68,656],'attribute':[514,656],'atk':[68,726],'def':[338,726],'data_panel':[28,608,534,224],'star_size':[32,32],'star_step':34,'attribute_size':[48,48],'stat_font_px':56},indent=2)+'\n')
print('Wrote',len(list(SRC.glob('*.svg'))),'layered SVG sources to',SRC)
