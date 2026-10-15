using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MSCLoader;
using MscDuo.Duplication;
using UnityEngine;

namespace MscDuo.Tools
{
    internal class CheckCommand : ConsoleCommand
    {
        const float Radius = 35f;
        const float Cell = 0.25f;
        const float StepLimit = 0.2f;
        const float StackLimit = 0.4f;

        static readonly string[] IgnoredRoots = { "SATSUMA", "CARPARTS", "PLAYER", "ITEMS" };

        public override string Name => "duo_check";
        public override string Help => "duo_check  procura degraus, espetos e colisões estranhas no mapa da casa 2 e compara os colisores das duas Satsumas";

        public override void Run(string[] args)
        {
            var folder = Path.Combine(MscDuo.Instance.SettingsFolder, "dumps");
            Directory.CreateDirectory(folder);
            var map = CheckMap(folder);
            var car = CheckCar(folder);
            ModConsole.Print($"<color=orange>[MSC Duo]</color> verificação: {map}\n{car}\nArquivos em {folder} (map_check.txt, car_check.txt).");
        }

        static string CheckMap(string folder)
        {
            var house = HouseCloner.Clone;
            if (house == null) return "mapa: casa 2 não existe";
            var center = house.transform.position;
            var n = Mathf.CeilToInt(Radius * 2f / Cell);
            var heights = new float[n, n];
            var owners = new Collider[n, n];
            var stacks = new Dictionary<string, Hazard>();

            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    var x = center.x - Radius + i * Cell;
                    var z = center.z - Radius + j * Cell;
                    heights[i, j] = float.MinValue;
                    var hits = new List<RaycastHit>();
                    foreach (var hit in Physics.RaycastAll(new Vector3(x, center.y + 30f, z), Vector3.down, 60f))
                        if (!hit.collider.isTrigger && !Ignored(hit.collider.transform)) hits.Add(hit);
                    hits.Sort((a, b) => b.point.y.CompareTo(a.point.y));
                    if (hits.Count == 0) continue;
                    heights[i, j] = hits[0].point.y;
                    owners[i, j] = hits[0].collider;
                    for (int k = 1; k < hits.Count; k++)
                    {
                        var gap = hits[0].point.y - hits[k].point.y;
                        if (gap < 0.01f || gap > StackLimit || hits[k].collider == hits[0].collider) continue;
                        Add(stacks, $"empilhado: {Label(hits[0].collider)} sobre {Label(hits[k].collider)}", hits[0].point, gap);
                    }
                }

            var steps = new Dictionary<string, Hazard>();
            for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < n - 1; j++)
                {
                    if (owners[i, j] == null) continue;
                    for (int side = 0; side < 2; side++)
                    {
                        var a = side == 0 ? i + 1 : i;
                        var b = side == 0 ? j : j + 1;
                        if (owners[a, b] == null) continue;
                        var dh = Mathf.Abs(heights[a, b] - heights[i, j]);
                        if (dh < StepLimit) continue;
                        var c1 = owners[i, j];
                        var c2 = owners[a, b];
                        if (InHouse(c1, house) && InHouse(c2, house)) continue;
                        var low = heights[i, j] < heights[a, b] ? c1 : c2;
                        var high = low == c1 ? c2 : c1;
                        var point = new Vector3(center.x - Radius + i * Cell, Mathf.Max(heights[i, j], heights[a, b]), center.z - Radius + j * Cell);
                        Add(steps, $"degrau: {Label(low)} -> {Label(high)}", point, dh);
                    }
                }

            var text = new StringBuilder();
            text.AppendLine($"# verificação do mapa em volta da casa 2 {HouseCloner.Format(center)}, raio {Radius} m, grade {Cell} m");
            text.AppendLine("# degraus (diferença de altura entre vizinhos maior que " + StepLimit + " m), sem contar os da própria casa");
            foreach (var h in steps.Values.OrderByDescending(h => h.Count)) text.AppendLine(h.ToString());
            text.AppendLine("# superfícies empilhadas (duas superfícies separadas por menos de " + StackLimit + " m)");
            foreach (var h in stacks.Values.OrderByDescending(h => h.Count)) text.AppendLine(h.ToString());
            File.WriteAllText(Path.Combine(folder, "map_check.txt"), text.ToString());

