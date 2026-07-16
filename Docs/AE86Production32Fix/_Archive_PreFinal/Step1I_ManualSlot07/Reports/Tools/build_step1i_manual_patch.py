"""Deterministic region-based pixel reconstruction of AE86 slot 07.

This script starts only from the Step 1D slot 07.  It moves semantic pixel
clusters independently (roof/glass, hood/hatch, wheels, lower body), then
repairs gaps with neighboring identity pixels.  It never rotates, resizes,
filters, antialiases, or transforms the complete bitmap.
"""
from pathlib import Path
from collections import deque
import csv, math
import numpy as np
from PIL import Image, ImageDraw

HERE=Path(__file__).resolve(); OUT=HERE.parents[2]; PROJECT=OUT.parents[2]
OLD=PROJECT/'Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_07_11.25_step1d.png'
S6=PROJECT/'Docs/AE86Production32Fix/Step1F_IdentityRedraw/PNG/slot_06_22.50_step1f.png'
S8=PROJECT/'Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_08_0.00_step1d.png'

def load(p): return Image.open(p).convert('RGBA')
def alpha(im): return np.asarray(im)[:,:,3]>0
def metrics(im):
 a=alpha(im); ys,xs=np.where(a); pts=np.c_[xs,ys].astype(float); vals,vecs=np.linalg.eigh(np.cov(pts,rowvar=False)); v=vecs[:,np.argmax(vals)]
 ang=math.degrees(math.atan2(-v[1],v[0])); ang=((ang+90)%180)-90
 return {'pca':ang,'width':xs.max()-xs.min()+1,'height':ys.max()-ys.min()+1,'cx':xs.mean(),'cy':ys.mean(),'baseline':ys.max(),'area':len(xs),'projected_length':2*math.sqrt(vals.max())*math.sqrt(12)}

def move_region(src, dst, region, dy_left, dy_right, changed):
 """Move one explicit semantic rectangle with nearest integer row offsets."""
 x0,y0,x1,y1=region; arr=src.copy(); mask=arr[y0:y1,x0:x1,3]>0
 # Clear only opaque pixels belonging to this semantic patch.
 yy,xx=np.where(mask); dst[y0+yy,x0+xx]=0
 for ly,lx in zip(yy,xx):
  x=x0+lx; t=(x-x0)/max(1,x1-x0-1); dy=round(dy_left*(1-t)+dy_right*t)
  ny=y0+ly+dy
  if 1<=ny<185: dst[ny,x]=arr[y0+ly,x]; changed[ny,x]=1

def fill_internal_gaps(arr, changed):
 # Repair only transparent one-pixel gaps enclosed horizontally or vertically,
 # using the nearest original outline/body cluster color.
 old=np.asarray(load(OLD)); a=arr[:,:,3]>0
 for _ in range(2):
  add=[]
  for y in range(1,185):
   for x in range(1,185):
    if a[y,x]: continue
    if (a[y,x-1] and a[y,x+1]) or (a[y-1,x] and a[y+1,x]): add.append((y,x))
  for y,x in add:
   neigh=[arr[y,x-1],arr[y,x+1],arr[y-1,x],arr[y+1,x]]
   pix=next((p for p in neigh if p[3]),old[y,x]); arr[y,x]=pix; arr[y,x,3]=255; a[y,x]=1; changed[y,x]=1

def patch(depth):
 src=np.asarray(load(OLD)).copy(); dst=np.zeros_like(src); changed=np.zeros((186,186),bool)
 def inside(x,y,r): return r[0]<=x<r[2] and r[1]<=y<r[3]
 regions=[
  ('rear wheel',(29,143,68,170), min(3,round(depth*.10)),min(3,round(depth*.10))),
  ('front wheel',(119,143,158,170),-min(7,round(depth*.28)),-min(7,round(depth*.28))),
  ('roof/glass',(20,86,113,137), min(7,round(depth*.24)),-min(7,round(depth*.24))),
  ('hood/front',(105,105,181,151),0,-min(7,round(depth*.28))),
  ('hatch/rear',(17,112,47,148),min(5,round(depth*.16)),min(2,round(depth*.07))),
  ('lower trim',(25,137,181,163),min(6,round(depth*.23)),-min(6,round(depth*.23))),
 ]
 # Each source pixel is assigned to at most one explicit semantic region.
 for y,x in zip(*np.where(src[:,:,3]>0)):
  dy=0
  for _,r,dl,dr in regions:
   if inside(x,y,r):
    t=(x-r[0])/max(1,r[2]-r[0]-1); dy=round(dl*(1-t)+dr*t); break
  ny=max(1,min(169,y+dy)); dst[ny,x]=src[y,x]
  if dy: changed[ny,x]=1
 fill_internal_gaps(dst,changed)
 # Restore binary alpha and baseline; remove any detached pixels below baseline.
 dst[:,:,3]=np.where(dst[:,:,3]>0,255,0); dst[170:]=0
 return Image.fromarray(dst,'RGBA'),changed

