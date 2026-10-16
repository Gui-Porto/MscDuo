using System.Collections;
using System.Collections.Generic;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class VehicleCloner
    {
        internal const string Suffix = "_P2";
        const float TrailerReach = 15f;
        const float GroundProbe = 3f;
        internal const string HomeFlag = "MscDuo_casa.txt";
        internal const string CopiesFlag = "MscDuo_copias.txt";
        const float SettleSeconds = 3f;

        internal class Spec
        {
            internal readonly string Root;
            internal readonly string Current;
            internal readonly bool Home;
            internal readonly string TowedBy;

            internal Spec(string root, string current, bool home, string towedBy = null)
            {
                Root = root;
                Current = current;
                Home = home;
                TowedBy = towedBy;
            }
        }

        internal static readonly Spec[] Specs =
        {
            new Spec("JONNEZ ES(Clone)", "Jonnez", true),
            new Spec("HAYOSIKO(1500kg, 250)", "Hayosiko", true),
            new Spec("KEKMET(350-400psi)", "Kekmet", false),
            new Spec("FLATBED", null, false, "KEKMET(350-400psi)"),
            new Spec("GIFU(750/450psi)", "Gifu", false),
            new Spec("FERNDALE(1630kg)", "Ferndale", false),
            new Spec("RCO_RUSCKO12(270)", "Ruscko", false),
        };

        const string HomeVan = "HAYOSIKO(1500kg, 250)";

        internal static bool MovedHomeVan { get; private set; }
        internal static bool MovedCopies { get; private set; }

        static string HomeFlagPath => System.IO.Path.Combine(Application.persistentDataPath, HomeFlag);
        static string CopiesFlagPath => System.IO.Path.Combine(Application.persistentDataPath, CopiesFlag);

        internal static void MarkHomeSaved()
        {
            var van = SatsumaCloner.FindRoot(HomeVan);
            if (van != null && (MovedHomeVan || System.IO.File.Exists(HomeFlagPath)))
            {
                var p = van.transform.position;
                Mark(true, HomeFlagPath, string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} {1} {2} {3}", p.x, p.y, p.z, van.transform.eulerAngles.y));
            }
            Mark(MovedCopies, CopiesFlagPath, "cópias levadas para o lado dos originais");
        }

        static void Mark(bool done, string path, string text)
        {
            if (!done) return;
            try { System.IO.File.WriteAllText(path, text); }
            catch (System.Exception e) { ModConsole.Warning($"[MSC Duo] não foi possível gravar {path}: {e.Message}"); }
        }

        static bool RestoreHomeVan(GameObject van)
        {
            try
            {
                var parts = System.IO.File.ReadAllText(HomeFlagPath).Trim().Split(' ');
                if (parts.Length < 4) return false;
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                var position = new Vector3(float.Parse(parts[0], inv), float.Parse(parts[1], inv), float.Parse(parts[2], inv));
                if ((van.transform.position - position).magnitude < 3f) return true;
                van.transform.position = position;
                van.transform.rotation = Quaternion.Euler(0f, float.Parse(parts[3], inv), 0f);
                var body = van.GetComponent<Rigidbody>();
                if (body != null && !body.isKinematic) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                ModConsole.Print("<color=orange>[MSC Duo]</color> van 1 devolvida para onde você salvou.");
                return true;
            }
            catch (System.Exception e)
            {
                ModConsole.Warning($"[MSC Duo] {HomeFlag} inválido: {e.Message}");
                return false;
            }
        }

        class Runner : MonoBehaviour { }

        static IEnumerator MoveCopiesOnce(Dictionary<string, GameObject> originals, GameObject runner)
        {
            yield return new WaitForSeconds(SettleSeconds);
            var moved = new List<string>();
            foreach (var spec in Specs)
            {
                GameObject original, clone;
                if (spec.Home || !originals.TryGetValue(spec.Root, out original) || !clones.TryGetValue(spec.Root, out clone) || original == null || clone == null) continue;
                var player = GameObject.Find("PLAYER");
                if (player != null && player.transform.IsChildOf(clone.transform)) continue;
                if (Placement.Beside(clone, original)) moved.Add(clone.name);
            }
            MovedCopies = true;
            ModConsole.Print($"<color=orange>[MSC Duo]</color> cópias levadas para o lado dos originais: {string.Join(", ", moved.ToArray())}. Salve para guardar.");
            Object.Destroy(runner);
        }

        static readonly Dictionary<string, GameObject> clones = new Dictionary<string, GameObject>();

        internal static GameObject CloneOf(string root)
        {
            GameObject clone;
            return clones.TryGetValue(root, out clone) ? clone : null;
        }

        internal static void Spawn(GameObject house1, GameObject house2, GameObject satsuma2)
        {
            clones.Clear();
            var move = house2.transform.localToWorldMatrix * house1.transform.worldToLocalMatrix;
            var turn = house2.transform.rotation * Quaternion.Inverse(house1.transform.rotation);
            var map = new Dictionary<Object, Object>();
            var originals = new Dictionary<string, GameObject>();
            var placedBeside = 0;
            var fallback = new List<string>();
            MovedHomeVan = false;

            foreach (var spec in Specs)
            {
                var original = SatsumaCloner.FindRoot(spec.Root);
                if (original == null)
                {
                    ModConsole.Warning($"[MSC Duo] veículo '{spec.Root}' não encontrado; sem cópia para o jogador 2.");
                    continue;
                }
                originals[spec.Root] = original;
                if (spec.Root == HomeVan)
                {
                    UncleVan.Lock(original);
                    if (!System.IO.File.Exists(HomeFlagPath)) MovedHomeVan = SatsumaCloner.Original != null && Placement.Beside(original, SatsumaCloner.Original);
                    else if (!RestoreHomeVan(original)) MovedHomeVan = SatsumaCloner.Original != null && Placement.Beside(original, SatsumaCloner.Original);
                }

                GameObject clone;
                GameObject tow;
                GameObject towClone;
                if (spec.TowedBy != null && originals.TryGetValue(spec.TowedBy, out tow) && clones.TryGetValue(spec.TowedBy, out towClone) &&
                    (original.transform.position - tow.transform.position).magnitude < TrailerReach)
                {
                    var relative = towClone.transform.localToWorldMatrix * tow.transform.worldToLocalMatrix;
                    var relativeTurn = towClone.transform.rotation * Quaternion.Inverse(tow.transform.rotation);
                    clone = SatsumaCloner.CloneRoot(original, spec.Root + Suffix, relative, relativeTurn, map);
                }
                else
                {
                    clone = SatsumaCloner.CloneRoot(original, spec.Root + Suffix, Matrix4x4.identity, Quaternion.identity, map);
                    var anchor = spec.Home ? satsuma2 : original;
                    if (Placement.Beside(clone, anchor, original)) placedBeside++;
                    else
                    {
                        fallback.Add(clone.name);
                        if (spec.Home) clone.transform.position = move.MultiplyPoint3x4(original.transform.position);
                        if (spec.Home) clone.transform.rotation = turn * original.transform.rotation;
                        Snap(clone);
                    }
                }
                clones[spec.Root] = clone;
            }

            int fsms = 0, remapped = 0, saves = 0, failed = 0, confined = 0;
            foreach (var pair in clones)
            {
                var clone = pair.Value;
                foreach (var fsm in clone.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    try
                    {
                        FsmTools.EnsureLoaded(fsm);
                        remapped += SatsumaCloner.Remap(fsm, map);
                        saves += SatsumaCloner.RedirectSaves(fsm);
                        confined += FsmTools.ConfineBroadcasts(fsm, clone);
                        fsms++;
                    }
                    catch (System.Exception e)
                    {
                        failed++;
                        if (failed <= 5) ModConsole.Warning($"[MSC Duo] {clone.name}: '{fsm.gameObject.name}/{fsm.FsmName}' não preparada: {e.Message}");
                    }
                }
                remapped += SatsumaCloner.RemapLists(clone, map);
                remapped += RemapJoints(clone, map);
                saves += SatsumaCloner.RedirectAutoSaves(clone);
            }

            foreach (var spec in Specs)
            {
                GameObject original, clone;
                if (spec.Current == null || !originals.TryGetValue(spec.Root, out original) || !clones.TryGetValue(spec.Root, out clone)) continue;
                CurrentVehicle.Separate(spec.Current, original, clone, new[] { original }, new[] { clone });
            }

            MovedCopies = false;
            if (!System.IO.File.Exists(CopiesFlagPath))
            {
                var runner = new GameObject("MscDuo_CopiesMover");
                runner.AddComponent<Runner>().StartCoroutine(MoveCopiesOnce(originals, runner));
            }
            if (fallback.Count > 0) ModConsole.Warning($"[MSC Duo] sem espaço livre ao lado para: {string.Join(", ", fallback.ToArray())}; colocados no lugar aproximado.");
            if (MovedHomeVan) ModConsole.Print("<color=orange>[MSC Duo]</color> van 1 levada para o lado da casa 1 (fica no save quando você salvar).");
            ModConsole.Print($"<color=orange>[MSC Duo]</color> veículos do jogador 2: {clones.Count} cópias ({placedBeside} ao lado do lugar certo), {fsms} FSMs ({failed} com erro), {remapped} referências redirecionadas, {saves} saves em {SatsumaCloner.SaveFile}, {confined} avisos restritos a cada cópia.");
        }

        static int RemapJoints(GameObject clone, Dictionary<Object, Object> map)
        {
            var count = 0;
            foreach (var joint in clone.GetComponentsInChildren<Joint>(true))
            {
                var other = joint.connectedBody;
                if (other == null || other.transform.IsChildOf(clone.transform)) continue;
                Object mapped;
                if (map.TryGetValue(other, out mapped)) joint.connectedBody = (Rigidbody)mapped;
                else Object.DestroyImmediate(joint);
                count++;
            }
            return count;
        }

        internal static float BottomOffset(GameObject vehicle)
        {
            var bottom = float.MaxValue;
            foreach (var wheel in vehicle.GetComponentsInChildren<Wheel>(true))
            {
                var center = wheel.model != null ? wheel.model.transform.position : wheel.transform.position;
                bottom = Mathf.Min(bottom, center.y - wheel.radius);
            }
            if (bottom == float.MaxValue)
                foreach (var renderer in vehicle.GetComponentsInChildren<Renderer>())
                    if (renderer.enabled && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
            return bottom == float.MaxValue ? 0f : bottom - vehicle.transform.position.y;
        }

        internal static void Snap(GameObject clone)
        {
            var bottom = float.MaxValue;
            var found = false;
            var footprint = new Bounds();
            foreach (var wheel in clone.GetComponentsInChildren<Wheel>(true))
            {
                var center = wheel.model != null ? wheel.model.transform.position : wheel.transform.position;
                var low = center.y - wheel.radius;
                var point = new Bounds(new Vector3(center.x, low, center.z), Vector3.zero);
                if (!found) footprint = point; else footprint.Encapsulate(point);
                if (low < bottom) bottom = low;
                found = true;
            }
            if (!found)
                foreach (var renderer in clone.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                    var b = renderer.bounds;
                    if (!found) footprint = b; else footprint.Encapsulate(b);
                    if (b.min.y < bottom) bottom = b.min.y;
                    found = true;
                }
            if (!found) return;
            var ground = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector3(
                    Mathf.Lerp(footprint.min.x, footprint.max.x, (i & 1) == 0 ? 0.25f : 0.75f),
                    bottom,
                    Mathf.Lerp(footprint.min.z, footprint.max.z, (i & 2) == 0 ? 0.25f : 0.75f));
                float y;
                if (Ground.TryHeight(corner, GroundProbe, out y, clone) && y > ground) ground = y;
            }
            if (ground == float.MinValue) return;
            clone.transform.position += Vector3.up * (ground - bottom + 0.02f);
        }
    }
}
