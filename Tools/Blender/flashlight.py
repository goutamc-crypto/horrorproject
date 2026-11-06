# Builds the first-person torch: worn metal flashlight + right hand baked in a handshake grip + jacket sleeve.
# Run: blender --background --python Tools/Blender/flashlight.py
# Axes (Blender): +Y = torch forward, +X = right, +Z = up. Origin = grip centre on the torch axis.
import math, os
import bpy, bmesh
import numpy as np
from mathutils import Vector

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                   "Assets", "MAIN GAME", "3D models", "Flashlight")
os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)

BARREL_R = 0.017
HAND_TRIS = 3000


def new_obj(name, bm):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for p in mesh.polygons:
        p.use_smooth = True
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def lathe(bm, profile, sides=20):
    """Revolve (radius, y) points around the Y axis."""
    rings = []
    for r, y in profile:
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / sides), y, r * math.sin(2 * math.pi * i / sides)))
                      for i in range(sides)])
    for a, b in zip(rings, rings[1:]):
        for i in range(sides):
            j = (i + 1) % sides
            quad = [a[i], a[j], b[j], b[i]]
            if len({v.co.to_tuple(5) for v in quad}) >= 3:
                bm.faces.new(quad)


def tube(bm, pts, radii, sides=8):
    """Capped tube through points, for fingers and arm."""
    pts = [Vector(p) for p in pts]
    last = None
    for k, (p, r) in enumerate(zip(pts, radii)):
        d = (pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]).normalized()
        u = d.cross(Vector((0.31, 0.17, 0.93))).normalized()
        v = d.cross(u)
        ring = [bm.verts.new(p + (u * math.cos(2 * math.pi * i / sides) + v * math.sin(2 * math.pi * i / sides)) * r)
                for i in range(sides)]
        if last:
            for i in range(sides):
                j = (i + 1) % sides
                bm.faces.new([last[i], last[j], ring[j], ring[i]])
        else:
            bm.faces.new(list(reversed(ring)))
        last = ring
    bm.faces.new(last)


def sharpen(bm, degrees=40):
    bm.normal_update()
    edges = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle() > math.radians(degrees)]
    bmesh.ops.split_edges(bm, edges=edges)


def at(angle_deg, radius, y):
    a = math.radians(angle_deg)  # 0 = +X (right), 90 = +Z (top)
    return (radius * math.cos(a), y, radius * math.sin(a))


# --- Flashlight body ---
bm = bmesh.new()
lathe(bm, [(0, -.095), (.013, -.095), (.0165, -.092), (.0165, -.084), (.0155, -.082), (.0155, -.079), (BARREL_R, -.077),
           (BARREL_R, .052), (.0160, .054), (.0160, .057), (BARREL_R, .059), (.026, .085), (.029, .090), (.029, .112),
           (.031, .114), (.031, .122), (.027, .123), (.025, .117)])
tube(bm, [(0, .040, BARREL_R - .001), (0, .040, BARREL_R + .003)], [.0052, .0044], 8)  # rubber push switch
sharpen(bm)
body = new_obj("Body", bm)

# --- Reflector: never lit from behind, but seen by any reflection or death-cam ---
bm = bmesh.new()
lathe(bm, [(.025, .117), (.008, .103), (0, .103)])
reflector = new_obj("Reflector", bm)

# --- Glow: lens disc + tiny LED by the thumb, so the on/off state shows from the first-person view ---
bm = bmesh.new()
lathe(bm, [(0, .1165), (.0252, .1165)])
tube(bm, [(0, .056, .0158), (0, .056, .0172)], [.0022, .0018], 6)
glow = new_obj("Glow", bm)

