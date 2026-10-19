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
    {"name": "tv", "file": "tv.glb", "length": 1.4, "front": -90, "facing": "-y", "at": (3.9, 15.74), "floor": MEZ_Z + 0.75, "tris": 1000, "tex": 512, "screen_front": 0.035},
    {"name": "fliperama", "file": "fliperama.glb", "height": 1.85, "front": 180, "facing": "-y", "at": (10.15, 15.32), "floor": MEZ_Z, "tris": 8000, "tex": 1024, "screen_object": "ArcadeMachine_Screen_01", "drop": ["ArcadeMachine_Glass"]},
    {"name": "fliperama2", "file": "fliperama2.glb", "height": 1.78, "front": 0, "facing": "-y", "at": (10.95, 15.38), "floor": MEZ_Z, "tris": 6000, "tex": 1024, "screen_model": [(79.0, -28.2, 120.0), (79.0, 19.9, 120.0), (79.0, 19.9, 157.4), (79.0, -28.2, 157.4)]},
    {"name": "fliperama3", "file": "fliperama3.glb", "height": 1.75, "front": -90, "facing": "-y", "at": (11.7, 15.42), "floor": MEZ_Z, "tris": 9000, "tex": 1024, "screen_model": [(-0.123, 2.333, 1.026), (-0.534, 2.333, 1.026), (-0.534, 2.333, 1.345), (-0.123, 2.333, 1.345)]},
    {"name": "sinuca", "file": "sinuca.glb", "length": 2.1, "front": 0, "facing": "+x", "at": (11.3, 12.8), "floor": MEZ_Z, "tris": 8000, "tex": 1024, "thin": True},
    {"name": "dardos", "file": "dardos.glb", "length": 0.45, "pitch": 90, "front": -90, "facing": "-y", "at": (14.1, 15.77), "floor": MEZ_Z + 1.505, "tris": 4000, "tex": 512, "board": True},
    {"name": "estante_livros", "file": "estante_livros.glb", "height": 1.8, "front": 180, "facing": "-y", "at": (2.05, 15.6), "floor": MEZ_Z, "tris": 20000, "tex": 1024, "copies": [(5.75, 15.6)]},
    {"name": "estante_garagem", "file": "estante_garagem.glb", "height": 2.0, "front": 0, "facing": "-y", "at": (16.2, 15.3), "floor": MEZ_Z, "tris": 20000, "tex": 1024, "copies": [(17.4, 15.3), (18.6, 15.3)]},
    {"name": "planta_sala", "file": "plantas.glb", "object": "Pot_4_", "height": 1.3, "front": 0, "facing": "-y", "at": (0.65, 11.0), "floor": MEZ_Z, "tris": 2000, "tex": 512, "alpha": True, "double": True},
    {"name": "planta_escada", "file": "plantas.glb", "object": "Pot_12_", "height": 1.0, "front": 0, "facing": "-y", "at": (23.35, 12.3), "floor": MEZ_Z, "tris": 2000, "tex": 512, "alpha": True, "double": True},
    {"name": "planta_escritorio", "file": "plantas.glb", "object": "Pot_9_", "height": 1.1, "front": 0, "facing": "-y", "at": (20.2, 11.6), "floor": MEZ_Z, "tris": 2000, "tex": 512, "alpha": True, "double": True},
    {"name": "planta_porta1", "file": "plantas.glb", "object": "Pot_1_", "height": 0.8, "front": 0, "facing": "-y", "at": (0.6, 11.9), "floor": 0.0, "tris": 2000, "tex": 512, "alpha": True, "double": True},
    {"name": "planta_porta2", "file": "plantas.glb", "object": "Pot_2_", "height": 0.8, "front": 0, "facing": "-y", "at": (0.6, 14.1), "floor": 0.0, "tris": 2000, "tex": 512, "alpha": True, "double": True},
    {"name": "planta_lounge", "file": "plantas.glb", "object": "Pot_5_", "height": 1.3, "front": 0, "facing": "-y", "at": (13.3, 15.3), "floor": 0.0, "tris": 2000, "tex": 512, "alpha": True, "double": True},
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
    if item.get("drop"):
        for o in [o for o in meshes if any(o.name.startswith(d) for d in item["drop"])]:
            meshes.remove(o)
            new.remove(o)
            bpy.data.objects.remove(o, do_unlink=True)
    if item.get("object"):
        keep = [o for o in meshes if o.name.startswith(item["object"])]
        drop = [o for o in meshes if o not in keep]
        new = [o for o in new if o not in drop]
        for o in drop:
            bpy.data.objects.remove(o, do_unlink=True)
        meshes = keep
    for o in meshes:
        o.data = o.data.copy()
        o.data.transform(o.matrix_world)
        if item.get("screen_object") and o.name.startswith(item["screen_object"]):
            item["_screen_points"] = [v.co.copy() for v in o.data.vertices]
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


