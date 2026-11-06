# Kuldhara modular kit generator (low-poly, mobile).
# Run: blender -b --factory-startup -P kuldhara_kit.py -- <out_dir> <contact_sheet_png> [AssetName ...]
# out_dir must contain Textures/Sandstone_Diffuse.jpg, Sandstone_Normal.jpg, Wood_Diffuse.jpg.
# Conventions: meters, pivot = bottom-center, front faces -Y in Blender (verify facing in Unity).
#   Walls run along X, centered, thickness on Y. Wall modules are 4 m (half = 2 m), 4 m high, 0.6 m thick.
#   Wall-mounted props (doors/windows/jharokha) have their back at y=0: place them on a wall's front face.
#   All assets share 4 materials (Kit_Sandstone/Kit_Dark/Kit_Wood/Kit_Cloth) and world-space UVs
#   (2 m stone tile), so modules placed on a 2 m grid tile seamlessly and batch in Unity.
import bpy, bmesh, sys, os, math, random
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index("--") + 1:]
OUT, SHEET, ONLY = args[0], args[1], set(args[2:])
TEX = os.path.join(OUT, "Textures")
bpy.ops.wm.read_factory_settings(use_empty=True)

# ---------------- materials ----------------
def _img(name, color=True):
    i = bpy.data.images.load(os.path.join(TEX, name))
    if not color: i.colorspace_settings.name = "Non-Color"
    return i
def mat_tex(name, diff, nor=None, rough=0.9):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    d = nt.nodes.new("ShaderNodeTexImage"); d.image = _img(diff); nt.links.new(d.outputs["Color"], b.inputs["Base Color"])
    if nor:
        n = nt.nodes.new("ShaderNodeTexImage"); n.image = _img(nor, False); nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(n.outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    b.inputs["Roughness"].default_value = rough
    return m
def mat_flat(name, rgb):
    m = bpy.data.materials.new(name); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*rgb, 1); m.diffuse_color = (*rgb, 1)
    return m
STONE = mat_tex("Kit_Sandstone", "Sandstone_Diffuse.jpg", "Sandstone_Normal.jpg")
WOOD = mat_tex("Kit_Wood", "Wood_Diffuse.jpg", rough=0.8)
DARK = mat_flat("Kit_Dark", (0.05, 0.035, 0.025))
CLOTH = mat_flat("Kit_Cloth", (0.55, 0.12, 0.08))
TILE = {"Kit_Wood": 1.0}  # meters per texture repeat; default 2.0

# ---------------- primitives ----------------
def _fin(o, m): o.data.materials.append(m); return [o]
def box(sx, sy, sz, x=0, y=0, z=0, m=STONE, rot=(0, 0, 0)):  # z = bottom
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z + sz / 2), rotation=rot); o = bpy.context.object
    o.scale = (sx, sy, sz); return _fin(o, m)
def cyl(r, h, x=0, y=0, z=0, n=8, m=STONE):
    bpy.ops.mesh.primitive_cylinder_add(vertices=n, radius=r, depth=h, location=(x, y, z + h / 2)); return _fin(bpy.context.object, m)
def frustum(r1, r2, h, x=0, y=0, z=0, n=4, m=STONE):  # square by default (rotated 45 deg so sides align to axes)
    bpy.ops.mesh.primitive_cone_add(vertices=n, radius1=r1, radius2=r2, depth=h, location=(x, y, z + h / 2),
                                    rotation=(0, 0, math.pi / 4 if n == 4 else 0)); return _fin(bpy.context.object, m)
def disk(r, x, y, z, m=DARK, d=0.04, n=12):  # faces -Y
    bpy.ops.mesh.primitive_cylinder_add(vertices=n, radius=r, depth=d, location=(x, y, z), rotation=(math.pi / 2, 0, 0)); return _fin(bpy.context.object, m)
def dome(r, x=0, y=0, z=0, m=STONE):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=5, radius=r, location=(x, y, z)); o = bpy.context.object
    o.scale = (1, 1, 1.15); _fin(o, m)
    bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=r * 0.12, depth=r * 0.6, location=(x, y, z + r * 1.4)); return [o] + _fin(bpy.context.object, m)
