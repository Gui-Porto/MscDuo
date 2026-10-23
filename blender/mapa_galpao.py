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
GAME_GALPAO = os.path.join(FOLDER, "galpao_jogo.obj")
GAME_PLATFORM = os.path.join(FOLDER, "plataforma_jogo.obj")

HOUSE2 = (-7.46, 45.43, 0.38, 241.1472)
FRONT_X = -30.0
LEFT_Y = 13.0
YAW = 90.0
FLOOR_LIFT = 0.1
FLAT_MARGIN = 2.0
FLAT_BLEND = 6.0
FLAT_BELOW = 0.04
FRONT_CLEAR = 9.5
PLATFORM_CELL = 0.5
PLATFORM_SLOPE = 0.3
PLATFORM_REACH = 5.0
PLATFORM_BELOW = 0.02
PLATFORM_SINK = 0.04
SIDE_SKIRT = 1.0
GRAVEL_UV = 1.15
ROAD_COVER = 0.03
STUB_X = -21.0
STUB_REACH = 7.0
DIRT_REACH = 3.0
DIRT_FROM_Y = 15.5
DIRT_LIFT = 0.02
DIRT_START = 0.5
CORE_MARGIN = 1.0
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
            doomed = [f for f in bm.faces if x0 <= f.calc_center_median().x <= x1 + FRONT_CLEAR and y0 <= f.calc_center_median().y <= y1]
            removed += len(doomed)
            bmesh.ops.delete(bm, geom=doomed, context="FACES")
            bm.to_mesh(obj.data)
            bm.free()
    return flattened, removed


def platform(col, objects, floor, W, D):
    from mathutils.bvhtree import BVHTree
    dg = bpy.context.evaluated_depsgraph_get()
    grounds = [(o.name.split("/")[-1], BVHTree.FromObject(o, dg)) for o in objects if o.type == "MESH" and "TERRAIN_OBJ" in o.name]
    gravel_mat = next((o.material_slots[0].material for o in objects if o.name.endswith("TERRAIN_OBJ/Gravel") and o.material_slots), None)
    grass_mat = next((o.material_slots[0].material for o in objects if o.name.endswith("TERRAIN_OBJ/Grass1") and o.material_slots), None)

    def ground(x, y):
        best, kind = None, None
        for name, tree in grounds:
            hit = tree.ray_cast(Vector((x, y, floor + 30.0)), Vector((0, 0, -1)))
            if hit[0] is not None and (best is None or hit[0].z > best):
                best, kind = hit[0].z, name
        return best, kind

    cx0, cx1 = FRONT_X - D - CORE_MARGIN, FRONT_X + FRONT_CLEAR
    cy0, cy1 = LEFT_Y - CORE_MARGIN, LEFT_Y + W + CORE_MARGIN
    top = floor - PLATFORM_BELOW
    gx0, gx1 = cx0 - STUB_REACH, cx1 + STUB_REACH
    gy0, gy1 = cy0 - STUB_REACH, cy1 + STUB_REACH
    nx = int(round((gx1 - gx0) / PLATFORM_CELL)) + 1
    ny = int(round((gy1 - gy0) / PLATFORM_CELL)) + 1

    def reach(x, y):
        return max(cx0 - x, 0.0, x - cx1, cy0 - y, y - cy1)

    samples = {}
    for i in range(nx):
        for j in range(ny):
            samples[(i, j)] = ground(gx0 + i * PLATFORM_CELL, gy0 + j * PLATFORM_CELL)
    road = {k for k, (g, kind) in samples.items() if kind == "Road" and gx0 + k[0] * PLATFORM_CELL < STUB_X and reach(gx0 + k[0] * PLATFORM_CELL, gy0 + k[1] * PLATFORM_CELL) > 0.0}

    def near_road(i, j, cells):
        return any((i + a, j + b) in road for a in range(-cells, cells + 1) for b in range(-cells, cells + 1))

    points = {}
    for i in range(nx):
        for j in range(ny):
            x, y = gx0 + i * PLATFORM_CELL, gy0 + j * PLATFORM_CELL
            d = reach(x, y)
            g, kind = samples[(i, j)]
            if d == 0.0:
                points[(i, j)] = (top, False, False)
                continue
            if g is None:
                continue
            if x > cx1 + 1e-4:
                if y < cy0 or y > cy1:
                    continue
                want = top + (g - PLATFORM_SINK - top) * min(d / SIDE_SKIRT, 1.0)
                points[(i, j)] = (min(want, top), want > g + 0.01 and d <= SIDE_SKIRT, False)
                continue
            if x < STUB_X and near_road(i, j, 2):
                cover = near_road(i, j, 1)
                points[(i, j)] = (g + ROAD_COVER if cover else g - PLATFORM_SINK, False, cover)
                continue
            want = top - d * PLATFORM_SLOPE
            fill = want > g + 0.01 and kind not in ("Gravel", "Road", "DirtRoad")
            points[(i, j)] = (max(want, g - PLATFORM_SINK), fill, False)

    meshes = {"Plataforma": bmesh.new(), "PlataformaGrama": bmesh.new()}
    verts = {name: {} for name in meshes}
    uvs = {name: bm.loops.layers.uv.new("UVMap") for name, bm in meshes.items()}
    counts = {"nucleo": 0, "saia": 0, "aterro": 0, "estrada": 0}
    for i in range(nx - 1):
        for j in range(ny - 1):
            quad = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
            if any(k not in points for k in quad):
                continue
            cx = gx0 + (i + 0.5) * PLATFORM_CELL
            cy = gy0 + (j + 0.5) * PLATFORM_CELL
            d = reach(cx, cy)
            if d == 0.0:
                name, key = "Plataforma", "nucleo"
            elif cx > cx1:
                if d > SIDE_SKIRT or not any(points[k][1] for k in quad):
                    continue
                name, key = "Plataforma", "saia"
            elif cx < STUB_X and any(points[k][2] for k in quad):
                name, key = "PlataformaGrama", "estrada"
            elif d <= PLATFORM_REACH and any(points[k][1] for k in quad):
                name, key = "PlataformaGrama", "aterro"
            else:
                continue
            bm = meshes[name]
            face_verts = []
            for k in quad:
                if k not in verts[name]:
                    verts[name][k] = bm.verts.new((gx0 + k[0] * PLATFORM_CELL, gy0 + k[1] * PLATFORM_CELL, points[k][0]))
                face_verts.append(verts[name][k])
            face = bm.faces.new(face_verts)
            scale = GRAVEL_UV if name == "Plataforma" else 0.25
            for loop in face.loops:
                loop[uvs[name]].uv = (loop.vert.co.x * scale, loop.vert.co.y * scale)
            counts[key] += 1
    materials = {"Plataforma": gravel_mat or bpy.data.materials.new("plataforma_cascalho"),
                 "PlataformaGrama": grass_mat or bpy.data.materials.new("plataforma_grama")}
    for name, bm in meshes.items():
        me = bpy.data.meshes.new(name)
        bm.normal_update()
        bm.to_mesh(me)
        bm.free()
        me.materials.append(materials[name])
        old = bpy.data.objects.get(name)
        if old is not None:
            bpy.data.objects.remove(old, do_unlink=True)
        col.objects.link(bpy.data.objects.new(name, me))
    counts["terra"] = dirt(col, samples, gx0, gy0, nx, ny, cx1, cy1, top)
    print("[MSC Duo] plataforma:", counts)
    return sum(counts.values())


