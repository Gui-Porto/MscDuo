import os

import bmesh
import bpy
import math

from mathutils import Euler, Vector
from mathutils.bvhtree import BVHTree

RELEVO = os.path.join(os.path.expanduser("~"), "Documents", "MySummerCar", "Mods", "Config", "Mod Settings", "MscDuo", "dumps", "relevo.obj")
THICKNESS = 0.25
PLATE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "extensao.obj")
UNDER_SLAB = 0.6
EXTRA_ROAD = 0.2
SINK = 0.0
ROAD_LIFT = 0.01
FILL = os.path.join(os.path.dirname(os.path.abspath(__file__)), "aterro.obj")
FILL_AREA = (-13.5, -2.5, -17.5, -8.5)
FILL_CELL = 0.25
FILL_LEVEL = -0.96
FILL_SLOPE = 0.2
FILL_LIFT = 0.02
FILL_VERSION = 3
FILL_UNDER = 0.02
ROAD_TOLERANCE = 0.04
CRACK = [(-1.5, -8.6), (-2.6, -8.6), (-4.0, -9.2), (-4.35, -9.9), (-4.3, -11.0)]
HIDDEN = ("YARD/", "JONNEZ")
DUMPS = os.path.dirname(RELEVO)
LAJE_OBJ = os.path.join(DUMPS, "laje.obj")
LAJE_PNG = os.path.join(DUMPS, "laje.png")
IMAGE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "extensao.png")
IMAGE_U = (0.40, 0.90)
IMAGE_V = (-0.36, 0.20)
APRON_LEFT = (-3.37, -10.54)
APRON_RIGHT = (0.96, -15.24)
SIDE_INSET = 0.05
DEPTH = 0.6
EDIT = "EDITAR"
PLATE_VERSION = 4
APRON_U = (0.03321, -0.03610, 0.24511)
APRON_V = (0.03610, 0.03320, 0.48602)
LONGEST = 15.0

COLORS = {
    "terreno": (0.33, 0.45, 0.25, 1.0),
    "estrada": (0.42, 0.36, 0.28, 1.0),
    "casa2": (0.75, 0.72, 0.66, 1.0),
    "vegetacao": (0.2, 0.35, 0.15, 1.0),
    "lote": (0.5, 0.5, 0.55, 1.0),
    "outros": (0.6, 0.4, 0.4, 1.0),
    "extensao": (0.85, 0.3, 0.2, 1.0),
    "aterro": (0.45, 0.6, 0.25, 1.0),
    "chao_estrada": (0.25, 0.35, 0.6, 1.0),
    "chao_outros": (0.6, 0.4, 0.4, 1.0),
    "chao_lote": (0.5, 0.5, 0.55, 1.0),
}
REFERENCE = ("chao_casa2", "chao_terreno", "chao_vegetacao")


