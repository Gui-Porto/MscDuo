import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.join(HERE, "moveis")
TEXTURES = os.path.join(HERE, "moveis_tex_jogo")
OBJ = os.path.join(HERE, "moveis_jogo.obj")
ORIGIN = (-30.0, -0.497, 13.0)
MEZ_Z = 3.2

ITEMS = [
    {"name": "bancada", "file": "bancada.glb", "scale": 0.01, "front": -90, "facing": "-y", "at": (2.6, 15.4), "floor": 0.0, "tris": 12000, "tex": 1024},
    {"name": "armario_ferramentas", "file": "armario_ferramentas.glb", "height": 1.05, "front": 0, "facing": "-y", "at": (5.25, 15.5), "floor": 0.0, "tris": 10000, "tex": 1024},
    {"name": "sofa_grande", "file": "sofa_grande.glb", "height": 0.8, "front": 180, "facing": "-y", "at": (11.0, 14.3), "floor": 0.0, "tris": 60000, "tex": 1024},
    {"name": "geladeira", "file": "geladeira.glb", "height": 1.6, "front": 0, "facing": "-y", "at": (0.75, 15.35), "floor": MEZ_Z, "tris": 5000, "tex": 512},
    {"name": "sofa", "file": "sofa.glb", "height": 0.85, "front": -90, "facing": "+y", "at": (3.9, 12.3), "floor": MEZ_Z, "tris": 10000, "tex": 1024},
    {"name": "mesinha", "file": "mesinha.glb", "length": 1.0, "front": 0, "facing": "-y", "at": (3.9, 13.75), "floor": MEZ_Z, "tris": 2000, "tex": 512},
    {"name": "rack", "file": "rack.glb", "scale": 0.01, "front": 0, "facing": "-y", "at": (3.9, 15.5), "floor": MEZ_Z, "tris": 2000, "tex": 64, "color": (0.16, 0.11, 0.08, 1.0)},
    {"name": "tv", "file": "tv.glb", "length": 1.4, "front": -90, "facing": "-y", "at": (3.9, 15.74), "floor": MEZ_Z + 0.75, "tris": 1000, "tex": 512},
    {"name": "fliperama", "file": "fliperama.glb", "height": 1.85, "front": 180, "facing": "-y", "at": (8.6, 15.32), "floor": MEZ_Z, "tris": 8000, "tex": 1024},
    {"name": "fliperama2", "file": "fliperama2.glb", "height": 1.78, "front": 0, "facing": "-y", "at": (9.4, 15.38), "floor": MEZ_Z, "tris": 6000, "tex": 1024},
    {"name": "fliperama3", "file": "fliperama3.glb", "height": 1.75, "front": -90, "facing": "-y", "at": (10.15, 15.42), "floor": MEZ_Z, "tris": 9000, "tex": 1024},
    {"name": "sinuca", "file": "sinuca.glb", "length": 2.1, "front": 0, "facing": "+x", "at": (11.6, 12.8), "floor": MEZ_Z, "tris": 8000, "tex": 1024, "thin": True},
    {"name": "dardos", "file": "dardos.glb", "length": 0.45, "pitch": 90, "front": -90, "facing": "-y", "at": (13.4, 15.77), "floor": MEZ_Z + 1.505, "tris": 4000, "tex": 512, "board": True},
]

FACING = {"+x": 90.0, "-x": -90.0, "+y": 180.0, "-y": 0.0}


def local_to_blender(x, y, z):
    return Vector((ORIGIN[0] - y, ORIGIN[2] + x, ORIGIN[1] + z))


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def import_item(item):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(SOURCE, item["file"]))
    new = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in new if o.type == "MESH"]
    for o in meshes:
        o.data = o.data.copy()
        o.data.transform(o.matrix_world)
        o.parent = None
        o.matrix_world = Matrix.Identity(4)
    for o in new:
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = item["name"]
    return obj