def place(objs, loc=(0, 0, 0), rz=0.0):
    R = Matrix.Rotation(rz, 3, 'Z')
    for o in objs:
        o.location = R @ o.location + Vector(loc); o.rotation_euler.z += rz
    return objs

# ---------------- shared detail pieces (local: wall face y=0, outward -Y) ----------------
def arch_opening(w, h, z=0.0, y=0.0, frame=True):
    o = box(w + 0.3, 0.1, h + 0.2, 0, y - 0.05, z - 0.1) if frame else []
    return o + box(w, 0.04, h - w / 2, 0, y - 0.12, z, DARK) + disk(w / 2, 0, y - 0.12, z + h - w / 2)
def lattice(w, h, x, y, z, nx=3, nz=3):  # wooden jali bars over an opening
    o = []
    for i in range(1, nx + 1):
        o += box(0.05, 0.05, h, x - w / 2 + i * w / (nx + 1), y, z, WOOD)
    for i in range(1, nz + 1):
        o += box(w, 0.05, 0.05, x, y, z + i * h / (nz + 1), WOOD)
    return o
def relief(L, H, T, rng, n=6, hole=None):  # protruding blocks on both faces; hole=(x0,x1,z0,z1) keeps an area clear
    o = []
    for side in (-1, 1):
        for _ in range(n):
            w, h = rng.uniform(0.5, 1.0), rng.uniform(0.3, 0.45)
            x, z = rng.uniform(-L / 2 + w / 2, L / 2 - w / 2), rng.uniform(0.6, H - 0.6)
            if hole and x + w / 2 > hole[0] and x - w / 2 < hole[1] and z + h > hole[2] and z < hole[3]: continue
            o += box(w, 0.1, h, x, side * T / 2, z)
    return o
def rubble(n, rng, cx=0, cy=0, spread=1.0, s=(0.2, 0.5)):
    o = []
    for _ in range(n):
        a = rng.uniform(*s)
        o += box(a, a * rng.uniform(0.6, 1.2), a * rng.uniform(0.5, 0.9), cx + rng.uniform(-spread, spread),
                 cy + rng.uniform(-spread, spread), 0, rot=(rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), rng.uniform(0, 3.1)))
    return o
def parapet(sx, sy, z, cx=0.0, cy=0.0, h=0.7, step=2.5):
    o = []
    for side in (-1, 1):
        o += box(sx, 0.2, h, cx, cy + side * (sy / 2 - 0.1), z) + box(0.2, sy, h, cx + side * (sx / 2 - 0.1), cy, z)
    for i in range(max(1, round(sx / step)) + 1):
        x = cx - sx / 2 + i * sx / max(1, round(sx / step))
        o += box(0.3, 0.3, h + 0.25, x, cy - sy / 2 + 0.1, z) + box(0.3, 0.3, h + 0.25, x, cy + sy / 2 - 0.1, z)
    return o

# ---------------- MODULAR WALLS ----------------
L, H, T = 4.0, 4.0, 0.6
def wall_solid(l=L, seed=1, hole=None):
    rng = random.Random(seed)
    return (box(l, T, H) + box(l, T + 0.16, 0.45) + box(l, T + 0.1, 0.15, z=H) + relief(l, H, T, rng, int(l * 1.5), hole))
def wall_window(l=L):
    o = wall_solid(l, seed=2, hole=(-0.8, 0.8, 1.8, 3.4))
    o += box(1.1, T + 0.12, 1.1, 0, 0, 2.0) + box(0.8, T + 0.16, 0.8, 0, 0, 2.15, DARK) + box(1.3, T + 0.25, 0.1, 0, 0, 1.95)
    return o + lattice(0.8, 0.8, 0, -T / 2 - 0.09, 2.15) + lattice(0.8, 0.8, 0, T / 2 + 0.09, 2.15)
def wall_doorway(l=L, w=1.6, h=2.8):  # walkable opening
    p = (l - w) / 2; o = []
    for s in (-1, 1):
        x = s * (w / 2 + p / 2)
        o += box(p, T, H, x) + box(p, T + 0.16, 0.45, x) + box(0.25, T + 0.14, h, s * (w / 2 + 0.125))
    o += box(w, T, H - h, 0, 0, h) + box(w + 0.6, T + 0.14, 0.35, 0, 0, h) + box(l, T + 0.1, 0.15, z=H)
    return o + relief(l, H, T, random.Random(3), 4, (-w / 2 - 0.3, w / 2 + 0.3, 0, H))
