using System.Collections.Generic;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class VehicleCloner
    {
        internal const string Suffix = "_P2";
        const float NearHome = 40f;
        const float TrailerReach = 15f;
        const float SlotStart = 5f;
        const float SlotSpacing = 5f;
        const float GroundProbe = 3f;

        internal class Spec
        {
            internal readonly string Root;
            internal readonly string Current;
            internal readonly string TowedBy;

            internal Spec(string root, string current, string towedBy = null)
            {
                Root = root;
                Current = current;
                TowedBy = towedBy;
            }
        }

        internal static readonly Spec[] Specs =
        {
            new Spec("JONNEZ ES(Clone)", "Jonnez"),
            new Spec("KEKMET(350-400psi)", "Kekmet"),
            new Spec("FLATBED", null, "KEKMET(350-400psi)"),
            new Spec("HAYOSIKO(1500kg, 250)", "Hayosiko"),
            new Spec("GIFU(750/450psi)", "Gifu"),
            new Spec("FERNDALE(1630kg)", "Ferndale"),
            new Spec("RCO_RUSCKO12(270)", "Ruscko"),
        };

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
            var slot = 0;

            foreach (var spec in Specs)
            {
                var original = SatsumaCloner.FindRoot(spec.Root);
                if (original == null)
                {
                    ModConsole.Warning($"[MSC Duo] veículo '{spec.Root}' não encontrado; sem cópia para o jogador 2.");
                    continue;
                }
                originals[spec.Root] = original;
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
                else if ((original.transform.position - house1.transform.position).magnitude < NearHome)
                    clone = SatsumaCloner.CloneRoot(original, spec.Root + Suffix, move, turn, map);
                else
                {
                    clone = SatsumaCloner.CloneRoot(original, spec.Root + Suffix, Matrix4x4.identity, Quaternion.identity, map);
                    PlaceInSlot(clone, satsuma2 != null ? satsuma2.transform : house2.transform, slot++);
                }
                clones[spec.Root] = clone;
            }

            int fsms = 0, remapped = 0, saves = 0, failed = 0, confined = 0;
            foreach (var pair in clones)
            {
                var clone = pair.Value;
                Snap(clone);
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

            ModConsole.Print($"<color=orange>[MSC Duo]</color> veículos do jogador 2: {clones.Count} cópias, {fsms} FSMs ({failed} com erro), {remapped} referências redirecionadas, {saves} saves em {SatsumaCloner.SaveFile}, {confined} avisos restritos a cada cópia.");
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

        static void PlaceInSlot(GameObject clone, Transform anchor, int slot)
        {
            var right = anchor.right;
            right.y = 0f;
            right.Normalize();
            var position = anchor.position + right * (SlotStart + SlotSpacing * slot);
            clone.transform.position = position;
            clone.transform.rotation = Quaternion.Euler(0f, anchor.eulerAngles.y, 0f);
        }

        internal static void Snap(GameObject clone)
        {
            var bottom = float.MaxValue;
            var found = false;
            var footprint = new Bounds();
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
            clone.transform.position += Vector3.up * (ground - bottom + 0.05f);
        }
    }
}
