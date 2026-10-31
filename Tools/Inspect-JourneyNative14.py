"""Check installed-package candidates, never claim native visual/performance success."""
import argparse, hashlib, json, struct, zipfile, zlib
from pathlib import Path

def glb(archive, scene):
    path=scene if scene.endswith('.glb') else 'assets/SpatialAdapterExport/Assets/SwanJourney/Scenes/'+scene+'.unity.glb'
    with archive.open(path) as f:
        f.read(12); length, _ = struct.unpack('<II', f.read(8))
        data = json.loads(f.read(length)); f.read(8)
        return data, f.read()

def rgba_means(png):
    offset=8; packed=b''
    while offset < len(png):
        length=struct.unpack('>I',png[offset:offset+4])[0]; kind=png[offset+4:offset+8]
        data=png[offset+8:offset+8+length];offset+=length+12
        if kind==b'IHDR': w,h,depth,color,_,_,interlace=struct.unpack('>IIBBBBB',data)
        if kind==b'IDAT': packed+=data
        if kind==b'IEND': break
    assert depth==8 and color in (2,6) and interlace==0
    channels=4 if color==6 else 3; stride=w*channels; raw=zlib.decompress(packed)
    previous=bytearray(stride); sums=[0]*channels
    for y in range(h):
        start=y*(stride+1); ft=raw[start]; row=bytearray(raw[start+1:start+1+stride])
        for x in range(stride):
            a=row[x-channels] if x>=channels else 0; b=previous[x]; c=previous[x-channels] if x>=channels else 0
            if ft==1: row[x]=(row[x]+a)&255
            elif ft==2: row[x]=(row[x]+b)&255
            elif ft==3: row[x]=(row[x]+((a+b)//2))&255
            elif ft==4:
                p=a+b-c; pa,pb,pc=abs(p-a),abs(p-b),abs(p-c)
                row[x]=(row[x]+(a if pa<=pb and pa<=pc else b if pb<=pc else c))&255
            elif ft!=0: raise ValueError('Unsupported PNG filter')
        for ch in range(channels): sums[ch]+=sum(row[ch::channels])
        previous=row
    return [round(v/(w*h*255),5) for v in sums]

def main():
    p=argparse.ArgumentParser();p.add_argument('apk',type=Path);p.add_argument('--out',type=Path,required=True)
    p.add_argument('--require-canyon-container',action='store_true');p.add_argument('--canyon-prefab',action='store_true');a=p.parse_args()
    report={'passed':False,'nativeRenderingValidated':False,'devicePerformanceMeasured':False,'textures':[]}
    with zipfile.ZipFile(a.apk) as z:
        core,binary=glb(z,'SteamJourneySwan'); canyon,_=glb(z,'assets/SpatialAdapterExport/Assets/SwanJourney/Resources/SwanJourneyCanyon.prefab.glb' if a.canyon_prefab else 'SteamJourneyCanyon')
        report['mainNodes']=len(core['nodes']); report['canyonNodes']=len(canyon['nodes'])
        report['canyonRoot']= {k:v for k,v in canyon['nodes'][0].items() if k!='children'}
        assert all(abs(s-1)<1e-5 for s in canyon['nodes'][0].get('scale',[1,1,1])), 'Metres must already be baked into canyon'
        if a.require_canyon_container:
            container=canyon['nodes'][0]
            assert container['name']=='Deferred canyon scene container'
            rotation=container.get('rotation',[0,0,0,1])
            assert all(abs(v)<1e-6 for v in container.get('translation',[0,0,0]))
            # q and -q describe the same rotation; exporter uses identity w=-1.
            assert all(abs(v)<1e-6 for v in rotation[:3]) and abs(abs(rotation[3])-1)<1e-6
            assert len(container['children'])==1
            payload=canyon['nodes'][container['children'][0]]
            assert payload['name'].startswith('Mesa original canyon')
            assert all(abs(s-1)<1e-5 for s in payload.get('scale',[1,1,1]))
            report['canyonIdentityContainerAndMetrePayload']=True
        assert len(core['nodes'])<8000 and len(canyon['nodes'])<2000, 'Redundant nodes were not removed'
        sleepers=[n for n in core['nodes'] if n.get('name')=='Timber sleeper' and 'mesh' in n]
        assert len(sleepers)==26; report['workshopSleeperMeshes']=len(sleepers)
        targets=('Swan V23 0-Sleeper','Swan V23 1-Endgrain','Swan V23 200-Ballast','Swan V23 201-Ballast','Swan V23 202-Ballast','Swan V23 203-Ballast')
        for name in targets:
            material=next(m for m in core['materials'] if m.get('name')==name)
            for kind,info in [('metal',material['pbrMetallicRoughness']['metallicRoughnessTexture']),('ao',material['occlusionTexture'])]:
                im=core['images'][core['textures'][info['index']]['source']]; v=core['bufferViews'][im['bufferView']]
                image=binary[v.get('byteOffset',0):v.get('byteOffset',0)+v['byteLength']]
                means=rgba_means(image)
                # Inspect the final exporter-generated ORM, not intermediate
                # Unity PNGs: old package R(AO)=0 despite source AO G~0.93.
                if kind=='ao': assert means[0]>.35, (name,means)
                else: assert means[2]<.15 and means[1]>.2, (name,means)
                report['textures'].append({'material':name,'role':kind,'exportedOrmMeans':means,'pngSha256':hashlib.sha256(image).hexdigest()})
    report['passed']=True;a.out.parent.mkdir(parents=True,exist_ok=True);a.out.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(report,ensure_ascii=False,indent=2))
if __name__=='__main__': main()
