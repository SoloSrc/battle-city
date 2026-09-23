"""Original SOLOSRC interface artwork, MIT. Run from any directory.
Requires Python 3, CairoSVG 2.9.1 and fontTools; first run install_font.py.
SVGs are editable masters; PNGs are transparent runtime exports.
"""
from pathlib import Path
import json
import cairosvg
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools.pens.svgPathPen import SVGPathPen

ROOT = Path(__file__).resolve().parents[4]
SRC = Path(__file__).resolve().parent
UI = ROOT / "assets/ui"
EVIDENCE = ROOT / "docs/requests/evidence/interface157"
INK, WHITE, BLUE = "#293344", "#F6F8FA", "#3565A9"
CYAN, ORANGE, MUTED = "#79D8FF", "#FFA18B", "#637187"
fonts = {w: instantiateVariableFont(TTFont(UI / "fonts/local/Outfit.ttf"), {"wght": w}, inplace=True) for w in (400, 700)}

def text(s, x, y, size=28, color=INK, weight=400):
    """Outline specimens so the review matches the chosen font on every host."""
    f = fonts[weight]; gs = f.getGlyphSet(); cmap = f.getBestCmap()
    scale = size / f["head"].unitsPerEm
    parts = []; advance = 0
    for ch in s:
        name = cmap[ord(ch)]
        pen = SVGPathPen(gs); gs[name].draw(pen)
        parts.append(f'<path transform="translate({advance},0)" d="{pen.getCommands()}"/>')
        advance += gs[name].width
    return f'<g fill="{color}" transform="translate({x},{y}) scale({scale}, {-scale})">'+''.join(parts)+'</g>'

def rect(x,y,w,h,fill,stroke="none",sw=0,r=0):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}"/>'

def svg(w,h,body):
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">{body}</svg>\n'

assets = {}
def asset(name,w,h,body,slice_=0,minimum=None):
    source = SRC / (name + '.svg'); source.parent.mkdir(parents=True,exist_ok=True)
    source.write_text(svg(w,h,body))
    dest = UI / (name+'.png'); dest.parent.mkdir(parents=True,exist_ok=True)
    cairosvg.svg2png(url=str(source),write_to=str(dest))
    assets[name] = dict(size=[w,h],slice=slice_,minimum=minimum or [w,h])
    if slice_:
        resource = f"""[gd_resource type="StyleBoxTexture" load_steps=2 format=3]

[ext_resource type="Texture2D" path="res://assets/ui/{name}.png" id="1"]

[resource]
texture = ExtResource("1")
texture_margin_left = {slice_}.0
texture_margin_top = {slice_}.0
texture_margin_right = {slice_}.0
texture_margin_bottom = {slice_}.0
content_margin_left = {slice_}.0
content_margin_top = {slice_}.0
content_margin_right = {slice_}.0
content_margin_bottom = {slice_}.0
"""
        dest.with_suffix('.tres').write_text(resource)

def panel(fill=WHITE,stroke=INK,accent=None):
    b=rect(2,4,124,120,INK,r=12)+rect(2,2,124,120,fill,stroke,2,12)
    b+=f'<path d="M18 7 H110" stroke="#FFFFFF" stroke-width="2"/>'
    if accent: b+=rect(12,18,4,92,accent,r=2)
    return b

asset('dialogue/panel',128,128,panel(),24,[320,128])
asset('dialogue/name_tag',128,64,rect(2,2,124,60,BLUE,INK,2,10),16,[128,48])
asset('dialogue/continue',32,32,'<path d="M7 9 L25 9 L16 23 Z" fill="'+BLUE+'"/>')
asset('duel/lp_player',128,128,panel(accent=CYAN),24,[240,112])
asset('duel/lp_opponent',128,128,panel(accent=ORANGE),24,[240,112])
asset('duel/prompt',128,128,panel(),24,[360,192])
asset('duel/phase_idle',96,64,rect(2,2,92,60,WHITE,INK,2,8),12,[80,48])
asset('duel/phase_active',96,64,rect(2,2,92,60,BLUE,INK,2,8)+rect(14,53,68,4,CYAN),12,[80,48])
asset('duel/phase_unavailable',96,64,rect(2,2,92,60,'#E0E5EB','#8793A3',2,8),12,[80,48])
for side,color in [('player',CYAN),('opponent',ORANGE)]:
    asset('duel/chain_'+side,96,64,rect(2,2,92,60,WHITE,INK,2,10)+rect(10,14,5,36,color,r=2),16,[112,56])
