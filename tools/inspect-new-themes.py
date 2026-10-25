import sys,json,argparse
from pathlib import Path
sys.stdout.reconfigure(encoding='utf-8')
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'artifacts/video-tools'))
import av
import numpy as np
from PIL import Image,ImageDraw

parser=argparse.ArgumentParser(description='Measure flash colors from two local game recordings.')
parser.add_argument('recordings',type=Path,help='Folder containing the two MP4 recordings')
parser.add_argument('--palettes',type=Path,help='Optional explicit palette JSON to archive with measurements')
args=parser.parse_args()
out=Path('artifacts/new-themes');out.mkdir(parents=True,exist_ok=True)
if args.palettes:
    palettes=json.loads(args.palettes.read_text(encoding='utf-8-sig'))
    (out/'debug-palettes.json').write_text(json.dumps(palettes,ensure_ascii=False,indent=2),encoding='utf-8')
for stem in ['мудрость морозной сакуры','оракул разума']:
    root=out/stem;root.mkdir(exist_ok=True)
    source=args.recordings/(stem+'.mp4')
    tiles=[];next_time=0
    for i,frame in enumerate(av.open(str(source)).decode(video=0)):
        if frame.time<next_time:continue
        pic=frame.to_image();pic.save(root/f'frame-{i:04}.png')
        pic.thumbnail((500,180));tile=Image.new('RGB',(500,204),'#202020');tile.paste(pic,(0,24))
        ImageDraw.Draw(tile).text((8,6),f'{frame.time:.3f} s, frame {i}',fill='white');tiles.append(tile)
        next_time=frame.time+.5
    sheet=Image.new('RGB',(2000,204*((len(tiles)+3)//4)))
    for n,tile in enumerate(tiles):sheet.paste(tile,((n%4)*500,(n//4)*204))
    sheet.save(root/'contact.png')
    centers=[(59,50),(255,139),(470,191),(816,191),(1031,139),(1227,50)] if 'сакуры' in stem else [(71,59),(268,149),(483,199),(829,199),(1044,149),(1240,59)]
    side=33;yy,xx=np.mgrid[:side,:side];mask=(xx-side/2)**2+(yy-side/2)**2 <= (side/2)**2
    rows=[];patches=[]
    for i,frame in enumerate(av.open(str(source)).decode(video=0)):
        arr=frame.to_ndarray(format='rgb24');values=[]
        for lane,(x,y) in enumerate(centers):
            left,top=round(x-side/2),round(y-side/2)
            rgb=np.floor(arr[top:top+side,left:left+side][mask].mean(axis=0)).astype(int).tolist();values.append(rgb)
            if i%3==0 and lane in [1,2,3,4] and (('сакуры' in stem and frame.time<2) or ('разума' in stem and 9.6<frame.time<10.8)):
                tile=Image.new('RGB',(190,210),'#202020');tile.paste(Image.fromarray(arr).crop((x-80,y-80,x+80,y+80)),(15,40))
                ImageDraw.Draw(tile).text((6,5),f'{frame.time:.3f} {"ASDJKL"[lane]}\n{rgb}',fill='white');patches.append(tile)
        rows.append({'time':frame.time,'rgb':values})
    (root/'samples.json').write_text(json.dumps(rows))
    sheet=Image.new('RGB',(190*8,210*((len(patches)+7)//8)))
    for n,tile in enumerate(patches):sheet.paste(tile,((n%8)*190,(n//8)*210))
    sheet.save(root/'phases.png')
