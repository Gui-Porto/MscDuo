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
SINK = 0.003
SKIRT = 0.5
CRACK = [(-1.5, -8.6), (-2.6, -8.6), (-4.0, -9.2), (-4.35, -9.9), (-4.3, -11.0)]
HIDDEN = ("YARD/", "JONNEZ")
DUMPS = os.path.dirname(RELEVO)
LAJE_OBJ = os.path.join(DUMPS, "laje.obj")
LAJE_PNG = os.path.join(DUMPS, "laje.png")
TILE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "cimento.png")
APRON_U = (0.03321, -0.03610, 0.24511)
APRON_V = (0.03610, 0.03320, 0.48602)
TILE_U0 = 592 / 1024
TILE_V0 = 1 - 915 / 1024
TILE_W = 225 / 1024
TILE_H = 150 / 1024
LONGEST = 15.0

COLORS = {
    "terreno": (0.33, 0.45, 0.25, 1.0),
    "estrada": (0.42, 0.36, 0.28, 1.0),
    "casa2": (0.75, 0.72, 0.66, 1.0),
    "vegetacao": (0.2, 0.35, 0.15, 1.0),
    "lote": (0.5, 0.5, 0.55, 1.0),
    "outros": (0.6, 0.4, 0.4, 1.0),
    "extensao": (0.85, 0.3, 0.2, 1.0),
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


def outline(corners):
    c = [Vector(p[:2]) for p in corners]
    inward = (c[0] + c[1] - c[2] - c[3]).normalized()
    return [
        c[2] - inward * EXTRA_ROAD,
        c[3] - inward * EXTRA_ROAD,
        c[1] + inward * UNDER_SLAB,
        c[0] + inward * UNDER_SLAB,
    ] + [Vector(p) for p in CRACK]


def plate(col, corners, top):
    ring = outline(corners)
    if area(ring) < 0:
        ring.reverse()
    n = len(ring)
    normals = []
    for i in range(n):
        a, b = ring[i - 1], ring[i]
        d = (b - a).normalized()
        normals.append(Vector((d.y, -d.x)))
    corner = [(normals[i] + normals[(i + 1) % n]).normalized() for i in range(n)]
    bm = bmesh.new()
    upper = [bm.verts.new((p.x, p.y, top)) for p in ring]
    lower = [bm.verts.new((p.x + o.x * SKIRT, p.y + o.y * SKIRT, top - SKIRT)) for p, o in zip(ring, corner)]
    face = bm.faces.new(upper)
    bmesh.ops.triangulate(bm, faces=[face])
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((upper[i], lower[i], lower[j], upper[j]))
    bm.normal_update()
    layer = bm.loops.layers.uv.new("UV")
    for f in bm.faces:
        for loop in f.loops:
            loop[layer].uv = tile_uv(loop.vert.co.x, loop.vert.co.y)
    mesh = bpy.data.meshes.new("Extensao")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("Extensao", mesh)
    obj.data.materials.append(textured("extensao_cimento", TILE) if os.path.exists(TILE) else material("extensao"))
    col.objects.link(obj)
    write_obj(obj, PLATE)
    return obj


def tile_uv(x, y):
    u = APRON_U[0] * x + APRON_U[1] * y + APRON_U[2]
    v = APRON_V[0] * x + APRON_V[1] * y + APRON_V[2]
    return ((u - TILE_U0) / TILE_W, (v - TILE_V0) / TILE_H)


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


def write_obj(obj, path):
    with open(path, "w", encoding="utf-8") as f:
        f.write("# MSC Duo extensao: eixos do Blender, coordenadas da casa 2\n")
        for v in obj.data.vertices:
            f.write("v %.4f %.4f %.4f\n" % tuple(v.co))
        for v in obj.data.vertices:
            f.write("vt %.5f %.5f\n" % tile_uv(v.co.x, v.co.y))
        for p in obj.data.polygons:
            f.write("f " + " ".join("%d/%d" % (i + 1, i + 1) for i in p.vertices) + "\n")


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
        if len(corners) == 4:
            trees = {k: bvh(v) for k, v in objects.items()}
            slab_side = [trees[k] for k in ("casa2",) if k in trees]
            road_side = [trees[k] for k in ("chao_estrada", "estrada", "chao_terreno", "terreno") if k in trees]
            inward = Vector(corners[0][:2]) + Vector(corners[1][:2]) - Vector(corners[2][:2]) - Vector(corners[3][:2])
            inward.normalize()
            tops = []
            for i, c in enumerate(corners):
                p = Vector(c[:2]) + inward * (0.3 if i < 2 else -0.3)
                h = height(slab_side if i < 2 else road_side, p.x, p.y, c[2] + (0.3 if i < 2 else 5.0))
                tops.append(h if h is not None else c[2])
            print("[MSC Duo] alturas da extensao:", [round(t, 3) for t in tops])
            plate(col, corners, min(tops[0], tops[1]) - SINK)
        laje(col)
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
    frame()
    shade()
    with open(LOG, "a", encoding="utf-8") as log:
        log.write("ok\n")
except Exception:
    import traceback
    with open(LOG, "a", encoding="utf-8") as log:
        log.write(traceback.format_exc())
    raise
