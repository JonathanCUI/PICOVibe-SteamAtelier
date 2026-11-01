"""Check V23 feedback fixes in packaged GLBs; no device claims.

Only the GLB JSON chunk is read. Names are grounded in the 0.23.7 APK,
SwanFpsMeshBuilder, and the saved V23.8 StudioPartCards hierarchy.
"""
import argparse
from collections import Counter
from datetime import datetime, timezone
import json
import math
from pathlib import Path
import struct
import zipfile


MAIN_GLB = 'assets/SpatialAdapterExport/Assets/SwanJourney/Scenes/SteamJourneySwan.unity.glb'
RESOURCE_ROOT = 'assets/SpatialAdapterExport/Assets/SwanJourney/Generated/NativeVisuals/'
FLOW_COLORS = ((173, 230, 245), (191, 204, 204), (219, 237, 245), (94, 89, 82))
# These exact name/path pairs were read from the first V23.8 candidate APK.
# Fail on a changed pairing rather than silently inspecting an unrelated asset.
SOFT_RESOURCE_FILES = {
    'Material_0224.mat.glb': 'Swan V23 Cylinder filling vapor',
    'Material_0225.mat.glb': 'Swan V23 Completion glints',
    'Material_0226.mat.glb': 'Swan V23 Steam volume',
    'Material_0227.mat.glb': 'Swan V23 Bench rolling white steam',
}
STATIC_ZERO_EMISSION_RESOURCE = RESOURCE_ROOT + 'Material_0225.mat.glb'
SOFT_NAMES = tuple('Swan V23 ' + name for name in (
    'Mobile VolumetricFire', 'Fine boiling bubbles', 'Fine turbulent combustion smoke',
    'Condensation wisps', 'Cylinder steam', 'Soft condensation steam',
    'Mobile soft particles', 'Soft chimney smoke', 'Hot steel contact sparks',
    'Layered turbulent coal flame', 'Solar soft halo', 'Lunar soft halo',
    'Beam 0', 'Headlamp soft glow', 'Beam 1', 'Low drifting desert haze',
    'Luminous white steam', 'Warm headlamp star',
))
RESOURCE_ONLY_NAMES = tuple('Swan V23 ' + name for name in (
    'Cylinder filling vapor', 'Completion glints', 'Steam volume', 'Bench rolling white steam',
))
LABEL_NAMES = Counter({'PART / 部件': 1, 'Part text': 3, '工作原理': 1, '观察要点': 1})


def read_glb_json(archive, path):
    entry = archive.getinfo(path)
    with archive.open(entry) as stream:
        magic, version, length = struct.unpack('<III', stream.read(12))
        if magic != 0x46546C67 or version != 2 or length != entry.file_size:
            raise ValueError('Invalid GLB header or length: ' + path)
        json_length, chunk_type = struct.unpack('<II', stream.read(8))
        if chunk_type != 0x4E4F534A or not 0 < json_length <= min(length - 20, 256 * 1024 * 1024):
            raise ValueError('Invalid or unexpectedly large GLB JSON chunk: ' + path)
        payload = stream.read(json_length)
        if len(payload) != json_length:
            raise ValueError('Truncated GLB JSON: ' + path)
        return json.loads(payload), {'path': path, 'glbBytes': length,
                                    'jsonBytes': json_length, 'zipCrc32': f'{entry.CRC:08x}'}


def read_main_json(apk):
    with zipfile.ZipFile(apk) as archive:
        return read_glb_json(archive, MAIN_GLB)


