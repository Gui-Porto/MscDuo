import math
import os
import sys

import bmesh
import bpy

from mathutils import Matrix, Vector

FOLDER = os.path.dirname(os.path.abspath(__file__))
PREVIEW = os.path.join(FOLDER, "referencia_jogadores.png")
SPACING = 1.2

HIP = 0.92
SHOULDER = (0.235, 1.44)
NECK = 1.53
HEAD_DROP = 0.035

SKINS = {
    1: {
        "cores": {
            "Pele": (0.82, 0.6, 0.47),
            "Cabelo": (0.2, 0.13, 0.08),
            "Camiseta": (0.9, 0.9, 0.87),
            "Calca": (0.2, 0.29, 0.47),
            "Cinto": (0.28, 0.17, 0.09),
            "Tenis": (0.16, 0.16, 0.17),
            "Olho": (0.05, 0.05, 0.05),
        },
        "manga": True,
        "bigode": True,
        "bone": False,
        "listra": False,
    },
    2: {
        "cores": {
            "Pele": (0.9, 0.7, 0.58),
            "Cabelo": (0.86, 0.72, 0.4),
            "Camiseta": (0.91, 0.4, 0.04),
            "Calca": (0.13, 0.16, 0.28),
            "Listra": (0.92, 0.92, 0.92),
            "Tenis": (0.92, 0.92, 0.9),
            "Bone": (0.12, 0.25, 0.62),
            "Olho": (0.05, 0.05, 0.05),
        },
        "manga": False,
        "bigode": False,
        "bone": True,
        "listra": True,
    },
}


def material(name, color):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    return mat


def cube(bm, center, size, taper=None, bevel=0.0):
    geom = bmesh.ops.create_cube(bm, size=1.0)["verts"]
    for v in geom:
        x, y, z = v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]
        if taper and v.co.z > 0:
            x *= taper[0]
            y *= taper[1]
        v.co = Vector((x, y, z)) + Vector(center)
    if bevel > 0:
        edges = list({e for v in geom for e in v.link_edges})
        bmesh.ops.bevel(bm, geom=edges, offset=bevel, segments=1, affect="EDGES")


def tube(bm, top, bottom, r_top, r_bottom, segments=8):
    length = (Vector(top) - Vector(bottom)).length
    center = (Vector(top) + Vector(bottom)) / 2
    axis = (Vector(top) - Vector(bottom)).normalized()
    rot = Vector((0, 0, 1)).rotation_difference(axis).to_matrix().to_4x4()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=r_bottom, radius2=r_top,
                          depth=length, matrix=Matrix.Translation(center) @ rot)


def ball(bm, center, radius, keep=None, segments=12, rings=8):
    verts = bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=1.0)["verts"]
    for v in verts:
        v.co = Vector((v.co.x * radius[0], v.co.y * radius[1], v.co.z * radius[2])) + Vector(center)
    if keep:
        bmesh.ops.delete(bm, geom=[v for v in verts if not keep(v.co)], context="VERTS")


class Part:
    def __init__(self, name, pivot, smooth=False):
        self.name = name
        self.pivot = Vector(pivot)
        self.smooth = smooth
        self.pieces = []

    def add(self, mat, build):
        bm = bmesh.new()
        build(bm)
        self.pieces.append((mat, bm))


