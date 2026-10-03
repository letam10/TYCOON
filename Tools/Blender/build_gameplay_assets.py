"""Source-preserving runtime exports and a stylized farm/shop kit.

Blender --background --factory-startup --disable-autoexec --threads 4
--python-exit-code 1 --python Tools/Blender/build_gameplay_assets.py
No originals are saved, no automatic backups and no GPU render are used.
"""
import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Quaternion, Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'ArtSource/Processed/Gameplay'
MODELS = ROOT / 'Assets/_Game/Art/Gameplay/Models'
TEXTURES = ROOT / 'Assets/_Game/Art/Gameplay/Textures'
WORK = ROOT / 'work/session-20261003/asset-pipeline'
SOURCES = {
    'Character1': (1.80, 36000), 'Character2': (1.82, 36000),
    'Character3': (1.78, 36000), 'Cow': (1.35, 25000),
    'Chicken': (.64, 18000), 'Item1': (.85, 28000),
    'Item2': (1.10, 28000), 'Machine': (1.90, 32000),
    'Machine2': (1.35, 32000), 'Machine3': (1.65, 32000),
}
CLIPS = {'Idle': 60, 'Walk': 30, 'Run': 24, 'CarryIdle': 60,
         'CarryWalk': 36, 'Pickup': 36, 'Interact': 48}
PALETTE = {
    'FoliageLime': (.45, .80, .035, 1), 'FoliageLight': (.64, .90, .07, 1),
    'Grass': (.36, .76, .10, 1), 'WoodHoney': (.55, .32, .12, 1),
    'WoodLight': (.75, .52, .26, 1), 'RailBlue': (.02, .46, .87, 1),
    'RailYellow': (1, .78, .06, 1), 'BarnRed': (.76, .12, .09, 1),
    'Cream': (.97, .86, .66, 1), 'RoofSlate': (.08, .23, .32, 1),
    'MetalGrey': (.45, .53, .55, 1), 'ScreenDark': (.015, .10, .15, 1),
    'ScreenGreen': (.32, .85, .19, 1), 'MilkIvory': (.99, .96, .89, 1),
    'CapRed': (.94, .15, .10, 1), 'LabelBlue': (.04, .42, .80, 1),
    'EggCream': (.99, .87, .61, 1), 'CarrotOrange': (1, .30, .025, 1),
    'ProduceGreen': (.20, .61, .08, 1), 'CornGold': (1, .75, .02, 1),
    'CabbageLight': (.46, .77, .22, 1), 'TomatoRed': (.90, .075, .025, 1),
    'WheatGold': (.86, .62, .18, 1), 'FlourBag': (.94, .79, .49, 1),
    'BreadCrust': (.66, .30, .065, 1), 'BreadGold': (.92, .59, .20, 1),
    'PastryPink': (.96, .37, .53, 1), 'MoneyGreen': (.16, .72, .22, 1),
    'MoneyLight': (.57, .92, .35, 1), 'Soil': (.30, .16, .07, 1),
}


def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest().upper()


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1
    scene.unit_settings.length_unit = 'METERS'
    scene.render.fps = 30


def vertices(mesh):
    array = np.empty(len(mesh.vertices) * 3, dtype=np.float64)
    mesh.vertices.foreach_get('co', array)
    return array.reshape(-1, 3)


def box_bounds(obj):
    array = vertices(obj.data)
    return array.min(axis=0), array.max(axis=0)


def normalize(obj, height):
    obj.data.transform(obj.matrix_world)
    obj.matrix_world = Matrix.Identity(4)
    low, high = box_bounds(obj)
    scale = height / (high[2] - low[2])
    origin = Vector(((high[0] + low[0]) * .5, (high[1] + low[1]) * .5, low[2]))
    for vertex in obj.data.vertices:
        vertex.co = (vertex.co - origin) * scale
    obj.data.update()
    return {'original_bounds': [low.tolist(), high.tolist()], 'uniform_scale': float(scale),
            'output_height_m': height, 'origin': 'ground at bounding-box horizontal center'}


def linked_image(socket):
    if not socket.is_linked:
        return None
    node = socket.links[0].from_node
    if node.type == 'TEX_IMAGE':
        return node.image
    for value in node.inputs:
        image = linked_image(value)
        if image:
            return image
    return None


def save_map(source, name, role, size, channel=None):
    colorspace = 'sRGB' if role == 'BaseColor' else 'Non-Color'
    original_size = list(source.size)
    image = source.copy()
    image.name = name + '_' + role
    image.colorspace_settings.name = colorspace
    image.scale(size, size)
    if channel is not None:
        pixels = np.empty(size * size * 4, dtype=np.float32)
        image.pixels.foreach_get(pixels)
        pixels = pixels.reshape(-1, 4)
        pixels[:, :3] = pixels[:, channel:channel + 1]
        pixels[:, 3] = 1
        image.pixels.foreach_set(pixels.ravel())
    path = TEXTURES / (image.name + '.png')
    image.filepath_raw = str(path)
    image.file_format = 'PNG'
    image.save()
    # Chi giu PNG da xu ly; blend tham chieu PNG, khong giu payload 4K goc.
    if image.packed_file:
        image.unpack(method='REMOVE')
    image.filepath = str(path)
    return image, {'role': role, 'pixels': size, 'source_image': source.name,
                   'source_pixels': original_size, 'source_channel': channel,
                   'path': str(path), 'sha256': sha(path)}


