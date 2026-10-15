using System.Collections.Generic;
using System.Text;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal class StuckWatch : MonoBehaviour
    {
        const float Interval = 0.5f;
        const float StuckSpeed = 2f;
        const float SpinSpeed = 15f;
        const float ReportEvery = 4f;

        readonly Dictionary<string, float> contacts = new Dictionary<string, float>();
        Rigidbody body;
        Wheel[] wheels;
        Drivetrain drivetrain;
        float next;
        float nextReport;
        int stuckSamples;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            wheels = GetComponentsInChildren<Wheel>(true);
            drivetrain = GetComponent<Drivetrain>();
        }

        void OnCollisionStay(Collision collision) => Record(collision);
        void OnCollisionEnter(Collision collision) => Record(collision);

        void Record(Collision collision)
        {
            if (collision.collider.transform.IsChildOf(transform)) return;
            var mine = collision.contacts.Length > 0 ? collision.contacts[0].thisCollider : null;
            var key = $"{(mine != null ? Path(mine.transform) : "?")} em {Path(collision.collider.transform)}";
            contacts[key] = (contacts.TryGetValue(key, out var v) ? v : 0f) + 1f;
        }

        void FixedUpdate()
        {
            if (Time.time < next) return;
            next = Time.time + Interval;
            var speed = body.velocity.magnitude * 3.6f;
            var spinning = false;
            foreach (var w in wheels)
                if (Mathf.Abs(w.angularVelocity) > SpinSpeed) spinning = true;
            var throttle = drivetrain != null ? drivetrain.throttle : 0f;
            stuckSamples = speed < StuckSpeed && spinning && throttle > 0.2f ? stuckSamples + 1 : 0;

            if (stuckSamples >= 3 && Time.time >= nextReport)
            {
                nextReport = Time.time + ReportEvery;
                Report(speed, throttle);
            }
            contacts.Clear();
        }

        void Report(float speed, float throttle)
        {
            var text = new StringBuilder();
            text.Append($"[MSC Duo] Satsuma 2 ATOLADA: {speed:0.0} km/h, acelerador {throttle:0.00}, marcha {(drivetrain != null ? drivetrain.gear : 0)}, inclinação lateral {Mathf.Asin(Mathf.Clamp(-transform.right.y, -1f, 1f)) * Mathf.Rad2Deg:0}° (positivo = esquerda)");
            foreach (var w in wheels)
            {
                var ground = w.onGroundDown && w.hitDown.collider != null ? Path(w.hitDown.collider.transform) : "NO AR";
                text.Append($"\n  {w.name}: chão={ground} compressão={w.compression:0.00}/{w.suspensionTravel:0.00} força={w.normalForce:0} giro={w.angularVelocity:0} freio={w.brake:0.00} mão={w.handbrake:0.00}");
            }
            text.Append("\n  carroceria tocando:");
            if (contacts.Count == 0) text.Append(" nada");
            foreach (var pair in contacts) text.Append($"\n    {pair.Key} ({pair.Value:0}x)");
            ModConsole.Warning(text.ToString());
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
