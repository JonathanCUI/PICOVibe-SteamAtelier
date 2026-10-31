"""Summarize application diagnostic windows; no trace capture or device mutation."""
import argparse
import json
import re
from pathlib import Path


def summarize(text):
    windows = []
    transitions = []
    for line in text.splitlines():
        if 'SWAN_JOURNEY_DEFERRED_PHASE ' in line:
            fields = dict(re.findall(r'(\w+)=([^\s]+)', line))
            transitions.append({key: fields[key] for key in
                                ('attempt', 'phase', 'elapsedSeconds', 'sincePreviousSeconds', 'frame') if key in fields})
            continue
        if 'SWAN_JOURNEY_CPU_WINDOW ' not in line:
            continue
        fields = dict(re.findall(r'(\w+)=([^\s]+)', line))
        if not all(key in fields for key in ('mode', 'frames', 'twoHandFrames', 'phase')):
            continue
        stages = {}
        for key, value in fields.items():
            if not key.endswith('Ms') or value.count('/') != 2:
                continue
            mean, maximum, calls = value.split('/')
            stages[key[:-2]] = {'meanMsPerFrame': float(mean), 'maxSingleCallMs': float(maximum), 'calls': int(calls)}
        windows.append({'logPrefix': line.split('SWAN_JOURNEY_CPU_WINDOW', 1)[0].strip(),
                        'mode': fields['mode'], 'phase': fields['phase'], 'frames': int(fields['frames']),
                        'twoHandFrames': int(fields['twoHandFrames']),
                        'lateRecordedMs': None if fields.get('lateRecordedMs') == 'unavailable' else float(fields['lateRecordedMs']),
                        'measuredCallbackMs': float(fields['measuredCallbackMs']), 'stages': stages})
        if 'elapsedSeconds' in fields:
            windows[-1].update(elapsedSeconds=float(fields['elapsedSeconds']), unityFps=float(fields['unityFps']),
                               longestFrameMs=float(fields['longestFrameMs']), framesAtLeast50ms=int(fields['framesAtLeast50ms']))
    groups = []
    for mode, phase in sorted({(w['mode'], w['phase']) for w in windows}):
        selected = [w for w in windows if w['mode'] == mode and w['phase'] == phase]
        frames = sum(w['frames'] for w in selected)
        stages = []
        for name in {name for w in selected for name in w['stages']}:
            present = [w for w in selected if name in w['stages']]
            stages.append({'stage': name,
                           'meanMsPerFrame': round(sum(w['stages'][name]['meanMsPerFrame'] * w['frames'] for w in present) / frames, 4),
                           'maxSingleCallMs': max(w['stages'][name]['maxSingleCallMs'] for w in present),
                           'calls': sum(w['stages'][name]['calls'] for w in present)})
        groups.append({'mode': mode, 'phase': phase, 'windows': len(selected), 'frames': frames,
                       'stages': sorted(stages, key=lambda s: s['meanMsPerFrame'], reverse=True)})
        timed = [w for w in selected if 'elapsedSeconds' in w]
        if timed:
            groups[-1].update(timedWindows=len(timed),
                              unityFps=round(sum(w['frames'] for w in timed) / sum(w['elapsedSeconds'] for w in timed), 2),
                              longestFrameMs=max(w['longestFrameMs'] for w in timed),
                              framesAtLeast50ms=sum(w['framesAtLeast50ms'] for w in timed))
    return {'windows': windows, 'groups': groups, 'transitions': transitions,
            'limitations': ['These are callback wall times, including nested native calls or waits.',
                            'Unity frame cadence excludes load windows and is distinct from native display frame rate.',
                            'Only selected callbacks are measured; missing time is not proven SDK cost.',
                            'Mixed windows are kept separate from idle and all-two-hand windows.',
                            'ProfilerRecorder values refer to the previous frame; use window aggregates only.']}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('log', type=Path)
    parser.add_argument('--out', type=Path)
    args = parser.parse_args()
    result = summarize(args.log.read_text(encoding='utf-8-sig', errors='replace'))
    rendered = json.dumps(result, ensure_ascii=False, indent=2)
    if args.out:
        args.out.write_text(rendered, encoding='utf-8')
    print(rendered)
