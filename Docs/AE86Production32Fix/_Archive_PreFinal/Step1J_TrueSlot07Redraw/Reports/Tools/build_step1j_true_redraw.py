"""Explicit pixel-geometry redraws for AE86 slot 07; no image generation/transforms."""
from pathlib import Path
from collections import deque
import csv, math
import numpy as np
from PIL import Image, ImageDraw

HERE=Path(__file__).resolve(); OUT=HERE.parents[2]; PROJECT=OUT.parents[2]
OLD=PROJECT/'Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_07_11.25_step1d.png'
S6=PROJECT/'Docs/AE86Production32Fix/Step1F_IdentityRedraw/PNG/slot_06_22.50_step1f.png'
S1I=PROJECT/'Docs/AE86Production32Fix/Step1I_ManualSlot07/PNG/slot_07_11.25_step1i.png'
S8=PROJECT/'Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_08_0.00_step1d.png'

def load(p): return Image.open(p).convert('RGBA')
def approved_palette():
 c=set()
 for p in (PROJECT/'Docs/AE86Production32Fix/Step1F_IdentityRedraw/PNG').glob('*.png'):
  a=np.asarray(load(p)); c.update(map(tuple,a[a[:,:,3]>0,:3]))
 return np.array(sorted(c),dtype=np.int16)
PAL=approved_palette()
def col(rgb):
 q=np.array(rgb,dtype=np.int32); p=PAL.astype(np.int32); return tuple(PAL[np.argmin(((p-q)**2).sum(1))].astype(int))+(255,)
C={
 'outline':col((18,17,22)),'tire':col((30,28,33)),'wheel':col((53,49,57)),'hub':col((16,15,19)),
 'body':col((166,157,177)),'light':col((202,196,207)),'shade':col((126,116,139)),
 'glass':col((43,31,55)),'glass2':col((67,49,79)),'hood':col((29,25,32)),
 'trim':col((27,24,30)),'red':col((211,31,25)),'yellow':col((244,180,32)),'white':col((224,219,225))
}

# Each candidate is separately authored geometry, not a displacement series.
GEOS={
'A':dict(body=[(18,137),(22,124),(38,116),(51,97),(101,93),(119,99),(153,103),(170,111),(170,140),(155,147),(31,166),(18,156)],
 roof=[(48,99),(58,86),(104,84),(119,101),(101,107),(54,109)], wind=[(104,87),(118,102),(101,108),(94,91)],
 side=[(51,101),(56,91),(91,91),(98,108),(55,110)], rearwin=[(32,113),(48,101),(53,111),(42,126)], hood=[(119,103),(155,109),(169,120),(159,132),(116,128)],
 wheels=[(52,156),(139,145)], far=[(61,135),(129,123)], hoodedge=[(117,124),(159,126),(169,117)], hatch=[(22,125),(38,116),(44,132),(30,142)]),
'B':dict(body=[(18,139),(23,123),(40,113),(53,94),(99,88),(121,95),(154,98),(170,106),(170,136),(156,143),(31,166),(18,157)],
 roof=[(49,98),(59,82),(103,79),(121,99),(101,106),(54,109)], wind=[(103,82),(120,99),(101,107),(93,87)],
 side=[(50,100),(58,86),(91,86),(99,107),(54,110)], rearwin=[(31,115),(48,101),(53,112),(42,128)], hood=[(121,100),(155,106),(170,118),(159,130),(117,126)],
 wheels=[(52,156),(140,141)], far=[(62,134),(130,117)], hoodedge=[(117,119),(159,122),(170,112)], hatch=[(22,126),(40,113),(45,132),(30,144)]),
'C':dict(body=[(18,141),(24,123),(41,111),(54,91),(97,84),(122,91),(155,93),(170,101),(170,132),(157,139),(31,166),(18,158)],
 roof=[(50,96),(60,78),(101,75),(122,96),(101,104),(55,108)], wind=[(101,78),(121,96),(101,105),(91,83)],
 side=[(50,98),(59,82),(89,82),(99,105),(54,109)], rearwin=[(31,116),(49,99),(54,111),(42,130)], hood=[(122,97),(156,103),(170,116),(159,128),(117,124)],
 wheels=[(52,156),(141,138)], far=[(63,133),(130,113)], hoodedge=[(117,115),(159,118),(170,107)], hatch=[(22,128),(41,111),(46,134),(30,147)])}

