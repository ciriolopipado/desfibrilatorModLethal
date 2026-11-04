#!/usr/bin/env python3
"""Prepare a pinned API reference snapshot, NOT a verified copy of Lethal Company v81."""
import hashlib
import io
import json
from pathlib import Path
import tarfile
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / '.local' / 'refs'
CACHE = ROOT / '.local' / 'downloads'
COMMIT = 'bc9d11fbc412aca87f9478eebaedb3c739b97c74'
GAME_HASHES = {
    'Assembly-CSharp.dll': '1397015ad0c2f70632e73c4ab1209229129ffb5237aba0f190831f20e2f3ebd3',
    'Assembly-CSharp-firstpass.dll': '37ea3c9a0538112a0e6b18f67f1c8caeb60308c019e13986a51498c2402b5a41',
    'Unity.InputSystem.dll': '67684702463de1491246f3879ed7f26defd14b0999807d593492ff03958d56a2',
    'Unity.Netcode.Runtime.dll': '59a9aa5c074df90df0901ae36b3db9ef1c2024381b5a59db4f4995da95489c64',
}

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
    if provenance.exists() and json.loads(provenance.read_text()).get('source') != 'reference-snapshot':
        raise RuntimeError('Refusing to overwrite local game references with a reference snapshot')
    packages = [
        ('https://github.com/BepInEx/BepInEx/releases/download/v5.4.21/BepInEx_unix_5.4.21.0.zip',
         'bepinex-5.4.21.zip', '8e5cec2cda757e5ebffc59d8833379776510c364111b8f5ec0f7c3ba6a6953cf'),
        ('https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg',
         'unity-2021.3.33.nupkg', 'd32d34526d89958c63220d3a5ce26c5c44c11bfd411a2e1fbc3200088ef160c4'),
        ('https://api.nuget.org/v3-flatcontainer/evaisa.lethallib/1.2.0/evaisa.lethallib.1.2.0.nupkg',
         'lethallib-1.2.0.nupkg', 'e52d3b71cb08db9bd2f86c558c8fdcf52057653eff1755baf91f03ea61105630'),
    ]
    prepared = {}
    for url, name, digest in packages:
        with zipfile.ZipFile(io.BytesIO(fetch(url, name, digest))) as archive:
            for member in archive.namelist():
                leaf = member.rsplit('/', 1)[-1]
                if leaf.endswith('.dll') and ('/core/' in member or member.startswith('lib/')):
                    if leaf.startswith('UnityEngine') or leaf in ('BepInEx.dll', '0Harmony.dll', 'LethalLib.dll'):
                        prepared[leaf] = archive.read(member)
    with tarfile.open(fileobj=io.BytesIO(fetch(f'https://codeload.github.com/EvaisaDev/LethalLib/tar.gz/{COMMIT}', 'lethallib-source.tar.gz')), mode='r:gz') as archive:
        for name, digest in GAME_HASHES.items():
            member = archive.getmember(f'LethalLib-{COMMIT}/deps/{name}')
            data = archive.extractfile(member).read()
            if hashlib.sha256(data).hexdigest() != digest:
                raise RuntimeError(f'Reference checksum mismatch: {name}')
            prepared[name] = data
    for name, data in prepared.items():
        path = DEST / name
        if path.exists() and path.read_bytes() != data:
            raise RuntimeError(f'Refusing to overwrite different reference: {name}. Use a separate directory.')
    for name, data in prepared.items():
        (DEST / name).write_bytes(data)
    provenance.write_text(json.dumps({'source': 'reference-snapshot', 'lethallib_commit': COMMIT,
        'verified_game_version': None, 'notice': 'LethalLib v80-era game API snapshot + Unity 2021 reference modules; not v81 validation.'}, indent=2) + '\n')
    print(f'Prepared {len(prepared)} reference assemblies. Compile checks only; v81 runtime remains unverified.')

if __name__ == '__main__':
    main()
