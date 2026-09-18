bl_info = {
    "name": "AI Assistant",
    "author": "BARAKI",
    "version": (1, 0, 0),
    "blender": (3, 6, 0),
    "location": "View3D > Sidebar > AI",
    "description": "AI-powered command system for Blender",
    "category": "Development",
}

import bpy
import json
import os
from bpy.props import StringProperty, CollectionProperty, IntProperty, EnumProperty
from bpy.types import Operator, Panel, PropertyGroup, UIList

# ─── Command Database ───────────────────────────────────────────────
COMMANDS = {
    # ── Modeling ──
    "create cube": "bpy.ops.mesh.primitive_cube_add(size=2, location=(0, 0, 0))",
    "create sphere": "bpy.ops.mesh.primitive_uv_sphere_add(radius=1, location=(0, 0, 0))",
    "create cylinder": "bpy.ops.mesh.primitive_cylinder_add(radius=1, depth=2, location=(0, 0, 0))",
    "create cone": "bpy.ops.mesh.primitive_cone_add(radius1=1, depth=2, location=(0, 0, 0))",
    "create torus": "bpy.ops.mesh.primitive_torus_add(major_radius=1, minor_radius=0.25, location=(0, 0, 0))",
    "create plane": "bpy.ops.mesh.primitive_plane_add(size=2, location=(0, 0, 0))",
    "create monkey": "bpy.ops.mesh.primitive_monkey_add(location=(0, 0, 0))",
    "create ico sphere": "bpy.ops.mesh.primitive_ico_sphere_add(radius=1, location=(0, 0, 0))",
    "create grid": "bpy.ops.mesh.primitive_grid_add(x_subdivisions=10, y_subdivisions=10, size=2, location=(0, 0, 0))",
    "create circle": "bpy.ops.mesh.primitive_circle_add(radius=1, vertices=32, location=(0, 0, 0))",

    # ── Arrays ──
    "create grid of cubes": """
for x in range(5):
    for y in range(5):
        bpy.ops.mesh.primitive_cube_add(size=0.5, location=(x * 1.5, y * 1.5, 0))
""",
    "create circle of objects": """
import math
for i in range(8):
    angle = (2 * math.pi / 8) * i
    x = math.cos(angle) * 3
    y = math.sin(angle) * 3
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.5, location=(x, y, 0))
""",
    "create tower": """
for i in range(10):
    bpy.ops.mesh.primitive_cube_add(size=2 - i * 0.1, location=(0, 0, i * 0.9))
""",
    "create wall of bricks": """
for row in range(5):
    for col in range(8):
        offset = 0.5 if row % 2 == 1 else 0
        bpy.ops.mesh.primitive_cube_add(
            size=1,
            location=(col * 1.05 + offset, 0, row * 0.55)
        )
""",
    "create stairs": """
for i in range(10):
    bpy.ops.mesh.primitive_cube_add(
        size=1,
        location=(i * 1.1, 0, i * 0.55)
    )
""",
    "create dna helix": """
import math
for i in range(50):
    angle = i * 0.3
    x1 = math.cos(angle) * 2
    y1 = math.sin(angle) * 2
    x2 = math.cos(angle + math.pi) * 2
    y2 = math.sin(angle + math.pi) * 2
    z = i * 0.2
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.15, location=(x1, y1, z))
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.15, location=(x2, y2, z))
    if i % 3 == 0:
        bpy.ops.mesh.primitive_cylinder_add(radius=0.05, depth=4, location=(0, 0, z), rotation=(0, math.pi/2, angle))
""",

    # ── Materials ──
    "make red": """
mat = bpy.data.materials.new(name="Red")
mat.use_nodes = True
mat.node_tree.nodes["Principled BSDF"].inputs[0].default_value = (1, 0, 0, 1)
bpy.context.object.data.materials.append(mat)
""",
    "make blue": """
mat = bpy.data.materials.new(name="Blue")
mat.use_nodes = True
mat.node_tree.nodes["Principled BSDF"].inputs[0].default_value = (0, 0, 1, 1)
bpy.context.object.data.materials.append(mat)
""",
    "make green": """
mat = bpy.data.materials.new(name="Green")
mat.use_nodes = True
mat.node_tree.nodes["Principled BSDF"].inputs[0].default_value = (0, 1, 0, 1)
bpy.context.object.data.materials.append(mat)
""",
    "make yellow": """
mat = bpy.data.materials.new(name="Yellow")
mat.use_nodes = True
mat.node_tree.nodes["Principled BSDF"].inputs[0].default_value = (1, 1, 0, 1)
bpy.context.object.data.materials.append(mat)
""",
    "make purple": """
mat = bpy.data.materials.new(name="Purple")
mat.use_nodes = True
mat.node_tree.nodes["Principled BSDF"].inputs[0].default_value = (0.5, 0, 0.5, 1)
bpy.context.object.data.materials.append(mat)
""",
    "make orange": """
mat = bpy.data.materials.new(name="Orange")
mat.use_nodes = True
mat.node_tree.nodes["Principled BSDF"].inputs[0].default_value = (1, 0.5, 0, 1)
bpy.context.object.data.materials.append(mat)
""",
    "make cyan": """
mat = bpy.data.materials.new(name="Cyan")
mat.use_nodes = True
mat.node_tree.nodes["Principled BSDF"].inputs[0].default_value = (0, 1, 1, 1)
bpy.context.object.data.materials.append(mat)
""",
    "make pink": """
mat = bpy.data.materials.new(name="Pink")
mat.use_nodes = True
mat.node_tree.nodes["Principled BSDF"].inputs[0].default_value = (1, 0.4, 0.7, 1)
bpy.context.object.data.materials.append(mat)
""",
    "make gold": """
mat = bpy.data.materials.new(name="Gold")
mat.use_nodes = True
bsdf = mat.node_tree.nodes["Principled BSDF"]
bsdf.inputs[0].default_value = (1, 0.765, 0.333, 1)
bsdf.inputs[6].default_value = 1.0
bsdf.inputs[7].default_value = 0.3
bpy.context.object.data.materials.append(mat)
""",
    "make silver": """
mat = bpy.data.materials.new(name="Silver")
mat.use_nodes = True
bsdf = mat.node_tree.nodes["Principled BSDF"]
bsdf.inputs[0].default_value = (0.8, 0.8, 0.8, 1)
bsdf.inputs[6].default_value = 1.0
bsdf.inputs[7].default_value = 0.1
bpy.context.object.data.materials.append(mat)
""",
    "make glass": """
mat = bpy.data.materials.new(name="Glass")
mat.use_nodes = True
bsdf = mat.node_tree.nodes["Principled BSDF"]
bsdf.inputs[0].default_value = (1, 1, 1, 1)
bsdf.inputs[15].default_value = 0.0
bsdf.inputs[16].default_value = 1.5
mat.blend_method = 'BLEND' if hasattr(mat, 'blend_method') else None
bpy.context.object.data.materials.append(mat)
""",
    "make emission": """
mat = bpy.data.materials.new(name="Emission")
mat.use_nodes = True
bsdf = mat.node_tree.nodes["Principled BSDF"]
bsdf.inputs[0].default_value = (1, 1, 1, 1)
bsdf.inputs[19].default_value = 5.0
bpy.context.object.data.materials.append(mat)
""",
    "make wood": """
mat = bpy.data.materials.new(name="Wood")
mat.use_nodes = True
bsdf = mat.node_tree.nodes["Principled BSDF"]
bsdf.inputs[0].default_value = (0.4, 0.2, 0.1, 1)
bsdf.inputs[4].default_value = 0.8
noise = mat.node_tree.nodes.new('ShaderNodeTexNoise')
noise.inputs[2].default_value = 15.0
mat.node_tree.links.new(noise.outputs[0], bsdf.inputs[4])
bpy.context.object.data.materials.append(mat)
""",
    "make marble": """
mat = bpy.data.materials.new(name="Marble")
mat.use_nodes = True
bsdf = mat.node_tree.nodes["Principled BSDF"]
bsdf.inputs[0].default_value = (0.9, 0.9, 0.85, 1)
voronoi = mat.node_tree.nodes.new('ShaderNodeTexVoronoi')
voronoi.inputs[2].default_value = 8.0
mat.node_tree.links.new(voronoi.outputs[0], bsdf.inputs[4])
bpy.context.object.data.materials.append(mat)
""",
    "make checker": """
mat = bpy.data.materials.new(name="Checker")
mat.use_nodes = True
bsdf = mat.node_tree.nodes["Principled BSDF"]
checker = mat.node_tree.nodes.new('ShaderNodeTexChecker')
checker.inputs[3].default_value = 8.0
mat.node_tree.links.new(checker.outputs[0], bsdf.inputs[0])
bpy.context.object.data.materials.append(mat)
""",
    "assign random color": """
import random
mat = bpy.data.materials.new(name="RandomColor")
mat.use_nodes = True
mat.node_tree.nodes["Principled BSDF"].inputs[0].default_value = (
    random.random(), random.random(), random.random(), 1
)
bpy.context.object.data.materials.append(mat)
""",
    "rainbow materials": """
import math
for i, obj in enumerate(bpy.context.selected_objects):
    hue = i / max(len(bpy.context.selected_objects), 1)
    r = max(0, min(1, abs(hue * 6 - 3) - 1))
    g = max(0, min(1, 2 - abs(hue * 6 - 2)))
    b = max(0, min(1, 2 - abs(hue * 6 - 4)))
    mat = bpy.data.materials.new(name=f"Rainbow_{i}")
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs[0].default_value = (r, g, b, 1)
    obj.data.materials.append(mat)
""",

    # ── Lighting ──
    "add sun": """
bpy.ops.object.light_add(type='SUN', location=(5, 5, 10))
bpy.context.object.data.energy = 5
""",
    "add point light": """
bpy.ops.object.light_add(type='POINT', location=(0, 0, 5))
bpy.context.object.data.energy = 1000
""",
    "add area light": """
bpy.ops.object.light_add(type='AREA', location=(0, 0, 5))
bpy.context.object.data.energy = 500
bpy.context.object.data.size = 3
""",
    "add spot light": """
bpy.ops.object.light_add(type='SPOT', location=(0, 0, 5))
bpy.context.object.data.energy = 1000
""",
    "three point lighting": """
bpy.ops.object.light_add(type='AREA', location=(5, -5, 5))
bpy.context.object.data.energy = 800
bpy.context.object.data.size = 3
bpy.context.object.data.name = "Key Light"
bpy.ops.object.light_add(type='AREA', location=(-5, -3, 3))
bpy.context.object.data.energy = 400
bpy.context.object.data.size = 5
bpy.context.object.data.name = "Fill Light"
bpy.ops.object.light_add(type='AREA', location=(0, 5, 5))
bpy.context.object.data.energy = 600
bpy.context.object.data.size = 2
bpy.context.object.data.name = "Back Light"
""",
    "add hdri lighting": """
world = bpy.context.scene.world
if not world:
    world = bpy.data.worlds.new("World")
    bpy.context.scene.world = world
world.use_nodes = True
nodes = world.node_tree.nodes
links = world.node_tree.links
nodes.clear()
output = nodes.new('ShaderNodeOutputWorld')
env = nodes.new('ShaderNodeTexEnvironment')
bg = nodes.new('ShaderNodeBackground')
links.new(env.outputs[0], bg.inputs[0])
links.new(bg.outputs[0], output.inputs[0])
bg.inputs[1].default_value = 1.0
""",
    "remove all lights": """
for obj in bpy.context.scene.objects:
    if obj.type == 'LIGHT':
        bpy.data.objects.remove(obj, do_unlink=True)
""",

    # ── Camera ──
    "add camera": """
bpy.ops.object.camera_add(location=(7, -7, 5))
bpy.context.object.rotation_euler = (1.1, 0, 0.8)
bpy.context.scene.camera = bpy.context.object
""",
    "camera front": """
if bpy.context.scene.camera:
    cam = bpy.context.scene.camera
    cam.location = (0, -10, 0)
    cam.rotation_euler = (math.pi/2, 0, 0)
""",
    "camera top": """
if bpy.context.scene.camera:
    cam = bpy.context.scene.camera
    cam.location = (0, 0, 10)
    cam.rotation_euler = (0, 0, 0)
""",
    "camera side": """
if bpy.context.scene.camera:
    cam = bpy.context.scene.camera
    cam.location = (10, 0, 0)
    cam.rotation_euler = (0, math.pi/2, 0)
""",

    # ── Modifiers ──
    "subdivide": """
mod = bpy.context.object.modifiers.new(name="Subsurf", type='SUBSURF')
mod.levels = 2
mod.render_levels = 2
""",
    "add mirror": """
mod = bpy.context.object.modifiers.new(name="Mirror", type='MIRROR')
mod.use_mirror_x = True
""",
    "add array x": """
mod = bpy.context.object.modifiers.new(name="ArrayX", type='ARRAY')
mod.count = 5
mod.use_relative_offset = True
mod.relative_offset_displace = (1, 0, 0)
""",
    "add array y": """
mod = bpy.context.object.modifiers.new(name="ArrayY", type='ARRAY')
mod.count = 5
mod.use_relative_offset = True
mod.relative_offset_displace = (0, 1, 0)
""",
    "add array z": """
mod = bpy.context.object.modifiers.new(name="ArrayZ", type='ARRAY')
mod.count = 5
mod.use_relative_offset = True
mod.relative_offset_displace = (0, 0, 1)
""",
    "add solidify": """
mod = bpy.context.object.modifiers.new(name="Solidify", type='SOLIDIFY')
mod.thickness = 0.1
""",
    "add bevel": """
mod = bpy.context.object.modifiers.new(name="Bevel", type='BEVEL')
mod.width = 0.1
mod.segments = 3
""",
    "add wireframe": """
mod = bpy.context.object.modifiers.new(name="Wireframe", type='WIREFRAME')
mod.thickness = 0.02
""",
    "add boolean union": """
bpy.ops.object.modifier_add(type='BOOLEAN')
bpy.context.object.modifiers[-1].operation = 'UNION'
""",
    "add subdivision surface": """
mod = bpy.context.object.modifiers.new(name="Subsurf", type='SUBSURF')
mod.levels = 3
mod.render_levels = 3
""",

    # ── Transform ──
    "scale up 2x": "bpy.context.object.scale = (2, 2, 2)",
    "scale down half": "bpy.context.object.scale = (0.5, 0.5, 0.5)",
    "move up": "bpy.context.object.location.z += 2",
    "move down": "bpy.context.object.location.z -= 2",
    "move left": "bpy.context.object.location.x -= 2",
    "move right": "bpy.context.object.location.x += 2",
    "rotate x 45": "bpy.context.object.rotation_euler.x += 0.785",
    "rotate y 45": "bpy.context.object.rotation_euler.y += 0.785",
    "rotate z 45": "bpy.context.object.rotation_euler.z += 0.785",
    "reset transform": """
bpy.context.object.location = (0, 0, 0)
bpy.context.object.rotation_euler = (0, 0, 0)
bpy.context.object.scale = (1, 1, 1)
""",
    "center origin": "bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')",
    "set origin to bottom": """
bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
bpy.context.object.location.z += bpy.context.object.dimensions.z / 2
""",
    "join selected": "bpy.ops.object.join()",
    "separate by material": "bpy.ops.object分离(type='MATERIAL')",
    "separate by loose": "bpy.ops.object分离(type='LOOSE')",

    # ── Edit Mode ──
    "enter edit mode": "bpy.ops.object.mode_set(mode='EDIT')",
    "exit edit mode": "bpy.ops.object.mode_set(mode='OBJECT')",
    "extrude up": "bpy.ops.mesh.extrude_region_move(TRANSFORM_OT_translate={'value': (0, 0, 1)})",
    "extrude down": "bpy.ops.mesh.extrude_region_move(TRANSFORM_OT_translate={'value': (0, 0, -1)})",
    "inset faces": "bpy.ops.mesh.inset Faces(thickness=0.1)",
    "bevel edges": "bpy.ops.mesh.bevel(offset=0.1, segments=3)",
    "smooth vertices": "bpy.ops.mesh.smooth_iterations(count=3)",
    "flip normals": "bpy.ops.mesh.flip_normals()",
    "recalculate normals": "bpy.ops.normals.make_consistent(inside=False)",
    "dissolve faces": "bpy.ops.mesh.dissolve_faces()",
    "triangulate faces": "bpy.ops.mesh.tris_convert_from_quads()",
    "decimate": """
mod = bpy.context.object.modifiers.new(name="Decimate", type='DECIMATE')
mod.ratio = 0.5
""",
    "merge by distance": "bpy.ops.mesh.remove_doubles(threshold=0.001)",
    "loop cut": "bpy.ops.mesh.loopcut_slide(NUMBER_CUTS=1)",

    # ── Animation ──
    "add keyframe location": """
bpy.ops.anim.keyframe_insert(type='LOCATION', frame=bpy.context.scene.frame_current)
""",
    "add keyframe rotation": """
bpy.ops.anim.keyframe_insert(type='ROTATION', frame=bpy.context.scene.frame_current)
""",
    "add keyframe scale": """
bpy.ops.anim.keyframe_insert(type='SCALING', frame=bpy.context.scene.frame_current)
""",
    "add keyframe all": """
bpy.ops.anim.keyframe_insert(type='LOCATION', frame=bpy.context.scene.frame_current)
bpy.ops.anim.keyframe_insert(type='ROTATION', frame=bpy.context.scene.frame_current)
bpy.ops.anim.keyframe_insert(type='SCALING', frame=bpy.context.scene.frame_current)
""",
    "set frame start": "bpy.context.scene.frame_start = bpy.context.scene.frame_current",
    "set frame end": "bpy.context.scene.frame_end = bpy.context.scene.frame_current",
    "go to frame start": "bpy.context.scene.frame_current = bpy.context.scene.frame_start",
    "go to frame end": "bpy.context.scene.frame_current = bpy.context.scene.frame_end",
    "next frame": "bpy.context.scene.frame_current += 1",
    "previous frame": "bpy.context.scene.frame_current -= 1",
    "clear animation": """
if bpy.context.object and bpy.context.object.animation_data:
    bpy.context.object.animation_data_clear()
""",
    "bake simulation": """
bpy.ops.ptcache.bake_all(bake=True)
""",

    # ── Rendering ──
    "render image": "bpy.ops.render.render(write_still=True)",
    "render animation": "bpy.ops.render.render(animation=True)",
    "set render engine eevee": "bpy.context.scene.render.engine = 'BLENDER_EEVEE'",
    "set render engine cycles": "bpy.context.scene.render.engine = 'CYCLES'",
    "set resolution hd": """
bpy.context.scene.render.resolution_x = 1920
bpy.context.scene.render.resolution_y = 1080
bpy.context.scene.render.resolution_percentage = 100
""",
    "set resolution 4k": """
bpy.context.scene.render.resolution_x = 3840
bpy.context.scene.render.resolution_y = 2160
bpy.context.scene.render.resolution_percentage = 100
""",
    "set render samples": "bpy.context.scene.cycles.samples = 128",
    "set transparent bg": """
bpy.context.scene.render.film_transparent = True
""",

    # ── Scene ──
    "clear scene": """
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()
""",
    "delete selected": "bpy.ops.object.delete()",
    "select all": "bpy.ops.object.select_all(action='SELECT')",
    "deselect all": "bpy.ops.object.select_all(action='DESELECT')",
    "select by type mesh": "bpy.ops.object.select_all(action='DESELECT'); [obj.select_set(True) for obj in bpy.context.scene.objects if obj.type == 'MESH']",
    "select by type light": "bpy.ops.object.select_all(action='DESELECT'); [obj.select_set(True) for obj in bpy.context.scene.objects if obj.type == 'LIGHT']",
    "select by type camera": "bpy.ops.object.select_all(action='DESELECT'); [obj.select_set(True) for obj in bpy.context.scene.objects if obj.type == 'CAMERA']",
    "duplicate selected": "bpy.ops.object.duplicate()",
    "link objects to collection": "bpy.ops.object.move_to_collection(collection_index=0)",
    "add collection": "bpy.context.scene.collection.children.new('NewCollection')",

    # ── Sculpting ──
    "enter sculpt mode": "bpy.ops.object.mode_set(mode='SCULPT')",
    "enter vertex paint": "byp.ops.object.mode_set(mode='VERTEX_PAINT')",
    "enter weight paint": "bpy.ops.object.mode_set(mode='WEIGHT_PAINT')",
    "enter texture paint": "bpy.ops.object.mode_set(mode='TEXTURE_PAINT')",

    # ── Utility ──
    "list objects": """
for obj in bpy.context.scene.objects:
    print(f"{obj.name} ({obj.type}) at {obj.location[:]}")
""",
    "list materials": """
for mat in bpy.data.materials:
    print(mat.name)
""",
    "list meshes": """
for mesh in bpy.data.meshes:
    print(f"{mesh.name}: {len(mesh.polygons)} faces, {len(mesh.vertices)} vertices")
""",
    "list lights": """
for light in bpy.data.lights:
    print(f"{light.name}: {light.type}, energy={light.energy}")
""",
    "print scene info": """
print(f"Objects: {len(bpy.context.scene.objects)}")
print(f"Materials: {len(bpy.data.materials)}")
print(f"Meshes: {len(bpy.data.meshes)}")
print(f"Render engine: {bpy.context.scene.render.engine}")
print(f"Resolution: {bpy.context.scene.render.resolution_x}x{bpy.context.scene.render.resolution_y}")
""",
    "undo": "bpy.ops.ed.undo()",
    "redo": "bpy.ops.ed.redo()",
    "save": "bpy.ops.wm.save_mainfile()",
    "save as": "bpy.ops.wm.save_as_mainfile(filepath='//untitled.blend')",
    "new file": "bpy.ops.wm.read_factory_settings(use_empty=False)",
    "open file browser": "bpy.ops.wm.open_mainfile()",
    "quit": "bpy.ops.wm.quit_blender()",
}