def screen_quad(obj, item, matrix):
    if item.get("screen_front"):
        xs = [v.co.x for v in obj.data.vertices]
        ys = [v.co.y for v in obj.data.vertices]
        zs = [v.co.z for v in obj.data.vertices]
        m = item["screen_front"]
        x = max(xs) + 0.004
        y0, y1 = min(ys) + m, max(ys) - m
        z0, z1 = min(zs) + m, max(zs) - m
        return [Vector((x, y1, z0)), Vector((x, y0, z0)), Vector((x, y0, z1)), Vector((x, y1, z1))]
    if item.get("screen_model"):
        return [matrix @ Vector(p) for p in item["screen_model"]]
    points = item.get("_screen_points")
    if not points:
        return None
    pts = [matrix @ p for p in points]
    c = sum(pts, Vector()) / len(pts)
    n = Vector((1.0, 0.0, 0.0))
    import mathutils
    cov = mathutils.Matrix(((0.0,) * 3,) * 3)
    for p in pts:
        d = p - c
        for i in range(3):
            for j in range(3):
                cov[i][j] += d[i] * d[j]
    best, normal = None, n
    for axis in (Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))):
        v = axis
        for _ in range(60):
            v = (cov.inverted_safe() @ v).normalized()
        normal = v
    if normal.x < 0:
        normal = -normal
    up = Vector((0, 0, 1))
    u = up.cross(normal).normalized()
    w = normal.cross(u).normalized()
    us = [(p - c).dot(u) for p in pts]
    ws = [(p - c).dot(w) for p in pts]
    inset = item.get("screen_inset", 0.0)
    u0, u1 = min(us) + inset, max(us) - inset
    w0, w1 = min(ws) + inset, max(ws) - inset
    o = c + normal * 0.004
    return [o + u * u0 + w * w0, o + u * u1 + w * w0, o + u * u1 + w * w1, o + u * u0 + w * w1]


def snap_screen(obj, quad):
    from mathutils.bvhtree import BVHTree
    me = obj.data
    bvh = BVHTree.FromPolygons([v.co.copy() for v in me.vertices], [list(p.vertices) for p in me.polygons])
    bl, br, tr, tl = quad
    normal = (br - bl).cross(tl - bl).normalized()
    if normal.x < 0:
        normal = -normal
    hits = []
    for u, v in ((0.2, 0.2), (0.8, 0.2), (0.8, 0.8), (0.2, 0.8), (0.5, 0.5)):
        p = bl + (br - bl) * u + (tl - bl) * v
        loc, nrm, idx, dist = bvh.ray_cast(p + normal * 0.4, -normal, 1.0)
        if loc is not None:
            hits.append(loc)
    if len(hits) < 3:
        return quad
    c = sum(hits, Vector()) / len(hits)
    a, b = hits[1] - hits[0], hits[3 if len(hits) > 3 else 2] - hits[0]
    plane = a.cross(b).normalized()
    if plane.dot(normal) < 0:
        plane = -plane
    if plane.dot(normal) < 0.8:
        plane = normal
    lift = max(0.0, max((h - c).dot(plane) for h in hits)) + 0.004
    out = []
    for q in quad:
        t = (c - q).dot(plane) / max(normal.dot(plane), 1e-3)
        out.append(q + normal * t + plane * lift)
    return out


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
    return matrix


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


