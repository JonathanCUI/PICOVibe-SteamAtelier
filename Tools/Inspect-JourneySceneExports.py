"""Verify scene partitioning in the packaged SpatialAdapter GLBs (no device claim)."""
import argparse
import json
from pathlib import Path
import struct
import zipfile


def inspect(archive, name):
    with archive.open(name) as stream:
        magic, version, length = struct.unpack('<III', stream.read(12))
        assert magic == 0x46546C67 and version == 2, name
        json_length, chunk_type = struct.unpack('<II', stream.read(8))
        assert chunk_type == 0x4E4F534A, name
        gltf = json.loads(stream.read(json_length))
    assert length == archive.getinfo(name).file_size, name
    nodes = gltf.get('nodes', [])
    meshes = gltf.get('meshes', [])
    accessors = gltf.get('accessors', [])
    views = gltf.get('bufferViews', [])
    geometry_views = set()
    triangles = 0
    for node in nodes:
        if 'mesh' not in node:
            continue
        for primitive in meshes[node['mesh']]['primitives']:
            if primitive.get('mode', 4) == 4:
                index = primitive.get('indices', primitive['attributes'].get('POSITION'))
                triangles += accessors[index]['count'] // 3
            for index in list(primitive['attributes'].values()) + ([primitive['indices']] if 'indices' in primitive else []):
                if 'bufferView' in accessors[index]:
                    geometry_views.add(accessors[index]['bufferView'])
    return {
        'path': name, 'glbBytes': length, 'nodes': len(nodes),
        'meshNodes': sum('mesh' in n for n in nodes),
        'referencedTriangles': triangles,
        'uniqueGeometryBufferBytes': sum(views[v]['byteLength'] for v in geometry_views),
        'canyonRoots': [n.get('name') for n in nodes if n.get('name', '').startswith('Mesa original canyon') or n.get('name')=='SwanJourneyCanyon'],
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('apk', type=Path)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--canyon-prefab',action='store_true')
    args = parser.parse_args()
    with zipfile.ZipFile(args.apk) as archive:
        paths = sorted(n for n in archive.namelist() if n.endswith('.unity.glb'))
        expected = sorted('assets/SpatialAdapterExport/Assets/SwanJourney/Scenes/' + n + '.unity.glb'
                          for n in (('SteamJourneyBoot', 'SteamJourneySwan') if args.canyon_prefab else ('SteamJourneyBoot', 'SteamJourneySwan', 'SteamJourneyCanyon')))
        assert paths == expected, f'Unexpected scene exports: {paths}'
        scenes = [inspect(archive, name) for name in paths]
        if args.canyon_prefab:
            scenes.append(inspect(archive,'assets/SpatialAdapterExport/Assets/SwanJourney/Resources/SwanJourneyCanyon.prefab.glb'))
    core = next(s for s in scenes if s['path'].endswith('/SteamJourneySwan.unity.glb'))
    canyon = next(s for s in scenes if 'SteamJourneyCanyon.' in s['path'] or 'SwanJourneyCanyon.prefab' in s['path'])
    boot = next(s for s in scenes if s['path'].endswith('/SteamJourneyBoot.unity.glb'))
    assert boot['glbBytes'] < 2_000_000 and boot['meshNodes'] < 30 and not boot['canyonRoots'], 'Boot must remain a small loading panel'
    assert not core['canyonRoots'], 'Canyon root still exists in startup GLB'
    assert len(canyon['canyonRoots']) == 1 and canyon['meshNodes'] > 0, 'Deferred canyon is absent/empty'
    report = {
        'passed': True,
        'scope': 'Packaged scene GLBs and mesh buffers only; not runtime load order, native completion or memory release',
        'apk': str(args.apk), 'nativeRenderingValidated': False, 'scenes': scenes,
    }
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