# ─── History Item ────────────────────────────────────────────────────
class AI_HistoryItem(PropertyGroup):
    text: StringProperty(name="Command")
    result: StringProperty(name="Result")
    success: bpy.props.BoolProperty(default=True)


# ─── Properties ─────────────────────────────────────────────────────
class AI_Properties(PropertyGroup):
    command: StringProperty(
        name="Command",
        description="Enter a command or Python code",
        default=""
    )
    history: CollectionProperty(type=AI_HistoryItem)
    history_index: IntProperty(default=-1)
    auto_execute: bpy.props.BoolProperty(
        name="Auto-execute",
        description="Execute command immediately on Enter",
        default=True
    )
    show_code: bpy.props.BoolProperty(
        name="Show Generated Code",
        description="Show the Python code before execution",
        default=True
    )
    command_filter: StringProperty(
        name="Filter",
        description="Filter commands",
        default=""
    )


# ─── Operators ──────────────────────────────────────────────────────
class AI_OT_Execute(Operator):
    bl_idname = "ai.execute"
    bl_label = "Execute"
    bl_description = "Execute the current command"

    def execute(self, context):
        props = context.scene.ai_assistant
        command = props.command.strip().lower()

        if not command:
            self.report({'WARNING'}, "Enter a command")
            return {'CANCELLED'}

        # Find matching command
        code = None
        matched_cmd = None
        for cmd, script in COMMANDS.items():
            if command == cmd.lower() or command in cmd.lower() or cmd.lower() in command:
                code = script
                matched_cmd = cmd
                break

        # If no match, try as Python code
        if code is None:
            if any(kw in command for kw in ['bpy.', 'import ', 'for ', 'if ', 'def ', 'class ',
                                              '=', 'print', '(', '[', '{']):
                code = props.command.strip()
                matched_cmd = "(custom code)"
            else:
                # Try fuzzy match
                words = command.split()
                for cmd, script in COMMANDS.items():
                    cmd_words = cmd.lower().split()
                    if any(w in cmd_words for w in words):
                        code = script
                        matched_cmd = cmd
                        break

        if code is None:
            self.report({'WARNING'}, f"Unknown command: {command}")
            # Add to history as failed
            item = props.history.add()
            item.text = props.command
            item.result = f"Unknown command: {command}"
            item.success = False
            props.history_index = len(props.history) - 1
            return {'CANCELLED'}

        # Execute
        try:
            exec(code)
            result = f"OK: {matched_cmd}"

            item = props.history.add()
            item.text = props.command
            item.result = result
            item.success = True
            props.history_index = len(props.history) - 1

            self.report({'INFO'}, result)
        except Exception as e:
            result = f"ERROR: {str(e)}"
            item = props.history.add()
            item.text = props.command
            item.result = result
            item.success = False
            props.history_index = len(props.history) - 1

            self.report({'ERROR'}, result)

        return {'FINISHED'}


