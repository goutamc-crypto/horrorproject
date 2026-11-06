# Low-poly Rajasthani haveli generator for mobile.
# Run: blender -b --factory-startup -P haveli.py -- <out_dir> <preview_png>
# out_dir must contain Textures/Sandstone_Diffuse.jpg + Sandstone_Normal.jpg. Front facade faces -Y (Blender).
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix

OUT, PREVIEW = sys.argv[sys.argv.index("--") + 1:][:2]
TEX = os.path.join(OUT, "Textures")
bpy.ops.wm.read_factory_settings(use_empty=True)

# ---------- materials ----------
def mat_stone():
    m = bpy.data.materials.new("Haveli_Sandstone"); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    d = nt.nodes.new("ShaderNodeTexImage"); d.image = bpy.data.images.load(os.path.join(TEX, "Sandstone_Diffuse.jpg"))
    nt.links.new(d.outputs["Color"], b.inputs["Base Color"])
    n = nt.nodes.new("ShaderNodeTexImage"); n.image = bpy.data.images.load(os.path.join(TEX, "Sandstone_Normal.jpg"))
    n.image.colorspace_settings.name = "Non-Color"
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(n.outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    b.inputs["Roughness"].default_value = 0.9
    return m
def mat_flat(name, rgb):
    m = bpy.data.materials.new(name); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*rgb, 1)
    m.diffuse_color = (*rgb, 1)
    return m
STONE, DARK = mat_stone(), mat_flat("Haveli_Dark", (0.05, 0.035, 0.025))

# ---------- primitives (return object lists so groups can be placed/rotated) ----------
def _fin(o, m): o.data.materials.append(m); return [o]
def box(sx, sy, sz, x, y, z, m=STONE):  # z = bottom
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z + sz / 2)); o = bpy.context.object
    o.scale = (sx, sy, sz); return _fin(o, m)
def cyl(r, h, x, y, z, n=8, m=STONE):  # vertical, z = bottom
    bpy.ops.mesh.primitive_cylinder_add(vertices=n, radius=r, depth=h, location=(x, y, z + h / 2)); return _fin(bpy.context.object, m)
def disk(r, x, y, z, m=DARK):  # arch head facing -Y
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=r, depth=0.04, location=(x, y, z), rotation=(math.pi / 2, 0, 0)); return _fin(bpy.context.object, m)
def dome(r, x, y, z, m=STONE):  # z = base; low-poly onion-ish dome + finial
    bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=5, radius=r, location=(x, y, z)); o = bpy.context.object
    o.scale = (1, 1, 1.15); _fin(o, m)
    bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=r * 0.12, depth=r * 0.6, location=(x, y, z + r * 1.15 + r * 0.25)); return [o] + _fin(bpy.context.object, m)

def place(objs, loc=(0, 0, 0), rz=0.0):
    R = Matrix.Rotation(rz, 3, 'Z')
    for o in objs:
        o.location = R @ o.location + Vector(loc); o.rotation_euler.z += rz
    return objs

# ---------- facade elements (local: wall plane y=0, outward -Y, z=element base) ----------
def arch_window(w, h, z=0.0):
    o = box(w + 0.3, 0.1, h + 0.2, 0, -0.05, z - 0.1)                      # stone frame
    o += box(w, 0.04, h - w / 2, 0, -0.12, z, DARK)                       # dark opening
    o += disk(w / 2, 0, -0.12, z + h - w / 2)                             # arched head
    return o

def jharokha(w, z, with_dome=True, with_window=True):
    o = box(w * 0.7, 0.5, 0.45, 0, -0.25, z - 0.45)                       # bracket
    o += box(w + 0.2, 1.0, 0.15, 0, -0.5, z)                              # floor slab
    o += box(w, 0.08, 0.75, 0, -0.96, z + 0.15)                           # front railing
    o += box(0.08, 0.9, 0.75, -w / 2, -0.5, z + 0.15) + box(0.08, 0.9, 0.75, w / 2, -0.5, z + 0.15)
    for px in (-w / 2 + 0.08, w / 2 - 0.08):
        o += cyl(0.07, 1.65, px, -0.9, z + 0.15)                          # pillars
    o += box(w + 0.45, 1.25, 0.15, 0, -0.55, z + 1.8)                     # chajja roof
    if with_window:
        o += arch_window(w * 0.55, 1.5, z + 0.2)                          # opening behind
    if with_dome:
        o += dome(w * 0.42, 0, -0.5, z + 1.95)
    return o

