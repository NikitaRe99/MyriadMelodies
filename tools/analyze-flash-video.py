"""Decode the supplied recording locally; no screen input or game interaction."""
import sys, json
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'artifacts/video-tools'))
import av
import numpy as np
from PIL import Image, ImageDraw

source = sys.argv[1]
ode = '1229-54' in source
out = Path('artifacts/flash-analysis/ode' if ode else 'artifacts/flash-analysis')
out.mkdir(parents=True, exist_ok=True)
container = av.open(source)
stream = container.streams.video[0]
print(json.dumps({'width':stream.width,'height':stream.height,'rate':str(stream.average_rate),'duration':float(stream.duration*stream.time_base) if stream.duration else None}))
tiles=[]
next_time=0
for i,frame in enumerate(container.decode(stream)):
    t=float(frame.time)
    if t < next_time: continue
    pic=frame.to_image()
    pic.save(out / f'frame-{i:05}.png')
    pic.thumbnail((480,270))
    tile=Image.new('RGB',(480,294),'#202020');tile.paste(pic,(0,24))
    ImageDraw.Draw(tile).text((8,6),f'{i}: {t:.3f}s',fill='white')
    tiles.append(tile)
    next_time=t+1
sheet=Image.new('RGB',(480*4,294*((len(tiles)+3)//4)))
for n,tile in enumerate(tiles):sheet.paste(tile,((n%4)*480,(n//4)*294))
sheet.save(out/'contact.png')

# Cropped recording: centers measured from the six idle targets, not screen edges.
centers=[(78,42),(340,162),(626,230),(1088,230),(1375,162),(1636,42)] if ode else [(150,126),(412,245),(698,313),(1158,313),(1445,245),(1708,126)]
side=44
yy,xx=np.mgrid[:side,:side]
mask=(xx-side/2)**2+(yy-side/2)**2 <= (side/2)**2
rows=[]; purple=[]
for i,frame in enumerate(av.open(source).decode(video=0)):
    arr=frame.to_ndarray(format='rgb24')
    values=[]
    for lane,(x,y) in enumerate(centers):
        region=arr[y-side//2:y+side//2,x-side//2:x+side//2]
        rgb=np.floor(region[mask].mean(axis=0)).astype(int).tolist()
        values.append(rgb)
        if (rgb[2]>220 and rgb[0]>140 and rgb[0]-rgb[1]>15) or (ode and rgb[0]>235 and rgb[1]>205 and rgb[1]-rgb[2]>12):
            tile=Image.fromarray(arr).crop((x-85,y-85,x+85,y+85))
            if i%2==0:
                patch=Image.new('RGB',(190,210),'#202020');patch.paste(tile,(10,30))
                ImageDraw.Draw(patch).text((6,4),f'{i/30:.3f}s {"ASDJKL"[lane]}\n{rgb}',fill='white')
                purple.append(patch)
    rows.append({'frame':i,'time':float(frame.time),'rgb':values})
(out/'samples.json').write_text(json.dumps(rows,indent=2))
if purple:
    sheet=Image.new('RGB',(190*8,210*((len(purple)+7)//8)))
    for n,tile in enumerate(purple):sheet.paste(tile,((n%8)*190,(n//8)*210))
    sheet.save(out/'purple-phases.png')
print('purple tiles',len(purple))