class AI_OT_ClearHistory(Operator):
    bl_idname = "ai.clear_history"
    bl_label = "Clear History"

    def execute(self, context):
        context.scene.ai_assistant.history.clear()
        context.scene.ai_assistant.history_index = -1
        return {'FINISHED'}


class AI_OT_ApplyCommand(Operator):
    bl_idname = "ai.apply_command"
    bl_label = "Apply Command"

    command_text: StringProperty()

    def execute(self, context):
        context.scene.ai_assistant.command = self.command_text
        return {'FINISHED'}


class AI_OT_CopyCode(Operator):
    bl_idname = "ai.copy_code"
    bl_label = "Copy Code"

    code_text: StringProperty()

    def execute(self, context):
        context.window_manager.clipboard = self.code_text
        self.report({'INFO'}, "Code copied to clipboard")
        return {'FINISHED'}


class AI_OT_DeleteHistory(Operator):
    bl_idname = "ai.delete_history"
    bl_label = "Delete History Item"

    index: IntProperty()

    def execute(self, context):
        context.scene.ai_assistant.history.remove(self.index)
        return {'FINISHED'}


# ─── UI List ─────────────────────────────────────────────────────────
class AI_UL_HistoryList(UIList):
    def draw_item(self, context, layout, data, item, icon, active_data, active_property, index):
        if self.layout_type in {'DEFAULT', 'COMPACT'}:
            row = layout.row(align=True)
            icon = 'CHECKMARK' if item.success else 'ERROR'
            row.label(text=item.text, icon=icon)
            row.label(text=item.result)
        elif self.layout_type == 'GRID':
            layout.alignment = 'CENTER'
            layout.label(text="", icon='CHECKMARK' if item.success else 'ERROR')


