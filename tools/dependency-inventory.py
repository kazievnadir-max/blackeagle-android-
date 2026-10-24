#!/usr/bin/env python3
"""Print package-declared licenses from a restored Android dependency graph.

This inventory is not a legal audit of bundled third-party code or fonts.
"""
import json
import pathlib
import sys
import xml.etree.ElementTree as ET

root = pathlib.Path(__file__).resolve().parents[1]
assets = root / 'clients/android/src/BlackEagle.Android/obj/project.assets.json'
graph = json.loads(assets.read_text())
folders = [pathlib.Path(p) for p in graph['packageFolders']]
print('# NuGet dependency inventory\n')
print('Package-declared licenses only; native components and embedded fonts require separate review.\n')
print('| Package | Declared license |')
print('| --- | --- |')
missing = []
for name, info in sorted(graph['libraries'].items()):
    if info['type'] != 'package':
        continue
    specs = [p for folder in folders for p in (folder / info['path']).glob('*.nuspec')]
    license_text = 'NOT FOUND'
    if specs:
        document = ET.parse(specs[0])
        for element in document.iter():
            if element.tag.split('}')[-1] == 'license':
                license_text = element.text or 'EMPTY'
    if license_text in ('NOT FOUND', 'EMPTY'):
        missing.append(name)
    print(f'| {name} | {license_text} |')
if missing:
    print('Missing declarations: ' + ', '.join(missing), file=sys.stderr)
    sys.exit(1)