def chhatri(z, s=1.6):
    o = box(s, s, 0.2, 0, 0, z)
    for dx in (-1, 1):
        for dy in (-1, 1):
            o += cyl(0.08, 1.4, dx * (s / 2 - 0.15), dy * (s / 2 - 0.15), z + 0.2)
    o += box(s + 0.3, s + 0.3, 0.15, 0, 0, z + 1.6)
    return o + dome(s * 0.45, 0, 0, z + 1.75)

def parapet(sx, sy, z, cx=0.0, cy=0.0, h=0.7, step=2.5):
    o = []
    for side in (-1, 1):
        o += box(sx, 0.2, h, cx, cy + side * (sy / 2 - 0.1), z)
        o += box(0.2, sy, h, cx + side * (sx / 2 - 0.1), cy, z)
    n = max(1, round(sx / step))
    for i in range(n + 1):
        x = cx - sx / 2 + i * sx / n
        for side in (-1, 1):
            o += box(0.3, 0.3, h + 0.25, x, cy + side * (sy / 2 - 0.1), z)
    m = max(1, round(sy / step))
    for i in range(1, m):
        y = cy - sy / 2 + i * sy / m
        for side in (-1, 1):
            o += box(0.3, 0.3, h + 0.25, cx + side * (sx / 2 - 0.1), y, z)
    return o

# ---------- building ----------
W, D = 20.0, 12.0
P = 1.2                      # plinth height
G = 4.5                      # ground floor height
F = 4.0                      # first floor height
Z1 = P + G + 0.25            # first floor base (after cornice)
ZR = Z1 + F                  # roof slab base
FY = -D / 2                  # front wall plane

box(W + 2, D + 2, P, 0, 0, 0)                                             # plinth
for i in range(5):                                                       # stairs
    dd = 0.45 * (i + 1); box(6.0, dd, P - 0.24 * i, 0, -(D + 2) / 2 - dd / 2, 0)
box(W, D, G, 0, 0, P)                                                     # ground floor
box(W + 0.8, D + 0.8, 0.25, 0, 0, P + G)                                  # cornice 1
box(W, D, F, 0, 0, Z1)                                                    # first floor
box(W + 0.8, D + 0.8, 0.3, 0, 0, ZR)                                      # cornice 2 / roof
for sx in (-1, 1):                                                        # corner pilasters
    for sy in (-1, 1):
        box(0.7, 0.7, ZR - P, sx * (W / 2 - 0.2), sy * (D / 2 - 0.2), P)

# entrance porch: pillars + beam + three arches
for x in (-3.3, -1.15, 1.15, 3.3):
    box(0.5, 0.5, G - 0.4, x, FY - 0.9, P); box(0.7, 0.7, 0.3, x, FY - 0.9, P)
box(8.0, 1.4, 0.45, 0, FY - 0.7, P + G - 0.45)
place(arch_window(2.2, 3.7), (0, FY, P))
for x in (-2.25, 2.25):
    place(arch_window(1.4, 3.0), (x, FY, P))

# front ground floor: windows + jharokhas
for x in (-5.3, 5.3):
    place(arch_window(1.0, 1.9), (x, FY, P + 1.0))
for x in (-7.8, 7.8):
    place(jharokha(1.9, 2.4), (x, FY, P + 0.6))

# front first floor: wide central balcony (flat roof) + side jharokhas
place(jharokha(7.2, 0, with_dome=False, with_window=False), (0, FY, Z1 + 0.4))
for x in (-2.4, 0, 2.4):
    place(arch_window(1.2, 1.4), (x, FY, Z1 + 0.6))
place(parapet(7.6, 1.2, Z1 + 0.4 + 1.95, cy=0, h=0.45, step=1.9), (0, FY - 0.55, 0))
for x, w in ((-5.4, 2.0), (5.4, 2.0), (-8.4, 1.6), (8.4, 1.6)):
    place(jharokha(w, 0), (x, FY, Z1 + 0.6))

