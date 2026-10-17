import csv
import math
import os
import runpy
import sys

import bmesh
import bpy
from mathutils import Vector

FOLDER = os.path.dirname(os.path.abspath(__file__))
SETTINGS = os.path.join(os.path.expanduser("~"), "Documents", "MySummerCar", "Mods", "Config", "Mod Settings", "MscDuo")
MAP = os.path.join(SETTINGS, "maps", "20261014-112211")
RELEVO = os.path.join(SETTINGS, "dumps", "relevo.obj")
AREA = os.path.join(SETTINGS, "dumps", "area", "area.obj")
BLEND = os.path.join(FOLDER, "mapa-galpao.blend")
PREVIEW = os.path.join(FOLDER, "mapa-galpao-{}.png")

HOUSE2 = (-7.46, 45.43, 0.38, 241.1472)
FRONT_X = -30.0
LEFT_Y = 13.0
YAW = 90.0
FLOOR_LIFT = 0.1
FLAT_MARGIN = 2.0
FLAT_BLEND = 6.0
FLAT_BELOW = 0.04
COLLECTION = "MAPA"


def collection(name):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    for obj in list(col.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    return col


def meta():
    values = {}
    for line in open(os.path.join(MAP, "meta.txt"), encoding="utf-8"):
        key, _, value = line.strip().partition("=")
        values[key] = value
    return float(values["center_x"]), float(values["center_z"]), float(values["half"])


def heights():
    grid = {}
    for row in csv.DictReader(open(os.path.join(MAP, "heights.csv"), encoding="utf-8")):
        grid[(round(float(row["x"]), 2), round(float(row["z"]), 2))] = float(row["y"])
    xs = sorted({k[0] for k in grid})
    zs = sorted({k[1] for k in grid})
    return grid, xs, zs


def terrain(col, grid, xs, zs, cx, cz, half):
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    verts = {}
    for i, x in enumerate(xs):
        for j, z in enumerate(zs):
            y = grid.get((x, z))
            if y is not None:
                verts[(i, j)] = bm.verts.new((x, z, y))
    for i in range(len(xs) - 1):
        for j in range(len(zs) - 1):
            quad = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
            if any(k not in verts for k in quad):
                continue
            face = bm.faces.new([verts[k] for k in quad])
            for loop in face.loops:
                co = loop.vert.co
                loop[uv].uv = ((co.x - cx + half) / (2 * half), (co.y - cz + half) / (2 * half))
    me = bpy.data.meshes.new("Terreno")
    bm.to_mesh(me)
    bm.free()
    for poly in me.polygons:
        poly.use_smooth = True
    mat = bpy.data.materials.new("mapa_foto")
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(MAP, "map.png"), check_existing=True)
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.95
    me.materials.append(mat)
    obj = bpy.data.objects.new("Terreno", me)
    col.objects.link(obj)
    return obj


def house2(col):
    if not os.path.exists(RELEVO):
        return 0
    verts, faces, current = [], {}, None
    for line in open(RELEVO, encoding="utf-8"):
        if line.startswith("v "):
            p = line.split()
            verts.append((float(p[1]), float(p[2]), float(p[3])))
        elif line.startswith(("o ", "g ")):
            current = line.split(maxsplit=1)[1].strip()
        elif line.startswith("f ") and current and current.startswith("casa2|"):
            faces.setdefault("casa2", []).append([int(t.split("/")[0]) - 1 for t in line.split()[1:]])
    used = sorted({i for f in faces.get("casa2", []) for i in f})
    if not used:
        return 0
    remap = {old: new for new, old in enumerate(used)}
    me = bpy.data.meshes.new("Casa2")
    me.from_pydata([verts[i] for i in used], [], [[remap[i] for i in f] for f in faces["casa2"]])
    mat = bpy.data.materials.new("mapa_casa")
    mat.use_nodes = True
    next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (0.78, 0.74, 0.68, 1.0)
    me.materials.append(mat)
    obj = bpy.data.objects.new("Casa2", me)
    x, y, z, yaw = HOUSE2
    obj.location = (x, y, z)
    obj.rotation_euler = (0.0, 0.0, -math.radians(yaw))
    col.objects.link(obj)
    return len(used)