def source_material(obj, name):
    if len(obj.data.materials) != 1:
        raise RuntimeError(name + ' expected a single source material')
    material = obj.data.materials[0]
    shader = next(n for n in material.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    maps = {}
    report = []
    size = 2048 if name.startswith('Character') else 1024
    for role, socket, resolution in [('BaseColor', 'Base Color', size),
                                     ('Normal', 'Normal', size),
                                     ('Roughness', 'Roughness', 1024),
                                     ('Metallic', 'Metallic', 1024)]:
        source = linked_image(shader.inputs[socket])
        if source is None:
            raise RuntimeError(name + ' missing source ' + role + ' texture')
        # glTF can encode roughness/metallic together (G/B), FBX can keep separate maps.
        channel = None
        link = shader.inputs[socket].links[0]
        if role in {'Roughness', 'Metallic'} and link.from_node.type in {'SEPARATE_COLOR', 'SEPXYZ', 'SEPRGB'}:
            index = ['Red', 'Green', 'Blue', 'R', 'G', 'B', 'X', 'Y', 'Z'].index(link.from_socket.name)
            channel = index % 3
        maps[role], entry = save_map(source, name, role, resolution, channel)
        report.append(entry)
    nodes = material.node_tree.nodes
    nodes.clear()
    out = nodes.new('ShaderNodeOutputMaterial')
    shader = nodes.new('ShaderNodeBsdfPrincipled')
    material.node_tree.links.new(shader.outputs['BSDF'], out.inputs['Surface'])
    for index, (role, image) in enumerate(maps.items()):
        node = nodes.new('ShaderNodeTexImage')
        node.image = image
        node.location = (-450, 250 - index * 180)
        if role == 'Normal':
            normal = nodes.new('ShaderNodeNormalMap')
            normal.inputs['Strength'].default_value = 1
            material.node_tree.links.new(node.outputs['Color'], normal.inputs['Color'])
            material.node_tree.links.new(normal.outputs['Normal'], shader.inputs['Normal'])
        else:
            socket = 'Base Color' if role == 'BaseColor' else role
            material.node_tree.links.new(node.outputs['Color'], shader.inputs[socket])
    material.name = name + '_SourceMaterial'
    return report


def runtime_mesh(obj, target):
    before = vertices(obj.data)
    obj.data.calc_loop_triangles()
    source_triangles = len(obj.data.loop_triangles)
    # Hop vi tri GLB tai UV seams, UV loop van giu nguyen.
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    bm.to_mesh(obj.data)
    bm.free()
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    modifier = obj.modifiers.new('Runtime reduction', 'DECIMATE')
    modifier.ratio = min(1, target / source_triangles)
    modifier.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.data.calc_loop_triangles()
    after = vertices(obj.data)
    bounds_error = float(np.abs(np.r_[before.min(0), before.max(0)] - np.r_[after.min(0), after.max(0)]).max())
    if bounds_error > .015:
        raise RuntimeError(obj.name + ' silhouette bounds changed over 15 mm')
    return {'source_triangles': source_triangles, 'runtime_triangles': len(obj.data.loop_triangles),
            'vertices': len(obj.data.vertices), 'bound_change_max_m': bounds_error,
            'method': 'GLB seam position weld preserving loop UV; Blender collapse decimation'}


def rig_source(obj, name, height):
    armature = bpy.data.armatures.new(name + '_Skeleton')
    rig = bpy.data.objects.new(name + '_Rig', armature)
    bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    coords = vertices(obj.data)
    def bone(name, head, tail, parent=None, deform=True):
        b = armature.edit_bones.new(name)
        b.head, b.tail = head, tail
        if parent:
            b.parent = armature.edit_bones[parent]
        b.use_deform = deform
        return b
    if name.startswith('Character'):
        s = height / 1.8
        bone('Root', (0, 0, 0), (0, 0, .15), deform=False)
        bone('Hips', (0, -.09 * s, .51 * s), (0, -.09 * s, .70 * s), 'Root')
        bone('Spine', (0, -.09 * s, .70 * s), (0, -.07 * s, .88 * s), 'Hips', False)
        bone('Head', (0, 0, 1.02 * s), (0, 0, 1.5 * s), 'Spine')
        bone('CarryAnchor', (0, -.49 * s, .73 * s), (0, -.49 * s, .83 * s), 'Hips', False)
        for side, sign in [('L', 1), ('R', -1)]:
            foot = coords[(coords[:, 2] < .16 * s) & (coords[:, 0] * sign > .03 * s)]
            xy = np.median(foot[:, :2], axis=0)
            x, y = float(xy[0]), float(xy[1])
            bone('UpperLeg.' + side, (x * .65, y, .45 * s), (x * .86, y, .28 * s), 'Hips')
            bone('LowerLeg.' + side, (x * .86, y, .28 * s), (x, y, .115 * s), 'UpperLeg.' + side)
            bone('Foot.' + side, (x, y, .115 * s), (x, y - .17 * s, .10 * s), 'LowerLeg.' + side)
            bone('UpperArm.' + side, (sign * .22 * s, -.08 * s, .90 * s),
                 (sign * .35 * s, -.12 * s, .75 * s), 'Spine', False)
            bone('Hand.' + side, (sign * .35 * s, -.12 * s, .75 * s),
                 (sign * .34 * s, -.20 * s, .82 * s), 'UpperArm.' + side, False)
    else:
        low, high = box_bounds(obj)
        center = (low + high) * .5
        bone('Root', (0, 0, 0), (0, 0, .1), deform=False)
        bone('Body', (0, 0, center[2]), (0, 0, center[2] + .25 * height), 'Root')
    bpy.ops.object.mode_set(mode='OBJECT')
    groups = {b.name: obj.vertex_groups.new(name=b.name) for b in armature.bones if b.use_deform}
    def smooth(t):
        t = min(1, max(0, t))
        return t * t * (3 - 2 * t)
    for vertex in obj.data.vertices:
        x, y, z = vertex.co
        if not name.startswith('Character'):
            weights = {'Body': 1}
        else:
            s = height / 1.8
            xn, yn, zn = x / s, y / s, z / s
            side = 'L' if xn > 0 else 'R'
            # Tay cham hong giu chung transform than; khong cat mesh hay xoay ngon cai.
            leg = (1 - smooth((zn - .40) / .09)) * smooth((abs(xn) - .025) / .05)
            head = smooth((zn - .98) / .15)
            if abs(xn) > .30 and zn < 1.23:
                head *= smooth((zn - 1.12) / .11)
            lower = 1 - smooth((zn - .24) / .09)
            foot = 1 - smooth((zn - .11) / .06)
            weights = {'Hips': (1 - leg) * (1 - head), 'Head': (1 - leg) * head,
                       'UpperLeg.' + side: leg * (1 - lower),
                       'LowerLeg.' + side: leg * lower * (1 - foot),
                       'Foot.' + side: leg * lower * foot}
        total = sum(weights.values())
        for group, weight in weights.items():
            if weight > 1e-7:
                groups[group].add([vertex.index], weight / total, 'REPLACE')
    mod = obj.modifiers.new('SourcePoseSkin', 'ARMATURE')
    mod.object = rig
    # Unity FBX dung linear skinning; audit va render cung mot phep bien dang.
    mod.use_deform_preserve_volume = False
    obj.parent = rig
    obj.matrix_parent_inverse = Matrix.Identity(4)
    rig['AnimationType'] = 'Generic'
    rig['RootMotion'] = False
    rig['UpperBodyPolicy'] = 'Source contact pose preserved; no hand/hip separation'
    return rig


def global_rotate(rig, name, axis, angle):
    bone = rig.pose.bones[name]
    rest = rig.data.bones[name].matrix_local.to_quaternion()
    bone.rotation_quaternion = rest.inverted() @ Quaternion(axis, angle) @ rest


def evaluated_vertices(obj):
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    coords = vertices(mesh)
    evaluated.to_mesh_clear()
    return coords


def animate(obj, rig, name, height):
    scene = bpy.context.scene
    rest = vertices(obj.data)
    edge = np.empty(len(obj.data.edges) * 2, dtype=np.int32)
    obj.data.edges.foreach_get('vertices', edge)
    edge = edge.reshape(-1, 2)
    original_length = np.linalg.norm(rest[edge[:, 0]] - rest[edge[:, 1]], axis=1)
    valid = original_length > 1e-5
    edge, original_length = edge[valid], original_length[valid]
    rig.animation_data_create()
    reports = []
    definitions = CLIPS if name.startswith('Character') else {'Idle': 90}
    for clip, duration in definitions.items():
        action = bpy.data.actions.new(clip)
        action.use_fake_user = True
        rig.animation_data.action = action
        if hasattr(rig.animation_data, 'action_slot') and action.slots:
            rig.animation_data.action_slot = action.slots[0]
        sample_frames = sorted(set([1, duration + 1] + list(range(1, duration + 1, 3))))
        for frame in sample_frames:
            scene.frame_set(frame)
            phase = (frame - 1) / duration * 2 * math.pi
            for bone in rig.pose.bones:
                bone.rotation_mode = 'QUATERNION'
                bone.rotation_quaternion = (1, 0, 0, 0)
                bone.location = (0, 0, 0)
                bone.scale = (1, 1, 1)
            if name.startswith('Character'):
                stride = .11 if clip in {'Walk', 'CarryWalk'} else .16 if clip == 'Run' else 0
                if name in {'Character2', 'Character3'}:
                    stride *= .60
                for side, offset in [('L', 0), ('R', math.pi)]:
                    wave = math.sin(phase + offset)
                    global_rotate(rig, 'UpperLeg.' + side, (1, 0, 0), stride * wave)
                    global_rotate(rig, 'LowerLeg.' + side, (1, 0, 0), -stride * wave * .65)
                    global_rotate(rig, 'Foot.' + side, (1, 0, 0), -stride * wave * .35)
                lean = .022 if clip.startswith('Carry') else 0
                if clip == 'Pickup':
                    lean = .060 * (1 - math.cos(phase)) * .5
                if clip == 'Interact':
                    lean = .032 * (1 - math.cos(phase * 2)) * .5
                global_rotate(rig, 'Hips', (1, 0, 0), lean)
                global_rotate(rig, 'Head', (0, 0, 1), .012 * math.sin(phase))
                bpy.context.view_layer.update()
                floor = float(evaluated_vertices(obj)[:, 2].min())
                # Chinh Z cua Hips, Root giu bat dong cho movement cua Unity.
                lift = -floor
                rest_rot = rig.data.bones['Hips'].matrix_local.to_quaternion()
                rig.pose.bones['Hips'].location = rest_rot.inverted() @ Vector((0, 0, lift))
            else:
                global_rotate(rig, 'Body', (0, 0, 1), .012 * math.sin(phase))
                bpy.context.view_layer.update()
                floor = float(evaluated_vertices(obj)[:, 2].min())
                rest_rot = rig.data.bones['Body'].matrix_local.to_quaternion()
                rig.pose.bones['Body'].location = rest_rot.inverted() @ Vector((0, 0, -floor))
            for bone in rig.pose.bones:
                bone.keyframe_insert('rotation_quaternion', frame=frame, group=bone.name)
                bone.keyframe_insert('location', frame=frame, group=bone.name)
        worst, p99, floors = 1.0, 1.0, []
        for frame in range(1, duration + 2):
            scene.frame_set(frame)
            bpy.context.view_layer.update()
            coords = evaluated_vertices(obj)
            ratios = np.linalg.norm(coords[edge[:, 0]] - coords[edge[:, 1]], axis=1) / original_length
            if not np.isfinite(coords).all() or not np.isfinite(ratios).all():
                raise RuntimeError(name + ' non-finite deformed mesh')
            worst = max(worst, float(ratios.max()))
            p99 = max(p99, float(np.percentile(ratios, 99)))
            floors.append(float(coords[:, 2].min()))
        if worst > 1.75 or p99 > 1.20 or min(floors) < -.003:
            raise RuntimeError(name + ' ' + clip + ' deformation or ground gate failed: ' + str((worst, p99, min(floors))))
        reports.append({'name': clip, 'seconds': duration / 30, 'sampled_frames': duration + 1,
                        'edge_stretch_max': worst, 'edge_stretch_p99': p99,
                        'floor_min_m': min(floors), 'root_motion': False,
                        'loop': clip not in {'Pickup', 'Interact'}})
        print('CLIP_PASS', name, clip, round(worst, 4), flush=True)
        track = rig.animation_data.nla_tracks.new()
        track.name = clip
        strip = track.strips.new(clip, 1, action)
        strip.action_frame_start, strip.action_frame_end = 1, duration + 1
        track.mute = True
    rig.animation_data.action = bpy.data.actions['Idle']
    scene.frame_set(1)
    return reports


def export(name, objects, rig=None):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig or objects[0]
    bpy.context.scene.frame_set(1)
    path = MODELS / (name + '.fbx')
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True,
                             object_types={'MESH', 'ARMATURE'}, global_scale=1,
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                             axis_forward='-Z', axis_up='Y', add_leaf_bones=False,
                             use_armature_deform_only=False, bake_anim=bool(rig),
                             bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
                             bake_anim_use_all_actions=bool(rig), bake_anim_step=1,
                             bake_anim_simplify_factor=0, mesh_smooth_type='FACE',
                             use_mesh_modifiers=True, path_mode='ABSOLUTE', embed_textures=False)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / (name + '.blend')), compress=True, check_existing=False)
    return str(path)