# side facades (+X / -X): windows + one jharokha per side
for sx in (1, -1):                   # build in local wall frame, rotate -Y outward onto +X / -X wall
    objs = []
    for lx in (-3.5, 3.5):
        objs += place(arch_window(1.0, 1.9), (lx, 0, P + 1.0))
        objs += place(arch_window(1.0, 1.6), (lx, 0, Z1 + 0.8))
    objs += place(arch_window(1.3, 2.6), (0, 0, P + 0.8))
    objs += place(jharokha(2.0, 0), (0, 0, Z1 + 0.6))
    place(objs, (sx * W / 2, 0, 0), sx * math.pi / 2)
back = []
for lx in (-7, -3.5, 0, 3.5, 7):
    back += place(arch_window(1.0, 1.9), (lx, 0, P + 1.0)) + place(arch_window(1.0, 1.6), (lx, 0, Z1 + 0.8))
place(back, (0, D / 2, 0), math.pi)  # +Y wall (rotate 180 so it faces outward)

# roof: parapet, corner chhatris, rooftop pavilion
RT = ZR + 0.3
parapet(W + 0.6, D + 0.6, RT)
for sx in (-1, 1):
    for sy in (-1, 1):
        place(chhatri(RT), (sx * (W / 2 - 1.0), sy * (D / 2 - 1.0), 0))
PW, PD, PH, PY = 8.0, 5.0, 3.0, 1.0
box(PW, PD, PH, 0, PY, RT)
for x in (-2.5, 0, 2.5):
    place(arch_window(1.1, 1.8), (x, PY - PD / 2, RT + 0.4))
box(PW + 0.6, PD + 0.6, 0.2, 0, PY, RT + PH)
parapet(PW + 0.4, PD + 0.4, RT + PH + 0.2, cy=PY, h=0.45, step=2.0)
for sx in (-1, 1):
    place(dome(0.6, 0, 0, 0), (sx * (PW / 2 - 0.2), PY - PD / 2 + 0.2, RT + PH + 0.2))

# ---------- join, apply, world-space box UVs ----------
objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
bpy.ops.object.select_all(action='SELECT')
bpy.context.view_layer.objects.active = objs[0]
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.object.join()
obj = bpy.context.object; obj.name = "Haveli"
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.remove_doubles(threshold=0.001); bpy.ops.object.mode_set(mode='OBJECT')

TILE = 3.0  # meters per texture repeat
bm = bmesh.new(); bm.from_mesh(obj.data)
uv = bm.loops.layers.uv.verify()
for f in bm.faces:
    n = f.normal; ax = max(range(3), key=lambda i: abs(n[i]))
    for l in f.loops:
        c = l.vert.co
        l[uv].uv = (c.x / TILE, c.y / TILE) if ax == 2 else (c.y / TILE, c.z / TILE) if ax == 0 else (c.x / TILE, c.z / TILE)
bm.to_mesh(obj.data); bm.free()
for p in obj.data.polygons: p.use_smooth = False

tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
d = obj.dimensions
print(f"[ok] Haveli tris={tris} mats={len(obj.data.materials)} dims={d.x:.1f}x{d.y:.1f}x{d.z:.1f}")

# ---------- export ----------
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "Haveli.fbx"), use_selection=False, object_types={'MESH'},
    apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, axis_forward='-Z', axis_up='Y',
    mesh_smooth_type='FACE', path_mode='RELATIVE', embed_textures=False)

# ---------- preview render ----------
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
sc.display.shading.show_shadows = True; sc.display.shading.show_cavity = True
sc.render.resolution_x, sc.render.resolution_y = 1280, 720
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.location = (-30, -42, 20)
t = bpy.data.objects.new("T", None); sc.collection.objects.link(t); t.location = (0, 0, 6.5)
c = cam.constraints.new('TRACK_TO'); c.target = t; c.track_axis = 'TRACK_NEGATIVE_Z'; c.up_axis = 'UP_Y'
sc.render.filepath = PREVIEW
bpy.ops.render.render(write_still=True)
print("[ok] preview", PREVIEW)
