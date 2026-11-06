# Low-poly horror props sharing one atlas/material. Pivots at base centre, each FBX has _LOD0 and _LOD1.
# Run: blender --background --factory-startup --python Tools/Blender/horror_props.py
import bpy, bmesh, math, os, random
import numpy as np
from mathutils import Vector, noise

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, "..", "..", "Assets", "MAIN GAME", "3D models", "HorrorProps")
os.makedirs(OUT, exist_ok=True)
BARK, WOOD, STONE, LEAF = 0, 1, 2, 3
REGION = {BARK: (0, .5), WOOD: (.5, .5), STONE: (0, 0), LEAF: (.5, 0)}  # atlas quadrant origin
UV_PER_M = .5 / 1.6

# ---------- atlas ----------
def octave(n, cx, cy, rs):
    g = rs.random((cy + 1, cx + 1)); g[-1] = g[0]; g[:, -1] = g[:, 0]
    y = np.linspace(0, cy, n, endpoint=False); x = np.linspace(0, cx, n, endpoint=False)
    yi, xi = y.astype(int), x.astype(int); fy, fx = (y - yi)[:, None], (x - xi)[None, :]
    a = g[yi][:, xi]; b = g[yi][:, xi + 1]; c = g[yi + 1][:, xi]; d = g[yi + 1][:, xi + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy

def tile(n, col, var, stretch, rs):  # noise cells are stretched along v (the long axis of a face)
    v = sum(octave(n, int(4 * 2 ** i * stretch), 4 * 2 ** i, rs) / 2 ** i for i in range(4)) / 1.875
    return np.stack([np.clip(c * (1 + (v - .5) * var), 0, 1) for c in col], -1)

def save_atlas():
    n, rs = 256, np.random.default_rng(7)
    a = np.zeros((n * 2, n * 2, 4)); a[..., 3] = 1  # pixels run bottom-up, matching UV origin
    a[n:, :n, :3] = tile(n, (.27, .21, .16), 1.6, 5, rs)
    a[n:, n:, :3] = tile(n, (.36, .29, .21), 1.3, 6, rs)
    a[:n, :n, :3] = tile(n, (.55, .44, .32), 1.0, 1, rs)
    a[:n, n:, :3] = tile(n, (.29, .26, .14), 2.0, 1, rs)
    img = bpy.data.images.new("HorrorProps_Atlas", n * 2, n * 2)
    img.pixels[:] = a.ravel(); img.filepath_raw = os.path.join(OUT, "HorrorProps_Atlas.png")
    img.file_format = 'PNG'; img.save()
    return img

# ---------- mesh helpers: every face stores its atlas region in material_index until UVs are made ----------
def tube(bm, pts, region, sides, ground=False):  # pts = [(Vector, radius), ...]
    rings = []
    for i, (p, r) in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)][0] - pts[max(i - 1, 0)][0]).normalized()
        a = d.orthogonal().normalized(); b = d.cross(a)
        rings.append([bm.verts.new(p + (a * math.cos(k * 2 * math.pi / sides) + b * math.sin(k * 2 * math.pi / sides)) * r) for k in range(sides)])
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(sides):
            f = bm.faces.new((r0[k], r0[(k + 1) % sides], r1[(k + 1) % sides], r1[k])); f.material_index = region; f.smooth = True

def block(bm, size, loc, region, rot=(0, 0, 0)):
    m = bmesh.ops.create_cube(bm, size=1)["verts"]
    fs = {f for v in m for f in v.link_faces}
    e = __import__("mathutils").Euler(rot)
    for v in m:
        c = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2])); c.rotate(e); v.co = c + Vector(loc)
    for f in fs: f.material_index = region

def blob(bm, r, loc, region, sq, seed, amp=.35, subdiv=2):
    vs = bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1)["verts"]
    fs = {f for v in vs for f in v.link_faces}
    o = Vector((seed * 3.1, seed * 1.7, seed * .9))
    for v in vs:
        n = v.co.copy(); s = 1 + amp * noise.fractal(n * 1.4 + o, 1.0, 2.0, 3)
        v.co = Vector((n.x * r * sq[0] * s, n.y * r * sq[1] * s, n.z * r * sq[2] * s)) + Vector(loc)
    for f in fs: f.material_index = region

