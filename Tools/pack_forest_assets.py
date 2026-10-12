"""Normalize approved generated layers into Unity Multiple sprite sheets.
Keeps source images outside Assets; writes exact slice rectangles and size report.
"""
from pathlib import Path
import json
from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path(r'C:\Users\aaa\.codex\generated_images\01a12eef-cdd0-7c83-ad7e-3dd6380a686b')
OUT = ROOT / 'Assets/Resources/Battle/Art/Forest'
OUT.mkdir(parents=True, exist_ok=True)
SOURCES = {
    'Background': 'exec-d3e38be5-64a3-4627-8f44-cb1f74651348.png',
    'Trees': 'exec-e1f19e51-319c-45cf-ae90-4f01b0ad939c.png',
    'Vegetation': 'exec-6a466360-cb25-4594-a6d3-536c79021e7f.png',
    'Tileset': 'exec-f99752cc-8afd-4a0a-b639-85d1f749be03.png',
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
trees=Image.open(SOURCE/SOURCES['Trees'])
save_sheet('Trees', [('Oak',trees.crop((0,0,910,1024))),('Birch',trees.crop((910,0,1536,1024)))],2,(640,768))
props=Image.open(SOURCE/SOURCES['Vegetation'])
names=['MossStones','Shrub','Grass','Boulder','Fern','Signpost']
save_sheet('Vegetation',[(n,props.crop(((i%3)*512,(i//3)*512,(i%3+1)*512,(i//3+1)*512))) for i,n in enumerate(names)],3,(256,256))

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
(ROOT/'Docs/forest-assets-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(json.dumps(manifest,indent=2))
