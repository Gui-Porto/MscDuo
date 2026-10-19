using System.Collections.Generic;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class SlotCloner
    {
        internal const string Prefix = "Galpao_CacaNiquel";
        const string SourcePath = "STORE/LOD/GFX_Store/SlotMachine";
        const string BodyName = "slot_machine 1";
        const float MezzanineFloor = 3.205f;
        const float BackWall = 15.78f;

        internal static void Build(Transform root)
        {
            var source = DynoCloner.Find(SourcePath);
            var body = source != null ? source.transform.Find(BodyName) : null;
            var mesh = body != null ? body.GetComponent<MeshCollider>() : null;
            if (mesh == null || mesh.sharedMesh == null)
            {
                ModConsole.Warning("[MSC Duo] caça-níquel da loja não encontrado; mezanino sem caça-níquel.");
                return;
            }
            var front = -source.transform.up;
            front.y = 0f;
            if (front.sqrMagnitude < 1e-4f) front = -source.transform.forward;
            var toward = Warehouse.Local(0f, -1f, 0f) - Warehouse.Local(0f, 0f, 0f);
            var turn = Quaternion.FromToRotation(front.normalized, toward);

            var copy = (GameObject)Object.Instantiate(source, source.transform.position, turn * source.transform.rotation);
            copy.name = source.name;
            var holder = new GameObject(Prefix);
            holder.transform.parent = root;
            copy.transform.parent = holder.transform;
            var map = new Dictionary<Object, Object>();
            SatsumaCloner.MapHierarchy(source.transform, copy.transform, map);

            var copyBody = copy.transform.Find(BodyName);
            var corners = Corners(copyBody.GetComponent<MeshCollider>().sharedMesh.bounds, copyBody.localToWorldMatrix);
            float minX = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue, minZ = float.MaxValue;
            foreach (var c in corners)
            {
                var local = Warehouse.ToLocal(c);
                minX = Mathf.Min(minX, local.x);
                maxX = Mathf.Max(maxX, local.x);
                maxY = Mathf.Max(maxY, local.y);
                minZ = Mathf.Min(minZ, local.z);
            }
            var x = WarehouseConfig.Get("cacaniquel_x");
            var shift = Warehouse.Local(x - (minX + maxX) / 2f, BackWall - maxY, MezzanineFloor - minZ) - Warehouse.Local(0f, 0f, 0f);
            copy.transform.position += shift;
            copy.SetActive(true);

            int fsms = 0, remapped = 0;
            foreach (var fsm in copy.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                FsmTools.EnsureLoaded(fsm);
                DynoCloner.StripHooks(fsm);
                remapped += SatsumaCloner.Remap(fsm, map);
                fsms++;
            }
            remapped += SatsumaCloner.RemapLists(copy, map);
            ModConsole.Print($"<color=orange>[MSC Duo]</color> caça-níquel no mezanino: {fsms} FSMs, {remapped} referências.");
        }

        static Vector3[] Corners(Bounds b, Matrix4x4 m)
        {
            var result = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                var p = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                result[i] = m.MultiplyPoint3x4(p);
            }
            return result;
        }
    }
}