def flatten(terrain_obj, floor, W, D):
    x0, x1 = FRONT_X - D - FLAT_MARGIN, FRONT_X + FLAT_MARGIN
    y0, y1 = LEFT_Y - FLAT_MARGIN, LEFT_Y + W + FLAT_MARGIN
    target = floor - FLAT_BELOW
    changed = 0
    for v in terrain_obj.data.vertices:
        dx = max(x0 - v.co.x, 0.0, v.co.x - x1)
        dy = max(y0 - v.co.y, 0.0, v.co.y - y1)
        distance = math.hypot(dx, dy)
        if distance >= FLAT_BLEND:
            continue
        t = 1.0 - distance / FLAT_BLEND
        weight = t * t * (3 - 2 * t)
        v.co.z = v.co.z + (target - v.co.z) * weight
        changed += 1
    terrain_obj.data.update()
    return changed


def import_area(col):
    before = set(bpy.data.objects)
    bpy.ops.wm.obj_import(filepath=AREA, forward_axis="Y", up_axis="Z")
    imported = [o for o in bpy.data.objects if o not in before]
    fixed = set()
    for obj in imported:
        for slot in obj.material_slots:
            mat = slot.material
            if mat is None or mat in fixed or not mat.use_nodes:
                continue
            fixed.add(mat)
            tex = next((n for n in mat.node_tree.nodes if n.type == "TEX_IMAGE" and n.image is not None), None)
            bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
            if tex is None or bsdf is None:
                continue
            bsdf.inputs["Roughness"].default_value = 0.85
            bsdf.inputs["Specular IOR Level"].default_value = 0.2
            if "road_map" in tex.image.name.lower():
                for link in list(bsdf.inputs["Base Color"].links):
                    mat.node_tree.links.remove(link)
                bsdf.inputs["Base Color"].default_value = (0.36, 0.32, 0.27, 1.0)
                continue
            if "FOLIAGE" in obj.name or tex.image.name.lower().startswith(("tree", "bush", "spruce", "birch", "pine", "leaf", "grass")):
                mat.node_tree.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
                if hasattr(mat, "surface_render_method"):
                    mat.surface_render_method = "DITHERED"
                else:
                    mat.blend_method = "HASHED"
    for obj in imported:
        for c in list(obj.users_collection):
            c.objects.unlink(obj)
        col.objects.link(obj)
    return imported


def area_ground(objects, x, y):
    best = None
    for obj in objects:
        if "TERRAIN_OBJ" not in obj.name and "Road" not in obj.name:
            continue
        for v in obj.data.vertices:
            if abs(v.co.x - x) < 1.5 and abs(v.co.y - y) < 1.5:
                best = v.co.z if best is None else max(best, v.co.z)
    return best


def clear_and_flatten(objects, floor, W, D):
    x0, x1 = FRONT_X - D - FLAT_MARGIN, FRONT_X + FLAT_MARGIN
    y0, y1 = LEFT_Y - FLAT_MARGIN, LEFT_Y + W + FLAT_MARGIN
    target = floor - FLAT_BELOW
    flattened = removed = 0
    for obj in objects:
        if obj.type != "MESH":
            continue
        if "TERRAIN_OBJ" in obj.name:
            for v in obj.data.vertices:
                dx = max(x0 - v.co.x, 0.0, v.co.x - x1)
                dy = max(y0 - v.co.y, 0.0, v.co.y - y1)
                distance = math.hypot(dx, dy)
                if distance >= FLAT_BLEND:
                    continue
                t = 1.0 - distance / FLAT_BLEND
                v.co.z += (target - v.co.z) * t * t * (3 - 2 * t)
                flattened += 1
            obj.data.update()
        elif "FOLIAGE" in obj.name:
            bm = bmesh.new()
            bm.from_mesh(obj.data)
            doomed = [f for f in bm.faces if x0 <= f.calc_center_median().x <= x1 and y0 <= f.calc_center_median().y <= y1]
            removed += len(doomed)
            bmesh.ops.delete(bm, geom=doomed, context="FACES")
            bm.to_mesh(obj.data)
            bm.free()
    return flattened, removed


def save_view():
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type != "VIEW_3D":
                continue
            space = area.spaces.active
            space.shading.type = "MATERIAL"
            space.clip_end = 2000.0
            space.overlay.show_floor = False
            region = space.region_3d
            region.view_perspective = "PERSP"
            region.view_location = (FRONT_X - 4.0, LEFT_Y + 12.0, 0.0)
            region.view_distance = 70.0
            region.view_rotation = Vector((-0.6, 0.55, -0.58)).normalized().to_track_quat("-Z", "Y")


def ground_at(grid, xs, zs, x, y):
    def nearest(values, v):
        return min(values, key=lambda a: abs(a - v))
    return grid.get((nearest(xs, x), nearest(zs, y)))


