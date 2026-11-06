# Rigs Sketchfab GhostWoman (static A-pose) into Assets/MAIN GAME/3D models/Chudail/Chudail.fbx (T-pose, Humanoid-ready).
# blender -b --python Tools/chudail_rig.py
import bpy
from mathutils import Vector, Matrix
ROOT = r"D:\Goutam\UnityProjects\Horror project\Assets\MAIN GAME\3D models"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=ROOT + r"\Sketchfab\GhostWoman\GhostWoman.fbx")
ob = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
ob.name = "Chudail"
bpy.context.view_layer.objects.active = ob
ob.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=0.002)  # weld loose shells so the skirt can't tear open
bpy.ops.object.mode_set(mode='OBJECT')
m = ob.modifiers.new("d", 'DECIMATE'); m.ratio = 9000 / len(ob.data.polygons)
bpy.ops.object.modifier_apply(modifier="d")

# Joint positions read off the mesh (front = -Y, her left = +X).
J = {
 "Hips": ((0,-.01,.93),(0,-.01,1.02),None), "Spine": ((0,-.01,1.02),(0,-.01,1.12),"Hips"),
 "Spine1": ((0,-.01,1.12),(0,-.01,1.24),"Spine"), "Spine2": ((0,-.01,1.24),(0,0,1.40),"Spine1"),
 "Neck": ((0,0,1.40),(0,0,1.47),"Spine2"), "Head": ((0,0,1.47),(0,0,1.68),"Neck"),
}
for s, n in ((1, "Left"), (-1, "Right")):
    J.update({
     n+"Shoulder": ((s*.04,0,1.34),(s*.17,0,1.30),"Spine2"), n+"Arm": ((s*.17,0,1.30),(s*.31,0,1.115),n+"Shoulder"),
     n+"ForeArm": ((s*.31,0,1.115),(s*.445,0,1.03),n+"Arm"), n+"Hand": ((s*.445,0,1.03),(s*.50,-.01,.86),n+"ForeArm"),
     n+"UpLeg": ((s*.09,-.01,.90),(s*.09,.03,.50),"Hips"), n+"Leg": ((s*.09,.03,.50),(s*.09,.08,.09),n+"UpLeg"),
     n+"Foot": ((s*.09,.08,.09),(s*.09,-.03,.03),n+"Leg"), n+"ToeBase": ((s*.09,-.03,.03),(s*.09,-.11,.02),n+"Foot"),
    })
arm = bpy.data.objects.new("Armature", bpy.data.armatures.new("Armature"))
bpy.context.scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
for k, (h, t, _) in J.items():
    b = arm.data.edit_bones.new(k); b.head, b.tail = Vector(h), Vector(t)
for k, (_, _, p) in J.items():
    if p: arm.data.edit_bones[k].parent = arm.data.edit_bones[p]
bpy.ops.object.mode_set(mode='OBJECT')

def seg(p, k):
    a, b = Vector(J[k][0]), Vector(J[k][1]); d = b - a
    t = max(0, min(1, (p - a).dot(d) / d.dot(d)))
    return (a + d * t - p).length

def inv(p, names, scale=1.0):
    return {k: scale * (seg(p, k) + .01) ** -4 for k in names}

def norm(w):
    s = sum(w.values()); return {k: v / s for k, v in w.items()} if s else w

def weights(p):
    s = "Left" if p.x > 0 else "Right"
    if p.z > 1.46 and abs(p.x) < .2: return {"Head": 1}
    if abs(p.x) > .24 and p.z > .75:  # arm reached past the body silhouette
        return norm(inv(p, [s+"Arm", s+"ForeArm", s+"Hand"]))
    if p.z < .21 and abs(p.x) < .16:  # bare feet under the hem
        if p.y < -.03 and p.z < .05: return {s+"ToeBase": 1}
        return {s+"Foot": 1} if p.z < .11 else norm(inv(p, [s+"Leg", s+"Foot"]))
    if p.z < .95:  # skirt: split on x, keep some hips so the hem doesn't stretch like trousers
        L = max(0, min(1, (p.x + .22) / .44))
        w = {}
        w.update(inv(p, ["LeftUpLeg", "LeftLeg"], L + 1e-4)); w.update(inv(p, ["RightUpLeg", "RightLeg"], 1 - L + 1e-4))
        w = norm(w)
        legs = max(0, min(1, (.93 - p.z) / .5)) * .6
        w = {k: v * legs for k, v in w.items()}
        w["Hips"] = 1 - legs
        return w
    names = ["Hips", "Spine", "Spine1", "Spine2", "Neck", "Head"]
    if abs(p.x) > .1: names += [s+"Shoulder", s+"Arm"]
    if abs(p.x) > .2: names += [s+"ForeArm"]
    return norm(inv(p, names))

for k in J: ob.vertex_groups.new(name=k)
for v in ob.data.vertices:
    w = sorted(weights(v.co).items(), key=lambda kv: -kv[1])[:4]
    t = sum(x for _, x in w)
    for k, x in w: ob.vertex_groups[k].add([v.index], x / t, 'REPLACE')
ob.parent = arm
ob.modifiers.new("Armature", 'ARMATURE').object = arm

# Bake A-pose -> T-pose: Unity Humanoid measures muscle space from the bind pose.
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')
for s, n in ((1, "Left"), (-1, "Right")):
    for k in (n+"Arm", n+"ForeArm", n+"Hand"):
        pb = arm.pose.bones[k]
        bpy.context.view_layer.update()
        h, t = pb.head.copy(), pb.tail.copy()
        R = (t - h).rotation_difference(Vector((s, 0, 0))).to_matrix().to_4x4()
        pb.matrix = Matrix.Translation(h) @ R @ Matrix.Translation(-h) @ pb.matrix
bpy.context.view_layer.update()
bpy.ops.object.mode_set(mode='OBJECT')
bpy.context.view_layer.objects.active = ob
bpy.ops.object.modifier_apply(modifier="Armature")
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')
bpy.ops.pose.armature_apply(selected=False)
bpy.ops.object.mode_set(mode='OBJECT')
ob.modifiers.new("Armature", 'ARMATURE').object = arm

import os
os.makedirs(ROOT + r"\Chudail", exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=ROOT + r"\Chudail\Chudail.fbx", use_selection=True, add_leaf_bones=False, bake_anim=False,
    apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', path_mode='AUTO')
print("RIG_DONE tris", sum(len(p.vertices) - 2 for p in ob.data.polygons), "verts", len(ob.data.vertices), "maxgroups", max(len(v.groups) for v in ob.data.vertices))