def wall_broken(l=L, seed=4):
    rng = random.Random(seed); o = box(l, T + 0.16, 0.45); n = int(l / 0.5)
    for i in range(n):  # stepped broken top, tall on the left, collapsing to the right
        t = i / (n - 1); h = max(0.8, H * (1 - 0.7 * t ** 1.3) + rng.uniform(-0.35, 0.2))
        o += box(0.5, T, h, -l / 2 + 0.25 + i * 0.5)
    return o + rubble(6, rng, l * 0.25, -0.7, 0.9) + place(relief(l * 0.4, H * 0.55, T, rng, 3), (-l * 0.3, 0, 0))
def corner_post():
    return box(T + 0.1, T + 0.1, H + 0.25) + box(T + 0.35, T + 0.35, 0.5) + box(T + 0.3, T + 0.3, 0.2, z=H + 0.25)
def pillar_carved(h=4.5, s=1.0):
    k = h / 4.5; o = []
    for w, z0, hh in ((1.2, 0, 0.3), (1.0, 0.3, 0.3), (0.8, 0.6, 0.25)):
        o += box(w * s, w * s, hh * k, z=z0 * k)
    o += box(0.6 * s, 0.6 * s, 1.2 * k, z=0.85 * k) + cyl(0.4 * s, 0.2 * k, z=2.05 * k) + cyl(0.32 * s, 1.0 * k, z=2.25 * k)
    o += box(0.7 * s, 0.7 * s, 0.2 * k, z=3.25 * k) + box(0.55 * s, 0.55 * s, 0.5 * k, z=3.45 * k)
    o += frustum(0.42 * s, 0.9 * s, 0.35 * k, z=3.95 * k)
    return o + box(1.3 * s, 1.3 * s, 0.2 * k, z=4.3 * k)
def platform(sx=4.0, sy=4.0, h=0.6):
    return box(sx, sy, h) + box(sx + 0.1, sy + 0.1, 0.1, z=h - 0.05)
def stairs(w=3.0, n=4, rise=0.25, run=0.4):  # climbs toward +Y, front edge at y=0
    return sum((box(w, run * (n - i), rise, 0, run * i + run * (n - i) / 2, rise * i) for i in range(n)), [])

# ---------------- DOORS & WINDOWS (back at y=0, front -Y) ----------------
def door_arched(w=2.0, h=3.0):
    z0 = 0.25; o = box(w + 1.6, 1.0, z0, 0, -0.5)                        # threshold
    o += box(w + 0.5, 0.3, h + 0.35, 0, -0.15, z0)                       # back panel (spandrels)
    o += disk(w / 2 + 0.15, 0, -0.31, z0 + h - w / 2, STONE, 0.02, 12) + box(w + 0.3, 0.02, h - w / 2, 0, -0.31, z0)  # arch trim
    o += box(w, 0.08, h - w / 2, 0, -0.36, z0, WOOD) + disk(w / 2, 0, -0.36, z0 + h - w / 2, WOOD, 0.08)
    o += box(0.04, 0.1, h - 0.1, 0, -0.41, z0, DARK)                      # leaf seam
    for s in (-1, 1):                                                     # jambs with base + capital
        x = s * (w / 2 + 0.47)
        o += box(0.45, 0.6, h + 0.35, x, -0.3, z0) + box(0.65, 0.75, 0.45, x, -0.33, z0) + box(0.62, 0.72, 0.25, x, -0.32, z0 + h + 0.35)
    o += box(w + 1.5, 0.65, 0.55, 0, -0.32, z0 + h + 0.6) + box(w + 1.8, 0.8, 0.15, 0, -0.38, z0 + h + 1.15)
    return o
def window_balcony(w=1.3, h=1.9):  # wall jharokha window with brackets, railing, lattice
    o = []
    for x in (-0.9, -0.3, 0.3, 0.9):
        o += box(0.22, 0.6, 0.5, x, -0.3, 0.1)                            # brackets
    o += box(2.6, 0.95, 0.2, 0, -0.47, 0.6) + box(2.4, 0.1, 0.65, 0, -0.88, 0.8)
    o += box(2.4, 0.3, 3.0, 0, -0.15, 0.8)                                # back panel
    o += arch_opening(w, h, 1.0, -0.3, frame=False) + lattice(w, h - w / 2, 0, -0.45, 1.0, 4, 4)
    for s in (-1, 1):
        o += cyl(0.08, 2.2, s * 1.05, -0.8, 0.8)
    return o + box(2.9, 1.15, 0.15, 0, -0.55, 3.0) + box(2.5, 0.5, 0.45, 0, -0.25, 3.15)
