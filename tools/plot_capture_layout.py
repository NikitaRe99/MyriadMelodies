"""Diagnostic plate: real C# capture regions over the supplied reference frames."""
import argparse
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

parser = argparse.ArgumentParser()
parser.add_argument('screenshots', type=Path)
args = parser.parse_args()
records = json.loads(Path('artifacts/capture-layout/validation.json').read_text(encoding='utf-8'))
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 19)
panels = []
for record in records:
    im = Image.open(args.screenshots / f"Снимок экрана ({record['number']}).png").convert('RGB')
    draw = ImageDraw.Draw(im)
    for key, area in zip('ASDJKL', record['regions']):
        x, y, w, h = (area[k] for k in ('X', 'Y', 'Width', 'Height'))
        draw.rectangle((x, y, x+w-1, y+h-1), outline='#ff5798', width=max(2, im.height//480))
    crop = im.crop((0, int(im.height*.61), im.width, int(im.height*.92)))
    crop.thumbnail((720, 235), Image.Resampling.LANCZOS)
    panel = Image.new('RGB', (740, 280), '#171c2b')
    panel.paste(crop, ((740-crop.width)//2, 42))
    title = f"{record['width']} × {record['height']} | {'Spread' if record['spread'] else 'Vertical'} | error ≤ {record['maxError']:.2f} px"
    ImageDraw.Draw(panel).text((12, 8), title, font=font, fill='#eef1fa')
    panels.append(panel)
plate = Image.new('RGB', (1480, 1400), '#171c2b')
for i, panel in enumerate(panels):
    plate.paste(panel, ((i%2)*740, (i//2)*280))
plate.save('artifacts/capture-layout/comparison.png')