def double_sided(obj):
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    faces = list(bm.faces)
    copy = bmesh.ops.duplicate(bm, geom=faces)
    flipped = [f for f in copy["geom"] if isinstance(f, bmesh.types.BMFace)]
    bmesh.ops.reverse_faces(bm, faces=flipped)
    bm.to_mesh(obj.data)
    bm.free()


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


HASHES = {}


def bake_textures(obj, item):
    import hashlib
    names = []
    for index, slot in enumerate(obj.material_slots):
        name = "%s_%d%s" % (item["name"], index, "_alfa" if item.get("alpha") else "")
        image, color = base_image(slot.material)
        if item.get("color"):
            image, color = None, item["color"]
        path = os.path.join(TEXTURES, name + (".png" if item.get("alpha") else ".jpg"))
        if image is not None:
            copy = image.copy()
            size = item["tex"]
            if max(copy.size) > size:
                copy.scale(size, size)
            copy.filepath_raw = path
            copy.file_format = "PNG" if item.get("alpha") else "JPEG"
            copy.save()
            bpy.data.images.remove(copy)
        else:
            img = bpy.data.images.new(name, 4, 4)
            img.pixels = list(color) * 16
            img.filepath_raw = path
            img.file_format = "JPEG"
            img.save()
            bpy.data.images.remove(img)
        with open(path, "rb") as f:
            digest = hashlib.md5(f.read()).hexdigest()
        if digest in HASHES:
            os.remove(path)
            name = HASHES[digest]
        else:
            HASHES[digest] = name
        if slot.material:
            slot.material.name = name
        names.append(name)
    return names


def write_obj(objects, names, copies, screens):
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
        if obj.name in screens:
            out.append("screen %s %s" % (obj.name, " ".join("%.4f %.4f %.4f" % (p.x, p.y, p.z) for p in screens[obj.name])))
        for d in copies.get(obj.name, []):
            out.append("copy %s %.4f %.4f %.4f" % (obj.name, d.x, d.y, d.z))
    with open(OBJ, "w", encoding="utf-8") as f:
        f.write("\n".join(out) + "\n")


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    os.makedirs(TEXTURES, exist_ok=True)
    for f in os.listdir(TEXTURES):
        os.remove(os.path.join(TEXTURES, f))
    HASHES.clear()
    objects, names, copies, screens = [], {}, {}, {}
    for item in ITEMS:
        obj = import_item(item)
        if item.get("board"):
            print("%s: %d peças soltas removidas" % (item["name"], keep_main(obj)))
        if item.get("thin"):
            print("%s: %d peças finas removidas" % (item["name"], strip_thin(obj)))
        matrix = place(obj, item)
        quad = screen_quad(obj, item, matrix)
        if quad and not item.get("screen_front"):
            quad = snap_screen(obj, quad)
        if quad and quad[1].y < quad[0].y:
            quad = [quad[1], quad[0], quad[3], quad[2]]
        if quad:
            screens[obj.name] = quad
        count = decimate(obj, item["tris"])
        if item.get("double"):
            double_sided(obj)
        names[obj.name] = bake_textures(obj, item)
        xs = [v.co.x for v in obj.data.vertices]
        ys = [v.co.y for v in obj.data.vertices]
        zs = [v.co.z for v in obj.data.vertices]
        print("%s: %d triângulos, %.2f x %.2f x %.2f m, %d materiais" % (obj.name, count, max(ys) - min(ys), max(xs) - min(xs), max(zs) - min(zs), len(names[obj.name])))
        objects.append(obj)
        base = local_to_blender(item["at"][0], item["at"][1], item["floor"])
        copies[obj.name] = [local_to_blender(x, y, item["floor"]) - base for x, y in item.get("copies", [])]
    write_obj(objects, names, copies, screens)
    return objects


if __name__ == "__main__":
    build()
    if "--salvar" in sys.argv:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "moveis.blend"))