def jharokha(w=1.8, z=0.6, with_dome=True):
    o = box(w * 0.7, 0.5, 0.45, 0, -0.25, z - 0.45) + box(w + 0.2, 1.0, 0.15, 0, -0.5, z) + box(w, 0.08, 0.75, 0, -0.96, z + 0.15)
    o += box(0.08, 0.9, 0.75, -w / 2, -0.5, z + 0.15) + box(0.08, 0.9, 0.75, w / 2, -0.5, z + 0.15)
    for px in (-w / 2 + 0.08, w / 2 - 0.08):
        o += cyl(0.07, 1.65, px, -0.9, z + 0.15)
    o += box(w + 0.45, 1.25, 0.15, 0, -0.55, z + 1.8) + arch_opening(w * 0.55, 1.5, z + 0.2)
    return o + (dome(w * 0.42, 0, -0.5, z + 1.95) if with_dome else [])
def window_small(w=0.9, h=1.0, z=1.4):  # wall-face lattice window with wooden chajja
    return (box(w + 0.3, 0.12, h + 0.3, 0, -0.06, z - 0.15) + box(w, 0.04, h, 0, -0.14, z, DARK) + lattice(w, h, 0, -0.17, z)
            + box(w + 0.5, 0.2, 0.1, 0, -0.1, z - 0.25) + box(w + 0.7, 0.55, 0.08, 0, -0.27, z + h + 0.25, WOOD))

# ---------------- PROPS ----------------
def rubble_pile(seed=7):
    rng = random.Random(seed); return rubble(10, rng, 0, 0, 0.8) + rubble(4, rng, 0, 0, 0.3, (0.35, 0.6))
def awning(w=3.4, d=2.2):  # attaches to wall at y=0, slopes out to -Y
    a = math.atan2(0.6, d)
    o = box(w, d + 0.1, 0.03, 0, -d / 2, 2.6 - 0.3, CLOTH, rot=(-a, 0, 0))
    for s in (-1, 1):
        o += cyl(0.05, 2.35, s * (w / 2 - 0.1), -d, 0, 6, WOOD)
    return o

# ---------------- TEMPLE ----------------
def temple():
    o = box(11, 11, 0.9) + box(10, 10, 0.7, z=0.9)
    for i in range(6):
        d = 0.45 * (i + 1); o += box(4.0, d, 1.6 - 0.267 * i, 0, -5.5 - d / 2)
    o += box(5, 5, 3.6, 0, 1.8, 1.6)                                          # garbhagriha
    o += arch_opening(1.3, 2.4, 1.6, -0.7)
    for x, y in ((-3.4, -4.4), (-1.4, -4.4), (1.4, -4.4), (3.4, -4.4), (-3.4, -2.2), (3.4, -2.2)):
        o += place(pillar_carved(3.2, 0.6), (x, y, 1.6))                      # mandapa pillars
    o += box(8.4, 4.8, 0.3, 0, -2.6, 4.8) + box(9.0, 5.4, 0.12, 0, -2.6, 5.1)
    o += box(3.6, 3.0, 0.4, 0, -2.6, 5.22) + box(2.6, 2.2, 0.35, 0, -2.6, 5.62) + dome(0.9, 0, -2.6, 5.97)
    for sx in (-1, 1):
        for sy in (-4.9, -0.3):
            o += dome(0.3, sx * 4.2, sy, 5.22)
    for x in (-0.7, 0.7):                                                     # bells
        o += cyl(0.02, 0.5, x, -4.4, 4.3, 4, DARK) + frustum(0.16, 0.05, 0.28, x, -4.4, 4.02, 6, DARK)
    z = 5.2; o += box(5.6, 5.6, 0.4, 0, 1.8, z); z += 0.4                    # shikhara
    for i, (r1, r2, h) in enumerate(((3.39, 3.0, 1.2), (3.0, 2.5, 1.2), (2.5, 1.9, 1.1), (1.9, 1.2, 1.0), (1.2, 0.6, 0.8))):
        o += frustum(r1, r2, h, 0, 1.8, z); z += h
    for dx, dy in ((2.0, 0), (-2.0, 0), (0, 2.0), (0, -2.0)):                 # side mini-shikharas
        o += frustum(1.0, 0.3, 2.0, dx, 1.8 + dy, 5.6)
    o += cyl(0.75, 0.35, 0, 1.8, z, 12) + dome(0.3, 0, 1.8, z + 0.35)        # amalaka + kalash
    o += cyl(0.03, 1.8, 0, 1.8, z + 0.6, 4, WOOD) + box(0.8, 0.02, 0.5, 0.4, 1.8, z + 1.85, CLOTH)
    return o

