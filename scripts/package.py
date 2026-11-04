#!/usr/bin/env python3
"""Package only our binaries and original art, never game/reference libraries."""
import json
from pathlib import Path
import struct
import zipfile
import zlib

root = Path(__file__).resolve().parents[1]
version = json.loads((root / 'manifest.json').read_text())['version_number']
binary = root / 'src' / 'Desfibrilator' / 'bin' / 'Release' / 'netstandard2.1'
for name in ('Desfibrilator.dll', 'Desfibrilator.Core.dll'):
    if not (binary / name).is_file():
        raise SystemExit(f'Missing {name}; build Release first.')

def chunk(kind, data):
    return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)

# Original 256x256 icon, matching the orange case and screen of the procedural model.
rows = []
for y in range(256):
    row = bytearray([0])
    for x in range(256):
        color = (26, 29, 32, 255)
        if 24 <= x < 232 and 52 <= y < 220: color = (225, 95, 14, 255)
        if 40 <= x < 124 and 80 <= y < 155: color = (22, 54, 43, 255)
        if 48 <= x < 116 and 112 <= y < 119: color = (85, 230, 115, 255)
        if (163 <= x < 181 and 83 <= y < 156) or (139 <= x < 205 and 110 <= y < 129): color = (244, 238, 218, 255)
        if 80 <= x < 176 and 27 <= y < 44: color = (180, 185, 180, 255)
        row.extend(color)
    rows.append(bytes(row))
png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', 256, 256, 8, 6, 0, 0, 0))
png += chunk(b'IDAT', zlib.compress(b''.join(rows))) + chunk(b'IEND', b'')
dist = root / 'dist'
dist.mkdir(exist_ok=True)
output = dist / f'ciriolopipado-Desfibrilator-{version}.zip'
with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as archive:
    for name in ('manifest.json', 'README.md', 'LICENSE'):
        archive.write(root / name, name)
    archive.writestr('icon.png', png)
    for name in ('Desfibrilator.dll', 'Desfibrilator.Core.dll'):
        archive.write(binary / name, 'BepInEx/plugins/Desfibrilator/' + name)
    archive.write(root / 'docs' / 'pruebas.md', 'PRUEBAS.md')
    archive.write(root / 'docs' / 'instalacion-local.txt', 'LEEME_INSTALACION.txt')
with zipfile.ZipFile(output) as archive:
    assert archive.testzip() is None
    assert len([n for n in archive.namelist() if n.endswith('.dll')]) == 2
    assert archive.read('icon.png')[:8] == b'\x89PNG\r\n\x1a\n'
    assert all(not n.startswith(('/', '../')) and '\\' not in n for n in archive.namelist())
    assert json.loads(archive.read('manifest.json'))['version_number'] == version
print(output)
