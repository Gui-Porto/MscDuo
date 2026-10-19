using System;
using System.Collections.Generic;
using MSCLoader;
using UnityEngine;
using Object = UnityEngine.Object;

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
        static GateInput input;
        static AudioClip click;

        internal static void Build(Transform warehouse, Material baseMaterial, GateInput gateInput, GameObject mainLights, Dictionary<string, float> saved)
        {
            template = baseMaterial;
            root = warehouse;
            input = gateInput;
            click = GameClip("light_switch");
            var go = new GameObject(Prefix + "s");
            go.transform.parent = warehouse;
            go.transform.position = Warehouse.Local(0f, 0f, 0f);
            go.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            frame = go.transform;

            MainSwitches(mainLights, saved);
            Tiles();
            RoomLight("luz_banheiro", (Bath0 + Bath1) / 2f, 20.05f);
            RoomLight("luz_deposito", (Store0 + Store1) / 2f, 23.05f);
            Shelves();

            var placed = 0;
            var shower = Fixture(new[] { "YARD/Building/BATHROOM/Shower", "YARD/Building/BATHROOM/LOD_bathroom" }, "Chuveiro", new Vector2(Bath0, 15.1f), new Vector2(1f, 0f));
            if (shower != null)
            {
                placed++;
                foreach (var copy in shower)
                    foreach (var t in copy.GetComponentsInChildren<Transform>(true))
                        if (t.name.StartsWith("toilet_lamp")) t.gameObject.SetActive(false);
            }
            if (Fixture(new[] { "YARD/Building/LIVINGROOM/LOD_livingroom/wcbowl", "YARD/Building/LIVINGROOM/LOD_livingroom/SAVEGAME" }, "Vaso", new Vector2(20.3f, Back), new Vector2(0f, -1f)) != null) placed++;
            var sink = Fixture(new[] { "YARD/Building/LIVINGROOM/LOD_livingroom/ToiletSink" }, "Pia", new Vector2(Bath1, 13.5f), new Vector2(-1f, 0f));
            if (sink != null)
            {
                placed++;
                SinkWorks(sink[0]);
            }
            Mirror(Bath1, 13.5f, -1f);
            ModConsole.Print($"<color=orange>[MSC Duo]</color> cômodos: banheiro com {placed} de 3 peças da casa, som de interruptor {(click != null ? "ok" : "não encontrado")}.");
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

        static AudioClip GameClip(string name)
        {
            foreach (var clip in Resources.FindObjectsOfTypeAll<AudioClip>())
                if (clip != null && clip.name == name) return clip;
            return null;
        }

        internal static void Switch(string key, Vector2[] walls, Vector2[] normals, float height, Action<int> apply, int initial, string offLabel, string onLabel)
        {
            var rockers = new List<Transform>();
            var sounds = new List<AudioSource>();
            for (int i = 0; i < walls.Length; i++)
            {
                var wall = walls[i];
                var normal = normals[i];
                var holder = new GameObject("Interruptor_" + key);
                holder.transform.parent = frame;
                holder.transform.localPosition = L(wall.x, wall.y, height) + L(normal.x, normal.y, 0f) * 0.004f;
                holder.transform.localRotation = Quaternion.LookRotation(L(normal.x, normal.y, 0f), Vector3.up);
                var plate = new GateShape();
                plate.Box(new Vector3(-0.042f, -0.062f, 0f), new Vector3(0.042f, 0.062f, 0.01f));
                var plateGo = new GameObject("Espelho");
                plateGo.transform.parent = holder.transform;
                plateGo.transform.localPosition = Vector3.zero;
                plateGo.transform.localRotation = Quaternion.identity;
                plateGo.AddComponent<MeshFilter>().sharedMesh = plate.ToMesh("MscDuo_espelho_interruptor");
                plateGo.AddComponent<MeshRenderer>().sharedMaterial = Paint("espelho_interruptor", new Color(0.93f, 0.92f, 0.88f));
                var rocker = new GameObject("Tecla");
                rocker.transform.parent = holder.transform;
                rocker.transform.localPosition = new Vector3(0f, 0f, 0.012f);
                rocker.transform.localRotation = Quaternion.identity;
                var keyShape = new GateShape();
                keyShape.Box(new Vector3(-0.022f, -0.032f, -0.006f), new Vector3(0.022f, 0.032f, 0.006f));
                var keyGo = new GameObject("Mesh");
                keyGo.transform.parent = rocker.transform;
                keyGo.transform.localPosition = Vector3.zero;
                keyGo.transform.localRotation = Quaternion.identity;
                keyGo.AddComponent<MeshFilter>().sharedMesh = keyShape.ToMesh("MscDuo_tecla");
                keyGo.AddComponent<MeshRenderer>().sharedMaterial = Paint("tecla", new Color(0.97f, 0.97f, 0.95f));
                var box = plateGo.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0f, 0.015f);
                box.size = new Vector3(0.1f, 0.14f, 0.04f);
                var sound = holder.AddComponent<AudioSource>();
                sound.playOnAwake = false;
                sound.spatialBlend = 1f;
                sound.minDistance = 0.6f;
                sound.maxDistance = 8f;
                sound.dopplerLevel = 0f;
                rockers.Add(rocker.transform);
                sounds.Add(sound);
                input.Buttons.Add(new GateButton { Collider = box, Cap = holder.transform, Click = () => WarehouseToggles.Set(key, WarehouseToggles.Get(key) == 0 ? 1 : 0), Text = () => WarehouseToggles.Get(key) == 0 ? offLabel : onLabel });
            }
            var first = true;
            WarehouseToggles.Register(key, initial, v =>
            {
                foreach (var r in rockers) r.localRotation = Quaternion.Euler(v != 0 ? -12f : 12f, 0f, 0f);
                if (!first && click != null)
                {
                    AudioSource nearest = null;
                    var best = float.MaxValue;
                    var cam = Camera.main;
                    foreach (var sound in sounds)
                    {
                        var d = cam != null ? (sound.transform.position - cam.transform.position).sqrMagnitude : 0f;
                        if (d < best) { best = d; nearest = sound; }
                    }
                    if (nearest != null) nearest.PlayOneShot(click, 0.5f);
                }
                first = false;
                apply(v);
            });
        }

        static void MainSwitches(GameObject lights, Dictionary<string, float> saved)
        {
            if (lights == null) return;
            float value;
            var initial = saved.TryGetValue("luz_galpao", out value) && value > 0.5f ? 1 : 0;
            var walls = new[] { new Vector2(WarehouseConfig.Get("interruptor_portao1_x"), 0.2f), new Vector2(WarehouseConfig.Get("interruptor_portao2_x"), 0.2f), new Vector2(0.2f, WarehouseConfig.Get("interruptor_y")) };
            var normals = new[] { new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1f, 0f) };
            Switch("luz_galpao", walls, normals, WarehouseConfig.Get("interruptor_altura"), v => lights.SetActive(v != 0), initial, "Acender luzes", "Apagar luzes");
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

        static void RoomLight(string key, float centerX, float switchX)
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
            Switch(key, new[] { new Vector2(switchX, Front + 0.008f) }, new[] { new Vector2(0f, 1f) }, 1.25f, v =>
            {
                light.enabled = v != 0;
                var materials = fixture.GetComponent<Renderer>().sharedMaterials;
                materials[1] = v != 0 ? lit : dark;
                fixture.GetComponent<Renderer>().sharedMaterials = materials;
            }, 0, "Acender luz", "Apagar luz");
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

        static void Mirror(float wallX, float y, float side)
        {
            var shape = new GateShape();
            var x0 = wallX + side * 0.008f;
            var x1 = x0 + side * 0.01f;
            var x2 = x1 + side * 0.003f;
            shape.Use(0).Box(L(x0, y - 0.27f, 1.2f), L(x1, y + 0.27f, 1.85f));
            shape.Use(1).Box(L(x1, y - 0.25f, 1.22f), L(x2, y + 0.25f, 1.83f));
            Part("Espelho", shape, Paint("moldura", new Color(0.2f, 0.2f, 0.22f)), Paint("espelho", new Color(0.62f, 0.7f, 0.74f)));
        }

        static void Shelves()
        {
            var wood = Paint("prateleira", new Color(0.62f, 0.48f, 0.3f));
            var metal = Paint("mao_francesa", new Color(0.32f, 0.33f, 0.35f));
            var levels = new[] { 0.45f, 0.95f, 1.45f, 1.95f };
            Run("Prateleira_Fundo", Store0 + 0.05f, Back - 0.4f, Store1 - 0.05f, Back, levels, wood, metal, false);
            Run("Prateleira_Esquerda", Store0, 12.7f, Store0 + 0.35f, Back - 0.42f, levels, wood, metal, true);
            Run("Prateleira_Direita", Store1 - 0.35f, 12.7f, Store1, Back - 0.42f, levels, wood, metal, true);
        }

        static void Run(string name, float x0, float y0, float x1, float y1, float[] levels, Material wood, Material metal, bool alongY)
        {
            var shape = new GateShape();
            shape.Use(0);
            foreach (var z in levels) shape.Box(L(x0, y0, z), L(x1, y1, z + 0.025f));
            shape.Use(1);
            var length = alongY ? y1 - y0 : x1 - x0;
            var posts = Mathf.Max(2, Mathf.CeilToInt(length / 0.9f) + 1);
            for (int i = 0; i < posts; i++)
            {
                var t = i / (float)(posts - 1);
                if (alongY)
                {
                    var y = Mathf.Lerp(y0 + 0.03f, y1 - 0.03f, t);
                    var wx = x0 < 22f ? x0 : x1 - 0.025f;
                    shape.Box(L(wx, y - 0.015f, 0.2f), L(wx + 0.025f, y + 0.015f, levels[levels.Length - 1] + 0.1f));
                    foreach (var z in levels) shape.Box(L(x0, y - 0.01f, z - 0.05f), L(x1, y + 0.01f, z));
                }
                else
                {
                    var x = Mathf.Lerp(x0 + 0.03f, x1 - 0.03f, t);
                    shape.Box(L(x - 0.015f, y1 - 0.025f, 0.2f), L(x + 0.015f, y1, levels[levels.Length - 1] + 0.1f));
                    foreach (var z in levels) shape.Box(L(x - 0.01f, y0, z - 0.05f), L(x + 0.01f, y1, z));
                }
            }
            var part = Part(name, shape, wood, metal);
            foreach (var z in levels)
            {
                var box = part.AddComponent<BoxCollider>();
                box.center = L((x0 + x1) / 2f, (y0 + y1) / 2f, z + 0.0125f);
                box.size = L(x1 - x0, y1 - y0, 0.025f);
            }
        }

        static List<GameObject> Fixture(string[] paths, string name, Vector2 wallPoint, Vector2 front)
        {
            var sources = new List<GameObject>();
            foreach (var path in paths)
            {
                var go = DynoCloner.Find(path);
                if (go != null) sources.Add(go);
            }
            if (sources.Count == 0) return null;
            var anchor = sources[0].transform;
            var houseFront = HouseFront(anchor, sources);
            if (!houseFront.HasValue) return null;
            var floor = Floor(anchor, sources);
            var hf = new Vector3(houseFront.Value.x, 0f, houseFront.Value.z);
            var wanted = frame.TransformDirection(L(front.x, front.y, 0f));
            wanted.y = 0f;
            var turn = Quaternion.FromToRotation(hf.normalized, wanted.normalized);
            var target = frame.TransformPoint(L(wallPoint.x, wallPoint.y, 0f)) + wanted.normalized * houseFront.Value.w;
            return Place(sources, anchor, floor, turn, target, name);
        }

        static List<GameObject> Place(List<GameObject> sources, Transform anchor, float floor, Quaternion turn, Vector3 target, string name)
        {
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
            return copies;
        }

        static Bounds? RendererBounds(Transform t)
        {
            Bounds? result = null;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || r is ParticleRenderer) continue;
                if (result.HasValue) { var b = result.Value; b.Encapsulate(r.bounds); result = b; }
                else result = r.bounds;
            }
            return result;
        }

        static void SinkWorks(GameObject sink)
        {
            var bowl = sink.transform.Find("toilet_sink");
            var tap = sink.transform.Find("toilet_tap");
            var bowlBounds = bowl != null ? RendererBounds(bowl) : null;
            if (bowlBounds.HasValue)
            {
                var b = bowlBounds.Value;
                var box = sink.AddComponent<BoxCollider>();
                box.center = sink.transform.InverseTransformPoint(b.center);
                var size = sink.transform.InverseTransformVector(b.size);
                box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            }
            var tapBounds = tap != null ? RendererBounds(tap) : null;
            var kitchen = DynoCloner.Find("YARD/Building/KITCHEN/KitchenWaterTap");
            if (!tapBounds.HasValue || kitchen == null) return;
            var sources = new List<GameObject> { kitchen };
            var kitchenFront = HouseFront(kitchen.transform, sources);
            if (!kitchenFront.HasValue) return;
            var wanted = frame.TransformDirection(L(-1f, 0f, 0f));
            var turn = Quaternion.FromToRotation(new Vector3(kitchenFront.Value.x, 0f, kitchenFront.Value.z), wanted);
            var baseMesh = kitchen.transform.Find("kitchen_tap_base");
            var kitchenBase = baseMesh != null ? RendererBounds(baseMesh) : null;
            if (!kitchenBase.HasValue) return;
            var t = tapBounds.Value;
            var anchorOffset = new Vector3(kitchenBase.Value.center.x, kitchenBase.Value.min.y, kitchenBase.Value.center.z) - kitchen.transform.position;
            var targetBase = new Vector3(t.center.x, t.min.y, t.center.z);
            var position = targetBase - turn * anchorOffset;
            var copy = (GameObject)Object.Instantiate(kitchen, position, turn * kitchen.transform.rotation);
            copy.name = kitchen.name;
            copy.transform.parent = sink.transform;
            var map = new Dictionary<Object, Object>();
            SatsumaCloner.MapHierarchy(kitchen.transform, copy.transform, map);
            HouseCloner.FixStaticBatching(kitchen, copy);
            copy.SetActive(true);
            foreach (var fsm in copy.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                FsmTools.EnsureLoaded(fsm);
                DynoCloner.StripHooks(fsm);
                SatsumaCloner.Remap(fsm, map);
            }
            foreach (var r in tap.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }

        static Vector4? HouseFront(Transform anchor, List<GameObject> group)
        {
            var origin = anchor.position + Vector3.up * 0.6f;
            var best = Vector3.zero;
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