# ---------------- HOUSES ----------------
def house(ruined=False, seed=11):
    rng = random.Random(seed); HX, HY, HH, P = 8.0, 6.0, 3.4, 0.4
    o = box(HX + 1.5, HY + 1.5, P)
    fy = -HY / 2
    if ruined:   # four walls, two of them collapsing, no roof
        # front wall has a real 1.2 m doorway, so ruins can be entered
        o += box(2.6, 0.5, HH, -2.7, fy + 0.25, P) + box(4.2, 0.5, HH, 1.9, fy + 0.25, P) + box(1.2, 0.5, HH - 2.2, -0.8, fy + 0.25, P + 2.2)
        o += box(0.5, HY, HH, -HX / 2 + 0.25, 0, P)
        for i in range(16):
            t = i / 15; h = max(0.6, HH * (1 - 0.75 * t) + rng.uniform(-0.4, 0.2))
            o += box(0.5, 0.5, h, -HX / 2 + 0.25 + i * 0.5, HY / 2 - 0.25, P)
        for i in range(12):
            t = i / 11; h = max(0.6, HH * (0.4 + 0.5 * t) + rng.uniform(-0.4, 0.2))
            o += box(0.5, 0.5, h, HX / 2 - 0.25, -HY / 2 + 0.25 + i * 0.5, P)
        o += rubble(8, rng, 2.0, 1.5, 1.2) + rubble(5, rng, 3.0, 4.2, 0.8)
    else:
        o += box(HX, HY, HH, 0, 0, P) + parapet(HX, HY, P + HH, h=0.5, step=2.0)
        o += box(0.7, 0.7, 0.9, -HX / 2 + 0.35, fy + 0.35, P + HH) + box(0.7, 0.7, 0.9, HX / 2 - 0.35, fy + 0.35, P + HH)
        o += box(0.6, 0.6, 0.8, 2.5, 2.0, P + HH)
        o += place(awning(2.6, 1.6), (-3.2, fy, P + 0.4))
        o += place(window_small(0.9, 1.0, 1.4), (-3.2, fy, P))
    beams = [x * 0.5 - 3.5 for x in range(0, 15, 2)]
    for x in beams if not ruined else beams[:3]:                               # protruding wooden vigas
        o += box(0.18, 0.9, 0.18, x, fy - 0.2, P + HH - 0.45, WOOD)
    o += box(2.8, 1.6, 0.8 - P, -3.25, fy - 0.8, P)                           # otla platform
    for i in range(2):
        o += box(1.6, 0.4 * (2 - i), 0.2, -0.8, fy - 0.2 * (2 - i), 0.2 * i + P - 0.4 + 0.4)
    if not ruined: o += box(1.2, 0.04, 2.2, -0.8, fy - 0.02, P, WOOD)
    o += box(0.25, 0.2, 2.45, -1.52, fy - 0.1, P) + box(0.25, 0.2, 2.45, -0.08, fy - 0.1, P) + box(1.9, 0.3, 0.25, -0.8, fy - 0.15, P + 2.45)
    o += box(2.2, 0.6, 0.08, -0.8, fy - 0.3, P + 2.85, WOOD)
    o += place(window_small(0.9, 1.0, 1.3), (1.8, fy, P))
    if not ruined:                                                             # right side: window + jharokha
        o += place(window_small(0.9, 1.0, 1.3) + place(jharokha(1.4, 1.2), (1.4, 0, 0)), (HX / 2, -1.2, P), math.pi / 2)
    return o