# ─── Panels ──────────────────────────────────────────────────────────
class AI_PT_MainPanel(Panel):
    bl_label = "AI Assistant"
    bl_idname = "AI_PT_main"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "AI"

    def draw(self, context):
        layout = self.layout
        props = context.scene.ai_assistant

        # Command input
        box = layout.box()
        box.label(text="Command:", icon='TEXT')
        row = box.row(align=True)
        row.prop(props, "command", text="")
        row.operator("ai.execute", text="", icon='PLAY')

        # Quick buttons
        row = box.row(align=True)
        row.prop(props, "auto_execute", text="Auto-exec", toggle=True)
        row.prop(props, "show_code", text="Show code", toggle=True)


class AI_PT_CommandsPanel(Panel):
    bl_label = "Quick Commands"
    bl_idname = "AI_PT_commands"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "AI"
    bl_parent_id = "AI_PT_main"
    bl_options = {'DEFAULT_CLOSED'}

    def draw(self, context):
        layout = self.layout
        props = context.scene.ai_assistant

        # Search
        layout.prop(props, "command_filter", text="", icon='VIEWZOOM')

        # Category buttons
        categories = {
            "Primitive": ["create cube", "create sphere", "create cylinder", "create cone",
                         "create torus", "create plane", "create monkey", "create ico sphere"],
            "Array": ["create grid of cubes", "create circle of objects", "create tower",
                     "create wall of bricks", "create stairs", "create dna helix"],
            "Material": ["make red", "make blue", "make green", "make yellow",
                        "make purple", "make gold", "make glass", "make emission",
                        "make wood", "make marble", "make checker",
                        "assign random color", "rainbow materials"],
            "Light": ["add sun", "add point light", "add area light",
                     "three point lighting", "remove all lights"],
            "Camera": ["add camera", "camera front", "camera top", "camera side"],
            "Modifier": ["subdivide", "add mirror", "add array x", "add array z",
                        "add solidify", "add bevel", "add wireframe",
                        "add subdivision surface"],
            "Transform": ["scale up 2x", "scale down half", "move up", "move down",
                         "rotate x 45", "rotate z 45", "reset transform",
                         "center origin", "join selected"],
            "Edit": ["enter edit mode", "exit edit mode", "extrude up",
                    "inset faces", "bevel edges", "smooth vertices",
                    "flip normals", "merge by distance"],
            "Animation": ["add keyframe all", "next frame", "previous frame",
                         "clear animation"],
            "Render": ["render image", "set render engine eevee",
                      "set render engine cycles", "set resolution hd",
                      "set resolution 4k", "set transparent bg"],
            "Scene": ["clear scene", "delete selected", "select all",
                     "deselect all", "duplicate selected", "undo", "redo",
                     "save", "print scene info"],
        }

        filter_text = props.command_filter.lower()

        for cat_name, commands in categories.items():
            filtered = [c for c in commands if filter_text in c.lower()]
            if not filtered:
                continue

            box = layout.box()
            box.label(text=cat_name, icon='FILE_FOLDER')
            col = box.column(align=True)
            for cmd in sorted(filtered):
                row = col.row(align=True)
                op = row.operator("ai.apply_command", text=cmd, icon='PLAY')
                op.command_text = cmd