def wheel(d,cx,cy,r=13):
 d.ellipse((cx-r,cy-r,cx+r,cy+r),fill=C['outline']); d.ellipse((cx-r+2,cy-r+2,cx+r-2,cy+r-2),fill=C['tire'])
 d.ellipse((cx-7,cy-7,cx+7,cy+7),fill=C['wheel']); d.ellipse((cx-3,cy-3,cx+3,cy+3),fill=C['hub'])
 d.line((cx-6,cy,cx+6,cy),fill=C['outline']); d.line((cx,cy-6,cx,cy+6),fill=C['outline'])
def farwheel(d,cx,cy):
 d.ellipse((cx-7,cy-5,cx+7,cy+5),fill=C['outline']); d.ellipse((cx-5,cy-3,cx+5,cy+3),fill=C['tire'])

def draw_candidate(g):
 im=Image.new('RGBA',(186,186)); d=ImageDraw.Draw(im)
 # Far wheels are redrawn first and remain partially occluded by body.
 for p in g['far']: farwheel(d,*p)
 d.polygon(g['body'],fill=C['outline'])
 inner=[(x+(2 if x<90 else -2),y+2) for x,y in g['body']]; d.polygon(inner,fill=C['body'])
 d.polygon(g['hatch'],fill=C['shade'],outline=C['outline']); d.polygon(g['roof'],fill=C['light'],outline=C['outline'])
 d.polygon(g['rearwin'],fill=C['glass'],outline=C['outline']); d.polygon(g['side'],fill=C['glass2'],outline=C['outline'])
 d.polygon(g['wind'],fill=C['glass'],outline=C['outline']); d.polygon(g['hood'],fill=C['hood'],outline=C['outline'])
 d.line(g['hoodedge'],fill=C['light'],width=2)
 # Side door/seams and black lower trim are separately authored.
 d.line((55,111,54,144),fill=C['outline']); d.line((101,107,105,141),fill=C['outline'])
 d.polygon([(24,145),(160,137),(169,141),(157,151),(31,162),(19,153)],fill=C['trim'],outline=C['outline'])
 d.line((31,145,157,136),fill=C['shade'],width=2)
 # Near wheels are complete redraws, not translated old clusters.
 for p in g['wheels']: wheel(d,*p)
 # Identity landmarks: red rear lamp, yellow nose lamp, pop-up headlight, bumpers, handle.
 d.rectangle((19,132,24,143),fill=C['red'],outline=C['outline']); d.rectangle((162,119,169,124),fill=C['yellow'],outline=C['outline'])
 d.polygon([(151,108),(160,109),(163,116),(154,116)],fill=C['white'],outline=C['outline'])
 d.rectangle((75,121,82,124),fill=C['outline']); d.line((18,149,29,154),fill=C['light'],width=2); d.line((158,145,170,139),fill=C['light'],width=2)
 # Ensure baseline y=169 with connected near-wheel pixels.
 return im

def mask(im): return np.asarray(im)[:,:,3]>0
def metrics(im):
 a=mask(im);ys,xs=np.where(a);pts=np.c_[xs,ys].astype(float);vals,vecs=np.linalg.eigh(np.cov(pts,rowvar=False));v=vecs[:,np.argmax(vals)]
 ang=math.degrees(math.atan2(-v[1],v[0]));ang=((ang+90)%180)-90
 return dict(pca=ang,width=xs.max()-xs.min()+1,height=ys.max()-ys.min()+1,cx=xs.mean(),cy=ys.mean(),baseline=ys.max(),area=len(xs),projected_length=2*math.sqrt(vals.max())*math.sqrt(12))