def place(obj, item):
    me = obj.data
    if item.get("pitch"):
        me.transform(Matrix.Rotation(math.radians(item["pitch"]), 4, "X"))
    xs = [v.co.x for v in me.vertices]
    ys = [v.co.y for v in me.vertices]
    zs = [v.co.z for v in me.vertices]
    height = max(zs) - min(zs)
    if "length" in item:
        scale = item["length"] / max(max(xs) - min(xs), max(ys) - min(ys))
    else:
        scale = item.get("scale") or item["height"] / height
    center = Vector(((max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2, min(zs)))
    turn = math.radians(FACING[item["facing"]] - item["front"])
    target = local_to_blender(item["at"][0], item["at"][1], item["floor"])
    matrix = Matrix.Translation(target) @ Matrix.Rotation(turn, 4, "Z") @ Matrix.Scale(scale, 4) @ Matrix.Translation(-center)
    me.transform(matrix)
    me.update()


def keep_main(obj):
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    seen, parts = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        part, stack = [], [v]
        while stack:
            cur = stack.pop()
            if cur.index in seen:
                continue
            seen.add(cur.index)
            part.append(cur)
            for e in cur.link_edges:
                stack.append(e.other_vert(cur))
        parts.append(part)
    def box(part):
        lo = Vector((min(p.co.x for p in part), min(p.co.y for p in part), min(p.co.z for p in part)))
        hi = Vector((max(p.co.x for p in part), max(p.co.y for p in part), max(p.co.z for p in part)))
        return lo, hi
    lo, hi = box(max(parts, key=lambda q: (box(q)[1] - box(q)[0]).length))
    center, radius = (lo + hi) / 2, (hi - lo).length / 2
    away = [q for q in parts if ((box(q)[0] + box(q)[1]) / 2 - center).length > radius * 1.0]
    for q in away:
        bmesh.ops.delete(bm, geom=q, context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    return len(away)


def strip_thin(obj):
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    seen, parts = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        part, stack = [], [v]
        while stack:
            cur = stack.pop()
            if cur.index in seen:
                continue
            seen.add(cur.index)
            part.append(cur)
            for e in cur.link_edges:
                stack.append(e.other_vert(cur))
        parts.append(part)
    body = max(parts, key=len)
    top = max(p.co.z for p in body)
    floating = [part for part in parts if min(p.co.z for p in part) > top + 0.02 * (top - min(p.co.z for p in body))]
    for part in floating:
        bmesh.ops.delete(bm, geom=part, context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    return len(floating)


def decimate(obj, target):
    count = triangles(obj)
    if count <= target:
        return count
    mod = obj.modifiers.new("reduzir", "DECIMATE")
    mod.ratio = target / count
    mod.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return triangles(obj)


def base_image(mat):
    if not mat or not mat.use_nodes:
        return None, (0.6, 0.6, 0.6, 1.0)
    for node in mat.node_tree.nodes:
        if node.type != "BSDF_PRINCIPLED":
            continue
        socket = node.inputs["Base Color"]
        color = tuple(socket.default_value)
        stack = [l.from_node for l in socket.links]
        while stack:
            n = stack.pop()
            if n.type == "TEX_IMAGE" and n.image:
                return n.image, color
            for i in n.inputs:
                stack.extend(l.from_node for l in i.links)
        return None, color
    return None, (0.6, 0.6, 0.6, 1.0)


def bake_textures(obj, item):
    names = []
    for index, slot in enumerate(obj.material_slots):
        name = "%s_%d" % (item["name"], index)
        image, color = base_image(slot.material)
        if item.get("color"):
            image, color = None, item["color"]
        path = os.path.join(TEXTURES, name + ".png")
        if image is not None:
            copy = image.copy()
            size = item["tex"]
            if max(copy.size) > size:
                copy.scale(size, size)
            copy.filepath_raw = path
            copy.file_format = "PNG"
            copy.save()
            bpy.data.images.remove(copy)
        else:
            img = bpy.data.images.new(name, 4, 4)
            img.pixels = list(color) * 16
            img.filepath_raw = path
            img.file_format = "PNG"
            img.save()
            bpy.data.images.remove(img)
        if slot.material:
            slot.material.name = name
        names.append(name)
    return names


def write_obj(objects, names):
    out = ["# MSC Duo: móveis do galpão em coordenadas do mundo (eixos do Blender)"]
    vbase = tbase = 1
    for obj in objects:
        me = obj.data
        uv = me.uv_layers.active
        out.append("o " + obj.name)
        for v in me.vertices:
            out.append("v %.4f %.4f %.4f" % (v.co.x, v.co.y, v.co.z))
        for loop in me.loops:
            u, w = uv.data[loop.index].uv if uv else (0.0, 0.0)
            out.append("vt %.4f %.4f" % (u, w))
        groups = {}
        for poly in me.polygons:
            groups.setdefault(poly.material_index, []).append(poly)
        for index, polys in sorted(groups.items()):
            out.append("usemtl " + names[obj.name][min(index, len(names[obj.name]) - 1)])
            for poly in polys:
                ids = [(me.loops[li].vertex_index + vbase, li + tbase) for li in poly.loop_indices]
                for k in range(1, len(ids) - 1):
                    a, b, c = ids[0], ids[k], ids[k + 1]
                    out.append("f %d/%d %d/%d %d/%d" % (a[0], a[1], b[0], b[1], c[0], c[1]))
        vbase += len(me.vertices)
        tbase += len(me.loops)
    with open(OBJ, "w", encoding="utf-8") as f:
        f.write("\n".join(out) + "\n")


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    os.makedirs(TEXTURES, exist_ok=True)
    for f in os.listdir(TEXTURES):
        os.remove(os.path.join(TEXTURES, f))
    objects, names = [], {}
    for item in ITEMS:
        obj = import_item(item)
        if item.get("board"):
            print("%s: %d peças soltas removidas" % (item["name"], keep_main(obj)))
        if item.get("thin"):
            print("%s: %d peças finas removidas" % (item["name"], strip_thin(obj)))
        place(obj, item)
        count = decimate(obj, item["tris"])
        names[obj.name] = bake_textures(obj, item)
        xs = [v.co.x for v in obj.data.vertices]
        ys = [v.co.y for v in obj.data.vertices]
        zs = [v.co.z for v in obj.data.vertices]
        print("%s: %d triângulos, %.2f x %.2f x %.2f m, %d materiais" % (obj.name, count, max(ys) - min(ys), max(xs) - min(xs), max(zs) - min(zs), len(names[obj.name])))
        objects.append(obj)
    write_obj(objects, names)
    return objects


if __name__ == "__main__":
    build()
    if "--salvar" in sys.argv:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "moveis.blend"))
