using System.Collections.Generic;
using HutongGames.PlayMaker;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class GroundDecor
    {
        const float Back = 15.8f;
        static Transform frame;
        static Material template;
        static Transform root;

        internal static void Build(Transform warehouse, Material baseMaterial, GameObject lights)
        {
            template = baseMaterial;
            root = warehouse;
            var go = new GameObject("Galpao_Terreo");
            go.transform.parent = warehouse;
            go.transform.position = Warehouse.Local(0f, 0f, 0f);
            go.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            frame = go.transform;

            TireRack(0.25f, 0.8f, 1.5f, 5.5f);
            Drum(0.6f, 6.4f, new Color(0.12f, 0.25f, 0.55f));
            Drum(0.6f, 7.1f, new Color(0.62f, 0.12f, 0.08f));
            Bin(0.5f, 9.2f);
            Bin(22.65f, 0.7f);
            Rug(9.0f, 11.6f, 13.0f, 15.7f);
            FloorLamp(8.65f, 14.9f, lights);
            Clock(11f, Back, 2.25f);
            Pallets();
            var pieces = 0;
            pieces += Charger();
            pieces += Jack();
            WarehouseCart.Clear();
            pieces += ToolCart(7.0f);
            pieces += ToolCart(7.9f);
            ModConsole.Print($"<color=orange>[MSC Duo]</color> térreo: decoração montada, {pieces} de 4 peças do jogo (carregador, macaco e dois carrinhos de ferramentas).");
        }

        static Vector3 L(float x, float y, float z) => new Vector3(x, z, y);

        static Material Paint(string name, Color color) => new Material(template) { name = "MscDuo_terreo_" + name, color = color, mainTexture = null };

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

        static void Box(GameObject go, float x0, float y0, float z0, float x1, float y1, float z1)
        {
            var b = go.AddComponent<BoxCollider>();
            b.center = L((x0 + x1) / 2f, (y0 + y1) / 2f, (z0 + z1) / 2f);
            b.size = L(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0));
        }

        static void TireRack(float x0, float x1, float y0, float y1)
        {
            var shape = new GateShape();
            shape.Use(0);
            var levels = new[] { 0.1f, 0.85f };
            foreach (var x in new[] { x0, x1 - 0.04f })
                foreach (var y in new[] { y0, (y0 + y1) / 2f, y1 - 0.04f })
                    shape.Box(L(x, y, 0f), L(x + 0.04f, y + 0.04f, 1.6f));
            foreach (var z in levels)
                foreach (var x in new[] { x0 + 0.08f, x1 - 0.12f })
                    shape.Box(L(x, y0, z - 0.04f), L(x + 0.04f, y1, z));
            shape.Use(1);
            foreach (var z in levels)
                for (var y = y0 + 0.2f; y < y1 - 0.15f; y += 0.27f)
                {
                    var c = L((x0 + x1) / 2f, y, z + 0.31f);
                    shape.Cylinder(c - L(0f, 0.1f, 0f), c + L(0f, 0.1f, 0f), 0.31f, 24);
                }
            shape.Use(2);
            foreach (var z in levels)
                for (var y = y0 + 0.2f; y < y1 - 0.15f; y += 0.27f)
                {
                    var c = L((x0 + x1) / 2f, y, z + 0.31f);
                    shape.Cylinder(c - L(0f, 0.102f, 0f), c + L(0f, 0.102f, 0f), 0.18f, 18);
                }
            var go = Part("Rack_Pneus", shape, Paint("rack", new Color(0.85f, 0.45f, 0.08f)), Paint("pneu", new Color(0.07f, 0.07f, 0.075f)), Paint("roda", new Color(0.55f, 0.56f, 0.58f)));
            Box(go, x0, y0, 0f, x1, y1, 1.6f);
        }

        static void Drum(float x, float y, Color color)
        {
            var shape = new GateShape();
            var c = L(x, y, 0f);
            shape.Use(0).Cylinder(c, c + Vector3.up * 0.88f, 0.29f, 28);
            shape.Use(1);
            foreach (var h in new[] { 0.02f, 0.3f, 0.58f, 0.86f }) shape.Cylinder(c + Vector3.up * h, c + Vector3.up * (h + 0.025f), 0.297f, 28, false);
            shape.Cylinder(c + Vector3.up * 0.88f + L(0.14f, 0f, 0f), c + Vector3.up * 0.9f + L(0.14f, 0f, 0f), 0.03f, 10);
            var go = Part("Tambor", shape, Paint("tambor", color), Paint("tambor_aro", new Color(0.3f, 0.3f, 0.32f)));
            var cap = go.AddComponent<CapsuleCollider>();
            cap.center = c + Vector3.up * 0.44f;
            cap.radius = 0.29f;
            cap.height = 0.88f;
            cap.direction = 1;
        }

        static void Bin(float x, float y)
        {
            var shape = new GateShape();
            var c = L(x, y, 0f);
            shape.Use(0).Cylinder(c, c + Vector3.up * 0.62f, 0.22f, 24);
            shape.Use(1).Cylinder(c + Vector3.up * 0.62f, c + Vector3.up * 0.66f, 0.235f, 24);
            var go = Part("Lixeira", shape, Paint("lixeira", new Color(0.18f, 0.35f, 0.2f)), Paint("lixeira_tampa", new Color(0.12f, 0.12f, 0.12f)));
            var cap = go.AddComponent<CapsuleCollider>();
            cap.center = c + Vector3.up * 0.33f;
            cap.radius = 0.23f;
            cap.height = 0.66f;
            cap.direction = 1;
        }

        static void Rug(float x0, float y0, float x1, float y1)
        {
            var shape = new GateShape();
            shape.Quad(L(x0, y0, 0.008f), L(x1, y0, 0.008f), L(x1, y1, 0.008f), L(x0, y1, 0.008f), Vector3.up);
            Part("Tapete_Lounge", shape, Warehouse.Textured("tapete_lounge", template));
        }

        static void FloorLamp(float x, float y, GameObject lights)
        {
            var shape = new GateShape();
            var c = L(x, y, 0f);
            shape.Use(0);
            shape.Cylinder(c, c + Vector3.up * 0.03f, 0.18f, 24);
            shape.Cylinder(c + Vector3.up * 0.03f, c + Vector3.up * 1.45f, 0.015f, 10);
            shape.Use(1);
            shape.Cylinder(c + Vector3.up * 1.4f, c + Vector3.up * 1.7f, 0.2f, 24);
            var go = Part("Luminaria_Pe", shape, Paint("luminaria_haste", new Color(0.12f, 0.12f, 0.12f)), Paint("cupula", new Color(0.92f, 0.85f, 0.68f)));
            var cap = go.AddComponent<CapsuleCollider>();
            cap.center = c + Vector3.up * 0.85f;
            cap.radius = 0.18f;
            cap.height = 1.7f;
            cap.direction = 1;
            if (lights == null) return;
            var lightGo = new GameObject("Luminaria_Pe_Luz");
            lightGo.transform.parent = lights.transform;
            lightGo.transform.position = frame.TransformPoint(c + Vector3.up * 1.5f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 4f;
            light.intensity = 0.9f;
            light.color = new Color(1f, 0.82f, 0.55f);
            light.shadows = LightShadows.None;
        }

        static void Clock(float x, float wallY, float height)
        {
            var face = L(x, wallY - 0.01f, height);
            var shape = new GateShape();
            shape.Use(0).Cylinder(face, face - L(0f, 0.04f, 0f), 0.22f, 36);
            shape.Use(1).Cylinder(face - L(0f, 0.04f, 0f), face - L(0f, 0.042f, 0f), 0.2f, 36);
            shape.Use(2);
            for (int i = 0; i < 12; i++)
            {
                var a = i * 30f * Mathf.Deg2Rad;
                var p = face - L(0f, 0.043f, 0f) + L(Mathf.Sin(a) * 0.17f, 0f, Mathf.Cos(a) * 0.17f);
                var w = i % 3 == 0 ? 0.012f : 0.006f;
                var h = i % 3 == 0 ? 0.035f : 0.02f;
                var right = L(Mathf.Cos(a), 0f, -Mathf.Sin(a));
                var up = L(Mathf.Sin(a), 0f, Mathf.Cos(a));
                shape.Quad(p - right * w - up * h, p + right * w - up * h, p + right * w + up * h, p - right * w + up * h, Vector3.back);
            }
            Part("Relogio", shape, Paint("relogio_aro", new Color(0.15f, 0.15f, 0.16f)), Paint("relogio_fundo", new Color(0.96f, 0.95f, 0.9f)), Paint("relogio_marca", new Color(0.08f, 0.08f, 0.08f)));
            var clock = new GameObject("Relogio_Ponteiros");
            clock.transform.parent = frame;
            clock.transform.localPosition = face - L(0f, 0.046f, 0f);
            clock.transform.localRotation = Quaternion.identity;
            var hands = clock.AddComponent<ClockHands>();
            hands.Hour = Hand(clock.transform, "Hora", 0.1f, 0.012f, 0f);
            hands.Minute = Hand(clock.transform, "Minuto", 0.15f, 0.008f, -0.003f);
        }

        static Transform Hand(Transform parent, string name, float length, float width, float depth)
        {
            var pivot = new GameObject(name);
            pivot.transform.parent = parent;
            pivot.transform.localPosition = new Vector3(0f, 0f, depth);
            pivot.transform.localRotation = Quaternion.identity;
            var shape = new GateShape();
            shape.Box(new Vector3(-width, -0.02f, -0.002f), new Vector3(width, length, 0.002f));
            var go = new GameObject("Mesh");
            go.transform.parent = pivot.transform;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = shape.ToMesh("MscDuo_ponteiro");
            go.AddComponent<MeshRenderer>().sharedMaterial = Paint("ponteiro", new Color(0.08f, 0.08f, 0.08f));
            return pivot.transform;
        }

        class ClockHands : MonoBehaviour
        {
            internal Transform Hour;
            internal Transform Minute;
            PlayMakerFSM sun;
            float next;

            void Update()
            {
                if (Time.time < next) return;
                next = Time.time + 1f;
                if (sun == null)
                {
                    foreach (var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                        if (fsm != null && fsm.gameObject.name == "SUN" && fsm.FsmName == "Color") { sun = fsm; break; }
                    if (sun == null) return;
                }
                var time = sun.FsmVariables.FindFsmInt("Time");
                var minutes = sun.FsmVariables.FindFsmFloat("Minutes");
                if (time == null) return;
                var m = minutes != null ? Mathf.Repeat(minutes.Value, 60f) : 0f;
                var h = Mathf.Repeat(time.Value, 12f) + m / 60f;
                Hour.localRotation = Quaternion.Euler(0f, 0f, h * 30f);
                Minute.localRotation = Quaternion.Euler(0f, 0f, m * 6f);
            }
        }

        static void Lockers(float x0, int count)
        {
            const float w = 0.55f, d = 0.45f, h = 1.8f;
            var shape = new GateShape();
            for (int i = 0; i < count; i++)
            {
                var x = x0 + i * w;
                shape.Use(0).Box(L(x + 0.005f, Back - d, 0.08f), L(x + w - 0.005f, Back, h));
                shape.Use(1).Box(L(x, Back - d, 0f), L(x + w, Back, 0.08f));
                shape.Use(2);
                for (int k = 0; k < 4; k++)
                {
                    var z = 1.5f + k * 0.05f;
                    shape.Box(L(x + 0.15f, Back - d - 0.004f, z), L(x + w - 0.15f, Back - d, z + 0.02f));
                }
                shape.Box(L(x + w - 0.1f, Back - d - 0.03f, 1.0f), L(x + w - 0.08f, Back - d, 1.12f));
                shape.Box(L(x + w - 0.12f, Back - d - 0.012f, 1.05f), L(x + w - 0.06f, Back - d, 1.07f));
            }
            var go = Part("Armarios", shape, Paint("armario_vestiario", new Color(0.35f, 0.45f, 0.58f)), Paint("rodape", new Color(0.12f, 0.12f, 0.13f)), Paint("armario_detalhe", new Color(0.15f, 0.17f, 0.2f)));
            Box(go, x0, Back - d, 0f, x0 + count * w, Back, h);
        }

        static void Bench(float x0, float x1, float y)
        {
            var shape = new GateShape();
            shape.Use(0).Box(L(x0, y - 0.18f, 0.42f), L(x1, y + 0.18f, 0.46f));
            shape.Use(1);
            foreach (var x in new[] { x0 + 0.15f, x1 - 0.2f })
            {
                shape.Box(L(x, y - 0.16f, 0f), L(x + 0.05f, y - 0.12f, 0.42f));
                shape.Box(L(x, y + 0.12f, 0f), L(x + 0.05f, y + 0.16f, 0.42f));
                shape.Box(L(x, y - 0.16f, 0.1f), L(x + 0.05f, y + 0.16f, 0.13f));
            }
            var go = Part("Banco", shape, Paint("banco_madeira", new Color(0.58f, 0.42f, 0.25f)), Paint("banco_metal", new Color(0.2f, 0.2f, 0.22f)));
            Box(go, x0, y - 0.18f, 0f, x1, y + 0.18f, 0.46f);
        }

        static void Board(float x0, float x1)
        {
            var canvas = new PixelCanvas(160, 104);
            canvas.Clear(new Color32(245, 245, 240, 255));
            canvas.Text(4, 4, "SERVICOS", new Color32(30, 60, 160, 255), 2);
            var lines = new[] { "- TROCAR OLEO SATSUMA", "- REGULAR CARBURADOR", "- DINAMOMETRO: 2 CARROS", "- LAVAR VAN", "- COMPRAR CERVEJA!" };
            var colors = new[] { new Color32(30, 30, 30, 255), new Color32(30, 30, 30, 255), new Color32(30, 30, 30, 255), new Color32(30, 30, 30, 255), new Color32(190, 30, 30, 255) };
            for (int i = 0; i < lines.Length; i++) canvas.Text(4, 22 + i * 14, lines[i], colors[i]);
            canvas.Apply();
            var shape = new GateShape();
            shape.Use(0).Box(L(x0, Back - 0.03f, 1.1f), L(x1, Back, 1.95f));
            shape.Use(1);
            shape.Quad(L(x0 + 0.03f, Back - 0.031f, 1.13f), L(x1 - 0.03f, Back - 0.031f, 1.13f), L(x1 - 0.03f, Back - 0.031f, 1.92f), L(x0 + 0.03f, Back - 0.031f, 1.92f), Vector3.back);
            var shader = Shader.Find("Diffuse") ?? Shader.Find("Legacy Shaders/Diffuse");
            var board = shader != null ? new Material(shader) { mainTexture = canvas.Texture } : new Material(template) { mainTexture = canvas.Texture, color = Color.white };
            Part("Quadro_Servicos", shape, Paint("quadro_moldura", new Color(0.6f, 0.62f, 0.65f)), board);
        }

        static void Pallets()
        {
            var shape = new GateShape();
            var x0 = 22.75f;
            var x1 = 23.65f;
            foreach (var y0 in new[] { 7.2f, 8.5f })
            {
                var y1 = y0 + 1.1f;
                shape.Use(0);
                foreach (var y in new[] { y0, (y0 + y1) / 2f - 0.05f, y1 - 0.1f }) shape.Box(L(x0, y, 0f), L(x1, y + 0.1f, 0.1f));
                for (var x = x0; x < x1 - 0.05f; x += 0.15f) shape.Box(L(x, y0, 0.1f), L(x + 0.1f, y1, 0.125f));
            }
            shape.Use(1);
            foreach (var y in new[] { 7.3f, 7.75f, 8.6f })
            {
                shape.Box(L(x0 + 0.05f, y, 0.125f), L(x0 + 0.22f, y + 0.35f, 0.6f));
                shape.Box(L(x0 + 0.27f, y, 0.125f), L(x0 + 0.44f, y + 0.35f, 0.6f));
            }
            shape.Use(2);
            shape.Box(L(x0 + 0.5f, 7.3f, 0.125f), L(x1 - 0.05f, 8.1f, 0.6f));
            shape.Box(L(x0 + 0.5f, 8.6f, 0.125f), L(x1 - 0.05f, 9.5f, 0.75f));
            var go = Part("Pallets", shape, Paint("pallet", new Color(0.66f, 0.52f, 0.33f)), Paint("galao", new Color(0.75f, 0.1f, 0.06f)), Paint("caixa_terreo", new Color(0.58f, 0.43f, 0.26f)));
            Box(go, x0, 7.2f, 0f, x1, 9.6f, 0.75f);
        }

        static int Charger()
        {
            var start = frame.TransformPoint(L(2.3f, 15.3f, 2.0f));
            var surface = (float?)null;
            var floor = frame.position.y;
            foreach (var hit in Physics.RaycastAll(start, Vector3.down, 2.5f))
            {
                if (hit.collider == null || hit.collider.isTrigger) continue;
                var h = hit.point.y - floor;
                if (h < 0.6f || h > 1.15f) continue;
                if (surface == null || hit.point.y > surface.Value) surface = hit.point.y;
            }
            if (surface == null || surface.Value < frame.position.y + 0.3f) return 0;
            var copies = WarehouseRooms.Fixture(new[] { "YARD/Building/Garage/BatteryCharger" }, "Carregador", new Vector2(2.3f, Back - 0.15f), new Vector2(0f, -1f), surface.Value);
            return copies != null ? 1 : 0;
        }

        static int Jack()
        {
            var source = DynoCloner.Find("REPAIRSHOP/LOD/floor jack(itemx)");
            if (source == null) return 0;
            var list = new List<GameObject> { source };
            var floor = WarehouseRooms.Floor(source.transform, list);
            var target = frame.TransformPoint(L(3.95f, 11.3f, 0f));
            var copies = WarehouseRooms.Place(list, source.transform, floor, Quaternion.identity, target, "Macaco");
            if (copies == null || copies.Count == 0) return 0;
            SettleOnFloor(copies);
            return 1;
        }

        static int ToolCart(float x)
        {
            var source = DynoCloner.Find("REPAIRSHOP/LOD/ToolCart");
            if (source == null) return 0;
            var drawer = source.transform.Find("Drawer");
            var front = drawer != null ? -drawer.up : source.transform.forward;
            front.y = 0f;
            if (front.sqrMagnitude < 1e-4f) front = source.transform.forward;
            var wanted = frame.TransformDirection(L(0f, -1f, 0f));
            var turn = Quaternion.FromToRotation(front.normalized, wanted);
            var list = new List<GameObject> { source };
            var floor = WarehouseRooms.Floor(source.transform, list);
            var copies = WarehouseRooms.Place(list, source.transform, floor, turn, frame.TransformPoint(L(x, 15.0f, 0f)), "Carrinho");
            if (copies == null || copies.Count == 0) return 0;
            SettleOnFloor(copies);
            AgainstBackWall(copies, Back - 0.03f);
            WarehouseCart.Setup(copies[0], root.GetComponent<GateInput>() ?? root.gameObject.AddComponent<GateInput>());
            return 1;
        }

        static Bounds? ColliderBounds(List<GameObject> copies)
        {
            Bounds? result = null;
            foreach (var copy in copies)
                foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.enabled || r.isPartOfStaticBatch || r is ParticleRenderer || !r.gameObject.activeInHierarchy) continue;
                    if (result.HasValue) { var b = result.Value; b.Encapsulate(r.bounds); result = b; }
                    else result = r.bounds;
                }
            return result;
        }

        static void SettleOnFloor(List<GameObject> copies)
        {
            var bounds = ColliderBounds(copies);
            if (!bounds.HasValue) return;
            var lift = frame.position.y + 0.005f - bounds.Value.min.y;
            foreach (var copy in copies) copy.transform.position += Vector3.up * lift;
        }

        static void AgainstBackWall(List<GameObject> copies, float wallY)
        {
            var bounds = ColliderBounds(copies);
            if (!bounds.HasValue) return;
            var b = bounds.Value;
            var far = float.MinValue;
            foreach (var corner in new[] { b.min, b.max, new Vector3(b.min.x, b.min.y, b.max.z), new Vector3(b.max.x, b.min.y, b.min.z) })
                far = Mathf.Max(far, Warehouse.ToLocal(corner).y);
            var shift = Warehouse.Local(0f, wallY - far, 0f) - Warehouse.Local(0f, 0f, 0f);
            foreach (var copy in copies) copy.transform.position += shift;
        }
    }
}
