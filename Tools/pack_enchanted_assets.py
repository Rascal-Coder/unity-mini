"""Normalize approved generated layers into Unity Multiple sprite sheets.
Keeps source images outside Assets; writes exact slice rectangles and size report.
"""
from pathlib import Path
import json
from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path(r'C:\Users\aaa\.codex\generated_images\01a12eef-cdd0-7c83-ad7e-3dd6380a686b')
OUT = ROOT / 'Assets/Resources/Battle/Art/Enchanted'
OUT.mkdir(parents=True, exist_ok=True)
SOURCES = {
    'Background': 'exec-d0420b82-2b93-4cc1-9557-696bc5a5a271.png',
    'Vegetation': 'exec-6c1486ee-ccdb-45ff-9ec2-ceb60a382cbb.png',
    'Tileset': 'exec-02b293df-dad1-4bc0-83d7-1e977fa2f3d9.png',
}
report = []

def clean(im):
    im = im.convert('RGBA')
    # Remove almost invisible generated alpha haze, preserve edge antialiasing.
    im.putalpha(im.getchannel('A').point(lambda a: 0 if a < 12 else min(255, round((a-12)*255/240))))
    return im

def trim(im):
    box = im.getchannel('A').getbbox()
    return im.crop(box) if box else im

def save_sheet(name, sprites, columns, cell):
    cw, ch = cell
    rows = (len(sprites) + columns - 1) // columns
    sheet = Image.new('RGBA', (cw*columns, ch*rows))
    slices = []
    for i, (label, im) in enumerate(sprites):
        im = trim(clean(im))
        im.thumbnail((cw-16, ch-16), Image.Resampling.LANCZOS)
        x, y = (i%columns)*cw+8, (i//columns)*ch+8
        sheet.paste(im, (x,y))
        slices.append(dict(name=label, x=x, y=sheet.height-y-im.height,
                           width=im.width, height=im.height, pivotX=.5, pivotY=0))
    # Full RGBA PNG + lossless DEFLATE: no palette reduction that damages outlines.
    path = OUT/(name+'.png')
    sheet.save(path, optimize=True, compress_level=9)
    (OUT/(name+'.slices.json')).write_text(json.dumps({'sprites': slices}, indent=2), encoding='utf-8')
    report.append(dict(file=path.name, dimensions=list(sheet.size), bytes=path.stat().st_size,
                       sprites=len(slices), compression='lossless RGBA PNG'))

bg=Image.open(SOURCE/SOURCES['Background']).convert('RGB').resize((1920,640),Image.Resampling.LANCZOS)
bg.save(OUT/'Background.jpg', quality=90, subsampling=0, optimize=True)
report.append(dict(file='Background.jpg',dimensions=[1920,640],bytes=(OUT/'Background.jpg').stat().st_size,
                   sprites=1,compression='JPEG quality 90, full chroma'))
props=Image.open(SOURCE/SOURCES['Vegetation'])
names=['Runestone','MoonBush','Crystal','RootLeft','RootRight','Mushrooms']
save_sheet('Vegetation',[(n,props.crop(((i%3)*512,(0 if i<3 else 536),(i%3+1)*512,(536 if i<3 else 1024)))) for i,n in enumerate(names)],3,(384,384))

# A contiguous 3x2 ground layout, cut into independently editable panels.
# All joins come from adjacent source pixels. These are NOT arbitrary repeat tiles.
ground=clean(Image.open(SOURCE/SOURCES['Tileset']))
ground=ground.crop((0,max(0,ground.getchannel('A').getbbox()[1]-4),ground.width,ground.height))
ground=ground.resize((1536,384),Image.Resampling.LANCZOS)
sheet=Image.new('RGBA',(1560,400))
slices=[]
for row in range(2):
    for col in range(3):
        tile=ground.crop((col*512,row*192,(col+1)*512,(row+1)*192))
        x,y=col*520+4,row*200+4
        # Extrude edge pixels into a four-pixel gutter, avoiding bilinear seams.
        sheet.paste(tile,(x,y))
        sheet.paste(tile.crop((0,0,1,192)).resize((4,192)),(x-4,y))
        sheet.paste(tile.crop((511,0,512,192)).resize((4,192)),(x+512,y))
        sheet.paste(tile.crop((0,0,512,1)).resize((512,4)),(x,y-4))
        sheet.paste(tile.crop((0,191,512,192)).resize((512,4)),(x,y+192))
        slices.append(dict(name=f'Ground_{row}_{col}',x=x,y=400-y-192,width=512,height=192,pivotX=0,pivotY=1))
sheet.save(OUT/'Tileset.png',optimize=True,compress_level=9)
(OUT/'Tileset.slices.json').write_text(json.dumps({'sprites':slices},indent=2),encoding='utf-8')
report.append(dict(file='Tileset.png',dimensions=list(sheet.size),bytes=(OUT/'Tileset.png').stat().st_size,sprites=6,compression='lossless RGBA PNG'))
manifest=dict(tool='OpenAI image_gen.imagegen',generation_date='2026-10-12',sources=SOURCES,
              files=report,total_bytes=sum(f['bytes'] for f in report),
              source_bytes=sum((SOURCE/n).stat().st_size for n in SOURCES.values()),
              view='shallow elevated 3/4 ground, multirow battlefield',
              references=['https://marketplace.unity.com/packages/2d/environments/2d-hand-painted-platformer-environment-227159',
                          'https://learn.unity.com/project/2d-game-kit?uv=2020.2'],
              rights='Generated for this project. Community packs are structural references, no third-party pack art copied.',
              note='Ground panels assemble in named row/column order; not claimed seamless repeatable tiles. PNG compression is lossless after resizing. Background JPEG is lossy.')
(ROOT/'Docs/enchanted-assets-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(json.dumps(manifest,indent=2))

# Tiny reusable geometric contact shadow, centered pivot set by importer.
shadow=Image.new('RGBA',(128,48))
for y in range(48):
    for x in range(128):
        radius=((x-63.5)/63.5)**2+((y-23.5)/23.5)**2
        shadow.putpixel((x,y),(8,13,25,round(105*max(0,1-radius)**1.5)))
shadow.save(OUT/'ContactShadow.png',optimize=True)
