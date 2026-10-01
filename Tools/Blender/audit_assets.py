"""Read-only Blender source audit; run with --background --factory-startup --disable-autoexec."""
import argparse
import hashlib
import json
import struct
import sys
import traceback
from collections import Counter, defaultdict
from pathlib import Path

import bpy


def signature(path):
    with path.open('rb') as stream:
        header = stream.read(32)
    if header.startswith(b'Kaydara FBX Binary  \x00\x1a\x00'):
        kind = 'FBX_BINARY'
    elif header.startswith(b'glTF'):
        kind = 'GLB'
    elif header.startswith(b'BLENDER'):
        kind = 'BLEND'
    elif header.startswith(bytes.fromhex('28b52ffd')):
        kind = 'ZSTD_COMPRESSED'
    elif header.startswith(bytes.fromhex('1f8b')):
        kind = 'GZIP_COMPRESSED'
    else:
        kind = 'UNKNOWN'
    return kind, header.hex()


def checksum(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest().upper()


def glb_manifest(path):
    with path.open('rb') as stream:
        magic, version, length = struct.unpack('<4sII', stream.read(12))
        if magic != b'glTF' or version != 2 or length != path.stat().st_size:
            raise ValueError('Invalid GLB magic, version, or declared length')
        chunks = []
        manifest = None
        while stream.tell() < length:
            count, kind = struct.unpack('<II', stream.read(8))
            data = stream.read(count)
            if len(data) != count:
                raise ValueError('Truncated GLB chunk')
            chunks.append({'type': kind, 'bytes': count})
            if kind == 0x4E4F534A:
                manifest = json.loads(data.decode('utf-8'))
        if manifest is None:
            raise ValueError('GLB JSON chunk missing')
    return {
        'version': version, 'declared_bytes': length, 'chunks': chunks,
        'asset': manifest.get('asset'),
        'counts': {key: len(manifest.get(key, [])) for key in
                   ('meshes', 'materials', 'textures', 'images', 'skins', 'animations', 'nodes')},
        'images': manifest.get('images', []),
        'external_uris': [entry['uri'] for key in ('buffers', 'images')
                          for entry in manifest.get(key, []) if 'uri' in entry],
    }


class UnionFind:
    def __init__(self, size):
        self.parent = list(range(size))

    def find(self, index):
        while self.parent[index] != index:
            self.parent[index] = self.parent[self.parent[index]]
            index = self.parent[index]
        return index

    def union(self, left, right):
        left, right = self.find(left), self.find(right)
        if left != right:
            self.parent[right] = left


def vec(vector):
    return [round(float(value), 6) for value in vector]


def inspect_mesh(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    connected = UnionFind(len(mesh.vertices))
    for edge in mesh.edges:
        connected.union(*edge.vertices)
    welded = UnionFind(len(mesh.vertices))
    coordinate_owner = {}
    for vertex in mesh.vertices:
        coordinate = tuple(round(float(value), 6) for value in vertex.co)
        if coordinate in coordinate_owner:
            welded.union(vertex.index, coordinate_owner[coordinate])
        else:
            coordinate_owner[coordinate] = vertex.index
    for edge in mesh.edges:
        welded.union(*edge.vertices)
    groups = defaultdict(list)
    for vertex in mesh.vertices:
        groups[connected.find(vertex.index)].append(vertex.index)
    face_components = Counter(connected.find(poly.vertices[0]) for poly in mesh.polygons)
    components = []
    for root, indices in sorted(groups.items(), key=lambda item: len(item[1]), reverse=True):
        positions = [obj.matrix_world @ mesh.vertices[index].co for index in indices]
        components.append({
            'vertices': len(indices), 'polygons': face_components[root],
            'world_min': [round(min(v[axis] for v in positions), 6) for axis in range(3)],
            'world_max': [round(max(v[axis] for v in positions), 6) for axis in range(3)],
        })
    # Dung canh chung de dem mien vat lieu va UV, khong tach model theo so dinh.
    material_islands = UnionFind(len(mesh.polygons))
    uv_islands = UnionFind(len(mesh.polygons))
    edge_faces = defaultdict(list)
    active_uv = mesh.uv_layers.active
    for face in mesh.polygons:
        loops = list(face.loop_indices)
        for offset, loop_index in enumerate(loops):
            next_loop = loops[(offset + 1) % len(loops)]
            a = mesh.loops[loop_index].vertex_index
            b = mesh.loops[next_loop].vertex_index
            uv_edge = None
            if active_uv:
                uv_values = {
                    a: tuple(round(float(x), 5) for x in active_uv.data[loop_index].uv),
                    b: tuple(round(float(x), 5) for x in active_uv.data[next_loop].uv),
                }
                uv_edge = tuple((key, uv_values[key]) for key in sorted(uv_values))
            edge_faces[tuple(sorted((a, b)))].append((face.index, face.material_index, uv_edge))
    for entries in edge_faces.values():
        for left, right in zip(entries, entries[1:]):
            if left[1] == right[1]:
                material_islands.union(left[0], right[0])
            if active_uv and left[2] == right[2]:
                uv_islands.union(left[0], right[0])
    return {
        'object': obj.name, 'mesh': mesh.name,
        'vertices': len(mesh.vertices), 'edges': len(mesh.edges),
        'polygons': len(mesh.polygons), 'triangles': len(mesh.loop_triangles),
        'dimensions': vec(obj.dimensions), 'location': vec(obj.location),
        'rotation_euler': vec(obj.rotation_euler), 'scale': vec(obj.scale),
        'component_count': len(components), 'components_largest_24': components[:24],
        'coincident_vertices_1e6': len(mesh.vertices) - len(coordinate_owner),
        'coordinate_welded_component_count_1e6': len({welded.find(v.index) for v in mesh.vertices}),
        'material_island_count': len({material_islands.find(p.index) for p in mesh.polygons}),
        'uv_island_count': len({uv_islands.find(p.index) for p in mesh.polygons}) if active_uv else 0,
        'uv_layers': [uv.name for uv in mesh.uv_layers],
        'materials': [slot.material.name if slot.material else None for slot in obj.material_slots],
        'boundary_edges': sum(len(value) == 1 for value in edge_faces.values()),
        'nonmanifold_edges': sum(len(value) > 2 for value in edge_faces.values()),
        'zero_area_polygons': sum(poly.area < 1e-12 for poly in mesh.polygons),
        'vertex_groups': [group.name for group in obj.vertex_groups],
        'modifiers': [{'name': m.name, 'type': m.type} for m in obj.modifiers],
    }


def inspect_scene():
    bpy.context.view_layer.update()
    return {
        'loaded_blend_version': list(bpy.data.version),
        'objects': [{'name': obj.name, 'type': obj.type,
                     'parent': obj.parent.name if obj.parent else None}
                    for obj in bpy.context.scene.objects],
        'meshes': [inspect_mesh(obj) for obj in bpy.context.scene.objects if obj.type == 'MESH'],
        'armatures': [{'name': obj.name, 'bones': [bone.name for bone in obj.data.bones]}
                      for obj in bpy.context.scene.objects if obj.type == 'ARMATURE'],
        'actions': [{'name': action.name, 'frame_range': vec(action.frame_range)}
                    for action in bpy.data.actions],
        'images': [{'name': img.name, 'size': list(img.size), 'source': img.source,
                    'packed': bool(img.packed_file), 'filepath': img.filepath}
                   for img in bpy.data.images if img.type not in {'RENDER_RESULT', 'COMPOSITING'}],
        'materials': [{'name': mat.name, 'use_nodes': mat.use_nodes,
                       'image_nodes': [node.image.name for node in mat.node_tree.nodes
                                       if node.type == 'TEX_IMAGE' and node.image] if mat.use_nodes else []}
                      for mat in bpy.data.materials],
    }


def import_file(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if path.suffix.lower() == '.blend':
        return bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False, use_scripts=False)
    if path.suffix.lower() == '.fbx':
        return bpy.ops.import_scene.fbx(filepath=str(path), use_image_search=False)
    if path.suffix.lower() == '.glb':
        return bpy.ops.import_scene.gltf(filepath=str(path))
    raise ValueError('Unsupported extension')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    source = Path(args.source).resolve()
    output = Path(args.output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    report = {'blender_version': bpy.app.version_string, 'blender_binary': bpy.app.binary_path,
              'source': str(source), 'rendering': 'NONE_CPU_IMPORT_ONLY', 'assets': []}
    for path in sorted(source.iterdir()):
        if not path.is_file():
            continue
        kind, header = signature(path)
        entry = {'file': path.name, 'bytes': path.stat().st_size, 'sha256_before': checksum(path),
                 'signature': kind, 'header_hex_32': header}
        print('AUDIT_START ' + path.name, flush=True)
        if kind == 'GLB':
            try:
                entry['glb_manifest'] = glb_manifest(path)
            except Exception as error:
                entry['parse_error'] = str(error)
        try:
            result = import_file(path)
            entry['import_result'] = sorted(result)
            entry['declared_import_valid'] = 'FINISHED' in result
            if entry['declared_import_valid']:
                entry['scene'] = inspect_scene()
        except Exception as error:
            entry['declared_import_valid'] = False
            entry['import_error'] = str(error)
            entry['traceback'] = traceback.format_exc()
        entry['sha256_after'] = checksum(path)
        entry['source_unchanged'] = entry['sha256_before'] == entry['sha256_after']
        report['assets'].append(entry)
        output.write_text(json.dumps(report, indent=2), encoding='utf-8')
        print('AUDIT_DONE ' + path.name + ' valid=' + str(entry['declared_import_valid']), flush=True)
    duplicates = defaultdict(list)
    for entry in report['assets']:
        duplicates[entry['sha256_before']].append(entry['file'])
    report['exact_duplicate_groups'] = [items for items in duplicates.values() if len(items) > 1]
    output.write_text(json.dumps(report, indent=2), encoding='utf-8')
    print('AUDIT_COMPLETE ' + str(output), flush=True)


if __name__ == '__main__':
    main()
