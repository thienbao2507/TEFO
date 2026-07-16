from pathlib import Path
from collections import deque
import csv, math, shutil
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT.parents[2]
OUT = ROOT
REF6 = PROJECT / "Docs/AE86Production32Fix/Step1F_IdentityRedraw/PNG/slot_06_22.50_step1f.png"
OLD7 = PROJECT / "Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_07_11.25_step1d.png"
REF8 = PROJECT / "Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG/slot_08_0.00_step1d.png"
GENERATED = [
    Path(r"C:/Users/ThienBao/.codex/generated_images/019f65ce-43ea-75a3-9aa2-6e5175a71556/exec-d85a8a1b-f34b-40ef-a9f3-5ccb979ba968.png"),
    Path(r"C:/Users/ThienBao/.codex/generated_images/019f65ce-43ea-75a3-9aa2-6e5175a71556/exec-ae0fe13d-0ea3-4419-b0ad-b8989998fedb.png"),
    Path(r"C:/Users/ThienBao/.codex/generated_images/019f65ce-43ea-75a3-9aa2-6e5175a71556/exec-706ce111-27e5-4c02-ae5e-faf5cb6ac07b.png"),
]

def rgba(path): return Image.open(path).convert("RGBA")

def mask(im): return np.asarray(im)[:, :, 3] > 0

def bbox_metrics(im):
    a = mask(im); ys, xs = np.where(a)
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    pts = np.column_stack((xs, ys)).astype(float)
    cov = np.cov(pts, rowvar=False); vals, vecs = np.linalg.eigh(cov)
    v = vecs[:, np.argmax(vals)]
    angle = math.degrees(math.atan2(-v[1], v[0])); angle = ((angle + 90) % 180) - 90
    return dict(x=x0, y=y0, width=x1-x0+1, height=y1-y0+1,
                centroid_x=xs.mean(), centroid_y=ys.mean(), baseline=y1,
                area=len(xs), pca_angle=angle,
                projected_length=2.0*math.sqrt(vals.max())*math.sqrt(12.0))

def palette():
    colors=set()
    for p in (PROJECT/"Docs/AE86Production32Fix/Step1F_IdentityRedraw/PNG").glob("*.png"):
        arr=np.asarray(rgba(p)); colors.update(map(tuple,arr[arr[:,:,3]>0,:3]))
    return np.array(sorted(colors), dtype=np.int16)

PAL=palette()

def largest_component(a):
    h,w=a.shape; seen=np.zeros_like(a,bool); best=[]
    for sy,sx in zip(*np.where(a & ~seen)):
        if seen[sy,sx]: continue
        q=deque([(sy,sx)]); seen[sy,sx]=1; comp=[]
        while q:
            y,x=q.popleft(); comp.append((y,x))
            for dy in (-1,0,1):
                for dx in (-1,0,1):
                    ny,nx=y+dy,x+dx
                    if 0<=ny<h and 0<=nx<w and a[ny,nx] and not seen[ny,nx]: seen[ny,nx]=1; q.append((ny,nx))
        if len(comp)>len(best): best=comp
    out=np.zeros_like(a,bool)
    for y,x in best: out[y,x]=1
    return out

def normalize(src, target_w, target_h):
    im=rgba(src); arr=np.asarray(im); bg=arr[:,:,:3]
    # Generated chroma varies slightly; green dominance cleanly separates the car.
    a=~((bg[:,:,1] > 150) & (bg[:,:,1] > bg[:,:,0]*1.45) & (bg[:,:,1] > bg[:,:,2]*1.45))
    a=largest_component(a)
    ys,xs=np.where(a); crop=Image.fromarray(np.dstack((arr[ys.min():ys.max()+1,xs.min():xs.max()+1,:3],
        (a[ys.min():ys.max()+1,xs.min():xs.max()+1]*255).astype(np.uint8))),"RGBA")
    crop=crop.resize((target_w,target_h),Image.Resampling.NEAREST)
    ca=np.asarray(crop).copy(); opaque=ca[:,:,3]>0
    pix=ca[opaque,:3].astype(np.int16)
    # Strict nearest approved Step 1F palette.
    for start in range(0,len(pix),2048):
        block=pix[start:start+2048]; dist=((block[:,None,:]-PAL[None,:,:])**2).sum(2)
        pix[start:start+2048]=PAL[np.argmin(dist,axis=1)]
    ca[opaque,:3]=pix.astype(np.uint8); ca[:,:,3]=np.where(opaque,255,0)
    out=Image.new("RGBA",(186,186)); x=round(93-target_w/2); y=169-target_h+1
    out.alpha_composite(Image.fromarray(ca,"RGBA"),(x,y)); return out

