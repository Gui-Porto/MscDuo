import os

import bpy

RELEVO = os.path.join(os.path.expanduser("~"), "Documents", "MySummerCar", "Mods", "Config", "Mod Settings", "MscDuo", "dumps", "relevo.obj")
THICKNESS = 0.25

COLORS = {
    "terreno": (0.33, 0.45, 0.25, 1.0),
    "estrada": (0.42, 0.36, 0.28, 1.0),
    "casa2": (0.75, 0.72, 0.66, 1.0),
    "vegetacao": (0.2, 0.35, 0.15, 1.0),
    "lote": (0.5, 0.5, 0.55, 1.0),
    "outros": (0.6, 0.4, 0.4, 1.0),
    "extensao": (0.85, 0.3, 0.2, 1.0),
}


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
    with open(path, encoding="utf-8") as f:
        for line in f:
            if line.startswith("# extensao "):
                corners.append(tuple(float(x) for x in line.split()[2:5]))
            elif line.startswith("v "):
                verts.append(tuple(float(x) for x in line.split()[1:4]))
            elif line.startswith("usemtl "):
                current = groups.setdefault(line.split()[1], [])
            elif line.startswith("f ") and current is not None:
                current.append([int(p.split("/")[0]) - 1 for p in line.split()[1:]])
    return verts, corners, groups


def add_group(col, name, verts, faces):
    remap, local = {}, []
    for face in faces:
        for i in face:
            if i not in remap:
                remap[i] = len(local)
                local.append(verts[i])
    add_mesh(col, name, local, [[remap[i] for i in face] for face in faces], name)


def prism(col, outline, top, bottom):
    n = len(outline)
    verts = [(p[0], p[1], top) for p in outline] + [(p[0], p[1], bottom) for p in outline]
    faces = [list(range(n)), list(range(2 * n - 1, n - 1, -1))]
    faces += [[i, i + n, (i + 1) % n + n, (i + 1) % n] for i in range(n)]
    add_mesh(col, "Extensao", verts, faces, "extensao")


cube = bpy.data.objects.get("Cube")
if cube is not None:
    bpy.data.objects.remove(cube, do_unlink=True)

col = collection()
if os.path.exists(RELEVO):
    verts, corners, groups = read_relevo(RELEVO)
    for name, faces in groups.items():
        add_group(col, name, verts, faces)
    if len(corners) == 4:
        top = max(c[2] for c in corners)
        prism(col, [corners[0], corners[1], corners[3], corners[2]], top, top - THICKNESS)
    print("[MSC Duo] relevo:", {k: len(v) for k, v in groups.items()})
else:
    print("[MSC Duo] relevo.obj ainda não existe:", RELEVO)
