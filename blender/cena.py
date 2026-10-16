import os

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

RELEVO = os.path.join(os.path.expanduser("~"), "Documents", "MySummerCar", "Mods", "Config", "Mod Settings", "MscDuo", "dumps", "relevo.obj")
THICKNESS = 0.25
HIDDEN = ("YARD/", "JONNEZ")
LONGEST = 15.0

COLORS = {
    "terreno": (0.33, 0.45, 0.25, 1.0),
    "estrada": (0.42, 0.36, 0.28, 1.0),
    "casa2": (0.75, 0.72, 0.66, 1.0),
    "vegetacao": (0.2, 0.35, 0.15, 1.0),
    "lote": (0.5, 0.5, 0.55, 1.0),
    "outros": (0.6, 0.4, 0.4, 1.0),
    "extensao": (0.85, 0.3, 0.2, 1.0),
    "chao_estrada": (0.18, 0.18, 0.2, 1.0),
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
                skip = path.startswith(HIDDEN)
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
            prism(col, [corners[0], corners[1], corners[3], corners[2]], [tops[0], tops[1], tops[3], tops[2]])
        print("[MSC Duo] relevo:", {k: len(v) for k, v in groups.items()})
    else:
        print("[MSC Duo] relevo.obj ainda não existe:", RELEVO)


try:
    main()
    with open(LOG, "a", encoding="utf-8") as log:
        log.write("ok\n")
except Exception:
    import traceback
    with open(LOG, "a", encoding="utf-8") as log:
        log.write(traceback.format_exc())
    raise
