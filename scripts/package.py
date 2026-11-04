#!/usr/bin/env python3
"""Package only our binaries and original art, never game/reference libraries."""
import json
from pathlib import Path
import struct
import zipfile

root = Path(__file__).resolve().parents[1]
version = json.loads((root / 'manifest.json').read_text())['version_number']
binary = root / 'src' / 'Desfibrilator' / 'bin' / 'Release' / 'netstandard2.1'
for name in ('Desfibrilator.dll', 'Desfibrilator.Core.dll'):
    if not (binary / name).is_file():
        raise SystemExit(f'Missing {name}; build Release first.')

png = (root / 'icon.png').read_bytes()
if png[:8] != b'\x89PNG\r\n\x1a\n' or struct.unpack('>II', png[16:24]) != (256, 256):
    raise SystemExit('icon.png must be a 256x256 PNG.')
dist = root / 'dist'
dist.mkdir(exist_ok=True)
output = dist / f'ciriolopipado-Desfibrilator-{version}.zip'
with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as archive:
    for name in ('manifest.json', 'README.md', 'LICENSE', 'SECURITY.md'):
        archive.write(root / name, name)
    archive.writestr('icon.png', png)
    for name in ('Desfibrilator.dll', 'Desfibrilator.Core.dll'):
        archive.write(binary / name, 'BepInEx/plugins/Desfibrilator/' + name)
    archive.write(root / 'docs' / 'pruebas.md', 'PRUEBAS.md')
    archive.write(root / 'docs' / 'pruebas.md', 'docs/pruebas.md')
    archive.write(root / 'docs' / 'efectos-secundarios.md', 'docs/efectos-secundarios.md')
    archive.write(root / 'docs' / 'regresion-0.2.2.md', 'docs/regresion-0.2.2.md')
    archive.write(root / 'docs' / 'instalacion-local.txt', 'LEEME_INSTALACION.txt')
with zipfile.ZipFile(output) as archive:
    assert archive.testzip() is None
    assert len([n for n in archive.namelist() if n.endswith('.dll')]) == 2
    assert archive.read('icon.png')[:8] == b'\x89PNG\r\n\x1a\n'
    assert all(not n.startswith(('/', '../')) and '\\' not in n for n in archive.namelist())
    assert json.loads(archive.read('manifest.json'))['version_number'] == version
print(output)