for state,fill,stroke in [('normal',WHITE,INK),('hover','#E0F2FA',BLUE),('pressed',BLUE,INK),('disabled','#E0E5EB','#8793A3')]:
    asset('duel/button_'+state,96,64,rect(2,4,92,58,INK,r=9)+rect(2,2,92,56,fill,stroke,2,9),16,[128,56])
asset('duel/focus',96,64,rect(2,2,92,60,'none',BLUE,4,11)+rect(6,6,84,52,'none',CYAN,2,8),12,[128,56])

# Glyph artwork includes no font files or branded controller symbols.
keys = {'e':'E','enter':'ENTER','escape':'ESC','backspace':'BACK','tab':'TAB','shift':'SHIFT','space':'SPACE','g':'G','b':'B','l':'L','w':'W','a':'A','s':'S','d':'D'}
for key,label in keys.items():
    w = 112 if len(label)>1 else 64
    f = fonts[700]; gs=f.getGlyphSet(); cmap=f.getBestCmap()
    size=20 if len(label)>1 else 28
    width=sum(gs[cmap[ord(c)]].width for c in label)*size/f['head'].unitsPerEm
    body=rect(3,6,w-6,55,INK,r=10)+rect(3,3,w-6,54,WHITE,INK,2,10)+text(label,(w-width)/2,40,size,INK,700)
    asset('glyphs/key_'+key,w,64,body)
for direction,angle in [('up',0),('right',90),('down',180),('left',270)]:
    body=rect(3,6,58,55,INK,r=10)+rect(3,3,58,54,WHITE,INK,2,10)
    body+=f'<path d="M32 17 L45 31 H37 V46 H27 V31 H19 Z" fill="{INK}" transform="rotate({angle} 32 32)"/>'
    asset('glyphs/key_'+direction,64,64,body)
for direction,pos in [('south',(32,47)),('east',(47,32)),('west',(17,32)),('north',(32,17))]:
    body=f'<circle cx="32" cy="32" r="29" fill="{WHITE}" stroke="{INK}" stroke-width="2"/>'
    for x,y in [(32,17),(47,32),(32,47),(17,32)]:
        body+=f'<circle cx="{x}" cy="{y}" r="7" fill="{INK if (x,y)==pos else "#CBD2DD"}"/>'
    asset('glyphs/pad_'+direction,64,64,body)
for key,label in [('lb','LB'),('rb','RB'),('start','MENU'),('back','VIEW')]:
    w=112 if len(label)>2 else 80
    width=sum(fonts[700].getGlyphSet()[fonts[700].getBestCmap()[ord(c)]].width for c in label)*20/fonts[700]['head'].unitsPerEm
    asset('glyphs/pad_'+key,w,64,rect(3,8,w-6,48,WHITE,INK,2,16)+text(label,(w-width)/2,40,20,INK,700))
asset('glyphs/key_blank',96,64,rect(3,6,90,55,INK,r=10)+rect(3,3,90,54,WHITE,INK,2,10),16,[64,48])
asset('glyphs/pad_stick',64,64,f'<circle cx="32" cy="32" r="29" fill="{WHITE}" stroke="{INK}" stroke-width="2"/><circle cx="32" cy="32" r="15" fill="{INK}"/><path d="M32 7 L27 13 H37 Z M32 57 L27 51 H37 Z M7 32 L13 27 V37 Z M57 32 L51 27 V37 Z" fill="{BLUE}"/>')
asset('glyphs/mouse_left',64,64,f'<rect x="14" y="3" width="36" height="58" rx="18" fill="{WHITE}" stroke="{INK}" stroke-width="2"/><path d="M31 5 Q16 5 16 22 V29 H31 Z" fill="{INK}"/><path d="M32 5 V29 H49" fill="none" stroke="{INK}" stroke-width="2"/>')
(UI/'manifest.json').write_text(json.dumps(assets,indent=2)+'\n')
for name,weight in [('regular',400),('bold',700)]:
    (UI/f'fonts/outfit_{name}.tres').write_text(f"""[gd_resource type="FontVariation" load_steps=2 format=3]

[ext_resource type="FontFile" path="res://assets/ui/fonts/local/Outfit.ttf" id="1"]

[resource]
base_font = ExtResource("1")
variation_opentype = {{2003265652: {weight}.0}}
""")

# The review exports and Godot fixture use the same positions and live text.
EVIDENCE.mkdir(parents=True,exist_ok=True)
(UI/'review').mkdir(exist_ok=True)
parts=[]; nodes=[]; resources={}; node_index=0
def resource(path,type_):
    if path not in resources: resources[path]=(str(len(resources)+1),type_)
    return resources[path][0]
