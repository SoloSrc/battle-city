"""SVG viewport assembly of director-provided frames; runtime art/stat/icon slots cleared."""
import base64

def write_frames(src,save,layer):
 for name in ['normal','effect','fusion','ritual','spell','trap']:
  data=base64.b64encode((src/'references'/(name+'.jpg')).read_bytes()).decode()
  # Reference layer is clipped away from runtime artwork. Other viewports replace
  # variable text/badges with blank background strips from the same supplied frame.
  defs='<defs><image id="reference" width="590" height="860" preserveAspectRatio="none" href="data:image/jpeg;base64,'+data+'"/><clipPath id="frame"><path clip-rule="evenodd" d="M0 0 H590 V860 H0 Z M14 14 V630 H576 V14 Z"/></clipPath></defs>'
  body=defs+'<g clip-path="url(#frame)"><use href="#reference"/></g>'
  def patch(x,y,w,h,sx,sy,sw,sh):
   return f'<svg x="{x}" y="{y}" width="{w}" height="{h}" viewBox="{sx} {sy} {sw} {sh}" preserveAspectRatio="none"><use href="#reference"/></svg>'
  if name in ['normal','effect','fusion']:
   body+=patch(14,655,562,76,20,655,75,76)
   for x in [54,326]:body+=patch(x,746,208,72,x,746,208,8)
  elif name=='ritual':
   body+=patch(14,655,562,74,20,655,75,74)
   for x in [53,321]:body+=patch(x,745,216,71,x,742,216,6)
  else:
   # The central type badge is supplied dynamically; sample an empty part of its plate.
   body+=patch(247,706,96,91,126,706,96,91)
  save('frame_'+name,590,860,layer('reference-frame-and-cleared-slots',body))
