"""Compose factual reference/render evidence; requires Pillow. No image synthesis."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'docs/requests/evidence/character99'
ref = Image.open(ROOT / 'assets/source/benchmark-inputs/character-a.png').convert('RGB')
fontpath = '/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'
font = ImageFont.truetype(fontpath, 22)
small = ImageFont.truetype(fontpath, 17)
canvas = Image.new('RGB', (2400, 920), '#202833')
draw = ImageDraw.Draw(canvas)
draw.text((24,16), 'CHARACTER A / #99 BASE REVIEW — reference beside Blender render', font=font, fill='#f1eee5')
views = [('front',(50,20,495,880)), ('side',(540,20,775,880)), ('three-quarter',(1235,20,1635,880))]
for index,(view,box) in enumerate(views):
    x = index*800
    # One uniform scale for the entire reference sheet; per-view horizontal centering only.
    reference = ref.crop(box)
    reference = reference.resize((round(reference.width*.85),round(reference.height*.85)),Image.LANCZOS)
    canvas.paste(reference,(x+(400-reference.width)//2,72))
    # Fixed crop and scale for every full-body render; sole / crown alignment, no silhouette warping.
    render = Image.open(OUT / (view+'.png')).convert('RGB').crop((145,20,655,980))
    render = render.resize((392,738),Image.LANCZOS)
    canvas.paste(render,(x+404,72))
    draw.text((x+18,820),view.upper()+' / SHEET',font=small,fill='#f1eee5')
    draw.text((x+418,820),'BLENDER / BASE',font=small,fill='#f1eee5')
draw.text((24,866),'1.70 m • unrigged A-pose • neutral materials • #100 retopology and #101 textures follow approval',font=small,fill='#c2cedb')
draw.text((24,891),'Sheet views are illustrative, not calibrated orthographics. Side reference has relaxed arms; model retains its A-pose.',font=small,fill='#c2cedb')
canvas.save(OUT / 'comparison.png')
print(OUT / 'comparison.png')
# Body-only inspection and close-ups are the primary evidence for the revision.
for filename,views in [('body-review',['body-front','body-side','body-back','body-three-quarter']),
                       ('anatomy-details',['hand-palm','hand-back','pelvis-back','pelvis-side'])]:
    panel_width=600
    sheet=Image.new('RGB',(panel_width*len(views),860),'#202833')
    draw=ImageDraw.Draw(sheet)
    title='COMPLETE BODY / removable outfit hidden' if filename=='body-review' else 'ANATOMY DETAIL / palm, back of hand, pelvis'
    draw.text((20,14),title,font=font,fill='#f1eee5')
    for i,view in enumerate(views):
        pic=Image.open(OUT/(view+'.png')).convert('RGB')
        pic.thumbnail((600,740),Image.LANCZOS)
        sheet.paste(pic,(i*600+(600-pic.width)//2,60+(740-pic.height)//2))
        draw.text((i*600+20,810),view.upper(),font=small,fill='#f1eee5')
    sheet.save(OUT/(filename+'.png'))
