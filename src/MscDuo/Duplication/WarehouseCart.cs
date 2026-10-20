using System;
using System.Collections.Generic;
using System.IO;
using HutongGames.PlayMaker;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class WarehouseCart
    {
        const string OpenEvent = "MSCDUO_GAVETA";
        static Action<int> sender;
        static readonly List<CartPush> carts = new List<CartPush>();

        internal static int Count => carts.Count;

        internal static void Bind(Action<int> send) => sender = send;

        internal static void Clear() => carts.Clear();

        static string Prefix(int index) => index == 0 ? "carrinho" : "carrinho" + (index + 1);

        internal static void Write(BinaryWriter w, int index)
        {
            var cart = index < carts.Count ? carts[index] : null;
            var p = cart != null ? cart.transform.position : Vector3.zero;
            w.Write((byte)index);
            w.Write(cart != null);
            w.Write(p.x);
            w.Write(p.y);
            w.Write(p.z);
            w.Write(cart != null ? cart.transform.eulerAngles.y : 0f);
        }

        internal static void Read(BinaryReader r)
        {
            var index = r.ReadByte();
            var has = r.ReadBoolean();
            var p = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            var yaw = r.ReadSingle();
            if (has && index < carts.Count && carts[index] != null) carts[index].Remote(p, yaw);
        }

        internal static void Save(System.Text.StringBuilder sb)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            for (int i = 0; i < carts.Count; i++)
            {
                var cart = carts[i];
                if (cart == null) continue;
                var local = Warehouse.ToLocal(cart.transform.position);
                var key = Prefix(i);
                sb.AppendLine(string.Format(inv, "{0}_x = {1:0.000}", key, local.x));
                sb.AppendLine(string.Format(inv, "{0}_y = {1:0.000}", key, local.y));
                sb.AppendLine(string.Format(inv, "{0}_altura = {1:0.000}", key, local.z));
                sb.AppendLine(string.Format(inv, "{0}_giro = {1:0.0}", key, cart.transform.eulerAngles.y));
            }
        }

        internal static void Setup(GameObject root, GateInput input)
        {
            var index = carts.Count;
            var prefix = Prefix(index);
            var drawer = root.transform.Find("Drawer");
            PlayMakerFSM use = null;
            if (drawer != null)
                foreach (var fsm in drawer.GetComponents<PlayMakerFSM>())
                    if (fsm.FsmName == "Use") use = fsm;
            if (use != null && use.Fsm.GetState("Bool test") != null)
            {
                use.AddEvent(OpenEvent);
                use.AddGlobalTransition(OpenEvent, "Bool test");
                var open = use.FsmVariables.FindFsmBool("Open");
                var collider = drawer.GetComponent<Collider>();
                var fsm = use;
                var gaveta = prefix + "_gaveta";
                WarehouseToggles.Register(gaveta, 0, v =>
                {
                    if (open == null || open.Value != (v != 0)) fsm.SendEvent(OpenEvent);
                });
                if (collider != null)
                    input.Buttons.Add(new GateButton { Collider = collider, Cap = drawer, Priority = 1, Click = () => WarehouseToggles.Set(gaveta, WarehouseToggles.Get(gaveta) == 0 ? 1 : 0), Text = () => WarehouseToggles.Get(gaveta) == 0 ? "Abrir gaveta" : "Fechar gaveta" });
            }
            Shell(root, drawer);
            var bodies = new List<Collider>();
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
                if (!c.isTrigger && (drawer == null || !c.transform.IsChildOf(drawer))) bodies.Add(c);
            foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            var cart = root.AddComponent<CartPush>();
            cart.Index = index;
            carts.Add(cart);
            var saved = WarehouseGates.Load();
            float x, y, h, yaw;
            if (saved.TryGetValue(prefix + "_x", out x) && saved.TryGetValue(prefix + "_y", out y) && saved.TryGetValue(prefix + "_altura", out h) && saved.TryGetValue(prefix + "_giro", out yaw))
            {
                root.transform.position = Warehouse.Local(x, y, h);
                root.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Inverse(Quaternion.Euler(0f, root.transform.eulerAngles.y, 0f)) * root.transform.rotation;
            }
            foreach (var body in bodies)
                input.Buttons.Add(new GateButton { Collider = body, Cap = root.transform, Click = cart.Grab, Text = () => "Empurrar carrinho (segure)" });
            ModConsole.Print($"<color=orange>[MSC Duo]</color> carrinho de ferramentas {index + 1}: gaveta {(use != null ? "ok" : "não encontrada")}, empurrar com {bodies.Count} colisões.");
        }

        static Bounds? Bounds(Transform root, Transform space, Transform include, Transform exclude)
        {
            var found = false;
            Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
            foreach (var r in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (r.sharedMesh == null || !r.gameObject.activeInHierarchy) continue;
                if (include != null && !r.transform.IsChildOf(include)) continue;
                if (exclude != null && r.transform.IsChildOf(exclude)) continue;
                var renderer = r.GetComponent<Renderer>();
                if (renderer == null || !renderer.enabled) continue;
                var lb = r.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? lb.min.x : lb.max.x, (i & 2) == 0 ? lb.min.y : lb.max.y, (i & 4) == 0 ? lb.min.z : lb.max.z);
                    var p = space.InverseTransformPoint(r.transform.TransformPoint(corner));
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                    found = true;
                }
            }
            if (!found) return null;
            var b = new Bounds();
            b.SetMinMax(min, max);
            return b;
        }

        static void Shell(GameObject root, Transform drawer)
        {
            const float wall = 0.02f;
            var shell = new GameObject("Casco");
            shell.transform.parent = root.transform;
            shell.transform.localPosition = Vector3.zero;
            var forward = drawer != null ? drawer.up : root.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            shell.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            var body = Bounds(root.transform, shell.transform, null, drawer);
            if (!body.HasValue) return;
            var min = body.Value.min;
            var max = body.Value.max;
            var bottom = min.y + 0.1f;
            Add(shell, new Vector3(min.x, bottom, min.z), new Vector3(min.x + wall, max.y, max.z));
            Add(shell, new Vector3(max.x - wall, bottom, min.z), new Vector3(max.x, max.y, max.z));
            Add(shell, new Vector3(min.x, bottom, max.z - wall), new Vector3(max.x, max.y, max.z));
            Add(shell, new Vector3(min.x, max.y - wall, min.z), new Vector3(max.x, max.y, max.z));
            var slot = drawer != null ? Bounds(root.transform, shell.transform, drawer, null) : null;
            if (!slot.HasValue)
            {
                Add(shell, new Vector3(min.x, bottom, min.z), new Vector3(max.x, max.y, min.z + wall));
                return;
            }
            var s = slot.Value;
            if (s.min.y > bottom + 0.01f) Add(shell, new Vector3(min.x, bottom, min.z), new Vector3(max.x, s.min.y, min.z + wall));
            if (s.max.y < max.y - 0.01f) Add(shell, new Vector3(min.x, s.max.y, min.z), new Vector3(max.x, max.y, min.z + wall));
        }

        static void Add(GameObject shell, Vector3 a, Vector3 b)
        {
            var box = shell.AddComponent<BoxCollider>();
            box.center = (a + b) / 2f;
            box.size = new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), Mathf.Abs(b.z - a.z));
        }

        internal static void Notify(int index)
        {
            if (sender != null) sender(index);
        }

        class CartPush : MonoBehaviour
        {
            internal int Index;
            bool held;
            Vector3 offset;
            float yawOffset;
            float nextSend;
            float height = float.NaN;

            internal void Grab()
            {
                var cam = Camera.main;
                if (cam == null) return;
                held = true;
                if (float.IsNaN(height)) height = transform.position.y - Ground(transform.position);
                var camYaw = cam.transform.eulerAngles.y;
                var flat = transform.position - cam.transform.position;
                flat.y = 0f;
                if (flat.magnitude < 0.8f) flat = flat.normalized * 0.8f;
                offset = Quaternion.Euler(0f, -camYaw, 0f) * flat;
                yawOffset = Mathf.DeltaAngle(camYaw, transform.eulerAngles.y);
            }

            internal void Remote(Vector3 position, float yaw)
            {
                if (held) return;
                transform.position = position;
                transform.rotation = Quaternion.Euler(0f, yaw - transform.eulerAngles.y, 0f) * transform.rotation;
            }

            float Ground(Vector3 at)
            {
                foreach (var hit in Physics.RaycastAll(at + Vector3.up * 1.5f, Vector3.down, 4f))
                {
                    if (hit.collider == null || hit.collider.isTrigger || hit.collider.transform.IsChildOf(transform) || hit.collider.transform.root.name == "PLAYER") continue;
                    return hit.point.y;
                }
                return at.y;
            }

            void Update()
            {
                if (!held) return;
                if (!Input.GetMouseButton(0))
                {
                    held = false;
                    Notify(Index);
                    return;
                }
                var cam = Camera.main;
                if (cam == null) return;
                var camYaw = cam.transform.eulerAngles.y;
                var target = cam.transform.position + Quaternion.Euler(0f, camYaw, 0f) * offset;
                target.y = Ground(target) + height;
                transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * 8f);
                var yaw = camYaw + yawOffset;
                var delta = Mathf.DeltaAngle(transform.eulerAngles.y, yaw);
                transform.rotation = Quaternion.Euler(0f, delta * Mathf.Min(1f, Time.deltaTime * 6f), 0f) * transform.rotation;
                if (Time.time >= nextSend)
                {
                    nextSend = Time.time + 0.1f;
                    Notify(Index);
                }
            }
        }
    }
}
