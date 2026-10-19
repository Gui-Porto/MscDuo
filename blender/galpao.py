import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

FOLDER = os.path.dirname(os.path.abspath(__file__))
OBJ = os.path.join(FOLDER, "galpao.obj")
PREVIEW = os.path.join(FOLDER, "galpao-{}.png")
COLLECTION = "GALPAO"
TEXTURES = os.path.join(FOLDER, "galpao_tex")
TILE = {"concreto": 4.0, "parede": 2.0, "chapa": 2.0, "aco": 1.0, "portao": 3.0, "porta": 1.0,
        "faixa": 1.0, "luz": 1.0, "laje": 4.0, "degrau": 1.0, "vidro": 2.0}

W = 24.0
D = 16.0
EAVE = 6.0
RIDGE = 8.0
WALL = 0.2
SLAB = 0.25
OVERHANG = 0.5
ROOF = 0.12

GATES = [(6.0, 18.0)]
DYNO_BAYS = [(7.0, 10.5), (13.5, 17.0)]
DYNO_Y = (1.0, 12.8)
GATE_H = 4.5
SIDE_DOOR = (12.5, 13.5)
DOOR_H = 2.15
CLERESTORY = (4.4, 5.3)

FRAMES = [0.0, 6.0, 12.0, D]
COLUMN = 0.32
RAFTER = 0.45
PURLIN_STEP = 2.0

MEZ_Y = 10.0
MEZ_Z = 3.2
MEZ_COLUMNS = [6.0, 12.0, 18.0]

STAIR_X = (22.6, 23.8)
STAIR_Y0 = 4.0
STEPS = 16

RAIL_H = 1.05
ROOMS = (18.0, 12.0)

COLORS = {
    "concreto": (0.62, 0.63, 0.64, 1.0),
    "parede": (0.86, 0.86, 0.84, 1.0),
    "chapa": (0.16, 0.17, 0.18, 1.0),
    "aco": (0.03, 0.03, 0.035, 1.0),
    "vidro": (0.55, 0.75, 0.85, 0.25),
    "portao": (0.78, 0.79, 0.8, 1.0),
    "porta": (0.12, 0.12, 0.13, 1.0),
    "faixa": (0.95, 0.75, 0.05, 1.0),
    "luz": (1.0, 0.98, 0.92, 1.0),
    "laje": (0.5, 0.5, 0.5, 1.0),
    "degrau": (0.2, 0.2, 0.22, 1.0),
}


