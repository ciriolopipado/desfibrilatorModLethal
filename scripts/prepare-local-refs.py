#!/usr/bin/env python3
"""Copy private compile references from a user's game and mod profile; never package them."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--managed', required=True, type=Path, help='Lethal Company_Data/Managed from v81')
parser.add_argument('--profile', required=True, type=Path, help='Thunderstore profile directory containing BepInEx')
parser.add_argument('--out', type=Path, default=Path(__file__).resolve().parents[1] / '.local' / 'v81-refs')
args = parser.parse_args()
sources = [p for p in args.managed.glob('*.dll') if (p.name.startswith(('Unity', 'Assembly-CSharp')) or p.name == 'DissonanceVoip.dll')]
for name in ('BepInEx.dll', '0Harmony.dll', 'LethalLib.dll'):
    candidates = list((args.profile / 'BepInEx').rglob(name))
    if len(candidates) != 1:
        parser.error(f'Expected one {name} in the profile, found {len(candidates)}')
    sources.append(candidates[0])
for name in ('Assembly-CSharp.dll', 'Unity.Netcode.Runtime.dll', 'Unity.InputSystem.dll', 'UnityEngine.CoreModule.dll', 'DissonanceVoip.dll'):
    if not any(p.name == name for p in sources):
        parser.error(f'Missing {name} in Managed directory')
args.out.mkdir(parents=True, exist_ok=True)
for source in sources:
    dest = args.out / source.name
    if dest.exists() and dest.read_bytes() != source.read_bytes():
        parser.error(f'Refusing to overwrite different reference {dest}; choose a new --out directory')
for source in sources:
    shutil.copyfile(source, args.out / source.name)
(args.out / 'provenance.json').write_text(json.dumps({'source': 'local-game', 'game_version_declared_by_user': '81',
    'hashes': {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sources}}, indent=2) + '\n')
print('References copied to', args.out.resolve())
print('Build with -p:LocalReferenceDirectory="' + str(args.out.resolve()) + '"')
