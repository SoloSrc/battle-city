"""One continuous cloud field per frame, with approved isolated trim overlays."""
import base64

def write_frames(src,save,layer):
 palettes={'normal':('#ad9015','#fff394'),'effect':('#c97905','#ffd681'),'fusion':('#683280','#d4a6e8'),'ritual':('#22288f','#8392f0'),'spell':('#037e40','#a1d19a'),'trap':('#862361','#e0a2d0')}
 for name,(base,light) in palettes.items():
  # Identical noise coordinates/seed for every frame: no local repair rectangles.
  defs='<defs><filter id="cloud" x="0" y="0" width="100%" height="100%"><feTurbulence type="fractalNoise" baseFrequency=".013 .024" numOctaves="3" seed="19"/><feColorMatrix type="matrix" values="0 0 0 0 1 0 0 0 0 1 0 0 0 0 1 1.8 0 0 0 -.45"/><feGaussianBlur stdDeviation="2.8"/></filter></defs>'
  body=defs+f'<rect y="645" width="590" height="215" fill="{base}"/><rect x="14" y="655" width="562" height="195" fill="{light}" opacity=".15"/><rect x="14" y="655" width="562" height="195" filter="url(#cloud)" opacity=".40"/>'
  plates=[(54,743,210,78),(326,743,210,78)] if name in ['normal','effect','fusion'] else [(52,740,214,79),(321,740,214,79)] if name=='ritual' else [(53,698,485,100)]
  for x,y,w,h in plates:body+=f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{light}" opacity=".72"/>'
  trim=base64.b64encode((src/'trim'/(name+'.png')).read_bytes()).decode()
  body+='<image width="590" height="860" href="data:image/png;base64,'+trim+'"/>'
  save('frame_'+name,590,860,layer('continuous-cloud-field-and-approved-trim',body))
