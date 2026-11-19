from pathlib import Path
import numpy as np
import wave
root=Path(__file__).parent
sr=44100
rng=np.random.default_rng(32)
def env(t,d): return np.minimum(t/.003,1)*np.minimum((d-t)/.018,1)
def write(name,x):
 x=x-np.mean(x);x*=.4/max(np.max(np.abs(x)),1e-8)
 x[:128]*=np.linspace(0,1,128);x[-512:]*=np.linspace(1,0,512)
 assert np.isfinite(x).all() and np.max(np.abs(x))<.5
 with wave.open(str(root/name),'wb') as f:
  f.setparams((1,2,sr,0,'NONE','not compressed'));f.writeframes((x*32767).astype('<i2').tobytes())
 print(name,len(x)/sr,'seconds')
# Two clear warning pulses, rising in pitch.
x=np.zeros(int(sr*.32))
for start,freq in [(0,784),(.14,988)]:
 d=.12;t=np.arange(int(sr*d))/sr
 y=(np.sin(2*np.pi*freq*t)+.22*np.sin(6*np.pi*freq*t))*env(t,d)*np.exp(-8*t)
 i=int(sr*start);x[i:i+len(y)]+=y
write('Spear_Warning.wav',x)
# Sweeping chiptune hiss with a short descending tonal edge.
d=.16;t=np.arange(int(sr*d))/sr
noise=rng.normal(size=len(t));noise=np.convolve(noise,np.ones(5)/5,mode='same')
phase=2*np.pi*(1700*t-1300*t*t/(2*d))
x=(.65*noise+.15*np.sin(phase))*env(t,d)*np.exp(-12*t)
write('Spear_Release.wav',x)
# Metallic strike with inharmonic ringing partials and a low attack body.
d=.32;t=np.arange(int(sr*d))/sr
x=.8*np.sin(2*np.pi*120*t)*np.exp(-35*t)
for freq,gain,decay in [(850,.35,18),(1379,.23,22),(2213,.14,28)]:
 x+=gain*np.sin(2*np.pi*freq*t)*np.exp(-decay*t)
x+=rng.normal(size=len(t))*.16*np.exp(-70*t)
write('Spear_Impact.wav',x*env(t,d))