def dirt(col, samples, gx0, gy0, nx, ny, cx1, cy1, top):
    grass = ("Grass1", "Grass2")
    heights = {}
    for (i, j), (g, kind) in samples.items():
        x, y = gx0 + i * PLATFORM_CELL, gy0 + j * PLATFORM_CELL
        if g is None or x < cx1 - 1e-4 or x > cx1 + DIRT_REACH or y < DIRT_FROM_Y or y > cy1:
            continue
        d = x - cx1
        skirt = top + (g - PLATFORM_SINK - top) * min(d / SIDE_SKIRT, 1.0)
        if kind not in grass:
            heights[(i, j)] = g + 0.01
        elif d < DIRT_START - 1e-4:
            heights[(i, j)] = min(skirt, g) - 0.03
        else:
            heights[(i, j)] = max(g + DIRT_LIFT, skirt + 0.01)

    def strip(i, j):
        g, kind = samples[(i, j)]
        if kind not in grass or gx0 + i * PLATFORM_CELL - cx1 < DIRT_START - 1e-4:
            return False
        return any(samples.get((i + a, j), (None, None))[1] == "Gravel" for a in (1, 2, 3))

    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    verts = {}
    faces = 0
    for i in range(nx - 1):
        for j in range(ny - 1):
            quad = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
            if any(k not in heights for k in quad) or not any(strip(*k) for k in quad):
                continue
            face_verts = []
            for k in quad:
                if k not in verts:
                    verts[k] = bm.verts.new((gx0 + k[0] * PLATFORM_CELL, gy0 + k[1] * PLATFORM_CELL, heights[k]))
                face_verts.append(verts[k])
            face = bm.faces.new(face_verts)
            for loop in face.loops:
                loop[uv].uv = (loop.vert.co.x * GRAVEL_UV, loop.vert.co.y * GRAVEL_UV)
            faces += 1
    me = bpy.data.meshes.new("PlataformaTerra")
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    me.materials.append(bpy.data.materials.get("plataforma_terra") or bpy.data.materials.new("plataforma_terra"))
    old = bpy.data.objects.get("PlataformaTerra")
    if old is not None:
        bpy.data.objects.remove(old, do_unlink=True)
    if faces:
        col.objects.link(bpy.data.objects.new("PlataformaTerra", me))
    return faces