            var top = steps.Values.OrderByDescending(h => h.Count).Take(5).Select(h => $"  {h.Name} ({h.Count}x, até {h.Max:0.00} m, ex. {HouseCloner.Format(h.Example)})");
            return $"mapa: {steps.Count} tipos de degrau, {stacks.Count} tipos de superfície empilhada.\n" + string.Join("\n", top.ToArray());
        }

        static string CheckCar(string folder)
        {
            var car1 = SatsumaCloner.Original;
            var car2 = SatsumaCloner.Clone;
            var map = SatsumaCloner.Map;
            if (car1 == null || car2 == null || map == null) return "carro: Satsuma 2 não existe";

            var text = new StringBuilder();
            text.AppendLine("# colisores da Satsuma 2 comparados com a Satsuma 1");
            int differences = 0, extra = 0, missing = 0, embedded = 0;
            var reverse = new Dictionary<Object, Object>();
            foreach (var pair in map) if (pair.Value != null) reverse[pair.Value] = pair.Key;

            foreach (var c2 in car2.GetComponentsInChildren<Collider>(true))
            {
                if (Ignored(c2.transform, "PLAYER")) continue;
                Object o1;
                if (!reverse.TryGetValue(c2, out o1) || o1 == null)
                {
                    text.AppendLine($"SÓ NA 2: {Rel(c2.transform, car2.transform)} {Describe(c2, car2.transform)}");
                    extra++;
                    continue;
                }
                var c1 = (Collider)o1;
                var d1 = Describe(c1, car1.transform);
                var d2 = Describe(c2, car2.transform);
                if (d1 != d2)
                {
                    text.AppendLine($"{Rel(c2.transform, car2.transform)}\n   1: {d1}\n   2: {d2}");
                    differences++;
                }
            }
            foreach (var c1 in car1.GetComponentsInChildren<Collider>(true))
            {
                if (map.ContainsKey(c1) || c1.transform.GetComponentInParent<Rigidbody>() == null) continue;
                if (c1.transform.root.name == "PLAYER" || Rel(c1.transform, car1.transform).Contains("PLAYER")) continue;
                text.AppendLine($"SÓ NA 1: {Rel(c1.transform, car1.transform)} {Describe(c1, car1.transform)}");
                missing++;
            }

            text.AppendLine("# juntas da Satsuma 2 com ajustes diferentes da Satsuma 1");
            var joints = 0;
            foreach (var j2 in car2.GetComponentsInChildren<Joint>(true))
            {
                Object o1;
                if (!reverse.TryGetValue(j2.gameObject, out o1) || o1 == null) continue;
                var list = ((GameObject)o1).GetComponents(j2.GetType());
                var same = System.Array.IndexOf(j2.gameObject.GetComponents(j2.GetType()), j2);
                if (same < 0 || same >= list.Length) { text.AppendLine($"JUNTA SÓ NA 2: {Rel(j2.transform, car2.transform)} {j2.GetType().Name}"); joints++; continue; }
                var j1 = (Joint)list[same];
                var issues = new List<string>();
                if ((j1.anchor - j2.anchor).magnitude > 0.01f) issues.Add($"ponto {j1.anchor} / {j2.anchor}");
                if ((j1.connectedAnchor - j2.connectedAnchor).magnitude > 0.01f) issues.Add($"ponto ligado {j1.connectedAnchor} / {j2.connectedAnchor}");
                if ((j1.axis - j2.axis).magnitude > 0.01f) issues.Add($"eixo {j1.axis} / {j2.axis}");
                if ((j1.connectedBody == null) != (j2.connectedBody == null)) issues.Add($"ligada a {(j1.connectedBody != null ? j1.connectedBody.name : "mundo")} / {(j2.connectedBody != null ? j2.connectedBody.name : "mundo")}");
                if (issues.Count == 0) continue;
                text.AppendLine($"JUNTA: {Rel(j2.transform, car2.transform)} {j2.GetType().Name}: {string.Join("; ", issues.ToArray())}");
                joints++;
            }

            text.AppendLine("# scripts da Satsuma 2 (rodas, suspensão, motor...) com valores ou referências diferentes da Satsuma 1");
            var scripts = 0;
            foreach (var b2 in car2.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (b2 == null) continue;
                var type = b2.GetType();
                if (type.Name.StartsWith("PlayMaker") || type.Namespace == "MscDuo.Duplication" || Rel(b2.transform, car2.transform).Contains("PLAYER")) continue;
                Object o1;
                if (!reverse.TryGetValue(b2, out o1) || o1 == null) continue;
                foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                {
                    var v1 = field.GetValue(o1);
                    var v2 = field.GetValue(b2);
                    var issue = Compare(v1, v2, car1.transform, car2.transform, map);
                    if (issue == null) continue;
                    text.AppendLine($"SCRIPT: {Rel(b2.transform, car2.transform)} {type.Name}.{field.Name}: {issue}");
                    scripts++;
                }
            }

            text.AppendLine("# colisores da Satsuma 2 enfiados em algo de fora do carro agora");
            foreach (var c2 in car2.GetComponentsInChildren<Collider>())
            {
                if (!c2.enabled || c2.isTrigger || Rel(c2.transform, car2.transform).Contains("PLAYER")) continue;
                var b = c2.bounds;
                var radius = Mathf.Min(b.extents.x, Mathf.Min(b.extents.y, b.extents.z)) * 0.8f;
                if (radius < 0.02f) continue;
                foreach (var other in Physics.OverlapSphere(b.center, radius))
                {
                    if (other.isTrigger || other.transform.IsChildOf(car2.transform) || Ignored(other.transform, "PLAYER")) continue;
                    if (SatsumaCloner.Parts != null && other.transform.IsChildOf(SatsumaCloner.Parts.transform)) continue;
                    text.AppendLine($"ENFIADO: {Rel(c2.transform, car2.transform)} em {Label(other)} {HouseCloner.Format(b.center)}");
                    embedded++;
                }
            }
            File.WriteAllText(Path.Combine(folder, "car_check.txt"), text.ToString());
            return $"carro: {differences} colisores diferentes da Satsuma 1, {extra} só na 2, {missing} só na 1, {joints} juntas com ajuste diferente, {scripts} valores de script diferentes, {embedded} enfiados no mapa agora.";
        }

        static string Compare(object v1, object v2, Transform car1, Transform car2, Dictionary<Object, Object> map)
        {
            if (v2 is System.Collections.IEnumerable list && !(v2 is string))
            {
                var index = 0;
                foreach (var item in list)
                {
                    var element = item as Object;
                    var et = element is Component ? ((Component)element).transform : element is GameObject ? ((GameObject)element).transform : null;
                    if (et != null && et.IsChildOf(car1)) return $"item {index} APONTA PARA A SATSUMA 1 ({element.name})";
                    index++;
                }
                return null;
            }
            var o2 = v2 as Object;
            if (o2 != null || v2 is Object)
            {
                if (o2 == null) return v1 as Object != null ? "referência sumiu na 2" : null;
                var t2 = o2 is Component ? ((Component)o2).transform : o2 is GameObject ? ((GameObject)o2).transform : null;
                if (t2 != null && t2.IsChildOf(car1)) return $"APONTA PARA A SATSUMA 1 ({o2.name})";
                var o1 = v1 as Object;
                if (o1 != null && map.TryGetValue(o1, out var expected) && expected != o2) return $"referência diferente ({o1.name} -> {o2.name})";
                return null;
            }
            if (v1 is float f1 && v2 is float f2) return Mathf.Abs(f1 - f2) > Mathf.Max(0.01f, Mathf.Abs(f1) * 0.05f) ? $"{f1:0.###} / {f2:0.###}" : null;
            if (v1 is bool || v1 is int || v1 is string || (v1 != null && v1.GetType().IsEnum)) return Equals(v1, v2) ? null : $"{v1} / {v2}";
            if (v1 is Vector3 a && v2 is Vector3 b) return (a - b).magnitude > 0.02f ? $"{a} / {b}" : null;
            return null;
        }

        static string Describe(Collider c, Transform root)
        {
            var local = root.InverseTransformPoint(c.transform.position);
            return $"{c.GetType().Name} ligado={c.enabled}/{c.gameObject.activeInHierarchy} gatilho={c.isTrigger} camada={c.gameObject.layer} material={(c.sharedMaterial != null ? c.sharedMaterial.name : "-")} posição=({local.x:0.00},{local.y:0.00},{local.z:0.00})";
        }

        static bool Ignored(Transform t, string only = null)
        {
            var root = t.root.name;
            if (only != null) return root.StartsWith(only);
            foreach (var ignored in IgnoredRoots)
                if (root.StartsWith(ignored)) return true;
            return false;
        }

        static bool InHouse(Collider c, GameObject house) => c.transform.IsChildOf(house.transform);

        static string Label(Collider c) => $"{FullPath(c.transform)} [{c.GetType().Name}, camada {c.gameObject.layer}]";

        static string FullPath(Transform t) => t.parent == null ? t.name : FullPath(t.parent) + "/" + t.name;

        static string Rel(Transform t, Transform root) => t == root || t.parent == null ? t.name : Rel(t.parent, root) + "/" + t.name;

        static void Add(Dictionary<string, Hazard> hazards, string name, Vector3 point, float size)
        {
            Hazard h;
            if (!hazards.TryGetValue(name, out h)) hazards[name] = h = new Hazard { Name = name, Example = point };
            h.Count++;
            if (size > h.Max) { h.Max = size; h.Example = point; }
        }

        class Hazard
        {
            internal string Name;
            internal int Count;
            internal float Max;
            internal Vector3 Example;

            public override string ToString() => $"{Count}x até {Max:0.00} m, pior em {HouseCloner.Format(Example)}: {Name}";
        }
    }
}
