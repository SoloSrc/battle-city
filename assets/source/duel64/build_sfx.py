"""SOLOSRC original synthesized placeholder cues, MIT; no external samples."""
from pathlib import Path
import math, random, wave, json, struct
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'assets/audio/sfx';OUT.mkdir(parents=True,exist_ok=True)
RATE=48000
TAU=math.tau
DURATIONS={'draw':.25,'summon':.75,'set':.20,'attack':.48,'hit':.35,'activate':.65,'win':3.0,'lose':2.5}
def tone(t,f):return math.sin(TAU*f*t)
def bell(t,f):return (tone(t,f)+.25*tone(t,f*2.01)+.12*tone(t,f*3.99))*math.exp(-5*t)
def cue(name,d):
 rng=random.Random(6400+list(DURATIONS).index(name));smooth=0;values=[]
 for n in range(round(d*RATE)):
  t=n/RATE;u=t/d;noise=rng.uniform(-1,1);smooth=.88*smooth+.12*noise
  if name=='draw':v=(noise-smooth)*.6*math.sin(math.pi*u)**2+.12*tone(t,1000+1800*u)*math.sin(math.pi*u)**3
  elif name=='set':v=.65*tone(t,140-45*u)*math.exp(-25*t)+.2*smooth*math.exp(-45*t)
  elif name=='attack':v=(noise-smooth)*math.sin(math.pi*u)**1.4*.55+.20*math.sin(TAU*(220*t+900*t*t))*math.sin(math.pi*u)
  elif name=='hit':v=.65*tone(t,75)*math.exp(-15*t)+.7*smooth*math.exp(-25*t)+.13*noise*math.exp(-50*t)
  elif name=='summon':v=.4*bell(t,440)+.3*bell(max(0,t-.12),660)*(t>=.12)+.28*bell(max(0,t-.24),880)*(t>=.24)
  elif name=='activate':v=.45*bell(t,587.33)+.35*bell(max(0,t-.10),880)*(t>=.10)+.08*noise*math.exp(-30*t)
  else:
   notes=[392,493.88,587.33,783.99] if name=='win' else [392,349.23,293.66,196]
   v=0
   for j,f in enumerate(notes):
    dt=t-j*.18
    if dt>=0:v+=.35*(tone(dt,f)+.2*tone(dt,f*2))*math.exp(-2.5*dt)
  # Short onset/release ramps prevent a cut edge; no clipping or DC step.
  ramp=min(1,t/.006)*min(1,(d-t)/.025)
  values.append(v*ramp)
 envelope=[min(1,n/RATE/.006)*min(1,(len(values)-1-n)/RATE/.025) for n in range(len(values))]
 dc=sum(values)/sum(envelope);values=[v-dc*e for v,e in zip(values,envelope)]
 values[0]=values[-1]=0.0
 peak=max(map(abs,values));values=[v/peak*10**(-9/20) for v in values]
 return values
manifest=[];preview=[]
for name,d in DURATIONS.items():
 values=cue(name,d);file=OUT/('duel_'+name+'.wav')
 pcm=b''.join(int(v*8388607).to_bytes(3,'little',signed=True) for v in values)
 with wave.open(str(file),'wb') as w:w.setparams((1,3,RATE,0,'NONE','not compressed'));w.writeframes(pcm)
 manifest.append({'cue':name,'path':str(file.relative_to(ROOT)),'seconds':d,'sample_rate':RATE,'bits':24,'channels':1,'peak_dbfs':-9,'rms_dbfs':round(20*math.log10(math.sqrt(sum(v*v for v in values)/len(values))),2),'license':'MIT','origin':'original deterministic synthesis; no samples'})
 preview+=values+[0.0]*int(RATE*.55)
previewpath=ROOT/'docs/art/previews/duel64';previewpath.mkdir(parents=True,exist_ok=True)
with wave.open(str(previewpath/'sfx-review.wav'),'wb') as w:w.setparams((1,2,RATE,0,'NONE','not compressed'));w.writeframes(b''.join(struct.pack('<h',round(v*32767)) for v in preview))
(ROOT/'assets/source/duel64/sfx-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
print('SFX PASS: 8 original cues; 48kHz/24-bit/mono, normalized to -9 dBFS peak; review order '+', '.join(DURATIONS))
