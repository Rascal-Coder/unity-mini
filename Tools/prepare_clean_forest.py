"""Copy generated originals and define tight, alpha-aware sprite slices. No repainting."""
import json
import shutil
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path(r'C:\Users\aaa\.codex\generated_images\01a12f39-2099-7db2-95c9-493a9a43b6df')
OUT = ROOT / 'Assets/Resources/Battle/Art/Handpainted'
OUT.mkdir(parents=True, exist_ok=True)
FILES = {
    'Far.png': 'exec-3355e07c-5009-4099-a9ad-78d11d288efa.png',
    'Middle.png': 'exec-5ce9482e-3485-4a9d-95a7-1d689150accb.png',
    'TerrainWide.png': 'exec-a536a11f-4a08-49f2-9666-88960417d76c.png',
    'Props.png': 'exec-f3894e01-4a75-4888-b14e-ba81d5e10f63.png',
}
for dest, source in FILES.items():
    shutil.copy2(SOURCE / source, OUT / dest)

sheet = Image.open(OUT / 'Props.png').convert('RGBA')
regions = {
    'Runestone': (0, 0, 440, 475),
    'Crystal': (440, 0, 880, 475),
    'MoonBush': (880, 0, 1320, 475),
    'Mushrooms': (1320, 0, 1760, 475),
    'RootLeft': (0, 475, 510, 880),
    'RootRight': (512, 475, 1048, 880),
    'TrainingDummy': (1050, 475, 1320, 880),
}
slices = []
for name, box in regions.items():
    box = tuple(round(value * (sheet.width / 1760 if i % 2 == 0 else sheet.height / 880)) for i, value in enumerate(box))
    crop = sheet.crop(box)
    bounds = crop.getchannel('A').point(lambda a: 255 if a > 16 else 0).getbbox()
    assert bounds is not None, name
    # Include two pixels of gutter without crossing into neighbouring sprites.
    x0 = max(box[0], box[0] + bounds[0] - 2)
    y0 = max(box[1], box[1] + bounds[1] - 2)
    x1 = min(box[2], box[0] + bounds[2] + 2)
    y1 = min(box[3], box[1] + bounds[3] + 2)
    slices.append(dict(name=name, x=x0, y=sheet.height-y1, width=x1-x0, height=y1-y0, pivotX=.5, pivotY=0))
(OUT / 'Props.slices.json').write_text(json.dumps(dict(sprites=slices), indent=2), encoding='utf-8')
report = []
for dest in FILES:
    image = Image.open(OUT / dest)
    alpha = image.convert('RGBA').getchannel('A')
    extrema = alpha.getextrema()
    if dest != 'Far.png':
        assert 'A' in image.getbands() and extrema == (0, 255), (dest, extrema)
    report.append(dict(file=dest, size=image.size, mode=image.mode, alphaRange=extrema))
(ROOT / 'Docs/Previews/clean-forest-raster-qa.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(dict(images=report, sprites=slices), indent=2))
