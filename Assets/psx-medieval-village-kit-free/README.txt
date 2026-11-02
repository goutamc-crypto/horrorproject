PSX Medieval Village Kit Free
=============================

15 PS1-style pieces to build a timber-framed house on a medieval street, taken from PSX Medieval
Village Kit (157 pieces for a medieval village shut in with its plague: the plague doors, the
tavern, the smithy, the market and the gallows, the chapel and its graves, the pyre, the mill, the
palisade and its gate, the cunning woman's hut, a level to solve):
https://heyheythere.itch.io/psx-medieval-village-kit
Low-poly, one point-filtered texture atlas, the shading baked into vertex colours, real-world
scale (1 unit = 1 metre).

What's in the zip
-----------------
glb/        one file per piece, with its animations (the best format for Godot, Blender, three.js)
fbx/        one file per piece, with its animations as takes (Unreal, Unity, most 3D apps)
obj/        one file per piece, still (any 3D app)
textures/   atlas.png, the one texture every piece uses, and the seamless daub, thatch, rush and
            mud textures it tiles from, 128x128 a metre, for your own geometry
icons/      an icon per piece, 256x256 PNG with transparency
props.json  every piece: title, group, size, triangles, parts and animations
blender/    every piece in one .blend, the atlas packed in
addons/     the Godot addon, psx_medieval_village_kit/: this folder is a Godot 4.3+ project that
            opens on its gallery (.gdignore keeps Godot out of the other folders)

The grid
--------
Everything snaps to a 2 m grid, 3 m a storey, as PSX Hospital Kit's. A floor covers one 2 x 2 m
cell (the mud 4 m, its top 1 cm up), its pivot at the cell's centre. A wall is 2 m long, centred
on a cell's edge, its front facing -Y in Blender (+Z in Godot, -Z in Unity): the limewash in
front, the smoked daub behind, so it faces out. A post fills the corner where walls meet. The door
goes in the doorway at the same point; the herbs against a wall, their back on the wall's face. A
thatch slope goes on the walls' tops (z=3) over its cell, its eave out front, the far one turned
180; the gable at the same height on a two-cell end wall's line.

Godot 4.3 and later
-------------------
Copy addons/psx_medieval_village_kit/ into your project's addons/ folder. Each piece is a scene in
props/: drag it into your level and turn on grid snapping at 1 m. It has a box collider
(StaticBody3D) on each of its parts: the doorway's leave the way through clear and the door's
moves with it. A piece with animations has an AnimationPlayer:

    door_plank.get_node("AnimationPlayer").play("open")   # door_plank: the piece in your level

demo/gallery.tscn shows every piece: drag to turn, arrows to browse, A for all of them.
PSX Medieval Village Kit uses the same addon folder: it installs over this one.
The catalog is lit without distance fog so the whole collection stays visible.
The pieces use one material, props.tres: the atlas times the vertex colours, lit, so your lights
fall on them. For the full PS1 look (vertex snap, affine textures, 240p, dither), our PSX Look
shaders turn them over in one line: https://heyheythere.itch.io/psx-look
With PSX Look in res://addons/psx_look/, the gallery shows the pieces through it (P toggles it).

Other engines and apps
----------------------
Use fbx/, glb/ or obj/ with textures/atlas.png. Set the texture's filtering to nearest/point and
turn off mipmaps for the crisp PS1 texels, and multiply it by the vertex colours (the baked
shading) in your material.

License: CC BY 4.0, see LICENSE.txt for the credit line. Made by heyheythere: https://heyheythere.itch.io
