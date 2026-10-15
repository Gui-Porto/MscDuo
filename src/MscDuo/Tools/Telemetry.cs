using System.Collections.Generic;
using System.IO;
using System.Text;
using MSCLoader;
using MscDuo.Duplication;
using UnityEngine;

namespace MscDuo.Tools
{
    internal class Telemetry : MonoBehaviour
    {
        const float Interval = 0.5f;

        static Telemetry current;

        readonly Dictionary<string, float> contacts = new Dictionary<string, float>();
        StreamWriter writer;
        Rigidbody body;
        Wheel[] wheels;
        Drivetrain drivetrain;
        float next;

        internal static string Toggle(string folder)
        {
            if (current != null)
            {
                Destroy(current);
                return "telemetria desligada.";
            }
            var car = SatsumaCloner.Clone;
            if (car == null) return "Satsuma 2 não está ativa";
            current = car.AddComponent<Telemetry>();
            current.Open(System.IO.Path.Combine(folder, "telemetry.txt"));
            return "telemetria ligada: dirija até atolar e rode o comando de novo.";
        }

        void Open(string file)
        {
            writer = new StreamWriter(file, false) { AutoFlush = true };
            body = GetComponent<Rigidbody>();
            wheels = GetComponentsInChildren<Wheel>(true);
            drivetrain = GetComponent<Drivetrain>();
            writer.WriteLine("# tempo | velocidade | marcha rpm acelerador embreagem | rodas: nome chão(objeto) rotação freio/freio-de-mão força | contatos do carro (objeto: impulso)");
        }

        void OnCollisionStay(Collision collision) => Record(collision);
        void OnCollisionEnter(Collision collision) => Record(collision);

        void Record(Collision collision)
        {
            var mine = collision.contacts.Length > 0 ? collision.contacts[0].thisCollider : null;
            var key = $"{(mine != null ? mine.name : "?")} x {Path(collision.collider.transform)}";
            contacts[key] = (contacts.TryGetValue(key, out var v) ? v : 0f) + collision.relativeVelocity.magnitude;
        }

        void FixedUpdate()
        {
            if (Time.time < next) return;
            next = Time.time + Interval;
            var line = new StringBuilder();
            line.Append($"{Time.time:0.0} | {body.velocity.magnitude * 3.6f:0.0} km/h");
            if (drivetrain != null)
            {
                var clutch = drivetrain.clutch != null ? drivetrain.clutch.GetClutchPosition().ToString("0.00") : "-";
                line.Append($" | marcha {drivetrain.gear} rpm {drivetrain.rpm:0} acel {drivetrain.throttle:0.00} embr {clutch}");
            }
            line.Append(" |");
            foreach (var w in wheels)
            {
                var ground = w.onGroundDown && w.hitDown.collider != null ? w.hitDown.collider.name : "NO AR";
                line.Append($" {w.name}:{ground} rot={w.angularVelocity:0} freio={w.brake:0.00}/{w.handbrake:0.00} força={w.normalForce:0}");
            }
            line.Append(" | contatos:");
            if (contacts.Count == 0) line.Append(" nenhum");
            foreach (var pair in contacts) line.Append($" [{pair.Key}: {pair.Value:0.0}]");
            contacts.Clear();
            writer.WriteLine(line.ToString());
        }

        void OnDestroy()
        {
            if (writer != null) writer.Close();
            ModConsole.Print("<color=orange>[MSC Duo]</color> telemetria salva em telemetry.txt.");
            current = null;
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
