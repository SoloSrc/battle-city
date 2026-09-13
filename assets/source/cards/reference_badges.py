"""Reference badge viewports plus original matching tooltip symbols."""
import base64, math

def write_badges(src,save,layer):
 def uri(name):return 'data:image/png;base64,'+base64.b64encode((src/'references'/name).read_bytes()).decode()
 sheet=uri('attributes.png')
 for name,col,row in [('wind',0,0),('water',1,0),('earth',2,0),('fire',0,1),('dark',1,1),('light',2,1),('divine',0,2),('spell',1,2),('trap',2,2)]:
  prefix='st_' if name in ['spell','trap'] else 'attr_'
  body=f'<defs><clipPath id="disc"><circle cx="64" cy="64" r="63"/></clipPath></defs><g clip-path="url(#disc)"><svg width="128" height="128" viewBox="{col*200} {row*200} 200 200"><image width="600" height="600" href="{sheet}"/></svg></g>'
  save(prefix+name,128,128,layer('director-reference-badge',body))
 save('star',64,64,'<defs><clipPath id="disc"><circle cx="32" cy="32" r="31.8"/></clipPath></defs><g clip-path="url(#disc)"><svg width="64" height="64" viewBox="0 0 49 49"><image width="633" height="569" href="'+uri('levels.png')+'"/></svg></g>')
 symbols={
 'equip':'M40 26 L24 40 L34 56 L43 50 V100 H85 V50 L94 56 L104 40 L88 26 L78 40 H50 Z',
 'continuous':'M64 64 C31 20 12 48 24 72 C40 98 64 64 64 64 C97 20 116 48 104 72 C88 98 64 64 64 64',
 'quick_play':'M74 23 L36 68 H61 L53 105 L96 56 H70 Z',
 'counter':'M28 79 Q28 43 69 43 H95 M78 26 L96 43 L78 60 M42 92 H90',
 'ritual':'M38 31 H90 V48 Q90 72 64 76 Q38 72 38 48 Z M64 76 V99 M43 101 H85 M49 19 H79',
 'field':'M64 27 L103 49 L64 72 L25 49 Z M25 66 L64 89 L103 66 M25 83 L64 106 L103 83'}
 for name,shape in symbols.items():
  bg='<defs><radialGradient id="orb" cx="30%" cy="24%" r="85%"><stop stop-color="#dcb968"/><stop offset=".3" stop-color="#8e6638"/><stop offset=".7" stop-color="#30271f"/><stop offset="1" stop-color="#100e12"/></radialGradient><linearGradient id="edge"><stop stop-color="#fffac8"/><stop offset=".5" stop-color="#a4874f"/><stop offset="1" stop-color="#ffeda1"/></linearGradient></defs><circle cx="64" cy="64" r="60" fill="url(#orb)" stroke="url(#edge)" stroke-width="4"/><path d="M23 42 Q36 13 68 16" fill="none" stroke="#fff9d2" opacity=".65" stroke-width="4"/>'
  fill='#fffbea' if name in ['equip','quick_play'] else 'none'
  bg+=f'<path d="{shape}" transform="translate(1 2)" fill="{fill}" stroke="#28150a" stroke-width="9" stroke-linejoin="round"/><path d="{shape}" fill="{fill}" stroke="#fffbea" stroke-width="6" stroke-linejoin="round" stroke-linecap="round"/>'
  save('st_'+name,128,128,layer('tooltip-medallion',bg))