def collection():
    col = bpy.data.collections.get("MSCDUO")
    if col is None:
        col = bpy.data.collections.new("MSCDUO")
        bpy.context.scene.collection.children.link(col)
    for obj in list(col.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    return col


def material(name):
    color = COLORS.get(name, COLORS["outros"])
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = color
    return mat


def add_mesh(col, name, verts, faces, category):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    obj.data.materials.append(material(category))
    col.objects.link(obj)
    return obj


def read_relevo(path):
    verts, corners, groups = [], [], {}
    current = None
    skip = False
    upright = False

    def point(values):
        a, b, c = (float(x) for x in values)
        return (a, b, c) if upright else (a, -c, b)

    with open(path, encoding="utf-8") as f:
        for line in f:
            if line.startswith("# eixos 2"):
                upright = True
            elif line.startswith("# extensao "):
                corners.append(point(line.split()[2:5]))
            elif line.startswith("v "):
                verts.append(point(line.split()[1:4]))
            elif line.startswith("o "):
                category, path = line[2:].strip().split("|", 1)
                if path.startswith("MAP/MESH/TERRAIN_OBJ/Road"):
                    category = "estrada"
                skip = path.startswith(HIDDEN) or "house_base_concrete" in path
                current = groups.setdefault(category, [])
            elif line.startswith("f ") and not skip:
                current.append([int(p.split("/")[0]) - 1 for p in line.split()[1:]])
    return verts, corners, groups


def compact(verts, face):
    pts = [verts[i] for i in face]
    return all(sum((p[k] - q[k]) ** 2 for k in range(3)) < LONGEST ** 2 for p in pts for q in pts)


def add_group(col, name, verts, faces):
    faces = [f for f in faces if compact(verts, f)]
    remap, local = {}, []
    for face in faces:
        for i in face:
            if i not in remap:
                remap[i] = len(local)
                local.append(verts[i])
    return add_mesh(col, name, local, [[remap[i] for i in face] for face in faces], name)


def height(trees, x, y, start):
    for tree in trees:
        hit = tree.ray_cast(Vector((x, y, start)), Vector((0, 0, -1)), 20.0)
        if hit[0] is not None:
            return hit[0].z
    return None


def bvh(obj):
    return BVHTree.FromPolygons([v.co for v in obj.data.vertices], [p.vertices for p in obj.data.polygons])


def prism(col, outline, tops):
    n = len(outline)
    verts = [(p[0], p[1], t) for p, t in zip(outline, tops)] + [(p[0], p[1], t - THICKNESS) for p, t in zip(outline, tops)]
    faces = [list(range(n)), list(range(2 * n - 1, n - 1, -1))]
    faces += [[i, i + n, (i + 1) % n + n, (i + 1) % n] for i in range(n)]
    add_mesh(col, "Extensao", verts, faces, "extensao")


LOG = os.path.join(os.path.dirname(RELEVO), "blender_log.txt")


def uv_of(x, y):
    return (APRON_U[0] * x + APRON_U[1] * y + APRON_U[2], APRON_V[0] * x + APRON_V[1] * y + APRON_V[2])


def image_uv(x, y):
    u, v = uv_of(x, y)
    return ((u - IMAGE_U[0]) / (IMAGE_U[1] - IMAGE_U[0]), (v - IMAGE_V[0]) / (IMAGE_V[1] - IMAGE_V[0]))


def directions():
    gu = Vector(APRON_U[:2]).normalized()
    gv = Vector(APRON_V[:2]).normalized()
    return gu, -gv


def intersect(p, d, a, b):
    e = b - a
    den = d.x * e.y - d.y * e.x
    t = ((a.x - p.x) * e.y - (a.y - p.y) * e.x) / den
    return p + d * t


def outline(corners):
    c = [Vector(p[:2]) for p in corners]
    inward = (c[0] + c[1] - c[2] - c[3]).normalized()
    right, along = directions()
    corner_left, corner_right = Vector(APRON_LEFT), Vector(APRON_RIGHT)
    road_a, road_b = c[2] - inward * EXTRA_ROAD, c[3] - inward * EXTRA_ROAD
    road_right = intersect(corner_right, along, road_a, road_b)
    ring = [(road_a, "rua"), (road_right, "rua"), (corner_right, "laje"), (corner_left, "laje")]
    ring += [(Vector(p), "laje" if k == 0 else "livre") for k, p in enumerate(CRACK)]
    return ring


def plate(col, corners, top, road_top):
    tagged = outline(corners)
    if area([p for p, _ in tagged]) < 0:
        tagged.reverse()
    ring = [p for p, _ in tagged]
    kinds = [k for _, k in tagged]
    n = len(ring)
    bm = bmesh.new()
    heights = [road_top if k == "rua" else top for k in kinds]
    upper = [bm.verts.new((p.x, p.y, h)) for p, h in zip(ring, heights)]
    lower = [bm.verts.new((p.x, p.y, top - DEPTH)) for p in ring]
    face = bm.faces.new(upper)
    tops = bmesh.ops.triangulate(bm, faces=[face])["faces"]
    sides = []
    for i in range(n):
        j = (i + 1) % n
        if kinds[i] == kinds[j] and kinds[i] in ("laje", "rua"):
            continue
        sides.append(bm.faces.new((upper[i], lower[i], lower[j], upper[j])))
    bm.normal_update()
    layer = bm.loops.layers.uv.new("UV")
    for f in tops:
        for loop in f.loops:
            loop[layer].uv = image_uv(loop.vert.co.x, loop.vert.co.y)
    for f in sides:
        out = Vector((f.normal.x, f.normal.y))
        if out.length > 0:
            out.normalize()
        for loop in f.loops:
            p = loop.vert.co
            q = Vector((p.x, p.y)) + out * (top - p.z)
            loop[layer].uv = image_uv(q.x, q.y)
    mesh = bpy.data.meshes.new("Extensao")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("Extensao", mesh)
    obj["versao"] = PLATE_VERSION
    obj.data.materials.append(textured("extensao_cimento", IMAGE) if os.path.exists(IMAGE) else material("extensao"))
    col.objects.link(obj)
    write_obj(obj, PLATE)
    return obj


def textured(name, path):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = next((n for n in nodes if n.type == "BSDF_PRINCIPLED"), None)
    image = next((n for n in nodes if n.type == "TEX_IMAGE"), None) or nodes.new("ShaderNodeTexImage")
    image.image = bpy.data.images.load(path, check_existing=True)
    image.image.reload()
    if bsdf is not None:
        bsdf.inputs["Roughness"].default_value = 0.9
        mat.node_tree.links.new(image.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


def laje(col):
    if not (os.path.exists(LAJE_OBJ) and os.path.exists(LAJE_PNG)):
        return None
    verts, uvs, faces = [], [], []
    with open(LAJE_OBJ, encoding="utf-8") as f:
        for line in f:
            if line.startswith("v "):
                verts.append(tuple(float(x) for x in line.split()[1:4]))
            elif line.startswith("vt "):
                uvs.append(tuple(float(x) for x in line.split()[1:3]))
            elif line.startswith("f "):
                faces.append([int(p.split("/")[0]) - 1 for p in line.split()[1:]])
    mesh = bpy.data.meshes.new("laje")
    mesh.from_pydata(verts, [], faces)
    layer = mesh.uv_layers.new(name="UV")
    for loop in mesh.loops:
        layer.data[loop.index].uv = uvs[loop.vertex_index]
    obj = bpy.data.objects.new("laje", mesh)
    obj.data.materials.append(textured("laje", LAJE_PNG))
    col.objects.link(obj)
    return obj


def area(ring):
    return sum(ring[i - 1].x * ring[i].y - ring[i].x * ring[i - 1].y for i in range(len(ring))) / 2


def editing():
    col = bpy.data.collections.get(EDIT)
    if col is None:
        col = bpy.data.collections.new(EDIT)
        bpy.context.scene.collection.children.link(col)
    return col


def refresh_uv(obj):
    mesh = obj.data
    if not mesh.uv_layers:
        mesh.uv_layers.new(name="UV")
    layer = mesh.uv_layers.active.data
    top = max(v.co.z for v in mesh.vertices)
    for poly in mesh.polygons:
        out = Vector((poly.normal.x, poly.normal.y))
        flat = poly.normal.z > 0.5 or out.length == 0
        if not flat:
            out.normalize()
        for k in poly.loop_indices:
            p = mesh.vertices[mesh.loops[k].vertex_index].co
            q = Vector((p.x, p.y)) if flat else Vector((p.x, p.y)) + out * (top - p.z)
            layer[k].uv = image_uv(q.x, q.y)
    mesh.update()


def on_save(*args):
    obj = bpy.data.objects.get("Extensao")
    if obj is None or obj.type != "MESH":
        return
    if obj.mode == "EDIT":
        obj.update_from_editmode()
    refresh_uv(obj)
    write_obj(obj, PLATE)
    filled = bpy.data.objects.get("Aterro")
    if filled is not None and filled.type == "MESH":
        if filled.mode == "EDIT":
            filled.update_from_editmode()
        write_plain_obj(filled, FILL)
    print("[MSC Duo] extensao salva em", PLATE)


def hook_save():
    handlers = bpy.app.handlers.save_post
    for h in list(handlers):
        if getattr(h, "__name__", "") == "on_save":
            handlers.remove(h)
    handlers.append(on_save)


def write_obj(obj, path):
    mesh = obj.data
    layer = mesh.uv_layers.active.data
    with open(path, "w", encoding="utf-8") as f:
        f.write("# MSC Duo extensao: eixos do Blender, coordenadas da casa 2\n")
        for v in mesh.vertices:
            f.write("v %.4f %.4f %.4f\n" % tuple(v.co))
        for loop in mesh.loops:
            f.write("vt %.5f %.5f\n" % tuple(layer[loop.index].uv))
        for p in mesh.polygons:
            f.write("f " + " ".join("%d/%d" % (mesh.loops[k].vertex_index + 1, k + 1) for k in p.loop_indices) + "\n")


def segment_distance(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
    return (a + ab * t - p).length


def outline_edges(mesh):
    count = {}
    for poly in mesh.polygons:
        if poly.normal.z < 0.5:
            continue
        for key in poly.edge_keys:
            count[key] = count.get(key, 0) + 1
    edges = []
    for (a, b), n in count.items():
        if n == 1:
            pa, pb = mesh.vertices[a].co, mesh.vertices[b].co
            edges.append((Vector((pa.x, pa.y)), Vector((pb.x, pb.y))))
    return edges


def inside_edges(edges, p):
    crossings = 0
    for a, b in edges:
        if (a.y > p.y) != (b.y > p.y) and p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x:
            crossings += 1
    return crossings % 2 == 1


def fill(col, trees, plate_obj):
    ground = trees.get("chao_terreno") or trees.get("terreno")
    road = trees.get("chao_estrada")
    house = trees.get("chao_casa2")
    if ground is None or road is None or plate_obj is None:
        return None
    edges = outline_edges(plate_obj.data)
    x0, x1, y0, y1 = FILL_AREA
    nx = int(round((x1 - x0) / FILL_CELL)) + 1
    ny = int(round((y1 - y0) / FILL_CELL)) + 1

    def probe(tree, x, y):
        hit = tree.ray_cast(Vector((x, y, 5.0)), Vector((0, 0, -1)), 20.0)[0]
        return None if hit is None else hit.z

    plate_top = max(v.co.z for v in plate_obj.data.vertices)
    cells, road_cells = {}, []
    for i in range(nx):
        for j in range(ny):
            p = Vector((x0 + i * FILL_CELL, y0 + j * FILL_CELL))
            g = probe(ground, p.x, p.y)
            r = probe(road, p.x, p.y)
            h = probe(house, p.x, p.y) if house is not None else None
            base = max(v for v in (g, r) if v is not None) if (g is not None or r is not None) else None
            if base is None or (h is not None and h > base + 0.05):
                continue
            cap = None
            if inside_edges(edges, p):
                cap = plate_top - FILL_UNDER
            elif r is not None and r >= FILL_LEVEL - ROAD_TOLERANCE:
                road_cells.append(p)
                cap = r - FILL_UNDER
            cells[(i, j)] = (p, base, cap)

    heights = {}
    for key, (p, base, cap) in cells.items():
        if cap is not None:
            heights[key] = (cap, cap > base - 0.3)
            continue
        d = min(segment_distance(p, a, b) for a, b in edges)
        for q in road_cells:
            if abs(q.x - p.x) < d and abs(q.y - p.y) < d:
                d = min(d, (q - p).length)
        want = FILL_LEVEL - FILL_SLOPE * d
        heights[key] = (max(want, base + FILL_LIFT), want > base + FILL_LIFT)

    bm = bmesh.new()
    verts = {}
    for i in range(nx - 1):
        for j in range(ny - 1):
            quad = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
            if any(k not in heights for k in quad) or not any(heights[k][1] for k in quad) or all(cells[k][2] is not None for k in quad):
                continue
            face = []
            for k in quad:
                if k not in verts:
                    verts[k] = bm.verts.new((x0 + k[0] * FILL_CELL, y0 + k[1] * FILL_CELL, heights[k][0]))
                face.append(verts[k])
            bm.faces.new(face)
    if not bm.faces:
        bm.free()
        return None
    bm.normal_update()
    out = bpy.data.meshes.new("Aterro")
    bm.to_mesh(out)
    bm.free()
    obj = bpy.data.objects.new("Aterro", out)
    obj["versao"] = FILL_VERSION
    obj.data.materials.append(material("aterro"))
    col.objects.link(obj)
    write_plain_obj(obj, FILL)
    return obj


def write_plain_obj(obj, path):
    mesh = obj.data
    with open(path, "w", encoding="utf-8") as f:
        f.write("# MSC Duo aterro: eixos do Blender, coordenadas da casa 2\n")
        for v in mesh.vertices:
            f.write("v %.4f %.4f %.4f\n" % tuple(v.co))
        for p in mesh.polygons:
            f.write("f " + " ".join(str(i + 1) for i in p.vertices) + "\n")


def main():
    cube = bpy.data.objects.get("Cube")
    if cube is not None:
        bpy.data.objects.remove(cube, do_unlink=True)

    col = collection()
    if os.path.exists(RELEVO):
        verts, corners, groups = read_relevo(RELEVO)
        objects = {}
        for name, faces in groups.items():
            if faces:
                objects[name] = add_group(col, name, verts, faces)
                objects[name].hide_set(name in REFERENCE)
                objects[name].hide_render = name in REFERENCE
        slab = laje(col)
        if slab is not None:
            objects["laje"] = slab
        if len(corners) == 4:
            trees = {k: bvh(v) for k, v in objects.items()}
            slab_side = [trees[k] for k in ("laje", "casa2") if k in trees]
            road_side = [trees[k] for k in ("chao_estrada", "estrada", "chao_terreno", "terreno") if k in trees]
            inward = Vector(corners[0][:2]) + Vector(corners[1][:2]) - Vector(corners[2][:2]) - Vector(corners[3][:2])
            inward.normalize()
            tops = []
            for i, c in enumerate(corners):
                p = Vector(c[:2]) + inward * (0.3 if i < 2 else -0.3)
                h = height(slab_side if i < 2 else road_side, p.x, p.y, c[2] + (0.3 if i < 2 else 5.0))
                tops.append(h if h is not None else c[2])
            print("[MSC Duo] alturas da extensao:", [round(t, 3) for t in tops])
            editable = bpy.data.objects.get("Extensao")
            if editable is not None and editable.get("versao") != PLATE_VERSION:
                bpy.data.objects.remove(editable, do_unlink=True)
                editable = None
            if editable is None or editable.name not in editing().objects:
                plate(editing(), corners, min(tops[0], tops[1]) - SINK, max(tops[2], tops[3]) + ROAD_LIFT)
            filled = bpy.data.objects.get("Aterro")
            if filled is not None and filled.get("versao") != FILL_VERSION:
                bpy.data.objects.remove(filled, do_unlink=True)
                filled = None
            if filled is None:
                fill(editing(), trees, bpy.data.objects.get("Extensao"))
        print("[MSC Duo] relevo:", {k: len(v) for k, v in groups.items()})
    else:
        print("[MSC Duo] relevo.obj ainda não existe:", RELEVO)


def shade():
    scene = bpy.context.scene
    if scene.get("mscduo_textured"):
        return
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type == "VIEW_3D":
                area.spaces.active.shading.type = "MATERIAL"
                scene["mscduo_textured"] = True


def frame():
    scene = bpy.context.scene
    if scene.get("mscduo_framed"):
        return
    ext = bpy.data.objects.get("Extensao")
    if ext is None:
        return
    center = sum((v.co for v in ext.data.vertices), Vector()) / len(ext.data.vertices)
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type != "VIEW_3D":
                continue
            view = area.spaces.active.region_3d
            view.view_location = center
            view.view_distance = 35.0
            view.view_rotation = Euler((math.radians(55), 0, math.radians(20))).to_quaternion()
            view.view_perspective = "PERSP"
            area.spaces.active.clip_end = 1000.0
            scene["mscduo_framed"] = True


try:
    main()
    hook_save()
    frame()
    shade()
    with open(LOG, "a", encoding="utf-8") as log:
        log.write("ok\n")
except Exception:
    import traceback
    with open(LOG, "a", encoding="utf-8") as log:
        log.write(traceback.format_exc())
    raise
