#!/usr/bin/env python3
"""Prepare pinned public v81 API references; this does not execute or validate the game."""
import hashlib
import io
import json
from pathlib import Path
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / '.local' / 'reference-v81'
CACHE = ROOT / '.local' / 'downloads'
GAME_VERSION = '81.0.5-ngd.0'
GAME_SHA256 = 'b1d8bbd0a01172300db6a4a1f499c59db6756cc2a39d31eefd7d932c22d85657'

def fetch(url, name, digest=None):
    path = CACHE / name
    if not path.exists():
        with urllib.request.urlopen(url, timeout=120) as response:
            data = response.read()
        if digest and hashlib.sha256(data).hexdigest() != digest:
            raise RuntimeError(f'Checksum mismatch: {name}')
        path.write_bytes(data)
    data = path.read_bytes()
    if digest and hashlib.sha256(data).hexdigest() != digest:
        raise RuntimeError(f'Checksum mismatch: {name}; remove the cached file and retry')
    return data

def main():
    DEST.mkdir(parents=True, exist_ok=True)
    CACHE.mkdir(parents=True, exist_ok=True)
    provenance = DEST / 'provenance.json'
    if provenance.exists() and json.loads(provenance.read_text()).get('source') != 'reference-v81-nuget':
        raise RuntimeError('Refusing to overwrite local game references with a reference snapshot')
    packages = [
        ('https://github.com/BepInEx/BepInEx/releases/download/v5.4.21/BepInEx_unix_5.4.21.0.zip',
         'bepinex-5.4.21.zip', '8e5cec2cda757e5ebffc59d8833379776510c364111b8f5ec0f7c3ba6a6953cf'),
        ('https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg',
         'unity-2021.3.33.nupkg', 'd32d34526d89958c63220d3a5ce26c5c44c11bfd411a2e1fbc3200088ef160c4'),
        ('https://api.nuget.org/v3-flatcontainer/evaisa.lethallib/1.2.0/evaisa.lethallib.1.2.0.nupkg',
         'lethallib-1.2.0.nupkg', 'e52d3b71cb08db9bd2f86c558c8fdcf52057653eff1755baf91f03ea61105630'),
    ]
    packages.append((f'https://api.nuget.org/v3-flatcontainer/lethalcompany.gamelibs.steam/{GAME_VERSION}/lethalcompany.gamelibs.steam.{GAME_VERSION}.nupkg',
                     f'gamelibs-{GAME_VERSION}.nupkg', GAME_SHA256))
    prepared = {}
    for url, name, digest in packages:
        with zipfile.ZipFile(io.BytesIO(fetch(url, name, digest))) as archive:
            for member in archive.namelist():
                leaf = member.rsplit('/', 1)[-1]
                if leaf.endswith('.dll') and ('/core/' in member or member.startswith(('lib/', 'ref/netstandard2.1/'))):
                    if member.startswith('ref/netstandard2.1/') or leaf.startswith('UnityEngine') or leaf in ('BepInEx.dll', '0Harmony.dll', 'LethalLib.dll'):
                        prepared[leaf] = archive.read(member)
    for name, data in prepared.items():
        path = DEST / name
        if path.exists() and path.read_bytes() != data:
            raise RuntimeError(f'Refusing to overwrite different reference: {name}. Use a separate directory.')
    for name, data in prepared.items():
        (DEST / name).write_bytes(data)
    provenance.write_text(json.dumps({'source': 'reference-v81-nuget', 'game_package': 'LethalCompany.GameLibs.Steam',
        'game_reference_version': GAME_VERSION, 'game_package_sha256': GAME_SHA256,
        'unity_reference_modules': '2021.3.33', 'game_executed': False,
        'notice': 'v81 publicized game API references; Unity module reference API 2021.3.33; not a game integration test.'}, indent=2) + '\n')
    print(f'Prepared {len(prepared)} reference assemblies for game API {GAME_VERSION}. Game execution remains unverified.')

if __name__ == '__main__':
    main()