class AI_PT_HistoryPanel(Panel):
    bl_label = "History"
    bl_idname = "AI_PT_history"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "AI"
    bl_parent_id = "AI_PT_main"
    bl_options = {'DEFAULT_CLOSED'}

    def draw(self, context):
        layout = self.layout
        props = context.scene.ai_assistant

        row = layout.row()
        row.operator("ai.clear_history", text="Clear History", icon='TRASH')

        if len(props.history) == 0:
            layout.label(text="No history yet")
        else:
            for i, item in enumerate(props.history):
                box = layout.box()
                row = box.row(align=True)
                icon = 'CHECKMARK' if item.success else 'ERROR'
                row.label(text=item.text, icon=icon)
                row.label(text=item.result)


class AI_PT_CodeEditorPanel(Panel):
    bl_label = "Code Editor"
    bl_idname = "AI_PT_code_editor"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "AI"
    bl_parent_id = "AI_PT_main"
    bl_options = {'DEFAULT_CLOSED'}

    def draw(self, context):
        layout = self.layout
        props = context.scene.ai_assistant

        box = layout.box()
        box.label(text="Write Python code directly:", icon='SCRIPT')
        box.prop(props, "command", text="")

        row = box.row(align=True)
        row.operator("ai.execute", text="Run Code", icon='PLAY')