def change_stats(new):
 a=np.asarray(load(OLD));b=np.asarray(new);ao=a[:,:,3]>0;bo=b[:,:,3]>0;diff=np.any(a!=b,axis=2);ys,xs=np.where(diff)
 return dict(changed_opaque=int(np.count_nonzero(diff&(ao|bo))),added=int(np.count_nonzero(bo&~ao)),removed=int(np.count_nonzero(ao&~bo)),rect=f'{xs.min()}:{ys.min()}:{xs.max()}:{ys.max()}')
def sheet(images,labels,cols,guides=None):
 cw,ch=210,226;out=Image.new('RGB',(cols*cw,math.ceil(len(images)/cols)*ch),(32,33,38));d=ImageDraw.Draw(out)
 for i,(im,label) in enumerate(zip(images,labels)):
  ox=(i%cols)*cw+12;oy=(i//cols)*ch+24;bg=Image.new('RGBA',(186,186),(245,245,245,255));bg.alpha_composite(im)
  if guides and guides[i]: guides[i](ImageDraw.Draw(bg))
  out.paste(bg.convert('RGB'),(ox,oy));d.text((ox,5+(i//cols)*ch),label,fill='white')
 return out
def comp(final):
 angles=[90,78.75,67.5,56.25,45,33.75,22.5,11.25,0,348.75,337.5,326.25,315,303.75,292.5,281.25,270];ims=[]
 for i,a in enumerate(angles):
  if i==6:p=S6
  elif i==7:ims.append(final);continue
  elif i in (9,14):p=PROJECT/f'Docs/AE86Production32Fix/Step1F_IdentityRedraw/PNG/slot_{i:02d}_{a:.2f}_step1f.png'
  else:p=PROJECT/f'Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_{i:02d}_{a:.2f}_step1d.png'
  ims.append(load(p))
 return ims,angles
def main():
 for x in ['Candidates','PNG','Previews','Reports']:(OUT/x).mkdir(parents=True,exist_ok=True)
 ims=[]
 for n in 'ABC': im=draw_candidate(GEOS[n]);im.save(OUT/f'Candidates/slot_07_true_{n}.png');ims.append(im)
 base=[('slot06',load(S6)),('old_slot07',load(OLD)),('step1i_C',load(S1I))]+[(f'true_{n}',im) for n,im in zip('ABC',ims)]+[('slot08',load(S8))]
 rows=[{'item':n,**metrics(im)} for n,im in base]
 with open(OUT/'Reports/step1j_metrics.csv','w',newline='',encoding='utf8') as f:w=csv.DictWriter(f,fieldnames=rows[0]);w.writeheader();w.writerows(rows)
 with open(OUT/'Reports/step1j_pixel_change_manifest.csv','w',newline='',encoding='utf8') as f:
  fields=['candidate','source_regions_reused','regions_redrawn','changed_opaque_pixels','added_opaque_pixels','removed_opaque_pixels','edited_rect','wheels_redrawn','hood_shortened','roof_rebuilt'];w=csv.DictWriter(f,fieldnames=fields);w.writeheader()
  for n,im in zip('ABC',ims):
   s=change_stats(im);w.writerow(dict(candidate=n,source_regions_reused='palette and identity landmarks only',regions_redrawn='roof;windshield;hood/front;side windows;near/far wheels;hatch;outline/seams',changed_opaque_pixels=s['changed_opaque'],added_opaque_pixels=s['added'],removed_opaque_pixels=s['removed'],edited_rect=s['rect'],wheels_redrawn=True,hood_shortened=True,roof_rebuilt=True))
 sheet(ims,[f'{n} PCA {metrics(im)["pca"]:.2f}°' for n,im in zip('ABC',ims)],3).save(OUT/'Previews/step1j_candidate_comparison.png')
 def guide(n,im):
  g=GEOS[n];m=metrics(im)
  def fn(d):
   d.line((10,169,176,169),fill=(255,0,255,255));d.polygon(g['roof'],outline=(0,180,255,255));d.polygon(g['wind'],outline=(0,255,120,255));d.line(g['hoodedge'],fill=(255,150,0,255),width=1)
   for p in g['wheels']+g['far']:d.ellipse((p[0]-2,p[1]-2,p[0]+2,p[1]+2),outline=(255,0,0,255));d.ellipse((m['cx']-2,m['cy']-2,m['cx']+2,m['cy']+2),fill=(255,0,255,255))
   ang=math.radians(-m['pca']);dx=80*math.cos(ang);dy=80*math.sin(ang);d.line((m['cx']-dx,m['cy']-dy,m['cx']+dx,m['cy']+dy),fill=(255,0,255,255))
  return fn
 sheet([load(S6),ims[1],load(S8)],['slot06 guides','candidate B guides','Right guides'],3,[None,guide('B',ims[1]),None]).save(OUT/'Previews/step1j_geometry_guides.png')
 diff=np.any(np.asarray(load(OLD))!=np.asarray(ims[1]),axis=2)
 def hd(d):a=np.asarray(Image.new('RGBA',(186,186)));d.bitmap if False else None
 hi=ims[1].copy();a=np.asarray(hi).copy();a[diff,:3]=[255,20,180];hi=Image.fromarray(a,'RGBA')
 sheet([load(OLD),ims[1],hi],['old slot07','candidate B','magenta changed pixels'],3).save(OUT/'Previews/step1j_pixel_change_regions.png')
 # Select only if every numeric gate passes; manual report may still reject identity.
 valid=[]
 for i,im in enumerate(ims):
  m=metrics(im);d1=m['pca']-23.8807000192;d2=-3.16057819286-m['pca'];err=abs(m['projected_length']-263.36)/263.36
  if 10<=m['pca']<=12.5 and -16<=d1<=-10 and -16<=d2<=-8 and err<=.03 and 149<=m['width']<=153 and 84<=m['height']<=91 and math.hypot(m['cx']-91.04,m['cy']-124.75)<=4 and m['baseline']==169:valid.append(i)
 selected=valid[0] if valid else min(range(3),key=lambda i:abs(metrics(ims[i])['pca']-11.25));final=ims[selected]
 # No approved PNG is emitted here unless numeric gates pass; report performs identity gate.
 sheet([load(S6),final,load(S8)],['06 / 22.50°',f'07 candidate {"ABC"[selected]}','08 / Right'],3).save(OUT/'Previews/step1j_slot06_07_08_review.png')
 sheet([load(OLD),load(S1I),final,final.transpose(Image.Transpose.FLIP_LEFT_RIGHT)],['old Step1D','Step1I C',f'Step1J {"ABC"[selected]}','runtime mirror'],4).save(OUT/'Previews/step1j_before_after_comparison.png')
 composition,angles=comp(final);sheet(composition,[f'{i:02d}/{a:.2f}°' for i,a in enumerate(angles)],5).save(OUT/'Previews/step1j_17_contact_sheet.png')
 full=[];labels=[]
 for d in range(32):
  if d<=8:s=8-d;flip=False
  elif d<=23:s=d-8;flip=True
  else:s=40-d;flip=False
  full.append(composition[s].transpose(Image.Transpose.FLIP_LEFT_RIGHT) if flip else composition[s]);labels.append(f'{d:02d} {d*11.25:.2f}° s{s:02d} {"F" if flip else "-"}')
 sheet(full,labels,8).save(OUT/'Previews/step1j_full32_preview.png')
 print('valid',valid,'diagnostic','ABC'[selected],[metrics(x) for x in ims])
if __name__=='__main__':main()