# ---------------- build + export ----------------
ASSETS = [
    ("Kit_Wall_4m", wall_solid), ("Kit_Wall_2m", lambda: wall_solid(2.0, 5)), ("Kit_Wall_4m_Window", wall_window),
    ("Kit_Wall_4m_Doorway", wall_doorway), ("Kit_Wall_4m_Broken", wall_broken), ("Kit_Wall_CornerPost", corner_post),
    ("Kit_Pillar_Carved", pillar_carved), ("Kit_Platform_4m", platform), ("Kit_Stairs_3m", stairs),
    ("Kit_Door_Grand", door_arched), ("Kit_Door_Small", lambda: door_arched(1.3, 2.4)),
    ("Kit_Window_Balcony", window_balcony), ("Kit_Jharokha_Domed", jharokha),
    ("Kit_Rubble_Pile", rubble_pile), ("Kit_Awning", awning),
    ("Temple", temple), ("House_Small", house), ("House_Ruined", lambda: house(True)),
]

def reset():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o)
    for m in list(bpy.data.meshes): bpy.data.meshes.remove(m)

def finalize(name):
    objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='SELECT'); bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True); bpy.ops.object.join()
    obj = bpy.context.object; obj.name = name
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.remove_doubles(threshold=0.001); bpy.ops.object.mode_set(mode='OBJECT')
    bm = bmesh.new(); bm.from_mesh(obj.data); uv = bm.loops.layers.uv.verify()
    mats = obj.data.materials
    for f in bm.faces:
        t = TILE.get(mats[f.material_index].name, 2.0); n = f.normal; ax = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            c = l.vert.co
            l[uv].uv = (c.x / t, c.y / t) if ax == 2 else (c.y / t, c.z / t) if ax == 0 else (c.x / t, c.z / t)
    bm.to_mesh(obj.data); bm.free()
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True, object_types={'MESH'},
        apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, axis_forward='-Z', axis_up='Y',
        mesh_smooth_type='FACE', path_mode='RELATIVE', embed_textures=False)
    d = obj.dimensions
    print(f"[ok] {name:24s} tris={sum(len(p.vertices) - 2 for p in obj.data.polygons):5d} "
          f"mats={len(mats)} size={d.x:.1f}x{d.y:.1f}x{d.z:.1f}")
    return obj

def render(obj, path, res=384):
    sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'; sc.display.shading.show_cavity = True
    sc.render.resolution_x = sc.render.resolution_y = res
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; d = obj.dimensions; cam.data.ortho_scale = max(d) * 1.45
    c = obj.matrix_world @ (sum((Vector(b) for b in obj.bound_box), Vector()) / 8)
    cam.location = c + Vector((-1, -1.3, 0.8)).normalized() * 60; cam.data.clip_end = 200
    t = bpy.data.objects.new("T", None); sc.collection.objects.link(t); t.location = c
    k = cam.constraints.new('TRACK_TO'); k.target = t; k.track_axis = 'TRACK_NEGATIVE_Z'; k.up_axis = 'UP_Y'
    sc.render.filepath = path; bpy.ops.render.render(write_still=True)

import numpy as np
thumbs = []
tmp = os.path.dirname(SHEET)
for name, fn in ASSETS:
    if ONLY and name not in ONLY: continue
    reset(); fn(); obj = finalize(name)
    p = os.path.join(tmp, f"_thumb_{name}.png"); render(obj, p); thumbs.append(p)

cols = 6; rows = math.ceil(len(thumbs) / cols); R = 384
sheet = np.zeros((rows * R, cols * R, 4), dtype=np.float32)
for i, p in enumerate(thumbs):
    im = bpy.data.images.load(p); px = np.array(im.pixels[:], dtype=np.float32).reshape(R, R, 4)
    r, c = divmod(i, cols); sheet[(rows - 1 - r) * R:(rows - r) * R, c * R:(c + 1) * R] = px
out = bpy.data.images.new("sheet", cols * R, rows * R, alpha=True); out.pixels[:] = sheet.ravel()
out.filepath_raw = SHEET; out.file_format = 'PNG'; out.save()
print("[ok] sheet", SHEET, "order:", [os.path.basename(t)[7:-4] for t in thumbs])