def build(skin):
    s = SKINS[skin]
    parts = []

    body = Part("Tronco", (0, 0, HIP))
    body.add("Calca", lambda bm: cube(bm, (0, 0, 0.93), (0.34, 0.2, 0.16), bevel=0.02))
    body.add("Camiseta", lambda bm: cube(bm, (0, 0, 1.235), (0.34, 0.2, 0.47), taper=(1.22, 1.1), bevel=0.03))
    body.add("Pele", lambda bm: tube(bm, (0, 0, NECK + 0.02), (0, 0, 1.45), 0.06, 0.068))
    if "Cinto" in s["cores"]:
        body.add("Cinto", lambda bm: cube(bm, (0, 0, 1.0), (0.345, 0.205, 0.035)))
    if not s["manga"]:
        body.add("Pele", lambda bm: cube(bm, (0, 0.105, 1.43), (0.16, 0.02, 0.06)))
    parts.append(body)

    head = Part("Cabeca", (0, 0, NECK), smooth=True)
    head.add("Pele", lambda bm: ball(bm, (0, 0.005, 1.68), (0.1, 0.115, 0.125)))
    head.add("Pele", lambda bm: cube(bm, (0, 0.118, 1.665), (0.035, 0.04, 0.05), taper=(0.8, 0.6)))
    for x in (-0.1, 0.1):
        head.add("Pele", lambda bm, x=x: cube(bm, (x, 0.0, 1.67), (0.025, 0.05, 0.06), bevel=0.008))
    for x in (-0.042, 0.042):
        head.add("Olho", lambda bm, x=x: cube(bm, (x, 0.104, 1.705), (0.022, 0.012, 0.016)))
        head.add("Cabelo", lambda bm, x=x: cube(bm, (x, 0.108, 1.735), (0.045, 0.012, 0.012)))
    if s["bigode"]:
        head.add("Cabelo", lambda bm: cube(bm, (0, 0.118, 1.632), (0.08, 0.02, 0.018), bevel=0.005))
    if s["bone"]:
        head.add("Cabelo", lambda bm: ball(bm, (0, 0.0, 1.69), (0.107, 0.122, 0.12),
                                            keep=lambda c: c.z > 1.63 and c.y < 0.04))
        head.add("Bone", lambda bm: ball(bm, (0, 0.0, 1.725), (0.112, 0.127, 0.1), keep=lambda c: c.z >= 1.724))
        head.add("Bone", lambda bm: cube(bm, (0, 0.165, 1.735), (0.17, 0.1, 0.014), bevel=0.004))
    else:
        head.add("Cabelo", lambda bm: ball(bm, (0, -0.005, 1.7), (0.108, 0.125, 0.125),
                                            keep=lambda c: c.z > 1.745 or (c.y < 0.02 and c.z > 1.6)))
    for _, bm in head.pieces:
        bmesh.ops.translate(bm, vec=(0, 0, -HEAD_DROP), verts=bm.verts)
    parts.append(head)

    for side, label in ((-1, "E"), (1, "D")):
        sx, sz = SHOULDER[0] * side, SHOULDER[1]
        arm = Part("Braco_" + label, (sx, 0, sz))
        if s["manga"]:
            arm.add("Camiseta", lambda bm, sx=sx: tube(bm, (sx, 0, 1.47), (sx + 0.01 * side, 0, 1.29), 0.07, 0.065))
            arm.add("Pele", lambda bm, sx=sx: tube(bm, (sx + 0.01 * side, 0, 1.3), (sx + 0.025 * side, 0, 0.88), 0.05, 0.042))
        else:
            arm.add("Pele", lambda bm, sx=sx: ball(bm, (sx, 0, 1.43), (0.065, 0.065, 0.06), segments=8, rings=6))
            arm.add("Pele", lambda bm, sx=sx: tube(bm, (sx, 0, 1.45), (sx + 0.025 * side, 0, 0.88), 0.055, 0.042))
        arm.add("Pele", lambda bm, sx=sx: cube(bm, (sx + 0.028 * side, 0.005, 0.83), (0.065, 0.095, 0.12), bevel=0.015))
        parts.append(arm)

        hx = 0.095 * side
        leg = Part("Perna_" + label, (hx, 0, HIP))
        leg.add("Calca", lambda bm, hx=hx: tube(bm, (hx, 0, 0.98), (hx, 0, 0.1), 0.088, 0.062))
        if s["listra"]:
            leg.add("Listra", lambda bm, hx=hx: tube(bm, (hx + 0.08 * side, 0, 0.97), (hx + 0.054 * side, 0, 0.12), 0.014, 0.014, segments=4))
        leg.add("Tenis", lambda bm, hx=hx: cube(bm, (hx, 0.045, 0.05), (0.11, 0.27, 0.1), taper=(1, 0.85), bevel=0.025))
        parts.append(leg)
    return parts