def place_galpao(grid, xs, zs):
    lib = runpy.run_path(os.path.join(FOLDER, "galpao.py"), run_name="galpao_lib")
    objects = lib["build"]()
    W, D = lib["W"], lib["D"]
    yaw = math.radians(YAW)
    samples = []
    for u in range(0, int(W) + 1, 2):
        for v in range(0, int(D) + 1, 2):
            x = FRONT_X - v * math.sin(yaw) * 1.0 + u * math.cos(yaw)
            y = LEFT_Y + u * math.sin(yaw) + v * math.cos(yaw)
            h = ground_at(grid, xs, zs, FRONT_X - v, LEFT_Y + u)
            if h is not None:
                samples.append(h)
    floor = sum(samples) / len(samples) + FLOOR_LIFT if samples else 0.0
    anchor = bpy.data.objects.get("GalpaoPosicao")
    if anchor is None:
        anchor = bpy.data.objects.new("GalpaoPosicao", None)
        bpy.context.scene.collection.objects.link(anchor)
    anchor.empty_display_type = "ARROWS"
    anchor.empty_display_size = 3.0
    anchor.location = (FRONT_X, LEFT_Y, floor)
    anchor.rotation_euler = (0.0, 0.0, yaw)
    for obj in objects:
        obj.parent = anchor
    return floor, (min(samples), max(samples)) if samples else (0.0, 0.0)


def camera(name, location, target, lens):
    cam = bpy.data.objects.get(name)
    if cam is None:
        cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
        bpy.context.scene.collection.objects.link(cam)
    cam.location = location
    cam.rotation_euler = (Vector(target) - Vector(location)).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = lens
    return cam


def lighting():
    scene = bpy.context.scene
    sun = bpy.data.objects.get("MapaSol")
    if sun is None:
        sun = bpy.data.objects.new("MapaSol", bpy.data.lights.new("MapaSol", "SUN"))
        scene.collection.objects.link(sun)
    sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(50), math.radians(0), math.radians(-60))
    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    background = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    background.inputs[0].default_value = (0.55, 0.65, 0.8, 1.0)
    background.inputs[1].default_value = 0.8


def render():
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 48
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    shots = [
        ("aereo", (-20.0, 10.0, 95.0), (-20.0, 25.0, 0.0), 24),
        ("perspectiva", (5.0, -18.0, 22.0), (-32.0, 28.0, 2.0), 22),
        ("chegada", (-12.0, 22.0, 2.0), (-32.0, 24.0, 3.0), 20),
    ]
    for name, location, target, lens in shots:
        scene.camera = camera("MapaCam", location, target, lens)
        scene.render.filepath = PREVIEW.format(name)
        bpy.ops.render.render(write_still=True)


def show():
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type != "VIEW_3D":
                continue
            area.spaces.active.shading.type = "MATERIAL"
            area.spaces.active.clip_end = 2000.0
            region = next(r for r in area.regions if r.type == "WINDOW")
            anchor = bpy.data.objects.get("GalpaoPosicao")
            with bpy.context.temp_override(window=window, area=area, region=region):
                bpy.ops.object.select_all(action="DESELECT")
                if anchor is not None:
                    anchor.select_set(True)
                    bpy.context.view_layer.objects.active = anchor
                bpy.ops.view3d.view_selected()
    return None


def main():
    for name in ("Cube", "Light", "Camera"):
        obj = bpy.data.objects.get(name)
        if obj is not None:
            bpy.data.objects.remove(obj, do_unlink=True)
    cx, cz, half = meta()
    grid, xs, zs = heights()
    col = collection(COLLECTION)
    if os.path.exists(AREA):
        imported = import_area(col)
        verts = len(imported)
        floor, (low, high) = place_galpao(grid, xs, zs)
        flattened, removed = clear_and_flatten(imported, floor, 24.0, 16.0)
        print(f"[MSC Duo] área do jogo importada: {len(imported)} objetos, {removed} faces de árvores removidas")
    else:
        ground = terrain(col, grid, xs, zs, cx, cz, half)
        verts = house2(col)
        floor, (low, high) = place_galpao(grid, xs, zs)
        flattened = flatten(ground, floor, 24.0, 16.0)
    lighting()
    print(f"[MSC Duo] mapa: terreno {len(xs)}x{len(zs)}, casa 2 com {verts} vértices, {flattened} pontos nivelados, piso do galpão em {floor:.2f} m (chão entre {low:.2f} e {high:.2f})")
    if "--render" in sys.argv:
        render()
    if "--save" in sys.argv:
        save_view()
        bpy.ops.file.pack_all()
        bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    if not bpy.app.background:
        bpy.app.timers.register(show, first_interval=1.0)


if __name__ == "__main__":
    main()