def collection():
    col = bpy.data.collections.get(COLLECTION)
    if col is None:
        col = bpy.data.collections.new(COLLECTION)
        bpy.context.scene.collection.children.link(col)
    for obj in list(col.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    return col


def material(name):
    mat = bpy.data.materials.get("galpao_" + name)
    if mat is None:
        mat = bpy.data.materials.new("galpao_" + name)
        mat.use_nodes = True
    color = COLORS[name]
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = {"concreto": 0.25, "aco": 0.45, "chapa": 0.6, "vidro": 0.05, "portao": 0.4}.get(name, 0.7)
    bsdf.inputs["Metallic"].default_value = {"aco": 0.6, "chapa": 0.5, "portao": 0.3}.get(name, 0.0)
    if name == "vidro":
        bsdf.inputs["Alpha"].default_value = color[3]
        mat.blend_method = "BLEND" if hasattr(mat, "blend_method") else mat.blend_method
    if name == "luz":
        bsdf.inputs["Emission Color"].default_value = color
        bsdf.inputs["Emission Strength"].default_value = 6.0
    image_path = os.path.join(TEXTURES, name + ".png")
    if os.path.exists(image_path):
        nodes = mat.node_tree.nodes
        tex = next((n for n in nodes if n.type == "TEX_IMAGE"), None) or nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(image_path, check_existing=True)
        tex.location = (-400, 200)
        links = mat.node_tree.links
        links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if name == "luz":
            links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
    mat.diffuse_color = color
    return mat


def unwrap(bm, mat):
    tile = TILE.get(mat, 1.0)
    uv = bm.loops.layers.uv.verify()
    bm.normal_update()
    for face in bm.faces:
        n = face.normal
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        for loop in face.loops:
            co = loop.vert.co
            if az >= ax and az >= ay:
                u, v = (co.y, co.x) if mat == "chapa" else (co.x, co.y)
            elif ax >= ay:
                u, v = co.y, co.z
            else:
                u, v = co.x, co.z
            loop[uv].uv = (u / tile, v / tile)


class Builder:
    def __init__(self, col):
        self.col = col
        self.parts = {}

    def mesh(self, name, mat):
        key = (name, mat)
        if key not in self.parts:
            self.parts[key] = bmesh.new()
        return self.parts[key]

    def box(self, name, mat, x0, y0, z0, x1, y1, z1):
        bm = self.mesh(name, mat)
        xs, ys, zs = sorted((x0, x1)), sorted((y0, y1)), sorted((z0, z1))
        v = [bm.verts.new((xs[i & 1], ys[(i >> 1) & 1], zs[(i >> 2) & 1])) for i in range(8)]
        for f in ((0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)):
            bm.faces.new([v[i] for i in f])

    def beam(self, name, mat, p0, p1, width, height):
        bm = self.mesh(name, mat)
        p0, p1 = Vector(p0), Vector(p1)
        axis = (p1 - p0).normalized()
        side = axis.cross(Vector((0, 0, 1)))
        if side.length < 1e-4:
            side = Vector((1, 0, 0))
        side.normalize()
        up = side.cross(axis).normalized()
        corners = []
        for p in (p0, p1):
            for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                corners.append(bm.verts.new(p + side * (sx * width / 2) + up * (sz * height / 2)))
        a, b = corners[:4], corners[4:]
        bm.faces.new(a[::-1])
        bm.faces.new(b)
        for i in range(4):
            j = (i + 1) % 4
            bm.faces.new((a[i], a[j], b[j], b[i]))

    def prism(self, name, mat, outline, y0, y1):
        bm = self.mesh(name, mat)
        front = [bm.verts.new((x, y0, z)) for x, z in outline]
        back = [bm.verts.new((x, y1, z)) for x, z in outline]
        bm.faces.new(front)
        bm.faces.new(back[::-1])
        n = len(outline)
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((front[j], front[i], back[i], back[j]))

    def quad(self, name, mat, pts):
        bm = self.mesh(name, mat)
        bm.faces.new([bm.verts.new(p) for p in pts])

    def finish(self):
        objects = []
        names = sorted({n for n, _ in self.parts})
        for name in names:
            me = bpy.data.meshes.new(name)
            mats = [m for n, m in self.parts if n == name]
            combined = bmesh.new()
            for index, mat in enumerate(mats):
                bm = self.parts[(name, mat)]
                bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
                unwrap(bm, mat)
                for f in bm.faces:
                    f.material_index = index
                temp = bpy.data.meshes.new("tmp")
                bm.to_mesh(temp)
                combined.from_mesh(temp)
                bpy.data.meshes.remove(temp)
                bm.free()
            combined.to_mesh(me)
            combined.free()
            for mat in mats:
                me.materials.append(material(mat))
            obj = bpy.data.objects.new(name, me)
            self.col.objects.link(obj)
            objects.append(obj)
        return objects


def roof_z(x):
    return EAVE + (RIDGE - EAVE) * (1.0 - abs(x - W / 2) / (W / 2))


def floor(b):
    b.box("Piso", "concreto", -0.3, -0.3, -1.2, W + 0.3, D + 0.3, 0.0)
    y0, y1 = DYNO_Y
    for x0, x1 in DYNO_BAYS:
        for y in (y0, y1):
            b.box("Faixas", "faixa", x0, y - 0.06, 0.001, x1, y + 0.06, 0.004)
        for x in (x0, x1):
            b.box("Faixas", "faixa", x - 0.06, y0, 0.001, x + 0.06, y1, 0.004)


def front_back(b):
    for y0, gates in ((0.0, GATES), (D - WALL, [])):
        y1 = y0 + WALL
        edges = [0.0]
        for g0, g1 in gates:
            edges += [g0, g1]
        edges.append(W)
        for i in range(0, len(edges), 2):
            if edges[i + 1] > edges[i]:
                b.box("Paredes", "parede", edges[i], y0, 0.0, edges[i + 1], y1, EAVE)
        for g0, g1 in gates:
            b.box("Paredes", "parede", g0, y0, GATE_H, g1, y1, EAVE)
        b.prism("Paredes", "parede", [(0.0, EAVE), (W, EAVE), (W / 2, RIDGE)], y0, y1)


def sides(b):
    for x0 in (0.0, W - WALL):
        x1 = x0 + WALL
        if x0 > 0.0:
            b.box("Paredes", "parede", x0, WALL, 0.0, x1, D - WALL, CLERESTORY[0])
        b.box("Paredes", "parede", x0, WALL, CLERESTORY[1], x1, D - WALL, EAVE)
        b.box("Janelas", "vidro", x0 + 0.08, WALL, CLERESTORY[0], x1 - 0.08, D - WALL, CLERESTORY[1])
        for y in range(2, int(D), 2):
            b.box("Estrutura", "aco", x0 - 0.01, y - 0.04, CLERESTORY[0], x1 + 0.01, y + 0.04, CLERESTORY[1])
    b.box("Paredes", "parede", 0.0, WALL, 0.0, WALL, SIDE_DOOR[0], CLERESTORY[0])
    b.box("Paredes", "parede", 0.0, SIDE_DOOR[1], 0.0, WALL, D - WALL, CLERESTORY[0])
    b.box("Paredes", "parede", 0.0, SIDE_DOOR[0], DOOR_H, WALL, SIDE_DOOR[1], CLERESTORY[0])


def frame(b):
    for y in FRAMES:
        yc = min(max(y, WALL + COLUMN / 2), D - WALL - COLUMN / 2)
        for x in (WALL + COLUMN / 2, W - WALL - COLUMN / 2):
            b.box("Estrutura", "aco", x - COLUMN / 2, yc - COLUMN / 2, 0.0, x + COLUMN / 2, yc + COLUMN / 2, EAVE - 0.05)
        for x0, x1 in ((WALL, W / 2), (W - WALL, W / 2)):
            b.beam("Estrutura", "aco", (x0, yc, roof_z(x0) - RAFTER / 2 - 0.1), (x1, yc, RIDGE - RAFTER / 2 - 0.1), 0.22, RAFTER)
        b.beam("Estrutura", "aco", (WALL + 1.2, yc, EAVE - 1.0), (W - WALL - 1.2, yc, EAVE - 1.0), 0.06, 0.06)
    x = WALL
    while x < W - WALL:
        z = roof_z(x) - 0.12
        b.beam("Estrutura", "aco", (x, WALL, z), (x, D - WALL, z), 0.12, 0.2)
        x += PURLIN_STEP
    b.beam("Estrutura", "aco", (W / 2, WALL, RIDGE - 0.15), (W / 2, D - WALL, RIDGE - 0.15), 0.14, 0.24)


def roof(b):
    slope = (RIDGE - EAVE) / (W / 2)
    x_out = -OVERHANG
    z_out = EAVE - slope * OVERHANG
    for sign in (1, -1):
        xa = x_out if sign == 1 else W + OVERHANG
        xr = W / 2
        outline = [(xa, z_out), (xr, RIDGE), (xr, RIDGE + ROOF), (xa, z_out + ROOF)]
        b.prism("Telhado", "chapa", outline, -OVERHANG, D + OVERHANG)
    for sign in (1, -1):
        xa = -OVERHANG if sign == 1 else W + OVERHANG
        b.beam("Telhado", "aco", (xa, -OVERHANG, z_out - 0.05), (xa, D + OVERHANG, z_out - 0.05), 0.12, 0.18)


def mezzanine(b):
    b.box("Mezanino", "laje", WALL, MEZ_Y, MEZ_Z - SLAB, W - WALL, D - WALL, MEZ_Z)
    b.box("Mezanino", "concreto", WALL, MEZ_Y, MEZ_Z - 0.005, W - WALL, D - WALL, MEZ_Z + 0.005)
    b.beam("Estrutura", "aco", (WALL, MEZ_Y + 0.15, MEZ_Z - SLAB - 0.2), (W - WALL, MEZ_Y + 0.15, MEZ_Z - SLAB - 0.2), 0.25, 0.4)
    for x in MEZ_COLUMNS:
        b.box("Estrutura", "aco", x - 0.15, MEZ_Y, 0.0, x + 0.15, MEZ_Y + 0.3, MEZ_Z - SLAB)
        b.beam("Estrutura", "aco", (x, MEZ_Y, MEZ_Z - SLAB - 0.15), (x, D - WALL, MEZ_Z - SLAB - 0.15), 0.18, 0.3)
    x = STAIR_X[0]
    b.box("GuardaCorpo", "vidro", WALL + 0.1, MEZ_Y + 0.02, MEZ_Z, x - 0.1, MEZ_Y + 0.06, MEZ_Z + RAIL_H - 0.08)
    b.box("GuardaCorpo", "aco", WALL, MEZ_Y - 0.02, MEZ_Z + RAIL_H - 0.08, x, MEZ_Y + 0.1, MEZ_Z + RAIL_H)
    px = WALL + 0.1
    while px < x:
        b.box("GuardaCorpo", "aco", px - 0.03, MEZ_Y - 0.02, MEZ_Z, px + 0.03, MEZ_Y + 0.1, MEZ_Z + RAIL_H)
        px += 2.0


def stairs(b):
    run = (MEZ_Y - STAIR_Y0) / STEPS
    rise = MEZ_Z / STEPS
    for i in range(STEPS):
        y0 = STAIR_Y0 + i * run
        z = (i + 1) * rise
        b.box("Escada", "degrau", STAIR_X[0], y0, z - 0.05, STAIR_X[1], y0 + run + 0.02, z)
    for x in (STAIR_X[0] - 0.04, STAIR_X[1] + 0.04):
        b.beam("Escada", "aco", (x, STAIR_Y0, -0.1), (x, MEZ_Y, MEZ_Z - 0.1), 0.06, 0.3)
    x = STAIR_X[0] - 0.04
    b.beam("Escada", "aco", (x, STAIR_Y0, RAIL_H), (x, MEZ_Y, MEZ_Z + RAIL_H), 0.05, 0.05)
    for i in range(0, STEPS + 1, 4):
        y = STAIR_Y0 + i * run
        z = i * rise
        b.box("Escada", "aco", x - 0.025, y - 0.025, z, x + 0.025, y + 0.025, z + RAIL_H)


def rooms(b):
    x0, y0 = ROOMS
    top = MEZ_Z - SLAB
    t = 0.12
    b.box("Salas", "parede", x0, y0, 0.0, x0 + t, D - WALL, top)
    b.box("Salas", "parede", x0 + 3.0, y0, 0.0, x0 + 3.0 + t, D - WALL, top)
    doors = [(x0 + 0.9, x0 + 1.8), (x0 + 3.9, x0 + 4.8)]
    edges = [x0] + [e for d in doors for e in d] + [W - WALL]
    for i in range(0, len(edges), 2):
        b.box("Salas", "parede", edges[i], y0, 0.0, edges[i + 1], y0 + t, top)
    for d0, d1 in doors:
        b.box("Salas", "parede", d0, y0, 2.15, d1, y0 + t, top)


def gates(b):
    for index, (x0, x1) in enumerate(GATES, 1):
        name = "Portao{}".format(index)
        sections = 6
        h = GATE_H / sections
        for i in range(sections):
            z0 = i * h
            b.box(name, "portao", x0 + 0.02, -0.06, z0 + 0.01, x1 - 0.02, 0.02, z0 + h - 0.01)
            b.box(name, "aco", x0 + 0.02, -0.075, z0 + h - 0.03, x1 - 0.02, -0.055, z0 + h)
        b.box("Estrutura", "aco", x0 - 0.12, -0.1, 0.0, x0, WALL + 0.02, GATE_H + 0.12)
        b.box("Estrutura", "aco", x1, -0.1, 0.0, x1 + 0.12, WALL + 0.02, GATE_H + 0.12)
        b.box("Estrutura", "aco", x0 - 0.12, -0.1, GATE_H, x1 + 0.12, WALL + 0.02, GATE_H + 0.12)
    b.box("PortaLateral", "porta", -0.04, SIDE_DOOR[0] + 0.03, 0.0, 0.04, SIDE_DOOR[1] - 0.03, DOOR_H - 0.03)


def lights(b):
    for x in (6.0, 12.0, 18.0):
        for y0, y1 in ((1.5, 8.5),):
            b.box("Luzes", "luz", x - 0.08, y0, roof_z(x) - 0.9, x + 0.08, y1, roof_z(x) - 0.85)
            b.box("Luzes", "aco", x - 0.02, (y0 + y1) / 2 - 0.01, roof_z(x) - 0.85, x + 0.02, (y0 + y1) / 2 + 0.01, roof_z(x) - 0.2)
    for x in (3.0, 9.0, 15.0):
        b.box("Luzes", "luz", x - 1.2, MEZ_Y + 2.9, MEZ_Z - SLAB - 0.04, x + 1.2, MEZ_Y + 3.05, MEZ_Z - SLAB - 0.01)


def build():
    col = collection()
    b = Builder(col)
    floor(b)
    front_back(b)
    sides(b)
    frame(b)
    roof(b)
    mezzanine(b)
    stairs(b)
    rooms(b)
    gates(b)
    lights(b)
    objects = b.finish()
    for obj in objects:
        if obj.name.startswith("Portao"):
            obj["abre"] = True
    return objects


def export(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.ops.wm.obj_export(filepath=OBJ, export_selected_objects=True, export_materials=True,
                          export_triangulated_mesh=True, forward_axis="Y", up_axis="Z", apply_modifiers=True,
                          export_uv=True, path_mode="RELATIVE")


def camera(name, location, target, lens):
    cam = bpy.data.objects.get(name)
    if cam is None:
        cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
        bpy.context.scene.collection.objects.link(cam)
    cam.location = location
    direction = Vector(target) - Vector(location)
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = lens
    return cam


def render():
    scene = bpy.context.scene
    for obj in list(scene.objects):
        if obj.type in {"LIGHT"} and obj.name.startswith("GalpaoSol"):
            bpy.data.objects.remove(obj, do_unlink=True)
    sun = bpy.data.objects.new("GalpaoSol", bpy.data.lights.new("GalpaoSol", "SUN"))
    sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(140))
    scene.collection.objects.link(sun)
    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    background = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    background.inputs[0].default_value = (0.55, 0.65, 0.8, 1.0)
    background.inputs[1].default_value = 0.9
    ground = bpy.data.objects.get("GalpaoChao")
    if ground is None:
        me = bpy.data.meshes.new("GalpaoChao")
        bm = bmesh.new()
        bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=60)
        bm.to_mesh(me)
        bm.free()
        ground = bpy.data.objects.new("GalpaoChao", me)
        scene.collection.objects.link(ground)
        mat = bpy.data.materials.new("galpao_grama")
        mat.use_nodes = True
        next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (0.2, 0.3, 0.12, 1.0)
        me.materials.append(mat)
    ground.location = (W / 2, D / 2, -0.02)
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 48
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    gates_objects = [o for o in bpy.data.objects if o.name.startswith("Portao")]
    shots = [
        ("fora", (W + 14.0, -16.0, 9.0), (W / 2, D / 2, 3.0), 28, False),
        ("dentro", (4.5, 1.2, 1.7), (16.0, 12.0, 3.0), 18, True),
        ("mezanino", (2.0, D - 1.0, MEZ_Z + 1.6), (16.0, 2.0, 1.5), 18, True),
    ]
    for name, location, target, lens, hide_gates in shots:
        for g in gates_objects:
            g.hide_render = hide_gates
        roof = bpy.data.objects.get("Telhado")
        scene.camera = camera("GalpaoCam", location, target, lens)
        scene.render.filepath = PREVIEW.format(name)
        bpy.ops.render.render(write_still=True)
    for g in gates_objects:
        g.hide_render = False


def main():
    cube = bpy.data.objects.get("Cube")
    if cube is not None and cube.type == "MESH" and len(cube.data.vertices) == 8:
        bpy.data.objects.remove(cube, do_unlink=True)
    objects = build()
    export(objects)
    print("[MSC Duo] galpão:", len(objects), "objetos ->", OBJ)
    if "--render" in sys.argv:
        render()
    elif not bpy.app.background:
        bpy.app.timers.register(show, first_interval=1.0)


def show():
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type != "VIEW_3D":
                continue
            area.spaces.active.shading.type = "MATERIAL"
            region = next(r for r in area.regions if r.type == "WINDOW")
            with bpy.context.temp_override(window=window, area=area, region=region):
                bpy.ops.view3d.view_all()
    return None


if __name__ == "__main__":
    main()