def to_objects(skin, parts, offset):
    collection = bpy.data.collections.get("Jogador%d" % skin) or bpy.data.collections.new("Jogador%d" % skin)
    if collection.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(collection)
    colors = SKINS[skin]["cores"]
    for part in parts:
        mesh = bpy.data.meshes.new("J%d_%s" % (skin, part.name))
        bm = bmesh.new()
        names = []
        for mat, piece in part.pieces:
            if mat not in names:
                names.append(mat)
            index = names.index(mat)
            tmp = bpy.data.meshes.new("tmp")
            piece.to_mesh(tmp)
            for poly in tmp.polygons:
                poly.material_index = index
            bm.from_mesh(tmp)
            bpy.data.meshes.remove(tmp)
            piece.free()
        bm.to_mesh(mesh)
        bm.free()
        for name in names:
            mesh.materials.append(material("J%d_%s" % (skin, name), colors[name]))
        for poly in mesh.polygons:
            poly.use_smooth = part.smooth
        obj = bpy.data.objects.new(mesh.name, mesh)
        obj.location = Vector((offset, 0, 0))
        obj["pivo"] = list(part.pivot)
        obj["partes"] = names
        collection.objects.link(obj)


def export(skin):
    path = os.path.join(FOLDER, "jogador%d.obj" % skin)
    colors = SKINS[skin]["cores"]
    lines = ["# MSC Duo jogador %d, gerado por jogadores.py" % skin]
    for name, c in colors.items():
        lines.append("# cor %s %.3f %.3f %.3f" % (name, *c))
    base_v, base_n = 1, 1
    for obj in bpy.data.collections["Jogador%d" % skin].objects:
        mesh = obj.data
        mesh.calc_loop_triangles()
        pivot = obj["pivo"]
        lines.append("o %s" % obj.name.split("_", 1)[1])
        lines.append("# pivo %.4f %.4f %.4f" % tuple(pivot))
        for v in mesh.vertices:
            lines.append("v %.4f %.4f %.4f" % tuple(v.co))
        normals = [tuple(mesh.corner_normals[i].vector) for tri in mesh.loop_triangles for i in tri.loops]
        for n in normals:
            lines.append("vn %.4f %.4f %.4f" % n)
        current = None
        k = 0
        for tri in mesh.loop_triangles:
            name = mesh.materials[tri.material_index].name.split("_", 1)[1]
            if name != current:
                lines.append("usemtl %s" % name)
                current = name
            a, b, c = (base_v + i for i in tri.vertices)
            n = base_n + k
            lines.append("f %d//%d %d//%d %d//%d" % (a, n, b, n + 1, c, n + 2))
            k += 3
        base_v += len(mesh.vertices)
        base_n += len(normals)
    with open(path, "w", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("[MSC Duo] gravado", path)


def preview():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = False
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 900
    scene.view_settings.view_transform = "Standard"
    scene.render.filepath = os.environ.get("MSCDUO_PREVIA", PREVIEW)
    world = bpy.data.worlds.get("World") or bpy.data.worlds.new("World")
    scene.world = world
    cam = bpy.data.objects.get("Camera_Jogadores")
    if cam is None:
        cam = bpy.data.objects.new("Camera_Jogadores", bpy.data.cameras.new("Camera_Jogadores"))
        scene.collection.objects.link(cam)
    view = os.environ.get("MSCDUO_VISTA", "frente")
    target = Vector((0, 0, 1.0))
    direction = {"frente": Vector((0.35, 1, 0.12)), "costas": Vector((-0.35, -1, 0.12)),
                 "lado": Vector((1, 0.1, 0.1))}[view]
    distance = 4.2
    cam.location = target + direction.normalized() * distance
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 50
    scene.camera = cam
    bpy.ops.render.render(write_still=True)
    print("[MSC Duo] prévia", scene.render.filepath)


def main():
    cube = bpy.data.objects.get("Cube")
    if cube:
        bpy.data.objects.remove(cube)
    for skin in SKINS:
        collection = bpy.data.collections.get("Jogador%d" % skin)
        if collection:
            for obj in list(collection.objects):
                bpy.data.objects.remove(obj)
    for skin in SKINS:
        to_objects(skin, build(skin), (skin - 1.5) * SPACING)
        export(skin)
    if bpy.app.background:
        preview()


main()