def node(type_,x,y,w,h,extra):
    global node_index
    node_index+=1
    nodes.append(f'[node name="Item{node_index}" type="{type_}" parent="."]\noffset_left = {float(x)}\noffset_top = {float(y)}\noffset_right = {float(x+w)}\noffset_bottom = {float(y+h)}\nmouse_filter = 2\n'+extra+'\n')
def color(c):
    return 'Color('+', '.join(str(int(c[i:i+2],16)/255) for i in (1,3,5))+', 1)'
def block(x,y,w,h,c):
    parts.append(rect(x,y,w,h,c)); node('ColorRect',x,y,w,h,'color = '+color(c))
def label(s,x,y,size=28,c=INK,bold=False):
    parts.append(text(s,x,y+size,size,c,700 if bold else 400))
    fid=resource('fonts/outfit_'+('bold' if bold else 'regular')+'.tres','FontVariation')
    node('Label',x,y,0,size*1.4,'theme_override_colors/font_color = '+color(c)+f'\ntheme_override_fonts/font = ExtResource("{fid}")\ntheme_override_font_sizes/font_size = {size}\ntext = '+json.dumps(s, ensure_ascii=False))
def tile(name,x,y,w=None,h=None):
    a=assets[name]; sw,sh=a['size']; w=w or sw; h=h or sh
    # Raster export is sliced, not scaled; keeps corner radii and strokes intact.
    import base64,io
    from PIL import Image
    im=Image.open(UI/(name+'.png')); out=Image.new('RGBA',(w,h))
    m=a['slice']
    if m:
        sx=[0,m,sw-m,sw]; sy=[0,m,sh-m,sh]; dx=[0,m,w-m,w]; dy=[0,m,h-m,h]
        for i in range(3):
            for j in range(3):
                piece=im.crop((sx[i],sy[j],sx[i+1],sy[j+1]))
                piece=piece.resize((dx[i+1]-dx[i],dy[j+1]-dy[j]),Image.Resampling.LANCZOS)
                out.paste(piece,(dx[i],dy[j]))
    else: out=im.resize((w,h),Image.Resampling.LANCZOS)
    buf=io.BytesIO(); out.save(buf,format='PNG')
    parts.append(f'<image x="{x}" y="{y}" width="{w}" height="{h}" href="data:image/png;base64,{base64.b64encode(buf.getvalue()).decode()}"/>')
    rid=resource(name+'.png','Texture2D')
    node('NinePatchRect' if m else 'TextureRect',x,y,w,h,f'texture = ExtResource("{rid}")\n'+(f'patch_margin_left = {m}\npatch_margin_right = {m}\npatch_margin_top = {m}\npatch_margin_bottom = {m}' if m else 'expand_mode = 1\nstretch_mode = 0'))
def save(name):
    image=svg(1920,1080,''.join(parts))
    (EVIDENCE/(name+'.svg')).write_text(image)
    cairosvg.svg2png(bytestring=image.encode(),write_to=str(EVIDENCE/(name+'.png')))
    header=f'[gd_scene load_steps={len(resources)+1} format=3]\n\n'
    for path,(id_,type_) in resources.items(): header+=f'[ext_resource type="{type_}" path="res://assets/ui/{path}" id="{id_}"]\n'
    header+='\n[node name="InterfaceReview" type="Control"]\nlayout_mode = 3\nanchors_preset = 15\nanchor_right = 1.0\nanchor_bottom = 1.0\ngrow_horizontal = 2\ngrow_vertical = 2\n\n'
    (UI/'review'/(name+'.tscn')).write_text(header+'\n'.join(nodes))

block(0,0,1920,1080,'#DCE4EB')
block(0,0,1920,8,BLUE)
label('SOLOSRC  /  INTERFACE KIT',64,32,22,BLUE,True)
label('Clear decisions. Room for the duel.',64,68,42,INK,True)
label('01  /  COMPONENT REVIEW   •   1920 × 1080   •   #157',64,128,20,MUTED)
tile('duel/lp_player',64,196,340,128); label('PLAYER  /  LIFE POINTS',96,214,20,BLUE,True);label('8000',96,245,48,INK,True)
tile('duel/lp_opponent',436,196,340,128);label('NICO  /  LIFE POINTS',468,214,20,BLUE,True);label('2400',468,245,48,INK,True)
label('CHAIN  /  RESOLVES LAST TO FIRST',64,350,20,MUTED,True)
tile('duel/chain_player',64,386,200,64);label('1  •  PLAYER',89,401,22,INK,True)
tile('duel/chain_opponent',280,386,200,64);label('2  •  NICO',306,401,22,INK,True)
label('TURN 03  /  YOUR TURN',64,479,20,BLUE,True)
for i,s in enumerate(['DRAW','STANDBY','MAIN 1','BATTLE','MAIN 2','END']):
    state='active' if i==2 else 'idle'
    tile('duel/phase_'+state,64+i*120,518,112,64)
    label(s,77+i*120,538,18,WHITE if i==2 else INK,True)
