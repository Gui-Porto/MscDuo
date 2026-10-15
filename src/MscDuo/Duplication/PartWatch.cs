using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal class PartWatch : MonoBehaviour
    {
        const float HitSpeed = 1.5f;
        const float Cooldown = 2f;

        Transform car;
        float quietUntil;

        internal static int Attach(GameObject car)
        {
            var count = 0;
            foreach (var body in car.GetComponentsInChildren<Rigidbody>(true))
            {
                if (body.transform == car.transform || body.GetComponent<PartWatch>() != null || Path(body.transform).Contains("PLAYER")) continue;
                body.gameObject.AddComponent<PartWatch>().car = car.transform;
                count++;
            }
            return count;
        }

        void OnJointBreak(float force)
        {
            ModConsole.Warning($"[MSC Duo] Satsuma 2: a junta de '{Rel()}' ROMPEU com força {force:0}; a peça se soltou.");
        }

        void OnCollisionEnter(Collision collision)
        {
            if (Time.time < quietUntil || collision.relativeVelocity.magnitude < HitSpeed) return;
            var other = collision.collider.transform;
            if (car != null && other.IsChildOf(car)) return;
            quietUntil = Time.time + Cooldown;
            ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma 2: '{Rel()}' bateu em '{Path(other)}' a {collision.relativeVelocity.magnitude:0.0} m/s (preso ao carro: {(car != null && transform.IsChildOf(car))}).");
        }

        string Rel() => car != null && transform.IsChildOf(car) ? Path(transform).Substring(Path(car).Length + 1) : Path(transform);

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