def branch(bm, p, d, length, r, depth, rng, sides, region=BARK):
    n = 3 if depth > 0 else 2
    pts, step = [(p, r)], length / n
    for i in range(n):
        d = (d + Vector((rng.uniform(-.3, .3), rng.uniform(-.3, .3), rng.uniform(-.15, .2)))).normalized()
        p = p + d * step
        pts.append((p, r * (1 - .28 * (i + 1) / n) if depth > 0 else r * .25 * (n - i - 1) + .004))
    tube(bm, pts, region, sides)
    if depth <= 0: return
    for k in range(rng.randint(2, 3)):
        a = rng.uniform(0, 6.28); nd = (d + Vector((math.cos(a), math.sin(a), rng.uniform(-.25, .25))) * rng.uniform(.6, 1.1)).normalized()
        branch(bm, p if k < 2 else pts[-2][0], nd, length * rng.uniform(.55, .75), pts[-1][1] * .72, depth - 1, rng, max(3, sides - 1), region)

# ---------- props ----------
def tree(seed, h, r, spread):
    def make(bm):
        rng = random.Random(seed); lean = Vector((rng.uniform(-.2, .2), rng.uniform(-.2, .2), 1)).normalized()
        pts = [(Vector((0, 0, -.3)), r * 1.5), (Vector((0, 0, .05)), r * 1.35), (lean * h * .25, r * .95), (lean * h * .5 + Vector((rng.uniform(-.25, .25), rng.uniform(-.25, .25), 0)), r * .78)]
        tube(bm, pts, BARK, 6)
        for i in range(3):  # exposed roots
            a = i * 2.1 + seed; d = Vector((math.cos(a), math.sin(a), -.25)).normalized()
            tube(bm, [(Vector((0, 0, .25)), r * .55), (d * .7 + Vector((0, 0, .1)), r * .32), (d * 1.5, r * .08)], BARK, 3)
        for k in range(3):
            a = k * 2.2 + seed; d = (lean * .9 + Vector((math.cos(a), math.sin(a), 0)) * spread).normalized()
            branch(bm, pts[-1][0], d, h * .3, r * .55, 2, rng, 4)
    return make

def snag(bm):  # snapped trunk with a jagged top
    rng = random.Random(4)
    tube(bm, [(Vector((0, 0, -.3)), .5), (Vector((0, 0, .1)), .42), (Vector((.06, 0, 1.3)), .3), (Vector((.1, .05, 2.5)), .24), (Vector((.12, .05, 3.0)), .05)], BARK, 6)
    branch(bm, Vector((.06, 0, 1.6)), Vector((.8, .2, .5)).normalized(), 1.2, .09, 1, rng, 3)

def bush(seed, r, h, twigs):
    def make(bm):
        rng = random.Random(seed)
        for i in range(twigs):
            a = rng.uniform(0, 6.28); d = Vector((math.cos(a) * rng.uniform(.4, 1), math.sin(a) * rng.uniform(.4, 1), rng.uniform(.5, 1.1))).normalized()
            branch(bm, Vector((rng.uniform(-.2, .2) * r, rng.uniform(-.2, .2) * r, -.05)), d, max(r, h) * rng.uniform(.75, 1.1), .035, 1, rng, 3)
        for i in range(twigs * 2):
            a = rng.uniform(0, 6.28); q = rng.uniform(.15, .95) * r
            blob(bm, r * rng.uniform(.13, .24), (math.cos(a) * q, math.sin(a) * q, h * rng.uniform(.2, .95) * (1.15 - q / r * .6)), LEAF, (1, 1, .8), seed + i, .9, 1)
    return make

def rock(seed, r, sq):
    def make(bm):
        blob(bm, r, (0, 0, r * sq[2] * .55), STONE, sq, seed, .5, 2)
        for v in bm.verts: v.co.z = max(v.co.z, -.15)
    return make

def fence(seed, broken):
    def make(bm):
        rng = random.Random(seed); L = 3.2
        for i in range(4):
            x = -L / 2 + i * L / 3; tilt = rng.uniform(-.14, .14)
            if i in broken: block(bm, (.12, .12, rng.uniform(.5, .8)), (x, 0, .3), WOOD, (rng.uniform(-.1, .1), tilt, 0)); continue
            block(bm, (.13, .13, 1.9), (x, rng.uniform(-.04, .04), .9), WOOD, (rng.uniform(-.12, .12), tilt, 0))
        for z, a, b in ((1.5, -.1, .95), (.8, 0, .95)):  # rails
            block(bm, ((b - a) * L, .05, .1), ((a + b) * L / 2 - L / 2, .07, z), WOOD, (0, rng.uniform(-.06, .06), 0))
        for i in range(12):  # slats, gap-toothed
            if rng.random() < .3: continue
            x = -L / 2 + .15 + i * .26; h = rng.uniform(.8, 1.75)
            block(bm, (.14, .03, h), (x, .11, h / 2 + rng.uniform(0, .12)), WOOD, (0, rng.uniform(-.07, .07), 0))
        block(bm, (.14, .03, 1.5), (.6, .7, .04), WOOD, (1.5, 0, rng.uniform(-.9, .9)))
    return make