def panel(images, labels, cols, scale=3, cell=(210,230)):
    rows=math.ceil(len(images)/cols); out=Image.new("RGB",(cols*cell[0],rows*cell[1]),(35,35,40)); d=ImageDraw.Draw(out)
    for i,(im,label) in enumerate(zip(images,labels)):
        x=(i%cols)*cell[0]; y=(i//cols)*cell[1]
        tile=Image.new("RGBA",(186,186),(245,245,245,255)); tile.alpha_composite(im)
        tile=tile.resize((186,186),Image.Resampling.NEAREST); out.paste(tile.convert("RGB"),(x+12,y+24)); d.text((x+12,y+5),label,fill="white")
    return out

def main():
    for d in ("Candidates","PNG","References","Previews","Reports"): (OUT/d).mkdir(parents=True,exist_ok=True)
    for p in (REF6,OLD7,REF8): shutil.copy2(p,OUT/"References"/p.name)
    m6,m8=bbox_metrics(rgba(REF6)),bbox_metrics(rgba(REF8))
    tw=round((m6['width']+m8['width'])/2); th=round((m6['height']+m8['height'])/2)
    candidates=[]
    for src,name in zip(GENERATED,"ABC"):
        im=normalize(src,tw,th); im.save(OUT/f"Candidates/slot_07_candidate_{name}.png"); candidates.append(im)
    # Candidate B is the designed balanced midpoint; technical and manual review below gate readiness.
    selected=1; candidates[selected].save(OUT/"PNG/slot_07_11.25_step1h.png")
    refs=[rgba(REF6),rgba(OLD7),rgba(REF8)]; final=candidates[selected]
    metrics=[]
    for label,im in [("slot06",refs[0]),("old_slot07",refs[1]),("candidate_A",candidates[0]),("candidate_B",candidates[1]),("candidate_C",candidates[2]),("slot08",refs[2])]:
        m=bbox_metrics(im); m['item']=label; metrics.append(m)
    with open(OUT/"Reports/step1h_metrics.csv","w",newline="",encoding="utf-8") as f:
        cols=['item','pca_angle','width','height','centroid_x','centroid_y','baseline','area','projected_length','x','y']
        w=csv.DictWriter(f,fieldnames=cols); w.writeheader(); w.writerows(metrics)
    panel(candidates,["A — shallow","B — balanced (selected)","C — deeper"],3).save(OUT/"Previews/step1h_candidate_comparison.png")
    panel([refs[0],final,refs[2]],["06 / 22.50°","07 / 11.25° Step1H","08 / 0° Right"],3).save(OUT/"Previews/step1h_slot06_07_08_review.png")
    # Approved 17-source composition with only slot 07 replaced.
    comp=[]; labels=[]
    names=["slot_00_90.00_step1d.png","slot_01_78.75_step1d.png","slot_02_67.50_step1d.png","slot_03_56.25_step1d.png","slot_04_45.00_step1d.png","slot_05_33.75_step1d.png",None,None,"slot_08_0.00_step1d.png",None,"slot_10_337.50_step1d.png","slot_11_326.25_step1d.png","slot_12_315.00_step1d.png","slot_13_303.75_step1d.png",None,"slot_15_281.25_step1d.png","slot_16_270.00_step1d.png"]
    angles=[90,78.75,67.5,56.25,45,33.75,22.5,11.25,0,348.75,337.5,326.25,315,303.75,292.5,281.25,270]
    for i,n in enumerate(names):
        if i==6: im=rgba(REF6)
        elif i==7: im=final
        elif i in (9,14): im=rgba(PROJECT/f"Docs/AE86Production32Fix/Step1F_IdentityRedraw/PNG/slot_{i:02d}_{angles[i]:.2f}_step1f.png")
        else: im=rgba(PROJECT/"Docs/AE86Production32Fix/Step1D_LocalFlipRecovery/PNG"/n)
        comp.append(im); labels.append(f"{i:02d} / {angles[i]:.2f}°")
    panel(comp,labels,5).save(OUT/"Previews/step1h_17_contact_sheet.png")
    full=[]; flabel=[]
    for d in range(32):
        if d<=8: s=8-d; flip=False
        elif d<=23: s=d-8; flip=True
        else: s=40-d; flip=False
        im=comp[s].transpose(Image.Transpose.FLIP_LEFT_RIGHT) if flip else comp[s]
        full.append(im); flabel.append(f"{d:02d} {d*11.25:.2f}° s{s:02d} {'F' if flip else '-'}")
    panel(full,flabel,8).save(OUT/"Previews/step1h_full32_preview.png")
    mirrored=final.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    mm=bbox_metrics(final); oldm=bbox_metrics(refs[1])
    labels=["06 approved",f"old 07 PCA {oldm['pca_angle']:.2f}°",f"new 07 B PCA {mm['pca_angle']:.2f}°", "08 Right", "mirrored runtime"]
    panel([refs[0],refs[1],final,refs[2],mirrored],labels,5).save(OUT/"Previews/step1h_before_after_runtime_comparison.png")
    print('selected=B',mm)

if __name__=='__main__': main()
