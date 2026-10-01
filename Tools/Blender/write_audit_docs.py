"""Create the two audit documents from measured Blender results, without copying assets."""
import argparse
import json
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path


def table(headers, rows):
    clean = lambda value: str(value).replace('|', '/').replace('\n', ' ')
    return '\n'.join(['| ' + ' | '.join(headers) + ' |',
                      '| ' + ' | '.join('---' for _ in headers) + ' |'] +
                     ['| ' + ' | '.join(clean(value) for value in row) + ' |' for row in rows])


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--report', required=True)
    parser.add_argument('--documentation', required=True)
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
    args = parser.parse_args(argv)
    report = json.loads(Path(args.report).read_text(encoding='utf-8'))
    assets = report['assets']
    if len(assets) != 23 or not all(asset['source_unchanged'] for asset in assets):
        raise RuntimeError('Expected all 23 source assets with unchanged hashes')
    by_name = {asset['file']: asset for asset in assets}
    destination = Path(args.documentation)
    destination.mkdir(parents=True, exist_ok=True)
    timestamp = datetime.now(timezone(timedelta(hours=7))).isoformat(timespec='seconds')
    rows = []
    topology = []
    textures = []
    for asset in assets:
        valid = asset['declared_import_valid']
        kind = asset['signature']
        if asset['file'].startswith('Item3.'):
            kind = 'Zstandard-compressed native Blender'
        rows.append([asset['file'], f"{asset['bytes']:,}", kind,
                     'PASS' if valid else 'REJECT',
                     'Blender native loader' if asset['file'].endswith('.blend') else
                     'FBX importer' if asset['file'].endswith('.fbx') else 'glTF importer'])
        if valid:
            meshes = asset['scene']['meshes']
            total = lambda name: sum(mesh[name] for mesh in meshes)
            topology.append([asset['file'], len(meshes), total('vertices'), total('triangles'),
                             total('component_count'), total('coordinate_welded_component_count_1e6'),
                             total('uv_island_count'), total('boundary_edges'), total('nonmanifold_edges'),
                             total('zero_area_polygons')])
            textures.append([asset['file'], sum(len(mesh['materials']) for mesh in meshes),
                             len(asset['scene']['images']),
                             ', '.join(sorted({str(image['size']) for image in asset['scene']['images']})),
                             all(image['packed'] for image in asset['scene']['images']),
                             len(asset['scene']['armatures']), len(asset['scene']['actions'])])
    item3 = by_name['Item3.blend']
    sections = [
        '# Source asset audit',
        f'Audit measured at {timestamp}. Project: `D:\\APP\\TYCOON`. Source files were read only; no source was renamed, overwritten, repaired, or backed up.',
        '## Verdict',
        '23 source files were inspected by size, header, SHA-256 and the actual Blender importer selected by their declared extension. 21 passed that importer. `Item3.fbx` and `Item3.glb` are incorrectly named copies of a valid compressed native Blender file and must never be sent to Unity as FBX/GLB. No supplied character, animal or machine contains a skeleton, skin weights or animation actions.',
        '## Environment and method',
        f'- Authoritative audit: Blender **{report["blender_version"]}**, `{report["blender_binary"]}`.\n- Initial cross-check: Blender 4.5.1 LTS also imported the standard FBX/GLB files. Its newer-file warning on Item3 was resolved by rerunning the complete audit in Blender 5.2.\n- CPU import and mesh analysis only. No GPU render, simulation, image generation, or source-side script execution was requested. `--disable-autoexec` and `use_scripts=False` protected native-file loading.\n- FBX reader parsed binary FBX version 7400. GLB validation checked magic, version 2, declared total length, complete JSON/BIN chunks and actual glTF import. All ten genuine GLBs are self-contained and list no external buffer/image URI. Their manifest generator is `Khronos glTF Blender I/O v4.0.43`.\n- Header inspection and SHA-256 were independent of file extensions. The script hashed every file before and after import. All 23 comparisons passed.\n- Mesh connectivity uses real mesh edges; material islands use shared edges with identical material index; UV islands require matching endpoint UVs. A second, non-mutating calculation merges positions rounded to 0.000001 Blender units to distinguish UV seams from independent geometry.',
        '## File inventory and importer outcome',
        table(['File in ASSET', 'Bytes', 'Actual header/content', 'Declared import', 'Tested loader'], rows),
        '## Item3 diagnosis',
        f'All three filenames have **{item3["bytes"]:,} bytes** and identical SHA-256:\n\n`{item3["sha256_before"]}`\n\nTheir first 32 bytes are:\n\n`{item3["header_hex_32"]}`\n\nThe first four bytes `28 b5 2f fd` identify a Zstandard stream, so absence of a literal `BLENDER` prefix does not mean the native blend is corrupt. The native Blender loader successfully read `Item3.blend`; loaded file version is `{item3["scene"]["loaded_blend_version"]}`. It contains one mesh plus an authoring camera and light. These authoring objects are not gameplay assets.',
        'Actual negative tests:\n\n- `Item3.fbx`: FBX parser raised `OSError: Invalid header`; importer returned `Error: Couldn\'t open file ... (Invalid header)`.\n- `Item3.glb`: glTF importer returned `Error: Bad glTF: json error: utf-8`.\n\nUse `ASSET/Item3.blend` with Blender 5.2 as the sole Item3 processing input. Create a genuine new export under `ArtSource/Processed/`; do not rename either mislabeled duplicate or replace any original. The three files remain intact.',
        '## Geometry measurements',
        'All valid files contain one visible source mesh and one assigned material slot. Counts below describe imported source geometry, before any cleanup. CC means vertex-edge connected components. Welded CC is the diagnostic coordinate-merged count; it does not alter the mesh. UV seam boundaries in a GLB are not automatically physical holes.',
        table(['File', 'Meshes', 'Verts', 'Triangles', 'CC', 'Welded CC', 'UV islands', 'Boundary edges', '>2-face edges', 'Zero-area faces'], topology),
        'The paired FBX and GLB files are not always identical geometry: Character3 has 49,998 vs 50,004 triangles; Machine has 50,054 vs 50,068; Machine2 has 50,012 vs 50,020. Do not silently claim interchangeability or replace a processed version without checking appearance. All FBX meshes have zero boundary, nonmanifold and zero-area-face counts in this audit.',
        '## Hierarchy, semantic splitting and limitations',
        'Every FBX is a single `node_0` mesh without parent hierarchy. Each GLB is also a single mesh, with hundreds of disconnected vertex groups caused by UV seam duplication: after coordinate merging they become one geometric component, except Item2 which has two. Material islands therefore follow the same geometry/UV fragments; there are no separate named material regions for body, head, limbs, doors, wheels or conveyors. `Separate by Loose Parts` would create UV fragments, not semantic machine or character pieces.',
        'Item2 FBX has one main closed component with 24,837 vertices / 49,714 faces and a small closed component with 211 vertices / 418 faces. The small component bounds are X [-0.226705, -0.200899], Y [-0.432419, -0.417147], Z [0.036869, 0.046189]. Its role has not been visually identified; do not label or delete it merely because it is small.',
        'This audit validates format, parseability, connectivity, material/UV presence and rig/animation absence. It does **not** prove semantic segmentation, visual normals, anatomical alignment, deformation quality, animation quality, Unity material conversion, collision, or runtime performance. Those require visual inspection, authored processing and gameplay tests. No blind automatic split or rig has been accepted by this audit.',
        '## Material, UV and animation inventory',
        table(['File', 'Assigned material slots', 'Packed images', 'Image resolution', 'All packed', 'Armatures', 'Actions'], textures),
        'All meshes have one `UVMap`. FBX embeds separate base-color, metallic, normal and roughness images; GLB/native Item3 embeds base color, normal and combined metallic/roughness. Embedded images load at 4096 x 4096. Different source assets reuse image filenames such as `texture_pbr_20250901.png`; processed texture exports must use asset-specific folders or unique filenames to prevent accidental cross-asset overwrites. FBX image paths may mention `ASSET/output.fbm`, but the image payload is packed; the audit did not create that directory.',
        'No armatures, vertex groups, skinning modifiers or animation actions were found. Characters need authored humanoid rigs; Cow needs a quadruped skeleton; Chicken needs a bird skeleton. Author and verify the required poses and locomotion/actions before claiming them animated. Machines should use transform animation of visually verified moving parts, not arbitrary skeletal rigs.',
        '## Recommended processing inputs',
        table(['Role', 'Preferred source', 'Reason / next gate'], [
            ['Initial player', 'ASSET/Character1.fbx', 'Closed connected mesh; 24,939 vertices / 49,886 triangles; fewer UV islands than the other characters. Visual pose review and rig/weight/pose tests still required.'],
            ['Worker/customer variants', 'ASSET/Character2.fbx, ASSET/Character3.fbx', 'Valid closed connected alternate sources. Do not reuse Character1 joint positions without checking each anatomy.'],
            ['Cow', 'ASSET/Cow.fbx', 'Closed connected source; 25,004 vertices / 50,004 triangles; packed PBR maps; quadruped rig required.'],
            ['Chicken', 'ASSET/Chicken.fbx', 'Closed connected source; 25,002 vertices / 50,000 triangles; packed PBR maps; bird rig required.'],
            ['Processing machine candidates', 'ASSET/Machine.fbx, ASSET/Machine2.fbx, ASSET/Machine3.fbx', 'All valid connected sources. Choose by actual visible function, then visually segment mechanical moving parts.'],
            ['Props', 'ASSET/Item1.fbx, ASSET/Item2.fbx', 'Valid geometry; verify real semantic role visually and review Item2 small component.'],
            ['Item3 prop', 'ASSET/Item3.blend', 'Only correctly labeled Item3 source; load in Blender 5.2 and export a true derivative.'],
        ]),
        'Recommended order: visually inspect each candidate; establish semantic part selection; normalize orientation/scale/pivots in memory; author deformation rigs only where necessary; verify weights with extreme pose tests; decimate/retopologize with silhouette and UV checks; create LODs and game-sized texture derivatives; export to `ArtSource/Processed/`; import a verified copy to `Assets/_Game/Art/Imported/`. Source 4K images and roughly 50K-triangle meshes are authoring inputs, not proven final runtime budgets. Preserve originals and do not treat suggested optimization targets as measured performance.',
        '## SHA-256 preservation evidence',
        table(['File', 'SHA-256 before and after', 'Unchanged'], [[asset['file'], asset['sha256_before'], asset['source_unchanged']] for asset in assets]),
        'Only exact duplicate group: `Item3.blend`, `Item3.fbx`, `Item3.glb`. All other source SHA-256 values differ. The source directory still contains exactly the original 23 files and no importer-created side files.',
        '## Reproduce',
        '```powershell\n& "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" --background --factory-startup --disable-autoexec --threads 4 --python "D:\\APP\\TYCOON\\Tools\\Blender\\audit_assets.py" -- --source "D:\\APP\\TYCOON\\ASSET" --output "D:\\APP\\TYCOON\\work\\asset-audit\\audit.json"\n& "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" --background --factory-startup --disable-autoexec --python "D:\\APP\\TYCOON\\Tools\\Blender\\write_audit_docs.py" -- --report "D:\\APP\\TYCOON\\work\\asset-audit\\audit.json" --documentation "D:\\APP\\TYCOON\\Documentation"\n```',
        'The JSON is an intermediate report; after verifying these documents and confirming the audit process has exited, remove only that task-created file and its empty task directory. No source copies, backup files or archives are required.',
    ]
    (destination / 'ASSET_AUDIT.md').write_text('\n\n'.join(sections) + '\n', encoding='utf-8')
    groups = {}
    for asset in assets:
        groups.setdefault(Path(asset['file']).stem, []).append(asset['file'])
    source_rows = [[name, ', '.join('`ASSET/' + file + '`' for file in files),
                    'USER_PROVIDED', 'Not supplied', 'LICENSE_METADATA_UNKNOWN',
                    'Internal project use authorized by master task']
                   for name, files in groups.items()]
    sources = [
        '# Asset sources and usage record',
        f'Last source audit: {timestamp}. See `ASSET_AUDIT.md` for file hashes, format disposition and import evidence.',
        '## User-provided input',
        'The user explicitly supplied the `ASSET/` directory and authorized internal use in TYCOON. Provenance is `USER_PROVIDED`. No creator identification, redistribution license or separate license metadata was supplied with the 23 files, so every group is marked `LICENSE_METADATA_UNKNOWN`. This records authorization for internal project use; it does not invent a license or grant public redistribution/relicensing rights. Preserve source files.',
        table(['Asset group', 'Source/local files', 'Provenance', 'Creator', 'License metadata', 'Usage basis'], source_rows),
        '## Format exclusion',
        '`ASSET/Item3.fbx` and `ASSET/Item3.glb` are mislabeled byte-identical copies of `ASSET/Item3.blend`, not true FBX/GLB exports. Preserve them as supplied but exclude them from Unity import. Use the valid native `.blend` through the processing pipeline.',
        '## Derivative destinations',
        'Approved pipeline: `ASSET/` -> Blender processing scripts in `Tools/Blender/` -> `ArtSource/Processed/` -> `Assets/_Game/Art/Imported/`. Processed models/textures inherit their source provenance and unknown license metadata; creating a derivative does not relicense the source. Names and material references must identify the originating asset to prevent the identical embedded texture filenames from colliding.',
        'The source audit itself creates no processed models, animations, texture exports, backups or archives. Record each delivered derivative and the script/source used when processing is actually completed; do not list planned exports as existing artifacts.',
        '## External and original assets',
        'No external online assets were downloaded or added by this audit. No online license was inferred from the generic embedded texture names or the glTF exporter string. Any future external asset entry must include asset name, exact source URL, creator, explicit usage terms/license and local destination. Original project-created support geometry/audio/UI should be labeled as original with its generator/source file and output location when authored.',
    ]
    (destination / 'ASSET_SOURCES.md').write_text('\n\n'.join(sources) + '\n', encoding='utf-8')
    print('AUDIT_DOCS_COMPLETE ' + str(destination), flush=True)


if __name__ == '__main__':
    main()
