using System.Collections.Generic;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class GarageRamp
    {
        const float Slope = 0.15f;
        const float MinStep = 0.08f;
        const float MaxLength = 10f;
        const float SearchDistance = 12f;
        const float SearchStep = 0.2f;
        const float DefaultWidth = 3.5f;
        const float SideMargin = 0.4f;
        const float Skirt = 0.8f;

        static GameObject ramp;

        internal static void Apply(GameObject house)
        {
            if (ramp != null) Object.DestroyImmediate(ramp);
            ramp = null;

            var door = Find(house.transform, "GarageDoors");
            if (door == null)
            {
                ModConsole.Warning("[MSC Duo] casa 2: portão da garagem não encontrado; rampa não criada.");
                return;
            }

            var left = door.Find("ContactPivotLeft");
            var right = door.Find("ContactPivotRight");
            var hinges = left != null && right != null ? right.position - left.position : door.right;
            hinges.y = 0f;
            var dir = Vector3.Cross(Vector3.up, hinges).normalized;
            if (Covered(house, door.position + dir * 1.5f) && !Covered(house, door.position - dir * 1.5f)) dir = -dir;
            var across = Vector3.Cross(Vector3.up, dir);

            float slabY = 0f, edge = -1f;
            Collider slab = null;
            for (var s = 0f; s <= SearchDistance; s += SearchStep)
            {
                RaycastHit hit;
                if (!HouseHit(house, door.position + dir * s, door.position.y + 2f, out hit) || hit.point.y < door.position.y - 1.5f)
                {
                    if (edge >= 0f) break;
                    continue;
                }
                edge = s;
                slabY = hit.point.y;
                slab = hit.collider;
            }
            if (edge < 0f)
            {
                ModConsole.Warning("[MSC Duo] casa 2: laje da garagem não encontrada; rampa não criada.");
                return;
            }

            var start = door.position + dir * (edge + SearchStep * 0.5f);
            start.y = slabY;
            var length = 1f;
            float endGround = slabY;
            for (int i = 0; i < 4; i++)
            {
                if (!Ground.TryHeight(start + dir * length, 3f, out endGround, house)) endGround = slabY;
                var drop = slabY - endGround;
                if (drop < MinStep)
                {
                    if (i == 0)
                    {
                        ModConsole.Print("<color=orange>[MSC Duo]</color> casa 2: laje da garagem já está no nível do chão; rampa não precisa.");
                        return;
                    }
                    break;
                }
                length = Mathf.Clamp(drop / Slope, 1f, MaxLength);
            }

            var width = Width(door, across) + SideMargin * 2f;
            var endDrop = endGround - slabY - 0.03f;
            var bottom = Mathf.Min(endDrop, 0f) - Skirt;

            ramp = new GameObject("GarageRamp_P2");
            ramp.transform.parent = house.transform.parent;
            ramp.transform.position = start;
            ramp.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            ramp.layer = slab != null ? slab.gameObject.layer : house.layer;

            var mesh = BuildMesh(width, length, endDrop, bottom);
            ramp.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = ramp.AddComponent<MeshRenderer>();
            var material = Concrete(house);
            if (material != null) renderer.sharedMaterial = material;
            ramp.AddComponent<MeshCollider>().sharedMesh = mesh;

            ModConsole.Print($"<color=orange>[MSC Duo]</color> casa 2: rampa da garagem com {length:0.0} m de comprimento, {width:0.0} m de largura e {slabY - endGround:0.00} m de desnível, em {HouseCloner.Format(start)}.");
        }

        static Mesh BuildMesh(float width, float length, float endDrop, float bottom)
        {
            var w = width * 0.5f;
            var a = new Vector3(-w, 0f, 0f);
            var b = new Vector3(w, 0f, 0f);
            var c = new Vector3(w, endDrop, length);
            var d = new Vector3(-w, endDrop, length);
            var a0 = new Vector3(-w, bottom, 0f);
            var b0 = new Vector3(w, bottom, 0f);
            var c0 = new Vector3(w, bottom, length);
            var d0 = new Vector3(-w, bottom, length);

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            Quad(vertices, triangles, a, d, c, b);
            Quad(vertices, triangles, b, c, c0, b0);
            Quad(vertices, triangles, d, a, a0, d0);
            Quad(vertices, triangles, c, d, d0, c0);
            Quad(vertices, triangles, a, b, b0, a0);

            var uv = new Vector2[vertices.Count];
            for (int i = 0; i < uv.Length; i++)
            {
                var v = vertices[i];
                var side = Mathf.Abs(Mathf.Abs(v.x) - w) < 0.001f && i >= 4 && i < 12;
                uv[i] = side ? new Vector2(v.z * 0.5f, v.y * 0.5f) : new Vector2(v.x * 0.5f, v.z * 0.5f);
            }

            var mesh = new Mesh { name = "GarageRamp_P2" };
            mesh.vertices = vertices.ToArray();
            mesh.uv = uv;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void Quad(List<Vector3> vertices, List<int> triangles, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            var i = vertices.Count;
            vertices.Add(p0);
            vertices.Add(p1);
            vertices.Add(p2);
            vertices.Add(p3);
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
            triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
        }

        static bool HouseHit(GameObject house, Vector3 point, float top, out RaycastHit best)
        {
            best = new RaycastHit();
            var found = false;
            foreach (var hit in Physics.RaycastAll(new Vector3(point.x, top, point.z), Vector3.down, 6f))
            {
                if (hit.collider.isTrigger || !hit.collider.transform.IsChildOf(house.transform)) continue;
                if (found && hit.point.y <= best.point.y) continue;
                best = hit;
                found = true;
            }
            return found;
        }

        static float Width(Transform door, Vector3 across)
        {
            var found = false;
            float min = 0f, max = 0f;
            foreach (var r in door.GetComponentsInChildren<Renderer>(true))
            {
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    var x = Vector3.Dot(corner - door.position, across);
                    if (!found) { min = max = x; found = true; }
                    min = Mathf.Min(min, x);
                    max = Mathf.Max(max, x);
                }
            }
            var width = max - min;
            return found && width > 1.5f && width < 8f ? width : DefaultWidth;
        }

        static bool Covered(GameObject house, Vector3 point)
        {
            foreach (var hit in Physics.RaycastAll(point + Vector3.down * 0.5f, Vector3.up, 8f))
                if (!hit.collider.isTrigger && hit.collider.transform.IsChildOf(house.transform)) return true;
            return false;
        }

        static Vector3 Center(GameObject house)
        {
            var found = false;
            var bounds = new Bounds();
            foreach (var r in house.GetComponentsInChildren<Renderer>())
            {
                if (!found) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return found ? bounds.center : house.transform.position;
        }

        static Material Concrete(GameObject house)
        {
            Material fallback = null;
            foreach (var r in house.GetComponentsInChildren<Renderer>(true))
            {
                var name = r.name.EndsWith("_mesh") && r.transform.parent != null ? r.transform.parent.name : r.name;
                if (name.StartsWith("house_base_concrete")) return r.sharedMaterial;
                if (fallback == null && name.StartsWith("house_base")) fallback = r.sharedMaterial;
            }
            return fallback;
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