class AI_PT_HelpPanel(Panel):
    bl_label = "Help & Tips"
    bl_idname = "AI_PT_help"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "AI"
    bl_parent_id = "AI_PT_main"
    bl_options = {'DEFAULT_CLOSED'}

    def draw(self, context):
        layout = self.layout

        box = layout.box()
        box.label(text="How to use:", icon='INFO')
        col = box.column(align=True)
        col.label(text="1. Type a command in the input field")
        col.label(text="2. Press Enter or click Play button")
        col.label(text="3. Command executes in Blender")

        box = layout.box()
        box.label(text="Supported commands:", icon='QUESTION')
        col = box.column(align=True)
        col.label(text="• create [object] - primitives")
        col.label(text="• make [color/material] - materials")
        col.label(text="• add [light/camera] - add objects")
        col.label(text="• [transform] - move/rotate/scale")
        col.label(text="• [modifier] - add modifiers")
        col.label(text="• [edit] - edit mode operations")
        col.label(text="• [animation] - keyframes")
        col.label(text="• [render] - render settings")

        box = layout.box()
        box.label(text="Python code:", icon='TEXT')
        col = box.column(align=True)
        col.label(text="Type any Python code directly!")
        col.label(text="Example: bpy.ops.mesh.primitive_cube_add()")
        col.label(text="Example: print(bpy.context.object.name)")

        box = layout.box()
        box.label(text="Shortcuts:", icon='KEY')
        col = box.column(align=True)
        col.label(text="Enter - Execute command")
        col.label(text="Ctrl+Enter - Execute & clear")


# ─── Registration ────────────────────────────────────────────────────
classes = (
    AI_HistoryItem,
    AI_Properties,
    AI_OT_Execute,
    AI_OT_ClearHistory,
    AI_OT_ApplyCommand,
    AI_OT_CopyCode,
    AI_OT_DeleteHistory,
    AI_UL_HistoryList,
    AI_PT_MainPanel,
    AI_PT_CommandsPanel,
    AI_PT_HistoryPanel,
    AI_PT_CodeEditorPanel,
    AI_PT_HelpPanel,
)


def register():
    for cls in classes:
        bpy.utils.register_class(cls)
    bpy.types.Scene.ai_assistant = bpy.props.PointerProperty(type=AI_Properties)


def unregister():
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)
    del bpy.types.Scene.ai_assistant


if __name__ == "__main__":
    register()