def debris(bm):
    rng = random.Random(9)
    for i in range(9):
        L = rng.uniform(.8, 2.4); s = rng.choice(((.2, .05), (.12, .12), (.26, .04)))
        block(bm, (L, s[0], s[1]), (rng.uniform(-1, 1), rng.uniform(-.9, .9), .06 + i * .05), WOOD, (rng.uniform(-.14, .14), rng.uniform(-.14, .14), rng.uniform(0, 3.14)))
    for i in range(4):
        blob(bm, rng.uniform(.14, .26), (rng.uniform(-1.2, 1.2), rng.uniform(-1, 1), .1), STONE, (1, 1, .7), i, .5, 1)

def wheel(bm, loc, rot, R=.6):
    e = __import__("mathutils").Euler(rot); pts = []
    for k in range(10):
        a, b = k * .628, (k + 1) * .628
        mid = Vector((math.cos((a + b) / 2) * R, 0, math.sin((a + b) / 2) * R)); mid.rotate(e)
        block(bm, (R * .66, .09, .1), Vector(loc) + mid, WOOD, (rot[0], rot[1] - (a + b) / 2 + 1.571, rot[2]))
    for k in range(5):
        a = k * 1.257; mid = Vector((math.cos(a) * R / 2, 0, math.sin(a) * R / 2)); mid.rotate(e)
        block(bm, (R, .05, .05), Vector(loc) + mid, WOOD, (rot[0], rot[1] - a, rot[2]))

def cart(bm):  # bullock cart: one wheel gone, bed dropped onto its axle, shaft in the dirt
    rng = random.Random(3); t = (0, -.2, 0); h = .68
    for i in range(7):
        y = -.72 + i * .24
        if i != 4: block(bm, (2.5, .22, .05), (0, y, h + .07 * math.sin(i)), WOOD, (rng.uniform(-.02, .02), rng.uniform(-.25, -.15), 0))
    for s in (-1, 1):
        block(bm, (2.6, .08, .1), (0, s * .8, h - .1), WOOD, t)
        for x in (-.9, 0, .9):
            block(bm, (.06, .06, .65), (x, s * .8, h + .3 + x * .2), WOOD, t)
        block(bm, (2.4, .05, .08), (-.05, s * .8, h + .6), WOOD, t)
    block(bm, (2.2, .1, .1), (-2.0, 0, .4), WOOD, (0, -.32, 0))
    block(bm, (.1, 1.7, .1), (.25, 0, .52), WOOD, (0, 0, 0))
    wheel(bm, (.25, 1.0, .6), (0, 0, .05)); wheel(bm, (.3, -1.9, .08), (1.5, 0, .3))

def charpoy(bm):  # rope bed with one leg snapped
    rng = random.Random(5); W, D = 1.9, .95; t = (.0, .07, .0)
    for s in (-1, 1):
        block(bm, (W, .07, .07), (0, s * D / 2, .46), WOOD, t); block(bm, (.07, D, .07), (s * W / 2, 0, .46 - s * .07), WOOD, t)
    for sx, sy, h in ((-1, -1, .46), (-1, 1, .42), (1, -1, .24), (1, 1, .46)):
        block(bm, (.09, .09, h), (sx * W / 2, sy * D / 2, h / 2), WOOD, (rng.uniform(-.05, .05), 0, 0))
    for i in range(7):
        if i != 3: block(bm, (W * .95, .018, .018), (0, -D / 2 + (i + 1) * D / 8, .45), LEAF, (0, .07, 0))
    block(bm, (.018, D * .6, .018), (.3, -.1, .3), LEAF, (.5, 0, .3))

def wall_ruin(bm):  # mud-brick stub with a jagged, crumbling top
    rng = random.Random(6); L, H = 4.0, 2.8
    n = 9; w = L / n
    for i in range(n):
        t = abs(i - 2.5) / 6.5; h = max(.6, H * (1 - t) + rng.uniform(-.35, .25))
        block(bm, (w + .02, .5, h), (-L / 2 + w * (i + .5), rng.uniform(-.03, .03), h / 2 - .1), STONE)
    for i in range(5):
        blob(bm, rng.uniform(.14, .3), (rng.uniform(-L / 2, L / 2), rng.uniform(.4, 1.1) * rng.choice((-1, 1)), .1), STONE, (1, 1, .65), i, .5, 1)