class Inspector:
    def __init__(self, gltf, report):
        self.gltf, self.report = gltf, report
        self.nodes = gltf.get('nodes', [])
        self.meshes = gltf.get('meshes', [])
        self.materials = gltf.get('materials', [])
        self.accessors = gltf.get('accessors', [])
        self.parents = {}
        for parent, node in enumerate(self.nodes):
            for child in node.get('children', []):
                if not isinstance(child, int) or not 0 <= child < len(self.nodes) or child in self.parents:
                    raise ValueError('Invalid or multiply parented GLB node')
                self.parents[child] = parent

    def check(self, condition, message):
        self.report['checks'].append({'passed': bool(condition), 'message': message})
        if not condition:
            self.report['failures'].append(message)
        return bool(condition)

    def named(self, name):
        return [i for i, node in enumerate(self.nodes) if node.get('name') == name]

    def descendants(self, root):
        found, pending = set(), [root]
        while pending:
            index = pending.pop()
            if index in found:
                raise ValueError('Cyclic GLB hierarchy')
            found.add(index)
            pending.extend(self.nodes[index].get('children', []))
        return sorted(found)

    def path(self, index):
        names, seen = [], set()
        while True:
            if index in seen:
                raise ValueError('Cyclic GLB parent chain')
            seen.add(index)
            names.append(self.nodes[index].get('name', f'node[{index}]'))
            if index not in self.parents:
                return '/'.join(reversed(names))
            index = self.parents[index]

    def primitives(self, index):
        mesh = self.nodes[index].get('mesh')
        return self.meshes[mesh].get('primitives', []) if mesh is not None else []

    def geometry_present(self, index):
        primitives = self.primitives(index)
        return bool(primitives) and all(
            p.get('mode', 4) == 4 and 'POSITION' in p.get('attributes', {})
            and self.accessors[p['attributes']['POSITION']].get('count', 0) > 0
            and 'indices' in p and self.accessors[p['indices']].get('count', 0) >= 3
            for p in primitives)

    def node_summary(self, index):
        node = self.nodes[index]
        return {'index': index, 'path': self.path(index), 'mesh': node.get('mesh'),
                'parent': self.parents.get(index), 'translation': node.get('translation'),
                'rotation': node.get('rotation'), 'scale': node.get('scale'),
                'extras': node.get('extras'),
                'materials': sorted({p['material'] for p in self.primitives(index) if 'material' in p})}

    def material_summary(self, index):
        material = self.materials[index]
        return {'index': index, 'name': material.get('name'),
                'alphaMode': material.get('alphaMode', 'OPAQUE'),
                'baseColorFactor': material.get('pbrMetallicRoughness', {}).get('baseColorFactor', [1, 1, 1, 1]),
                'baseColorTexture': material.get('pbrMetallicRoughness', {}).get('baseColorTexture'),
                'emissiveFactor': material.get('emissiveFactor'),
                'emissiveTexture': material.get('emissiveTexture'),
                'extensions': material.get('extensions', {})}

    def fps(self):
        roots = self.named('FPS visible mesh text')
        self.report['fps'] = {'roots': roots, 'nodes': [], 'materials': []}
        if not self.check(len(roots) == 1, 'Exactly one FPS mesh root is exported'):
            return
        root = roots[0]
        indices = self.descendants(root)
        expected = Counter(['FPS visible mesh text'] + [f'FPS digit {slot} value {digit}'
                           for slot in range(3) for digit in range(11)])
        self.check(Counter(self.nodes[i].get('name') for i in indices) == expected,
                   'FPS subtree contains the exact fixed label and 33 saved digit variants')
        self.check(root not in self.parents, 'FPS mesh root is independent of the movable stage')
        meshes = [i for i in indices if 'mesh' in self.nodes[i]]
        self.check(len(meshes) == 34 and all(self.geometry_present(i) for i in meshes),
                   'All 34 FPS mesh nodes retain nonempty triangle geometry')
        # 0.23.25/26 passed the nonempty check with broken fixed letters: their
        # 106 triangles had been reduced to 70. These authored 5x7 strokes must
        # survive the complete build/export pipeline, including inactive digits.
        glyph_triangles = [26, 14, 16, 14, 18, 14, 18, 14, 22, 18, 2]
        for index in meshes:
            name = self.nodes[index].get('name', '')
            expected_triangles = 106 if index == root else glyph_triangles[int(name.rsplit(' ', 1)[1])]
            actual = sum(self.accessors[p['indices']]['count'] // 3 for p in self.primitives(index))
            self.check(actual == expected_triangles,
                       f'FPS strokes intact: {name} has {actual}/{expected_triangles} triangles')
        material_ids = set()
        for index in meshes:
            valid = True
            for primitive in self.primitives(index):
                mid = primitive.get('material')
                if mid is None:
                    valid = False
                    continue
                material_ids.add(mid)
                material = self.materials[mid]
                color = material.get('pbrMetallicRoughness', {}).get('baseColorFactor', [1, 1, 1, 1])
                valid &= (material.get('name') == 'FpsGlyph'
                          and 'KHR_materials_unlit' in material.get('extensions', {})
                          and material.get('alphaMode', 'OPAQUE') == 'OPAQUE'
                          and len(color) == 4 and color[3] >= .999 and max(color[:3]) > .5)
            self.check(valid, f'FPS node {index} references opaque FpsGlyph with KHR_materials_unlit')
        self.report['fps']['nodes'] = [self.node_summary(i) for i in indices]
        self.report['fps']['materials'] = [self.material_summary(i) for i in sorted(material_ids)]

    def removed_worktops(self):
        items = []
        for parent_name, leaf_name in (
            ('Presentation backdrop', 'Neutral display floor'),
            ('Surface V2 baked environment', 'Baked local tabletop receiver'),
        ):
            indices = self.named(leaf_name)
            self.check(all(self.nodes[self.parents[i]].get('name') == parent_name
                           for i in indices if i in self.parents) and all(i in self.parents for i in indices),
                       f'{leaf_name}: any retained node has its verified original parent')
            self.check(len(indices) <= 1 and all('mesh' not in self.nodes[i] for i in indices),
                       f'{parent_name}/{leaf_name} is removed or has no mesh')
            items.append({'expectedPathSuffix': parent_name + '/' + leaf_name,
                          'state': 'node-removed' if not indices else 'node-retained',
                          'nodes': [self.node_summary(i) for i in indices]})
        self.report['removedWorktops'] = items

    def cards(self):
        anchors = self.named('Anchored teaching plaque V18')
        canvases = self.named('Floating part introduction')
        result = {'anchors': [self.node_summary(i) for i in anchors],
                  'canvases': [self.node_summary(i) for i in canvases], 'labels': [], 'surfaces': []}
        self.report['cards'] = result
        anchor_ok = self.check(len(anchors) == 1, 'Exactly one original Anchored teaching plaque V18 exists')
        canvas_ok = self.check(len(canvases) == 1, 'Exactly one Floating part introduction is statically exported')
        if not (anchor_ok and canvas_ok):
            return
        anchor, canvas = anchors[0], canvases[0]
        self.check(self.parents.get(canvas) == anchor,
                   'Exported card canvas is directly parented to its original teaching anchor')
        descendants = self.descendants(canvas)
        projected = [i for i in descendants if self.nodes[i].get('name') == 'Projected part cards']
        projected_ok = self.check(len(projected) == 1 and self.parents.get(projected[0]) == canvas,
                                  'One Projected part cards node retains its canvas parent')
        if projected_ok:
            labels = [i for i in self.nodes[projected[0]].get('children', [])
                      if self.nodes[i].get('name') in LABEL_NAMES]
            self.check(Counter(self.nodes[i].get('name') for i in labels) == LABEL_NAMES,
                       'All six authored TMP label nodes are exported under the same card root')
            for index in labels:
                primitives = self.primitives(index)
                font_materials = bool(primitives) and all(
                    'material' in p and 'TextMeshPro' in self.materials[p['material']].get('name', '')
                    and 'KHR_materials_unlit' in self.materials[p['material']].get('extensions', {})
                    for p in primitives)
                self.check(self.geometry_present(index) and font_materials,
                           f'Card label {index} retains nonempty TMP glyph geometry and unlit font material')
            result['labels'] = [self.node_summary(i) for i in labels]
        surfaces = [i for i in descendants if self.nodes[i].get('name', '').startswith('Swan card surface ')]
        expected_surfaces = Counter({'Swan card surface Purpose card': 1,
                                     'Swan card surface Observation card': 1,
                                     'Swan card surface Cyan fine edge': 12})
        self.check(Counter(self.nodes[i].get('name') for i in surfaces) == expected_surfaces
                   and all(self.geometry_present(i) for i in surfaces),
                   'Both panel backgrounds and twelve frame surfaces remain under the exported canvas')
        self.check(set(surfaces) == {i for i, node in enumerate(self.nodes)
                                    if node.get('name', '').startswith('Swan card surface ')},
                   'No detached duplicate card surfaces exist outside the prepared canvas')
        result['surfaces'] = [self.node_summary(i) for i in surfaces]
        result['materials'] = [self.material_summary(i) for i in sorted({
            p['material'] for node in descendants for p in self.primitives(node) if 'material' in p})]

    def texture_backed(self, info):
        if not isinstance(info, dict) or not isinstance(info.get('index'), int):
            return False
        textures, images, views = (self.gltf.get(k, []) for k in ('textures', 'images', 'bufferViews'))
        index = info['index']
        if not 0 <= index < len(textures):
            return False
        source = textures[index].get('source')
        if not isinstance(source, int) or not 0 <= source < len(images):
            return False
        image = images[source]
        view = image.get('bufferView')
        if not isinstance(view, int) or not 0 <= view < len(views):
            return False
        backing = views[view]
        buffers = self.gltf.get('buffers', [])
        buffer_index = backing.get('buffer')
        offset, length = backing.get('byteOffset', 0), backing.get('byteLength', 0)
        return (isinstance(buffer_index, int) and 0 <= buffer_index < len(buffers)
                and isinstance(offset, int) and isinstance(length, int) and offset >= 0 and length > 0
                and offset + length <= buffers[buffer_index].get('byteLength', 0))

    def soft_particles(self):
        selected = set()
        for name in SOFT_NAMES:
            matches = [i for i, material in enumerate(self.materials) if material.get('name') == name]
            self.check(len(matches) == 1, f'Main-scene soft material exists exactly once: {name}')
            selected.update(matches)
        # Resource-only effects are checked if they also occur in this main GLB.
        selected.update(i for i, m in enumerate(self.materials) if m.get('name') in RESOURCE_ONLY_NAMES)
        flow = [i for i, m in enumerate(self.materials) if m.get('name', '').startswith('Native flow ')]
        self.check(bool(flow), 'Main GLB includes the pre-exported Native flow palette')
        selected.update(flow)
        for index in sorted(selected):
            material = self.materials[index]
            self.check(material.get('alphaMode') == 'BLEND' and self.texture_backed(material.get('emissiveTexture')),
                       f'Soft material {index} ({material.get("name")}) exports BLEND and a backed emissiveTexture')
        self.report['softParticles'] = {
            'materials': [self.material_summary(i) for i in sorted(selected)],
            'resourceOnlyNotInMainGlb': [n for n in RESOURCE_ONLY_NAMES
                                       if not any(m.get('name') == n for m in self.materials)],
            'scope': 'Main GLB materials; independent resource coverage is reported separately. Image pixels are not inspected.',
        }

    def resource_materials(self, apk):
        expected_flow = {
            RESOURCE_ROOT + f'Flow_{r}_{g}_{b}_{level}.mat.glb':
                f'Native flow {r}-{g}-{b} alpha {level}'
            for r, g, b in FLOW_COLORS for level in range(8)
        }
        expected_soft = {RESOURCE_ROOT + filename: name for filename, name in SOFT_RESOURCE_FILES.items()}
        result = {'scope': 'All 32 saved flow palette GLBs and four verified resource-only soft material GLBs; JSON and texture backing references only.',
                  'expectedFlowResources': 32, 'expectedSoftResources': 4,
                  'flowResourcesPassed': 0, 'softResourcesPassed': 0, 'resources': []}
        self.report['independentMaterialResources'] = result
        with zipfile.ZipFile(apk) as archive:
            flow_paths = {n for n in archive.namelist() if n.startswith(RESOURCE_ROOT + 'Flow_') and n.endswith('.mat.glb')}
            self.check(flow_paths == set(expected_flow),
                       'APK contains exactly the 32 verified flow RGB/alpha palette resource paths')
            for path, expected_name in {**expected_flow, **expected_soft}.items():
                record = {'path': path, 'expectedMaterialName': expected_name, 'passed': False}
                result['resources'].append(record)
                try:
                    gltf, record['glb'] = read_glb_json(archive, path)
                    resource = Inspector(gltf, self.report)
                    record['materials'] = [resource.material_summary(i) for i in range(len(resource.materials))]
                    name_ok = self.check(len(resource.materials) == 1 and resource.materials[0].get('name') == expected_name,
                                         f'Resource path contains its exact expected material: {path}')
                    if not name_ok:
                        continue
                    material = resource.materials[0]
                    factor = material.get('emissiveFactor')
                    factor_ok = (isinstance(factor, list) and len(factor) == 3
                                 and all(isinstance(v, (int, float)) and math.isfinite(v) and v >= 0 for v in factor)
                                 and any(v > 0 for v in factor))
                    blend_ok = self.check(material.get('alphaMode') == 'BLEND',
                                          f'{expected_name}: independent resource uses BLEND')
                    texture_ok = self.check(resource.texture_backed(material.get('emissiveTexture')),
                                            f'{expected_name}: independent resource has a backed emissiveTexture')
                    if path == STATIC_ZERO_EMISSION_RESOURCE:
                        # This single original Particle effect has no authored emission.
                        # The bridge skips ParticleSystemRenderer; the Swan finish callback
                        # also bypasses StudioAssemblyFinishV9.CreateSparks. Do not add light
                        # merely to satisfy the dynamic-soft/flow shader-feature check.
                        effective = [0, 0, 0] if factor is None else factor
                        emission_ok = self.check(isinstance(effective, list) and len(effective) == 3
                                                 and all(isinstance(v, (int, float)) and math.isfinite(v) and v == 0
                                                         for v in effective),
                                                 f'{expected_name}: preserves authored zero emission (omitted factor means zero)')
                        record['emissiveFactorPolicy'] = 'source-preserved-zero; exact Particle exception'
                        record['effectiveEmissiveFactor'] = effective
                        record['zeroEmissionEvidence'] = {
                            'source': 'Assets/AtelierJourney/StudioVisual/FeedbackV10/Materials/Completion glints.mat',
                            'reason': 'Original SparkleV10 material has no emission property; Convert preserves black. ParticleSystemRenderer is excluded from dynamic material emission; Swan nativeBegin bypasses the original spark creation path.',
                            'code': ['Assets/SwanJourney/Editor/SwanJourneyMaterials.cs:Convert',
                                     'Assets/SwanJourney/Scripts/SwanJourneyVisualBridge.cs:Discover',
                                     'Assets/AtelierJourney/StudioVisual/StudioAssemblyFinishV9.cs:Begin'],
                            'scope': 'Only this exact path/name may retain zero emission. BLEND and embedded emission-mask backing remain required.',
                        }
                    else:
                        emission_ok = self.check(factor_ok,
                                                 f'{expected_name}: independent resource has an explicit nonzero emissiveFactor')
                        record['emissiveFactorPolicy'] = 'explicit-nonzero'
                    record['passed'] = blend_ok and texture_ok and emission_ok
                    if record['passed']:
                        key = 'flowResourcesPassed' if path in expected_flow else 'softResourcesPassed'
                        result[key] += 1
                except (OSError, KeyError, IndexError, TypeError, ValueError, struct.error) as error:
                    record['error'] = f'{type(error).__name__}: {error}'
                    self.check(False, f'Cannot inspect expected material resource {path}: {record["error"]}')

    def node_alignment(self, apk, report_path):
        alignment = json.loads(report_path.read_text(encoding='utf-8-sig'))
        result = {'sourceReport': str(report_path.resolve()), 'scenes': [],
                  'scope': 'Generated scene DFS indices and parent paths versus packaged GLBs; runtime tracker agreement is reported by the device separately.'}
        self.report['nodeAlignment'] = result
        self.check(alignment.get('passed') is True, 'Node alignment generation report passed')
        scenes = alignment.get('scenes', [])
        deferred_path = 'Assets/SwanJourney/Resources/SwanJourneyCanyon.prefab' if any(s.get('path','').endswith('.prefab') for s in scenes) else 'Assets/SwanJourney/Scenes/SteamJourneyCanyon.unity'
        required_paths = {'Assets/SwanJourney/Scenes/SteamJourneySwan.unity',deferred_path}
        self.check(len(scenes) == 2 and {s.get('path') for s in scenes} == required_paths,
                   'Alignment report covers exactly the main and deferred canyon scenes')
        with zipfile.ZipFile(apk) as archive:
            for scene in scenes:
                source_path = scene.get('path')
                if source_path not in required_paths:
                    self.check(False, 'Unexpected alignment-report scene path: ' + str(source_path))
                    continue
                path = 'assets/SpatialAdapterExport/' + source_path + '.glb'
                gltf, metadata = read_glb_json(archive, path)
                resource = Inspector(gltf, self.report)
                record = {'scene': source_path, 'glb': metadata, 'expectedNodes': scene.get('nodes'),
                          'packagedNodes': len(resource.nodes), 'keyNodes': []}
                result['scenes'].append(record)
                self.check(scene.get('zeroEditorOnly') is True and scene.get('zeroDontSaveInBuild') is True,
                           source_path + ': generation guard found no Player-only hierarchy stripping')
                self.check(scene.get('nodes') == len(resource.nodes), source_path + ': saved DFS count matches GLB node count')
                roots = [i for i in range(len(resource.nodes)) if i not in resource.parents]
                # SaveAsPrefabAsset normalizes the single root name to the
                # prefab filename; child DFS/index/parent relationships persist.
                prefab_root=Path(source_path).stem if source_path.endswith('.prefab') else None
                expected_roots=[prefab_root] if prefab_root else scene.get('rootOrder')
                self.check([resource.nodes[i].get('name') for i in roots] == expected_roots,
                           source_path + ': saved root order matches GLB root order')
                keys = scene.get('keyNodes', [])
                if source_path.endswith('/SteamJourneySwan.unity'):
                    expected_roles = {'stage', 'fps-controller', 'fps-visual', 'move-handle-root',
                                      'move-handle-surface', 'card-canvas', 'deferred-placement-anchor'}
                    expected_roles.update('card-label-' + str(i) for i in range(6))
                    self.check(Counter(k.get('role') for k in keys) == Counter(expected_roles),
                               'Main alignment report contains all 13 distinct runtime canaries')
                else:
                    self.check(bool(keys) and all(k.get('role') == 'deferred-root' for k in keys),
                               'Deferred alignment report includes its scene-local root indices')
                for key in keys:
                    key=dict(key)
                    if prefab_root:key['path']=prefab_root+('/'+key['path'].split('/',1)[1] if '/' in key['path'] else '')
                    index = key.get('sceneLocalIndex')
                    valid = isinstance(index, int) and 0 <= index < len(resource.nodes)
                    actual = resource.node_summary(index) if valid else None
                    record['keyNodes'].append({'expected': key, 'packaged': actual})
                    self.check(valid and actual['path'] == key.get('path')
                               and resource.parents.get(index, -1) == key.get('parentSceneLocalIndex'),
                               source_path + ': path/index/parent agree for ' + str(key.get('role')))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('apk', type=Path)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--expected-release', help='Release label supplied by the caller; manifest version is checked separately.')
    parser.add_argument('--node-alignment-report', type=Path, help='Generated node-alignment.json to compare against both packaged scene GLBs.')
    args = parser.parse_args()
    report = {'passed': False, 'expectedRelease': args.expected_release, 'apk': str(args.apk.resolve()),
              'nativeRenderingValidated': False, 'devicePerformanceMeasured': False,
              'scope': 'Packaged main GLB feedback changes and 36 independent material resources; not manifest version, runtime visibility, or interaction.',
              'checks': [], 'failures': []}
    try:
        apk_stat = args.apk.stat()
        report['apkBytes'] = apk_stat.st_size
        report['apkModifiedUtc'] = datetime.fromtimestamp(apk_stat.st_mtime, timezone.utc).isoformat()
        gltf, report['glb'] = read_main_json(args.apk)
        inspector = Inspector(gltf, report)
        for name in ('fps', 'removed_worktops', 'cards', 'soft_particles'):
            try:
                getattr(inspector, name)()
            except (KeyError, IndexError, TypeError, ValueError) as error:
                inspector.check(False, f'{name}: invalid exported structure: {error}')
        inspector.resource_materials(args.apk)
        if args.node_alignment_report:
            inspector.node_alignment(args.apk, args.node_alignment_report)
        else:
            report['nodeAlignment'] = {'checked': False, 'reason': '--node-alignment-report was not provided'}
        after_stat = args.apk.stat()
        inspector.check((after_stat.st_size, after_stat.st_mtime_ns) == (apk_stat.st_size, apk_stat.st_mtime_ns),
                        'APK size and modification time remain unchanged throughout inspection')
        report['passed'] = not report['failures']
    except (OSError, KeyError, IndexError, TypeError, ValueError, struct.error, zipfile.BadZipFile) as error:
        report['failures'].append(f'Cannot inspect main GLB: {type(error).__name__}: {error}')
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'passed': report['passed'], 'checks': len(report['checks']),
                      'failures': report['failures'], 'report': str(args.out.resolve())}, ensure_ascii=True))
    return 0 if report['passed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