def render_review(obj, rig, name):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 8
    scene.cycles.use_denoising = True
    if hasattr(scene.cycles, 'denoising_use_gpu'):
        scene.cycles.denoising_use_gpu = False
    scene.render.resolution_x, scene.render.resolution_y = 420, 520
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.view_settings.view_transform = 'AgX'
    world = bpy.data.worlds.new('ReviewWorld')
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs[0].default_value = (.52, .60, .66, 1)
    world.node_tree.nodes['Background'].inputs[1].default_value = .5
    bpy.ops.mesh.primitive_plane_add(size=200)
    ground = bpy.context.object
    material = bpy.data.materials.new('ReviewGround')
    material.diffuse_color = (.24, .30, .33, 1)
    ground.data.materials.append(material)
    for location, energy, size in [((-3, -4, 5), 500, 4), ((3, 2, 4), 350, 3)]:
        light_data = bpy.data.lights.new('ReviewLight', 'AREA')
        light_data.energy, light_data.shape, light_data.size = energy, 'DISK', size
        light = bpy.data.objects.new('ReviewLight', light_data)
        bpy.context.collection.objects.link(light)
        light.location = location
        light.rotation_euler = (Vector((0, 0, 1)) - light.location).to_track_quat('-Z', 'Y').to_euler()
    camera_data = bpy.data.cameras.new('ReviewCamera')
    camera = bpy.data.objects.new('ReviewCamera', camera_data)
    bpy.context.collection.objects.link(camera)
    scene.camera = camera
    camera_data.type = 'ORTHO'
    camera_data.ortho_scale = 2.5 if name.startswith('Character') else 2.6
    views = [('Idle' if rig else 'Model', 1, (-3.5, -5, 2.0))]
    if name.startswith('Character'):
        views += [('Walk', 8, (-3.5, -5, 2)), ('Run', 7, (3.5, -5, 2)),
                  ('CarryWalk', 10, (-3.5, -5, 2)), ('Pickup', 19, (-3.5, -5, 2)),
                  ('Interact', 13, (3.5, -5, 2))]
    for clip, frame, location in views:
        if rig:
            rig.animation_data.action = bpy.data.actions[clip]
        scene.frame_set(frame)
        camera.location = location
        low, high = box_bounds(obj)
        target = Vector((low + high) * .5)
        if name.startswith('Character'):
            target = Vector((0, 0, .9))
        camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
        if not name.startswith('Character'):
            rot = camera.rotation_euler.to_matrix().transposed()
            projected = np.array([rot @ (Vector(c) - target) for c in vertices(obj.data)])
            extent = projected.max(0) - projected.min(0)
            camera_data.ortho_scale = max(extent[1], extent[0] * 520 / 420) * 1.20
        scene.render.filepath = str(WORK / (name + '_' + clip + '.png'))
        bpy.ops.render.render(write_still=True)
    return [str(WORK / (name + '_' + clip + '.png')) for clip, _, _ in views]


