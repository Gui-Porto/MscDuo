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
        static Action sender;
        static CartPush cart;

        internal static void Bind(Action send) => sender = send;

        internal static void Write(BinaryWriter w)
        {
            var p = cart != null ? cart.transform.position : Vector3.zero;
            w.Write(cart != null);
            w.Write(p.x);
            w.Write(p.y);
            w.Write(p.z);
            w.Write(cart != null ? cart.transform.eulerAngles.y : 0f);
        }

        internal static void Read(BinaryReader r)
        {
            var has = r.ReadBoolean();
            var p = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            var yaw = r.ReadSingle();
            if (has && cart != null) cart.Remote(p, yaw);
        }

        internal static void Save(System.Text.StringBuilder sb)
        {
            if (cart == null) return;
            var local = Warehouse.ToLocal(cart.transform.position);
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "carrinho_x = {0:0.000}", local.x));
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "carrinho_y = {0:0.000}", local.y));
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "carrinho_altura = {0:0.000}", local.z));
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "carrinho_giro = {0:0.0}", cart.transform.eulerAngles.y));
        }

        internal static void Setup(GameObject root, GateInput input)
        {
            cart = null;
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
                WarehouseToggles.Register("carrinho_gaveta", 0, v =>
                {
                    if (open == null || open.Value != (v != 0)) fsm.SendEvent(OpenEvent);
                });
                if (collider != null)
                    input.Buttons.Add(new GateButton { Collider = collider, Cap = drawer, Click = () => WarehouseToggles.Set("carrinho_gaveta", WarehouseToggles.Get("carrinho_gaveta") == 0 ? 1 : 0), Text = () => WarehouseToggles.Get("carrinho_gaveta") == 0 ? "Abrir gaveta" : "Fechar gaveta" });
            }
            Collider body = null;
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
                if (!c.isTrigger && (drawer == null || !c.transform.IsChildOf(drawer))) { body = c; break; }
            foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            cart = root.AddComponent<CartPush>();
            var saved = WarehouseGates.Load();
            float x, y, h, yaw;
            if (saved.TryGetValue("carrinho_x", out x) && saved.TryGetValue("carrinho_y", out y) && saved.TryGetValue("carrinho_altura", out h) && saved.TryGetValue("carrinho_giro", out yaw))
            {
                root.transform.position = Warehouse.Local(x, y, h);
                root.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Inverse(Quaternion.Euler(0f, root.transform.eulerAngles.y, 0f)) * root.transform.rotation;
            }
            if (body != null)
                input.Buttons.Add(new GateButton { Collider = body, Cap = root.transform, Click = cart.Grab, Text = () => "Empurrar carrinho (segure)" });
            ModConsole.Print($"<color=orange>[MSC Duo]</color> carrinho de ferramentas: gaveta {(use != null ? "ok" : "não encontrada")}, empurrar {(body != null ? "ok" : "sem colisão")}.");
        }

        internal static void Notify()
        {
            if (sender != null) sender();
        }

        class CartPush : MonoBehaviour
        {
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
                    Notify();
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
                    Notify();
                }
            }
        }
    }
}
