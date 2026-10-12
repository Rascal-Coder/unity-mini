import json
from pathlib import Path
from PIL import Image,ImageDraw
r=Path.cwd(); j=json.loads((r/'LC2_Hero.json').read_text(encoding='utf-8')); regions={}; pages={}; cur=None
for l in (r/'LC2_Hero.atlas').read_text().splitlines():
 l=l.strip()
 if not l: cur=None
 elif l.endswith('.png'): page=l; pages[page]=Image.open(r/page).convert('RGBA'); cur=None
 elif ':' not in l: cur={'page':page}; regions[l]=cur
 elif cur is not None: k,v=l.split(':',1); cur[k]=v
for group in ['appearance','armor']:
 skins=[s for s in j['skins'] if (any(x in s['name'] for x in ['head_hair/','dress/backpack/','dress/headwear/','dress/eyewear/']) if group=='appearance' else s['name'].startswith('002A_armor/') and '/PL/' not in s['name'])]
 sheet=Image.new('RGB',(1200,((len(skins)+7)//8)*135),'#26343e'); d=ImageDraw.Draw(sheet)
 for i,s in enumerate(skins):
  ims=[]
  for slot in s.get('attachments',{}).values():
   for key,a in slot.items():
    reg=regions.get(a.get('path',a.get('name',key)))
    if not reg or 'bounds' not in reg: continue
    x,y,w,h=map(int,reg['bounds'].split(','))
    if w*h<30: continue
    rot=reg.get('rotate')=='90'; im=pages[reg['page']].crop((x,y,x+(h if rot else w),y+(w if rot else h)))
    if rot: im=im.transpose(Image.Transpose.ROTATE_90)
    ims.append(im)
  xx,yy=i%8*150,i//8*135
  for k,im in enumerate(sorted(ims,key=lambda a:a.width*a.height,reverse=True)[:2]):
   im.thumbnail((72,100)); sheet.paste(im,(xx+k*74,yy),im)
  d.text((xx+2,yy+105),s['name'].rsplit('/',1)[-1],fill='white')
 sheet.save(r/'Logs'/f'{group}-source.png')