def material(role):
    key = 'Gameplay_' + role
    mat = bpy.data.materials.get(key)
    if mat:
        return mat
    mat = bpy.data.materials.new(key)
    mat.diffuse_color = PALETTE[role]
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = PALETTE[role]
    shader.inputs['Roughness'].default_value = .68
    if role in {'MetalGrey'}:
        shader.inputs['Metallic'].default_value = .35
    return mat


def mesh_finish(obj, name, role, bevel=0):
    obj.name = name
    obj.data.materials.append(material(role))
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new('Soft edges', 'BEVEL')
        mod.width, mod.segments = bevel, 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for face in obj.data.polygons:
        face.use_smooth = True
    return obj


def block(name, position, size, role, bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1, location=position)
    obj = bpy.context.object
    obj.scale = size
    return mesh_finish(obj, name, role, bevel)


def sphere(name, position, scale, role, segments=16, rings=10):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1, location=position)
    obj = bpy.context.object
    obj.scale = scale
    return mesh_finish(obj, name, role)


def cylinder(name, position, radius, depth, role, top=None, vertices_count=12):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices_count, radius1=radius,
                                    radius2=top if top is not None else radius,
                                    depth=depth, location=position)
    return mesh_finish(bpy.context.object, name, role, .006)


def leaf(name, position, length, role, angle=0, width=.05):
    obj = sphere(name, position, (width, width * .30, length), role, 10, 6)
    obj.rotation_euler = (.35, angle, angle * .3)
    return obj


