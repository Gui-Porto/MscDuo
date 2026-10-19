using System.Collections.Generic;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class WarehouseRooms
    {
        internal const string Prefix = "Galpao_Comodo";
        const float Bath0 = 18.12f, Bath1 = 21.0f, Store0 = 21.12f, Store1 = 23.8f, Front = 12.12f, Back = 15.8f;
        const float TileHeight = 2.1f;
        const float Ceiling = 2.94f;

        static Transform frame;
        static Material template;
        static Transform root;

        internal static void Build(Transform warehouse, Material baseMaterial, GateInput input)
        {
            template = baseMaterial;
            root = warehouse;
            var go = new GameObject(Prefix + "s");
            go.transform.parent = warehouse;
            go.transform.position = Warehouse.Local(0f, 0f, 0f);
            go.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            frame = go.transform;

            Tiles();
            RoomLight("luz_banheiro", (Bath0 + Bath1) / 2f, 20.05f, input);
            RoomLight("luz_deposito", (Store0 + Store1) / 2f, 23.05f, input);

            var placed = 0;
            placed += Fixture(new[] { "YARD/Building/BATHROOM/Shower", "YARD/Building/BATHROOM/LOD_bathroom" }, "Chuveiro", new Vector2(20.25f, Back), new Vector2(0f, -1f));
            placed += Fixture(new[] { "YARD/Building/LIVINGROOM/LOD_livingroom/wcbowl", "YARD/Building/LIVINGROOM/LOD_livingroom/SAVEGAME" }, "Vaso", new Vector2(Bath1, 14.9f), new Vector2(-1f, 0f));
            placed += Fixture(new[] { "YARD/Building/LIVINGROOM/LOD_livingroom/ToiletSink" }, "Pia", new Vector2(Bath0, 13.7f), new Vector2(1f, 0f));
            Mirror(Bath0, 13.7f);
            ModConsole.Print($"<color=orange>[MSC Duo]</color> cômodos: banheiro com {placed} de 3 peças da casa, luzes no banheiro e no depósito.");
        }

        static Vector3 L(float x, float y, float z) => new Vector3(x, z, y);

        static Material Paint(string name, Color color) => new Material(template) { name = "MscDuo_comodo_" + name, color = color, mainTexture = null };

        static GameObject Part(string name, GateShape shape, params Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.parent = frame;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = shape.ToMesh("MscDuo_" + name);
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        static void Tiles()
        {
            var wall = new GateShape();
            wall.Use(0);
            Strip(wall, Bath0, Back - 0.008f, Bath1, Back - 0.008f, Vector3.back);
            Strip(wall, Bath0 + 0.008f, Front, Bath0 + 0.008f, Back, Vector3.right);
            Strip(wall, Bath1 - 0.008f, Back, Bath1 - 0.008f, Front, Vector3.left);
            Strip(wall, Bath1, Front + 0.008f, 19.8f, Front + 0.008f, Vector3.forward);
            Strip(wall, 18.9f, Front + 0.008f, Bath0, Front + 0.008f, Vector3.forward);
            Part("Azulejo", wall, Warehouse.Textured("azulejo", template));
            var floor = new GateShape();
            for (var x = Bath0; x < Bath1 - 0.01f; x += 1f)
                for (var y = Front; y < Back - 0.01f; y += 1f)
                {
                    var x1 = Mathf.Min(x + 1f, Bath1);
                    var y1 = Mathf.Min(y + 1f, Back);
                    floor.Quad(L(x, y, 0.006f), L(x1, y, 0.006f), L(x1, y1, 0.006f), L(x, y1, 0.006f), Vector3.up);
                }
            Part("Piso_Banheiro", floor, Warehouse.Textured("piso_banheiro", template));
        }

        static void Strip(GateShape shape, float x0, float y0, float x1, float y1, Vector3 normal)
        {
            var a = new Vector2(x0, y0);
            var b = new Vector2(x1, y1);
            var length = (b - a).magnitude;
            for (var s = 0f; s < length - 0.01f; s += 1f)
            {
                var p = Vector2.Lerp(a, b, s / length);
                var q = Vector2.Lerp(a, b, Mathf.Min(s + 1f, length) / length);
                shape.Quad(L(p.x, p.y, 0f), L(q.x, q.y, 0f), L(q.x, q.y, TileHeight), L(p.x, p.y, TileHeight), normal);
            }
        }

        static void RoomLight(string key, float centerX, float switchX, GateInput input)
        {
            var centerY = (Front + Back) / 2f;
            var lamp = new GateShape();
            lamp.Use(0).Box(L(centerX - 0.3f, centerY - 0.08f, Ceiling - 0.05f), L(centerX + 0.3f, centerY + 0.08f, Ceiling));
            lamp.Use(1).Box(L(centerX - 0.28f, centerY - 0.065f, Ceiling - 0.055f), L(centerX + 0.28f, centerY + 0.065f, Ceiling - 0.05f));
            var lit = Unlit(key, new Color(1f, 0.97f, 0.9f));
            var dark = Paint(key + "_apagada", new Color(0.7f, 0.7f, 0.68f));
            var fixture = Part("Luminaria", lamp, Paint("luminaria", new Color(0.9f, 0.9f, 0.88f)), dark);
            var lightGo = new GameObject("Luz");
            lightGo.transform.parent = frame;
            lightGo.transform.localPosition = L(centerX, centerY, Ceiling - 0.3f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 4.5f;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.shadows = LightShadows.None;

            var plate = new GateShape();
            var z = Front + 0.012f;
            plate.Use(0).Box(L(switchX - 0.04f, z, 1.19f), L(switchX + 0.04f, z + 0.01f, 1.31f));
            var plateGo = Part("Interruptor", plate, Paint("espelho_interruptor", new Color(0.93f, 0.92f, 0.88f)));
            var rocker = new GameObject("Tecla");
            rocker.transform.parent = frame;
            rocker.transform.localPosition = L(switchX, z + 0.012f, 1.25f);
            rocker.transform.localRotation = Quaternion.identity;
            var key2 = new GateShape();
            key2.Box(new Vector3(-0.022f, -0.032f, -0.006f), new Vector3(0.022f, 0.032f, 0.006f));
            var keyGo = new GameObject("Mesh");
            keyGo.transform.parent = rocker.transform;
            keyGo.transform.localPosition = Vector3.zero;
            keyGo.transform.localRotation = Quaternion.identity;
            keyGo.AddComponent<MeshFilter>().sharedMesh = key2.ToMesh("MscDuo_tecla");
            keyGo.AddComponent<MeshRenderer>().sharedMaterial = Paint("tecla", new Color(0.97f, 0.97f, 0.95f));
            var box = plateGo.AddComponent<BoxCollider>();
            box.center = L(switchX, z + 0.015f, 1.25f);
            box.size = L(0.1f, 0.04f, 0.14f);
            WarehouseToggles.Register(key, 0, v =>
            {
                light.enabled = v != 0;
                var materials = fixture.GetComponent<Renderer>().sharedMaterials;
                materials[1] = v != 0 ? lit : dark;
                fixture.GetComponent<Renderer>().sharedMaterials = materials;
                rocker.transform.localRotation = Quaternion.Euler(v != 0 ? -12f : 12f, 0f, 0f);
            });
            input.Buttons.Add(new GateButton { Collider = box, Cap = plateGo.transform, Click = () => WarehouseToggles.Set(key, WarehouseToggles.Get(key) == 0 ? 1 : 0), Text = () => WarehouseToggles.Get(key) == 0 ? "Acender luz" : "Apagar luz" });
        }

        static Material Unlit(string name, Color color)
        {
            var shader = Shader.Find("Unlit/Texture");
            if (shader == null) return Paint(name, color);
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            return new Material(shader) { name = "MscDuo_comodo_" + name, mainTexture = texture };
        }

        static void Mirror(float wallX, float y)
        {
            var shape = new GateShape();
            var x = wallX + 0.012f;
            shape.Use(0).Box(L(x, y - 0.27f, 1.2f), L(x + 0.01f, y + 0.27f, 1.85f));
            shape.Use(1).Box(L(x + 0.01f, y - 0.25f, 1.22f), L(x + 0.013f, y + 0.25f, 1.83f));
            var mirror = Part("Espelho", shape, Paint("moldura", new Color(0.2f, 0.2f, 0.22f)), Paint("espelho", new Color(0.62f, 0.7f, 0.74f)));
            var m = mirror.GetComponent<Renderer>().sharedMaterials[1];
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.95f);
        }

        static int Fixture(string[] paths, string name, Vector2 wallPoint, Vector2 front)
        {
            var sources = new List<GameObject>();
            foreach (var path in paths)
            {
                var go = DynoCloner.Find(path);
                if (go != null) sources.Add(go);
            }
            if (sources.Count == 0) return 0;
            var anchor = sources[0].transform;
            var houseFront = HouseFront(anchor, sources);
            if (!houseFront.HasValue) return 0;
            var floor = Floor(anchor, sources);
            var distance = houseFront.Value.w;
            var hf = new Vector3(houseFront.Value.x, 0f, houseFront.Value.z);
            var wanted = frame.TransformDirection(L(front.x, front.y, 0f));
            wanted.y = 0f;
            var turn = Quaternion.FromToRotation(hf.normalized, wanted.normalized);
            var target = frame.TransformPoint(L(wallPoint.x, wallPoint.y, 0f)) + wanted.normalized * distance;
            var holder = new GameObject(Prefix + "_" + name);
            holder.transform.parent = root;
            var map = new Dictionary<Object, Object>();
            var copies = new List<GameObject>();
            foreach (var source in sources)
            {
                var offset = source.transform.position - anchor.position;
                var position = target + turn * new Vector3(offset.x, 0f, offset.z);
                position.y = target.y + (source.transform.position.y - floor);
                var copy = (GameObject)Object.Instantiate(source, position, turn * source.transform.rotation);
                copy.name = source.name;
                copy.transform.parent = holder.transform;
                SatsumaCloner.MapHierarchy(source.transform, copy.transform, map);
                HouseCloner.FixStaticBatching(source, copy);
                copies.Add(copy);
            }
            foreach (var copy in copies)
            {
                copy.SetActive(true);
                foreach (var fsm in copy.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    FsmTools.EnsureLoaded(fsm);
                    DynoCloner.StripHooks(fsm);
                    SatsumaCloner.Remap(fsm, map);
                }
            }
            return 1;
        }

        static Vector4? HouseFront(Transform anchor, List<GameObject> group)
        {
            var origin = anchor.position + Vector3.up * 0.6f;
            Vector3 best = Vector3.zero;
            var bestDistance = float.MaxValue;
            foreach (var dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, (Vector3.forward + Vector3.left).normalized, (Vector3.forward + Vector3.right).normalized, (Vector3.back + Vector3.left).normalized, (Vector3.back + Vector3.right).normalized })
            {
                foreach (var hit in Physics.RaycastAll(origin, dir, 1.6f))
                {
                    if (hit.collider == null || hit.collider.isTrigger) continue;
                    var own = false;
                    foreach (var g in group) if (hit.collider.transform.IsChildOf(g.transform)) own = true;
                    if (own) continue;
                    var flat = new Vector3(hit.normal.x, 0f, hit.normal.z);
                    if (flat.sqrMagnitude < 0.5f) continue;
                    var perpendicular = hit.distance * Mathf.Abs(Vector3.Dot(dir, flat.normalized));
                    if (perpendicular < bestDistance)
                    {
                        bestDistance = perpendicular;
                        best = flat.normalized;
                    }
                }
            }
            if (bestDistance == float.MaxValue) return null;
            best.y = 0f;
            best.Normalize();
            return new Vector4(best.x, 0f, best.z, Mathf.Max(0.02f, bestDistance));
        }

        static float Floor(Transform anchor, List<GameObject> group)
        {
            foreach (var hit in Physics.RaycastAll(anchor.position + Vector3.up * 0.3f, Vector3.down, 3f))
            {
                if (hit.collider == null || hit.collider.isTrigger) continue;
                var own = false;
                foreach (var g in group) if (hit.collider.transform.IsChildOf(g.transform)) own = true;
                if (!own) return hit.point.y;
            }
            return anchor.position.y;
        }
    }
}