# --- Hand: overlapping tubes, voxel-remeshed so it reads as one fleshy skin ---
WRIST = (.043, -.085, -.047)
ELBOW = (.190, -.430, -.300)
bm = bmesh.new()
FINGER_R = BARREL_R + .0085
for y, sweep, radius in [(.030, -190, .0095), (.008, -200, .0098), (-.014, -190, .0093), (-.034, -170, .0082)]:
    knuckle = Vector(at(28, FINGER_R + .010, y))
    tube(bm, [WRIST, tuple((Vector(WRIST) * .35 + knuckle * .65) + Vector((.006, 0, 0))), knuckle],
         [.014, .0135, .0115], 8)  # metacarpal
    steps = 6
    tube(bm, [knuckle] + [at(28 + (sweep - 28) * t / steps, FINGER_R, y - .006 * t / steps) for t in range(1, steps + 1)],
         [radius * (1.1 - .3 * t / steps) for t in range(steps + 1)], 8)
tube(bm, [(.033, -.058, -.004), (.034, -.030, .020), (.022, -.002, .033), (.006, .022, .030), (-.002, .034, .0275)],
     [.017, .0145, .0115, .0098, .0078], 8)  # thumb
tube(bm, [WRIST, tuple(Vector(WRIST).lerp(Vector(ELBOW), .3))], [.025, .029], 10)  # bare wrist
hand = new_obj("Hand", bm)
bpy.context.view_layer.objects.active = hand
hand.select_set(True)
mod = hand.modifiers.new("v", 'REMESH')
mod.mode = 'VOXEL'
mod.voxel_size = .0012
mod.use_smooth_shade = True
bpy.ops.object.modifier_apply(modifier="v")
ratio = HAND_TRIS / max(1, sum(len(p.vertices) - 2 for p in hand.data.polygons))
mod = hand.modifiers.new("d", 'DECIMATE')
mod.ratio = min(1.0, ratio)
bpy.ops.object.modifier_apply(modifier="d")
hand.select_set(False)
for p in hand.data.polygons:
    p.use_smooth = True

# --- Sleeve: rippled cloth tube ---
bm = bmesh.new()
w, e = Vector(WRIST), Vector(ELBOW)
tube(bm, [w.lerp(e, t) for t in (.10, .13, .16, .28, .40, .52, .64, .76, .88, 1.0)],
     [.030, .037, .038, .040, .046, .043, .050, .047, .054, .054], 12)
sleeve = new_obj("Sleeve", bm)

# --- Scuffed-metal albedo: speckle plus random scratches, tiled over the body by smart UVs ---
rng = np.random.default_rng(7)
SIZE = 256
tex = np.full((SIZE, SIZE), 0.20, dtype=np.float32)
for cells in (8, 16, 32, 64):  # value-noise octaves for grime patches
    coarse = rng.random((cells, cells)).astype(np.float32)
    tex += (np.kron(coarse, np.ones((SIZE // cells, SIZE // cells), dtype=np.float32)) - .5) * (.03 * 8 / cells + .006)
for _ in range(90):  # bright worn scratches
    x, y, a, n = rng.integers(0, SIZE), rng.integers(0, SIZE), rng.random() * 6.28, rng.integers(6, 40)
    for s in range(n):
        tex[int(y + s * math.sin(a)) % SIZE, int(x + s * math.cos(a)) % SIZE] += .10 + rng.random() * .12
img = bpy.data.images.new("Flashlight_Scuff", SIZE, SIZE)
rgba = np.ones((SIZE, SIZE, 4), dtype=np.float32)
rgba[..., :3] = np.clip(tex, 0, 1)[..., None]
img.pixels = rgba.ravel()
img.filepath_raw = os.path.join(OUT, "Flashlight_Scuff.png")
img.file_format = 'PNG'
img.save()

# UVs for the body only: hand, sleeve and glow are flat colour
bpy.context.view_layer.objects.active = body
body.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(island_margin=.01)
bpy.ops.object.mode_set(mode='OBJECT')
body.select_set(False)

for o in bpy.context.collection.objects:
    o.select_set(True)
    print(o.name, "tris", sum(len(p.vertices) - 2 for p in o.data.polygons))
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "Flashlight.fbx"), use_selection=True, object_types={'MESH'},
                         apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, axis_forward='-Z', axis_up='Y',
                         mesh_smooth_type='FACE', use_mesh_modifiers=False, bake_anim=False)
print("OK", OUT)