def pots(bm):  # three clay pots, one tipped over and broken open
    for (x, y, k, tilt) in ((0, 0, 1, 0), (.42, .22, .75, 0), (-.5, .3, .85, 1.3)):
        prof = ((0, .1), (.03, .2), (.22, .3), (.42, .25), (.56, .1), (.64, .13))
        lean = Vector((math.sin(tilt), 0, math.cos(tilt))); base = Vector((x, y, .22 if tilt else 0))
        tube(bm, [(base + lean * h * k, r * k) for h, r in prof], STONE, 8)

PROPS = [("DeadTree_A", tree(3, 8.0, .3, .8)), ("DeadTree_B", tree(8, 6.2, .26, 1.3)), ("DeadTree_Snag", snag),
         ("Bush_Dry", bush(5, .9, 1.1, 8)), ("Bush_Thicket", bush(12, 1.5, 2.5, 12)),
         ("Rock_Round", rock(2, 1.0, (1, .85, .75))), ("Rock_Slab", rock(6, 1.1, (.65, .5, 1.25))),
         ("Fence_Broken_A", fence(1, {2})), ("Fence_Broken_B", fence(4, {0, 3})),
         ("Wood_Debris", debris), ("Cart_Abandoned", cart), ("Charpoy_Broken", charpoy), ("WallRuin", wall_ruin), ("ClayPots", pots)]

# ---------- finish + export ----------
def to_object(name, make, mat):
    bm = bmesh.new(); make(bm); uv = bm.loops.layers.uv.verify(); rng = random.Random(1)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        o = REGION[f.material_index]; f.material_index = 0
        e = max(f.edges, key=lambda e: e.calc_length()); v = (e.verts[1].co - e.verts[0].co).normalized(); u = f.normal.cross(v)
        c = [(l, l.vert.co.dot(u) * UV_PER_M, l.vert.co.dot(v) * UV_PER_M) for l in f.loops]
        mu, mv = min(x[1] for x in c), min(x[2] for x in c); su, sv = max(x[1] for x in c) - mu, max(x[2] for x in c) - mv
        off = (o[0] + .01 + rng.random() * max(0, .48 - su), o[1] + .01 + rng.random() * max(0, .48 - sv))
        for l, a, b in c: l[uv].uv = (off[0] + min(a - mu, .48), off[1] + min(b - mv, .48))
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); me.materials.append(mat)
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    return ob

def tris(ob): return sum(len(p.vertices) - 2 for p in ob.data.polygons)

def clear():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o)

img = save_atlas()
mat = bpy.data.materials.new("HorrorProps"); mat.use_nodes = True
tn = mat.node_tree.nodes.new("ShaderNodeTexImage"); tn.image = img
mat.node_tree.links.new(tn.outputs[0], mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"])

for name, make in PROPS:
    clear()
    lod0 = to_object(name + "_LOD0", make, mat)
    lod1 = to_object(name + "_LOD1", make, mat)
    bpy.context.view_layer.objects.active = lod1
    m = lod1.modifiers.new("d", 'DECIMATE'); m.ratio = .4; bpy.ops.object.modifier_apply(modifier="d")
    for o in (lod0, lod1): o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True, object_types={'MESH'},
        apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, axis_forward='-Z', axis_up='Y',
        mesh_smooth_type='FACE', path_mode='RELATIVE', embed_textures=False)
    d = lod0.dimensions
    print(f"[ok] {name:16s} tris={tris(lod0):5d}/{tris(lod1):4d} size={d.x:.1f}x{d.y:.1f}x{d.z:.1f}")

# preview sheet
clear(); x = 0
for name, make in PROPS:
    o = to_object(name, make, mat); o.location.x = x + o.dimensions.x / 2; x += o.dimensions.x + 1
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'
sc.display.shading.color_type = 'TEXTURE'; sc.render.resolution_x, sc.render.resolution_y = 1000, 260
cam = bpy.data.objects.new("C", bpy.data.cameras.new("C")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = x * 1.02; cam.data.clip_end = 300
cam.location = (x / 2, -100, 4); cam.rotation_euler = (1.5708, 0, 0)
sc.render.filepath = os.path.join(ROOT, "horror_props_preview.png"); bpy.ops.render.render(write_still=True)