def clear_trees(objects, W, D):
    x0, x1 = FRONT_X - D - FLAT_MARGIN, FRONT_X + FLAT_MARGIN + FRONT_CLEAR
    y0, y1 = LEFT_Y - FLAT_MARGIN, LEFT_Y + W + FLAT_MARGIN
    removed = 0
    for obj in objects:
        if obj.type != "MESH" or "FOLIAGE" not in obj.name:
            continue
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        doomed = [f for f in bm.faces if x0 <= f.calc_center_median().x <= x1 and y0 <= f.calc_center_median().y <= y1]
        removed += len(doomed)
        bmesh.ops.delete(bm, geom=doomed, context="FACES")
        bm.to_mesh(obj.data)
        bm.free()
    return removed


def export_game(col):
    galpao = [o for o in bpy.data.collections["GALPAO"].objects if o.type == "MESH"]
    write_game_simple(GAME_GALPAO, galpao, lambda name, index: name.replace("galpao_", ""))
    plat = [o for o in col.objects if o.name in ("Plataforma", "PlataformaGrama", "PlataformaTerra")]
    write_game_simple(GAME_PLATFORM, plat, lambda name, index: "terra" if name.startswith("plataforma_terra") else "cascalho" if "GRAVEL" in name.upper() or "cascalho" in name else "grama")
    print("[MSC Duo] exportado para o jogo:", GAME_GALPAO, GAME_PLATFORM)


def write_game_simple(path, objects, rename):
    out = ["# MSC Duo: coordenadas do mundo em eixos do Blender (x, z, y da Unity)"]
    vbase = 1
    tbase = 1
    for obj in objects:
        me = obj.data
        uv_layer = me.uv_layers.active
        matrix = obj.matrix_world
        out.append("o " + obj.name)
        for v in me.vertices:
            w = matrix @ v.co
            out.append("v %.4f %.4f %.4f" % (w.x, w.y, w.z))
        for loop in me.loops:
            u, v = uv_layer.data[loop.index].uv if uv_layer else (0.0, 0.0)
            out.append("vt %.4f %.4f" % (u, v))
        groups = {}
        for poly in me.polygons:
            groups.setdefault(poly.material_index, []).append(poly)
        for index, polys in sorted(groups.items()):
            mat = obj.material_slots[index].material if index < len(obj.material_slots) else None
            out.append("usemtl " + rename(mat.name if mat else "sem_material", index))
            for poly in polys:
                ids = [(me.loops[li].vertex_index + vbase, li + tbase) for li in poly.loop_indices]
                for k in range(1, len(ids) - 1):
                    a, b, c = ids[0], ids[k], ids[k + 1]
                    out.append("f %d/%d %d/%d %d/%d" % (a[0], a[1], b[0], b[1], c[0], c[1]))
        vbase += len(me.vertices)
        tbase += len(me.loops)
    open(path, "w", encoding="utf-8").write(chr(10).join(out) + chr(10))


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
        verts = 0
        floor, (low, high) = place_galpao(grid, xs, zs)
        removed = clear_trees(imported, 24.0, 16.0)
        flattened = platform(col, imported, floor, 24.0, 16.0)
        print(f"[MSC Duo] área do jogo importada: {len(imported)} objetos, {removed} faces de árvores removidas, plataforma com {flattened} quadrados")
    else:
        ground = terrain(col, grid, xs, zs, cx, cz, half)
        verts = house2(col)
        floor, (low, high) = place_galpao(grid, xs, zs)
        flattened = flatten(ground, floor, 24.0, 16.0)
    lighting()
    print(f"[MSC Duo] mapa: terreno {len(xs)}x{len(zs)}, {flattened} pontos nivelados, piso do galpão em {floor:.2f} m (chão entre {low:.2f} e {high:.2f})")
    if "--render" in sys.argv:
        render()
    if os.path.exists(AREA):
        export_game(col)
    if "--save" in sys.argv:
        save_view()
        bpy.ops.file.pack_all()
        bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    if not bpy.app.background:
        bpy.app.timers.register(show, first_interval=1.0)


if __name__ == "__main__":
    main()