def sheet(images, labels, cols, highlights=None):
 cw,ch=210,226; rows=math.ceil(len(images)/cols); out=Image.new('RGB',(cols*cw,rows*ch),(32,33,38)); d=ImageDraw.Draw(out)
 for i,(im,label) in enumerate(zip(images,labels)):
  ox=(i%cols)*cw+12; oy=(i//cols)*ch+24; bg=Image.new('RGBA',(186,186),(245,245,245,255)); bg.alpha_composite(im)
  if highlights and highlights[i] is not None:
   a=np.asarray(bg).copy(); m=highlights[i]; a[m,:3]=[255,32,180]; bg=Image.fromarray(a,'RGBA')
  out.paste(bg.convert('RGB'),(ox,oy)); d.text((ox,5+(i//cols)*ch),label,fill='white')
 return out

def composition(final):
 angles=[90,78.75,67.5,56.25,45,33.75,22.5,11.25,0,348.75,337.5,326.25,315,303.75,292.5,281.25,270]; ims=[]
 for i,a in enumerate(angles):
  if i==6:p=S6
  elif i==7: ims.append(final); continue
  elif i in (9,14):p=PROJECT/f'Docs/AE86Production32Fix/Step1F_IdentityRedraw/PNG/slot_{i:02d}_{a:.2f}_step1f.png'
  else:p=PROJECT/f'Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_{i:02d}_{a:.2f}_step1d.png'
  ims.append(load(p))
 return ims,angles

def main():
 for d in ['Candidates','PNG','Previews','Reports']: (OUT/d).mkdir(parents=True,exist_ok=True)
 # Depths were tuned as region-patch magnitudes, never whole-image transforms.
 variants=[]; changes=[]
 for name,depth in zip('ABC',(20,25,30)):
  im,ch=patch(depth); im.save(OUT/f'Candidates/slot_07_manual_{name}.png'); variants.append(im); changes.append(ch)
 # Choose the candidate nearest the preferred +11.25 signed PCA, subject to visual review.
 selected=min(range(3),key=lambda i:abs(metrics(variants[i])['pca']-11.25)); final=variants[selected]
 final.save(OUT/'PNG/slot_07_11.25_step1i.png')
 rows=[]
 for label,im in [('slot06',load(S6)),('old_slot07',load(OLD)),('manual_A',variants[0]),('manual_B',variants[1]),('manual_C',variants[2]),('slot08',load(S8))]: rows.append({'item':label,**metrics(im)})
 with open(OUT/'Reports/step1i_metrics.csv','w',newline='',encoding='utf8') as f:
  w=csv.DictWriter(f,fieldnames=rows[0].keys());w.writeheader();w.writerows(rows)
 sheet(variants,[f'{n}: PCA {metrics(im)["pca"]:.2f}°' for n,im in zip('ABC',variants)],3).save(OUT/'Previews/step1i_candidate_comparison.png')
 old=load(OLD); sel='ABC'[selected]
 sheet([old,final,final],['old slot 07',f'selected {sel}', 'magenta = explicitly patched pixels'],3,[None,None,changes[selected]]).save(OUT/'Previews/step1i_pixel_edit_regions.png')
 sheet([load(S6),final,load(S8)],['06 / 22.50°',f'07 / manual {sel}','08 / Right'],3).save(OUT/'Previews/step1i_slot06_07_08_review.png')
 sheet([old,final,final.transpose(Image.Transpose.FLIP_LEFT_RIGHT)],['before Step1D','after Step1I','runtime mirror'],3).save(OUT/'Previews/step1i_before_after_comparison.png')
 comp,angles=composition(final); sheet(comp,[f'{i:02d} / {a:.2f}°' for i,a in enumerate(angles)],5).save(OUT/'Previews/step1i_17_contact_sheet.png')
 full=[]; labels=[]
 for d in range(32):
  if d<=8:s=8-d;flip=False
  elif d<=23:s=d-8;flip=True
  else:s=40-d;flip=False
  full.append(comp[s].transpose(Image.Transpose.FLIP_LEFT_RIGHT) if flip else comp[s]);labels.append(f'{d:02d} {d*11.25:.2f}° s{s:02d} {"F" if flip else "-"}')
 sheet(full,labels,8).save(OUT/'Previews/step1i_full32_preview.png')
 print('selected',sel,'metrics',metrics(final))

if __name__=='__main__': main()
