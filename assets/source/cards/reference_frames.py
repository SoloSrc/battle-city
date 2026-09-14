"""Generated cloud source with deterministic original bevels and gold plates."""
import base64

def bevel(x,y,w,h,b,colors):
    points=[[(x,y),(x+w,y),(x+w-b,y+b),(x+b,y+b)],[(x+w,y),(x+w,y+h),(x+w-b,y+h-b),(x+w-b,y+b)],[(x,y+h),(x+b,y+h-b),(x+w-b,y+h-b),(x+w,y+h)],[(x,y),(x+b,y+b),(x+b,y+h-b),(x,y+h)]]
    return ''.join('<polygon points="'+ ' '.join(f'{a},{b}' for a,b in p)+'" fill="'+c+'"/>' for p,c in zip(points,colors))

def plate(x,y,w,h,opacity):
    body=f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="#fff5d7" fill-opacity="{opacity}" stroke="#652b1f" stroke-width="7"/>'
    body+=f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="none" stroke="url(#gold)" stroke-width="4"/>'
    for cx,cy in [(x,y),(x+w,y),(x,y+h),(x+w,y+h)]:
        body+=f'<rect x="{cx-6}" y="{cy-6}" width="12" height="12" fill="url(#gold)" stroke="#783321" stroke-width="2"/><path d="M{cx-3},{cy+3}v-6h6" fill="none" stroke="#efb17d" stroke-width="2"/>'
    return body

def write_frames(src,save,layer):
    palettes={'normal':('#9e8023','#deca72'),'effect':('#af682e','#e1ad72'),'fusion':('#633da3','#b99bd4'),'ritual':('#354c9b','#99aed6'),'spell':('#258457','#9ac7a1'),'trap':('#a14276','#d6a0b8')}
    cloud=base64.b64encode((src/'cloud-mottle.png').read_bytes()).decode()
    for name,(dark,light) in palettes.items():
        lo=[int(dark[i:i+2],16)/255 for i in (1,3,5)];hi=[int(light[i:i+2],16)/255 for i in (1,3,5)]
        funcs=''.join(f'<feFunc{c} type="linear" slope="{(b-a)*2.6}" intercept="{a-(b-a)*.8}"/>' for c,a,b in zip('RGB',lo,hi))
        defs='<defs><filter id="tint" color-interpolation-filters="sRGB"><feComponentTransfer>'+funcs+'</feComponentTransfer></filter><clipPath id="panel"><rect y="630" width="590" height="230"/></clipPath><linearGradient id="gold" x2="0" y2="1"><stop stop-color="#e9a36b"/><stop offset=".35" stop-color="#c66a2d"/><stop offset=".6" stop-color="#943a22"/><stop offset="1" stop-color="#d98143"/></linearGradient></defs>'
        body=defs+f'<rect y="630" width="590" height="230" fill="{dark}"/><g clip-path="url(#panel)"><image y="460" width="590" height="590" filter="url(#tint)" href="data:image/png;base64,{cloud}"/></g>'
        body+=bevel(0,0,590,630,10,['#b9b9b9','#777777','#656565','#939393'])
        body+=bevel(0,630,590,230,10,[light,dark,dark,light])
        plates=[(34,738,244,94),(312,738,244,94)] if name not in ['spell','trap'] else [(34,693,522,104)]
        body+=''.join(plate(*p, .48 if name in ['spell','trap'] else .35) for p in plates)
        save('frame_'+name,590,860,layer('muted-clouds-thin-bevel-and-translucent-gold-plates',body))