label('DIALOGUE  /  28 PX BODY • 24 PX CONTENT INSET',64,622,20,MUTED,True)
tile('dialogue/panel',64,702,712,242);tile('dialogue/name_tag',88,674,176,56);label('NICO',112,684,24,WHITE,True)
label('A good deck is only the beginning.',96,752,28)
label('Ready to see what yours can do?',96,799,28)
tile('glyphs/key_e',552,871,40,40);label('Continue',604,876,22);tile('dialogue/continue',726,884,20,20)
tile('duel/prompt',864,196,560,424)
label('RESPONSE WINDOW',896,224,20,BLUE,True)
label('Activate a card?',896,264,36,INK,True)
label('Your opponent played a card.',896,323,26)
label('Choose a response or pass.',896,362,26)
tile('duel/button_normal',896,433,496,64);tile('duel/focus',896,433,496,64);label('Activate effect',928,447,26,INK,True)
tile('duel/button_normal',896,519,496,64);label('Pass',928,533,26,INK,True)
label('BUTTON STATES',864,665,20,MUTED,True)
for i,(state,s) in enumerate([('normal','Ready'),('hover','Hover'),('pressed','Pressed'),('disabled','Unavailable')]):
    x=864+(i%2)*288;y=708+(i//2)*100
    tile('duel/button_'+state,x,y,264,64);label(s,x+24,y+15,24,WHITE if state=='pressed' else (MUTED if state=='disabled' else INK),True)
label('Focus adds a double outline.',864,918,24)
label('State and side always have text cues.',864,952,24)
label('TYPE  /  OUTFIT',1496,202,20,BLUE,True)
label('Aa',1496,240,76,INK,True)
label('Regular 400',1496,342,26)
label('Bold 700',1496,385,26,INK,True)
label('0123456789',1496,445,32,INK,True)
label('I l 1 / O 0',1496,495,28)
label('ACCENTS',1496,578,20,MUTED,True)
for i,(c,s) in enumerate([(INK,'Ink'),(BLUE,'Cobalt'),(CYAN,'Player'),(ORANGE,'Opponent')]):
    block(1496,625+i*58,40,40,c);label(s,1556,628+i*58,26)
label('24–28 px body',1496,918,24)
label('48 px life totals',1496,952,24)
label('ART REVIEW • Static specimens; runtime placement and input remain with Fable.',64,1023,20,MUTED)
save('components')

parts.clear();nodes.clear();resources.clear();node_index=0
block(0,0,1920,1080,'#DCE4EB');block(0,0,1920,8,BLUE)
label('SOLOSRC  /  INPUT GLYPHS',64,40,22,BLUE,True)
label('One visual language for every action.',64,80,42,INK,True)
label('Positional face buttons avoid platform lettering assumptions. Pair every glyph with an action label.',64,146,26)
for i,(action,key,pad) in enumerate([('Interact / confirm','e','south'),('Cancel / back','escape','east'),('Advance phase','space','north'),('Graveyard','g','lb'),('Banished','b','rb'),('Duel log','l','back'),('Menu','tab','start'),('Move','w','stick')]):
    x=64+(i%2)*896;y=238+(i//2)*142
    tile('dialogue/panel',x,y,840,112)
    label(action,x+24,y+32,28,INK,True)
    k='glyphs/key_'+key;p='glyphs/pad_'+pad
    tile(k,x+444,y+24,assets[k]['size'][0],64);tile(p,x+644,y+24,assets[p]['size'][0],64)
label('ALTERNATE KEYS / MOVEMENT / POINTER',64,835,20,MUTED,True)
x=64
for key in ['enter','backspace','shift','a','s','d','up','down','left','right']:
    name='glyphs/key_'+key;w=assets[name]['size'][0];tile(name,x,883,w,64);x+=w+18
tile('glyphs/mouse_left',x,883,64,64);tile('glyphs/pad_west',x+82,883,64,64)
label('Use glyphs at 40–64 px. For remapped or unknown inputs, use a keycap with the live binding name.',64,986,24)
label('ART REVIEW • Mapping reflects project.godot on the #157 base commit; glyph selection is a runtime responsibility.',64,1030,20,MUTED)
save('glyphs')
print(f'Exported {len(assets)} assets, StyleBox resources, font weights and two review scenes.')
