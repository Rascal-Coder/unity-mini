import math,random,wave,struct
from pathlib import Path
out=Path('Assets/Resources/Battle/Audio'); out.mkdir(parents=True,exist_ok=True)
rate=44100
rng=random.Random(614)
def write(name,duration,fn):
 samples=[]
 for i in range(int(duration*rate)):
  t=i/rate; v=fn(t)*min(1,t/.003)*min(1,(duration-t)/.02)
  samples.append(int(max(-.9,min(.9,v))*32767))
 with wave.open(str(out/(name+'.wav')),'wb') as f:
  f.setnchannels(1); f.setsampwidth(2); f.setframerate(rate); f.writeframes(struct.pack('<'+'h'*len(samples),*samples))
def hit(t,start,f):
 d=t-start
 return 0 if d<0 else math.exp(-d*65)*(math.sin(2*math.pi*f*d)*.35+(rng.random()*2-1)*.16)
write('dice-roll',.65,lambda t:sum(hit(t,s,650+n*135) for n,s in enumerate([0,.07,.15,.24,.35,.48])))
write('dice-lock',.13,lambda t:math.sin(2*math.pi*1200*t)*math.exp(-t*45)*.32)
write('sword-swing',.32,lambda t:(rng.random()*2-1)*math.sin(math.pi*t/.32)**2*.44+math.sin(2*math.pi*(290*t-240*t*t))*.08)
write('wood-impact',.34,lambda t:math.exp(-t*17)*(.4*math.sin(2*math.pi*155*t)+.21*math.sin(2*math.pi*337*t)+(rng.random()*2-1)*.25*math.exp(-t*70)))
write('bow-release',.32,lambda t:math.sin(2*math.pi*(820*t-700*t*t))*math.exp(-t*18)*.38+(rng.random()*2-1)*.13*math.exp(-t*12))
write('arcane-cast',.62,lambda t:(math.sin(2*math.pi*(360*t+550*t*t))+.4*math.sin(2*math.pi*1080*t))*.23*math.sin(math.pi*t/.62))
write('victory',1.1,lambda t:sum(0 if t<s else math.sin(2*math.pi*f*(t-s))*math.exp(-(t-s)*4)*.22 for s,f in [(0,523.25),(.15,659.25),(.3,783.99),(.48,1046.5)]))
write('ui-click',.09,lambda t:math.sin(2*math.pi*680*t)*math.exp(-t*65)*.25)
print('8 synthesized WAV clips generated (44.1 kHz / mono / PCM16).')