def join_named(name, objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.name = name + '_Geometry'
    return obj


def kit_model(name):
    parts = []
    def b(*args, **kwargs):
        obj = block(*args, **kwargs); parts.append(obj); return obj
    def s(*args, **kwargs):
        obj = sphere(*args, **kwargs); parts.append(obj); return obj
    def c(*args, **kwargs):
        obj = cylinder(*args, **kwargs); parts.append(obj); return obj
    def l(*args, **kwargs):
        obj = leaf(*args, **kwargs); parts.append(obj); return obj
    if name == 'Tree':
        c('Trunk', (0, 0, .55), .115, 1.10, 'WoodHoney', .08)
        s('CrownMain', (0, 0, 1.30), (.58, .52, .72), 'FoliageLime')
        s('CrownHighlight', (-.17, -.16, 1.56), (.35, .34, .44), 'FoliageLight')
        s('CrownLower', (.24, .08, 1.11), (.37, .39, .40), 'FoliageLime')
    elif name == 'FenceSegment':
        for x in [-1, 1]:
            b('Post', (x, 0, .40), (.14, .14, .80), 'WoodHoney', .015)
            b('PostCap', (x, 0, .81), (.18, .18, .08), 'WoodLight', .022)
        for z in [.30, .62]:
            b('Rail', (0, 0, z), (2, .09, .11), 'WoodLight', .014)
    elif name == 'Barn':
        b('BarnWalls', (0, 0, 1.05), (3.2, 2.4, 2.10), 'BarnRed', .045)
        for x in np.arange(-1.45, 1.5, .24):
            b('FrontSiding', (float(x), -1.215, 1.04), (.065, .035, 2), 'CapRed', .005)
        verts = [(-1.76, -1.35, 2.10), (0, -1.35, 2.90), (1.76, -1.35, 2.10),
                 (-1.76, 1.35, 2.10), (0, 1.35, 2.90), (1.76, 1.35, 2.10)]
        mesh = bpy.data.meshes.new('GableRoof')
        mesh.from_pydata(verts, [], [(0, 1, 4, 3), (1, 2, 5, 4), (0, 2, 1), (3, 4, 5)])
        roof = bpy.data.objects.new('Roof', mesh); bpy.context.collection.objects.link(roof)
        mesh.materials.append(material('RoofSlate')); parts.append(roof)
        b('DoorDark', (0, -1.25, .67), (1.20, .055, 1.35), 'WoodHoney', .01)
        for x in [-.64, .64]:
            b('DoorFrame', (x, -1.31, .70), (.08, .07, 1.40), 'Cream', .01)
        b('DoorTop', (0, -1.31, 1.42), (1.35, .07, .09), 'Cream', .01)
        brace = b('DoorBrace', (0, -1.32, .67), (.075, .05, 1.7), 'Cream', .01)
        brace.rotation_euler[1] = .72
        b('Window', (1.07, -1.27, 1.55), (.45, .06, .45), 'LabelBlue', .025)
        b('WindowCross', (1.07, -1.31, 1.55), (.04, .05, .48), 'Cream', .005)
        b('WindowCross', (1.07, -1.31, 1.55), (.48, .05, .04), 'Cream', .005)
    elif name == 'Shelf':
        for x in [-.79, .79]:
            b('FrameUpright', (x, 0, .69), (.09, .58, 1.38), 'RailBlue', .018)
        for z in [.10, .54, .98, 1.37]:
            b('ShelfDeck', (0, 0, z), (1.65, .62, .075), 'RailYellow', .025)
        b('Backboard', (0, .285, .70), (1.56, .045, 1.30), 'RailBlue', .01)
        b('HeaderPanel', (0, -.025, 1.52), (1.66, .10, .25), 'RailBlue', .03)
        b('HeaderStripe', (0, -.081, 1.52), (1.38, .02, .085), 'RailYellow', .015)
    elif name == 'Checkout':
        b('CounterBase', (0, 0, .42), (1.7, .9, .84), 'RailBlue', .06)
        b('CounterTop', (0, 0, .88), (1.86, 1.02, .12), 'RailYellow', .05)
        b('Belt', (-.42, 0, .954), (.70, .72, .035), 'RoofSlate', .018)
        for y in [-.37, .37]:
            b('BeltRail', (-.43, y, .985), (.78, .035, .025), 'MetalGrey', .006)
        b('RegisterBase', (.46, .16, 1.00), (.32, .26, .08), 'MetalGrey', .02)
        b('RegisterStand', (.46, .18, 1.13), (.07, .08, .24), 'RailBlue', .01)
        screen = b('RegisterHousing', (.46, .16, 1.31), (.39, .075, .29), 'RailBlue', .025)
        screen.rotation_euler[0] = -.12
        display = b('RegisterDisplay', (.46, .114, 1.31), (.31, .015, .205), 'ScreenDark', .015)
        display.rotation_euler[0] = -.12
        for x in [.37, .43, .49, .55]:
            b('DisplayDigits', (x, .101, 1.34), (.032, .01, .062), 'ScreenGreen', .003)
        b('Scanner', (.08, -.21, .97), (.19, .20, .05), 'ScreenDark', .012)
        b('ScannerStripe', (.08, -.21, 1), (.15, .02, .008), 'CapRed', .002)
    elif name == 'Basket':
        b('BasketFloor', (0, 0, .055), (.43, .31, .11), 'RailYellow', .015)
        for y in [-.155, .155]:
            for z in [.11, .20, .29]:
                b('BasketRail', (0, y, z), (.45, .025, .032), 'RailBlue', .008)
            for x in [-.20, -.10, 0, .10, .20]:
                b('BasketSlat', (x, y, .20), (.022, .027, .22), 'RailBlue', .006)
        for x in [-.215, .215]:
            b('BasketEnd', (x, 0, .20), (.025, .32, .22), 'RailBlue', .01)
        b('BasketHandle', (0, 0, .45), (.37, .025, .025), 'RailYellow', .008)
        for x in [-.185, .185]:
            b('HandleUpright', (x, 0, .365), (.025, .025, .17), 'RailYellow', .008)
    elif name == 'Milk':
        b('CartonBody', (0, 0, .235), (.235, .235, .47), 'MilkIvory', .032)
        b('CartonShoulder', (0, 0, .49), (.21, .21, .11), 'MilkIvory', .03)
        c('ScrewCap', (0, 0, .575), .095, .068, 'CapRed', vertices_count=16)
        c('CapLip', (0, 0, .548), .104, .016, 'CapRed', vertices_count=16)
        b('MilkBlueLabel', (0, -.123, .29), (.205, .012, .18), 'LabelBlue', .012)
        b('MilkLabelBand', (0, -.131, .28), (.155, .008, .035), 'MilkIvory', .007)
        s('MilkDrop', (0, -.141, .34), (.026, .006, .04), 'MilkIvory', 10, 6)
    elif name == 'Egg':
        egg = s('Egg', (0, 0, .095), (.068, .068, .095), 'EggCream', 16, 10)
        for v in egg.data.vertices:
            t = (v.co.z + .095) / .19
            v.co.x *= 1 - .22 * t; v.co.y *= 1 - .22 * t
    elif name == 'Carrot':
        c('CarrotBody', (0, 0, .16), .024, .32, 'CarrotOrange', .078)
        for angle in [-.5, 0, .5]:
            l('CarrotLeaf', (.02 * angle, 0, .375), .10, 'ProduceGreen', angle, .025)
        for z in [.12, .19, .25]:
            b('CarrotRidge', (0, -.045, z), (.04, .006, .012), 'BreadGold', .003)
    elif name == 'Corn':
        s('CornCore', (0, 0, .17), (.065, .065, .16), 'CornGold')
        for row in range(7):
            for column in range(7):
                angle = column / 7 * math.tau
                z = .07 + row * .033
                s('Kernel', (.052 * math.cos(angle), .052 * math.sin(angle), z), (.020, .020, .023), 'CornGold', 8, 6)
        for angle in [-.6, .6]:
            l('CornHusk', (.04 * angle, .01, .13), .15, 'ProduceGreen', angle, .048)
    elif name == 'Cabbage':
        s('CabbageCore', (0, 0, .145), (.15, .15, .145), 'CabbageLight')
        for i in range(7):
            angle = i / 7 * math.tau
            obj = s('OuterLeaf', (.075 * math.cos(angle), .075 * math.sin(angle), .13), (.10, .07, .14), 'ProduceGreen', 12, 8)
            obj.rotation_euler[2] = angle
            l('LeafVein', (.13 * math.cos(angle), .13 * math.sin(angle), .15), .092, 'CabbageLight', .2, .009)
    elif name == 'Tomato':
        s('TomatoBody', (0, 0, .105), (.115, .115, .105), 'TomatoRed')
        for i in range(5):
            angle = i / 5 * math.tau
            obj = l('TomatoCalyx', (.041 * math.cos(angle), .041 * math.sin(angle), .198), .052, 'ProduceGreen', 1.15, .016)
            obj.rotation_euler[2] = angle
        c('TomatoStem', (0, 0, .225), .012, .065, 'ProduceGreen', .007)
    elif name == 'Wheat':
        for offset in [-.05, 0, .05]:
            c('WheatStalk', (offset, 0, .20), .008, .40, 'WheatGold', .005, 8)
            for i in range(5):
                z = .34 + i * .032
                for sign in [-1, 1]:
                    kernel = s('WheatKernel', (offset + sign * .018, 0, z), (.017, .012, .031), 'CornGold', 8, 6)
                    kernel.rotation_euler[1] = sign * .42
    elif name == 'Flour':
        b('FlourSack', (0, 0, .18), (.23, .16, .36), 'FlourBag', .06)
        b('BagFold', (0, 0, .372), (.21, .13, .048), 'Cream', .017)
        b('BagLabel', (0, -.087, .22), (.16, .012, .15), 'MilkIvory', .012)
        b('LabelFlourBand', (0, -.095, .22), (.12, .008, .03), 'WheatGold', .004)
    elif name == 'Bread':
        s('BreadLoaf', (0, 0, .10), (.23, .11, .10), 'BreadGold')
        b('LoafBottom', (0, 0, .037), (.39, .15, .045), 'BreadCrust', .02)
        for x in [-.11, -.038, .038, .11]:
            slash = b('BreadCut', (x, 0, .195), (.014, .12, .012), 'Cream', .005)
            slash.rotation_euler[2] = -.35
    elif name == 'Pastry':
        c('PastryCup', (0, 0, .055), .073, .11, 'BreadCrust', .095, 16)
        s('PastryTop', (0, 0, .126), (.105, .105, .050), 'BreadGold')
        for z, radius in [(.16, .08), (.195, .055), (.221, .033)]:
            s('Icing', (0, 0, z), (radius, radius, .032), 'PastryPink')
        s('Cherry', (0, 0, .264), (.026, .026, .026), 'TomatoRed', 10, 6)
    elif name == 'Money':
        for z in [.01, .025, .04]:
            b('Banknote', (0, 0, z), (.26, .13, .014), 'MoneyGreen', .006)
        b('NoteInset', (0, 0, .049), (.20, .09, .005), 'MoneyLight', .008)
        c('MoneyMedallion', (0, 0, .054), .027, .006, 'MoneyGreen', vertices_count=16)
        b('NoteBand', (.07, 0, .055), (.036, .135, .01), 'MilkIvory', .002)
    elif name == 'CropBed':
        b('Soil', (0, 0, .06), (1.75, 1.1, .12), 'Soil', .035)
        for y in [-.56, .56]:
            b('BedSide', (0, y, .10), (1.86, .08, .20), 'WoodHoney', .015)
        for x in [-.89, .89]:
            b('BedEnd', (x, 0, .10), (.08, 1.1, .20), 'WoodLight', .015)
        for y in [-.30, 0, .30]:
            b('SoilRidge', (0, y, .142), (1.58, .13, .055), 'Soil', .02)
    else:
        raise ValueError(name)
    return join_named(name, parts)


KIT = ['Tree', 'FenceSegment', 'Barn', 'Shelf', 'Checkout', 'Basket', 'Milk',
       'Egg', 'Carrot', 'Corn', 'Cabbage', 'Tomato', 'Wheat', 'Flour', 'Bread',
       'Pastry', 'Money', 'CropBed']


def verify_fbx():
    results = []
    for name in list(SOURCES) + KIT:
        reset()
        path = MODELS / (name + '.fbx')
        if not path.is_file():
            raise RuntimeError('Missing FBX ' + str(path))
        bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True)
        meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
        rigs = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']
        if len(meshes) != 1:
            raise RuntimeError(name + ' round-trip mesh count mismatch')
        obj = meshes[0]
        world = np.array([obj.matrix_world @ v.co for v in obj.data.vertices])
        if not np.isfinite(world).all():
            raise RuntimeError(name + ' non-finite exported vertices')
        obj.data.calc_loop_triangles()
        entry = {'name': name, 'fbx_sha256': sha(path),
                 'triangles': len(obj.data.loop_triangles), 'dimensions_m': (world.max(0) - world.min(0)).tolist(),
                 'materials': [m.name for m in obj.data.materials]}
        if name in SOURCES:
            expected_height = SOURCES[name][0]
            if abs(entry['dimensions_m'][2] - expected_height) > .012:
                raise RuntimeError(name + ' FBX height mismatch')
            if abs(float(world[:, 2].min())) > .005:
                raise RuntimeError(name + ' FBX origin is not at ground')
        if name.startswith('Character') or name in {'Cow', 'Chicken'}:
            if len(rigs) != 1:
                raise RuntimeError(name + ' missing round-trip armature')
            rig = rigs[0]
            actions = list(bpy.data.actions)
            expected = list(CLIPS) if name.startswith('Character') else ['Idle']
            labels = [a.name.rsplit('|', 1)[-1] for a in actions]
            if set(expected) != set(labels):
                raise RuntimeError(name + ' exported clips mismatch ' + str(labels))
            entry['clips'] = labels
            entry['bones'] = len(rig.data.bones)
            entry['maximum_vertex_influences'] = max(len(v.groups) for v in obj.data.vertices)
            if entry['maximum_vertex_influences'] > 4:
                raise RuntimeError(name + ' has more than four vertex influences')
            entry['action_pose_displacements_m'] = {}
            for action in actions:
                rig.animation_data.action = action
                low, high = action.frame_range
                initial = None
                maximum_delta = 0
                frames = sorted({int(low), int(high), int((low + high) * .5),
                                 int(low + (high - low) * .25), int(low + (high - low) * .75)})
                for frame in frames:
                    bpy.context.scene.frame_set(frame)
                    bpy.context.view_layer.update()
                    verts = evaluated_vertices(obj)
                    verts = np.array([obj.matrix_world @ Vector(v) for v in verts])
                    if not np.isfinite(verts).all() or verts[:, 2].min() < -.006:
                        raise RuntimeError(name + ' reimported pose falls below ground')
                    if rig.pose.bones['Root'].location.length > .0001:
                        raise RuntimeError(name + ' reimported Root has translation')
                    if initial is None:
                        initial = verts.copy()
                    maximum_delta = max(maximum_delta, float(np.linalg.norm(verts - initial, axis=1).max()))
                if maximum_delta < .0005:
                    raise RuntimeError(name + ' ' + action.name + ' has no exported pose motion')
                entry['action_pose_displacements_m'][action.name] = maximum_delta
            entry['round_trip_pose_pass'] = True
        results.append(entry)
        print('FBX_ROUNDTRIP_PASS', name, flush=True)
    report = {'pass': True, 'models': len(results), 'assets': results,
              'verification': 'Blender FBX reimport; source heights, ground origins, clip names, <=4 influences, Root fixed, finite poses',
              'unity_runtime_verification': 'Performed separately by root; this report is an FBX round-trip gate.'}
    (OUT / 'GameplayFbxAudit.json').write_text(json.dumps(report, indent=2, allow_nan=False), encoding='utf-8')
    print('FBX_ROUNDTRIP_COMPLETE', len(results), flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--only', nargs='*')
    parser.add_argument('--review', action='store_true')
    parser.add_argument('--verify-only', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    for folder in [OUT, MODELS, TEXTURES, WORK]:
        folder.mkdir(parents=True, exist_ok=True)
    if args.verify_only:
        verify_fbx()
        return
    before = {p.name: sha(p) for p in sorted((ROOT / 'ASSET').iterdir()) if p.is_file()}
    report = {'source_hashes_before': before, 'blender': bpy.app.version_string,
              'render_device': 'CPU; no GPU tasks launched', 'assets': [],
              'material_palette': {'Gameplay_' + k: v for k, v in PALETTE.items()},
              'coordinate_contract': 'FBX meters; feet/ground origin; forward Unity +Z (Blender -Y)',
              'limitations': ['Character upper body retains source pose and contact geometry; generic in-place conservative gait, no Humanoid retargeting.',
                              'Carry clips use CarryAnchor and preserve asymmetric source hand poses; bespoke two-hand reach is not supplied.',
                              'Cow/Chicken Idle preserves body geometry; independent head, tail and leg articulation is not supplied.',
                              'Runtime decimation bounds gate is not a pixel-perfect silhouette guarantee.']}
    for name, (height, triangles) in SOURCES.items():
        if args.only and name not in args.only:
            continue
        reset()
        path = ROOT / 'ASSET' / (name + '.glb')
        bpy.ops.import_scene.gltf(filepath=str(path))
        meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
        if len(meshes) != 1:
            raise RuntimeError(name + ' expected one source mesh')
        obj = meshes[0]
        obj.name = name + '_Body'
        entry = {'name': name, 'source': str(path), 'source_sha256': before[path.name]}
        entry['normalization'] = normalize(obj, height)
        entry['textures'] = source_material(obj, name)
        entry['geometry'] = runtime_mesh(obj, triangles)
        rig = None
        if name.startswith('Character') or name in {'Cow', 'Chicken'}:
            rig = rig_source(obj, name, height)
            entry['clips'] = animate(obj, rig, name, height)
            entry['rig_bones'] = [b.name for b in rig.data.bones]
        entry['fbx'] = export(name, [obj, rig] if rig else [obj], rig)
        if args.review:
            entry['review_images'] = render_review(obj, rig, name)
        report['assets'].append(entry)
        (OUT / (name + '_Audit.json')).write_text(json.dumps(entry, indent=2, allow_nan=False), encoding='utf-8')
        print('ASSET_COMPLETE', name, entry['geometry']['runtime_triangles'], flush=True)
    for name in KIT:
        if args.only and name not in args.only:
            continue
        reset()
        obj = kit_model(name)
        obj.data.calc_loop_triangles()
        low, high = box_bounds(obj)
        entry = {'name': name, 'authored_environment_or_product': True,
                 'dimensions_m': (high - low).tolist(), 'triangles': len(obj.data.loop_triangles),
                 'materials': [m.name for m in obj.data.materials]}
        entry['fbx'] = export(name, [obj])
        if args.review:
            entry['review_images'] = render_review(obj, None, name)
        report['assets'].append(entry)
        print('KIT_COMPLETE', name, flush=True)
    after = {p.name: sha(p) for p in sorted((ROOT / 'ASSET').iterdir()) if p.is_file()}
    if before != after:
        raise RuntimeError('Source hash or source inventory changed')
    report['source_hashes_after'] = after
    report['source_preservation_pass'] = True
    report['generated_models'] = len(report['assets'])
    counts = {a['name']: a.get('geometry', {}).get('runtime_triangles', a.get('triangles', 0)) for a in report['assets']}
    report['triangle_budget'] = {'12_customers_plus_4_staff': 16 * max((counts.get(n, 0) for n in ['Character1', 'Character2', 'Character3'])),
                               '10_cows': 10 * counts.get('Cow', 0),
                               'all_five_source_machines_conveyors': sum(counts.get(n, 0) for n in ['Machine', 'Machine2', 'Machine3', 'Item1', 'Item2']),
                               'kit_one_each': sum(counts.get(n, 0) for n in KIT)}
    report['triangle_budget']['total_representative'] = sum(report['triangle_budget'].values())
    (OUT / 'GameplayAssetAudit.json').write_text(json.dumps(report, indent=2, allow_nan=False), encoding='utf-8')
    print('GAMEPLAY_ASSET_PIPELINE_COMPLETE', len(report['assets']), 'SOURCE_HASHES_OK', flush=True)


if __name__ == '__main__':
    main()
