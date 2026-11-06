# Run: blender -b --factory-startup -P gltf2fbx.py -- <src_dir> <out_dir>
# Imports each <name>/scene.gltf, scales to target size, decimates to face budget, exports <name>.fbx with embedded textures.
import bpy, sys, os
from mathutils import Vector

src, out = sys.argv[sys.argv.index("--") + 1:][:2]
os.makedirs(out, exist_ok=True)

# name: (target largest dimension in meters, max faces for mobile)
MODELS = {
    "GhostWoman": (1.7, 20000),
    "JaisalmerHaveliDoor": (2.5, 15000),
    "OldWell": (2.5, 15000),
    "AncientClayPots": (1.0, 15000),
    "AartiThali": (0.35, 10000),
    "GoldenRitualLamp": (0.4, 10000),
    "DeadTree": (6.0, 15000),
}

for name, (size, budget) in MODELS.items():
    path = os.path.join(src, name, "scene.gltf")
    if not os.path.exists(path):
        print(f"[skip] {name}: no scene.gltf"); continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if not meshes:
        print(f"[FAIL] {name}: no meshes"); continue

    # Bake parent transforms (Sketchfab root nodes carry odd rotations/scales) into the meshes.
    bpy.ops.object.select_all(action='SELECT')
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    for o in [o for o in bpy.context.scene.objects if o.type == 'EMPTY']:
        bpy.data.objects.remove(o)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    # Join into one mesh so scale/pivot/decimation are uniform.
    bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = name

    faces = len(obj.data.polygons)
    if faces > budget:
        mod = obj.modifiers.new("Decimate", 'DECIMATE')
        mod.ratio = budget / faces
        bpy.ops.object.modifier_apply(modifier=mod.name)

    # Scale largest dimension to target, pivot at bottom-center, sit on ground.
    bb = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    dims = Vector((max(v[i] for v in bb) - min(v[i] for v in bb) for i in range(3)))
    s = size / max(dims)
    obj.scale = (s, s, s)
    bpy.ops.object.transform_apply(scale=True)
    bb = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    cx = (max(v.x for v in bb) + min(v.x for v in bb)) / 2
    cy = (max(v.y for v in bb) + min(v.y for v in bb)) / 2
    bpy.context.scene.cursor.location = (cx, cy, min(v.z for v in bb))
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    obj.location = (0, 0, 0)

    # Unlit (KHR_materials_unlit) materials have no Principled BSDF, so the FBX exporter drops their texture.
    for mat in obj.data.materials:
        if not mat or not mat.use_nodes: continue
        nt = mat.node_tree
        if any(n.type == 'BSDF_PRINCIPLED' for n in nt.nodes): continue
        tex = next((n for n in nt.nodes if n.type == 'TEX_IMAGE'), None)
        outn = next((n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL'), None)
        if not tex or not outn: continue
        bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
        nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
        nt.links.new(bsdf.outputs['BSDF'], outn.inputs['Surface'])
        print(f"  fixed unlit material {mat.name}")

    # Shrink textures > 2048 for mobile; save resized copy so the exporter embeds it.
    tmp = os.path.join(out, "_tex")
    os.makedirs(tmp, exist_ok=True)
    for img in bpy.data.images:
        if img.size[0] > 2048 or img.size[1] > 2048:
            w, h = img.size
            k = 2048 / max(w, h)
            img.scale(int(w * k), int(h * k))
            ext = ".png" if img.file_format == 'PNG' else ".jpg"
            img.filepath_raw = os.path.join(tmp, f"{name}_{img.name}{ext}".replace("/", "_"))
            img.file_format = 'PNG' if ext == ".png" else 'JPEG'
            img.save()
            print(f"  resized {img.name} {w}x{h} -> {img.size[0]}x{img.size[1]}")

    os.makedirs(os.path.join(out, name), exist_ok=True)
    for img in bpy.data.images:
        if img.packed_file and img.filepath_raw and os.path.exists(bpy.path.abspath(img.filepath_raw)):
            img.unpack(method='USE_ORIGINAL')
    fbx = os.path.join(out, name, name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=fbx, use_selection=False, object_types={'MESH'},
        apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True,
        axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE',
        path_mode='COPY', embed_textures=False)
    print(f"[ok] {name}: {faces} -> {len(obj.data.polygons)} faces, {sum(os.path.getsize(os.path.join(dp,f)) for dp,_,fs in os.walk(os.path.dirname(fbx)) for f in fs)/1e6:.1f} MB")
