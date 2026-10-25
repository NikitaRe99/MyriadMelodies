from PIL import Image, ImageChops
from pathlib import Path
import argparse
import json
parser = argparse.ArgumentParser(description='Measure the white judgement markers in screenshots 109–118.')
parser.add_argument('screenshots', type=Path)
args = parser.parse_args()
root = args.screenshots
results = []
for number in range(109,119):
    path = root / f'Снимок экрана ({number}).png'
    im = Image.open(path).convert('RGB')
    w,h = im.size
    top = int(h*.55)
    crop = im.crop((0,top,w,int(h*.92)))
    channels = crop.split()
    mask = ImageChops.darker(channels[0].point(lambda x: 255 if x > 218 else 0), ImageChops.darker(channels[1].point(lambda x: 255 if x > 225 else 0),channels[2].point(lambda x: 255 if x > 225 else 0)))
    cw,ch=mask.size
    data=bytearray(mask.tobytes())
    components=[]
    for p in range(len(data)):
        if not data[p]: continue
        data[p]=0
        queue=[p]; xs=[]; ys=[]
        while queue:
            v=queue.pop(); y,x=divmod(v,cw); xs.append(x); ys.append(y)
            for n in (v-1 if x else -1,v+1 if x+1<cw else -1,v-cw if y else -1,v+cw if y+1<ch else -1):
                if n>=0 and data[n]: data[n]=0; queue.append(n)
        bw=max(xs)-min(xs)+1; bh=max(ys)-min(ys)+1
        if .009*h < bw < .05*h and .009*h < bh < .05*h and .8<bw/bh<1.25:
            components.append({'x':round(sum(xs)/len(xs),2),'y':round(top+sum(ys)/len(ys),2),'width':bw,'height':bh,'fill':round(len(xs)/(bw*bh),3)})
    results.append({'number':number,'width':w,'height':h,'components':components})
print(json.dumps(results,ensure_ascii=False,indent=2))
Path('artifacts/capture-layout/measurements.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
