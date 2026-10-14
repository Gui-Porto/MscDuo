using System;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class SatsumaCloner
    {
        const string OriginalName = "SATSUMA(557kg, 248)";
        static readonly Vector3 StartPosition = new Vector3(-16.23f, -0.28f, 11.37f);
        static readonly Quaternion StartRotation = Quaternion.Euler(0f, 180f, 0f);

        static readonly Type[] StripOrder =
        {
            typeof(Joint),
            typeof(MonoBehaviour),
            typeof(AudioSource),
            typeof(Animation),
            typeof(Camera),
            typeof(Light),
            typeof(Projector),
            typeof(ParticleRenderer),
            typeof(ParticleAnimator),
            typeof(ParticleEmitter),
        };

        internal static GameObject Clone { get; private set; }

        internal static void Spawn(GameObject house1, GameObject house2)
        {
            var original = FindRoot(OriginalName);
            if (original == null)
            {
                ModConsole.Warning($"[MSC Duo] '{OriginalName}' não encontrada; Satsuma 2 não criada.");
                return;
            }

            var local = house1.transform.InverseTransformPoint(StartPosition);
            var position = house2.transform.TransformPoint(local);
            var rotation = house2.transform.rotation * Quaternion.Inverse(house1.transform.rotation) * StartRotation;

            Clone = (GameObject)UnityEngine.Object.Instantiate(original, position, rotation);
            Clone.name = "SATSUMA_P2";
            Clone.transform.parent = house2.transform.parent;

            var removed = 0;
            foreach (var type in StripOrder)
                foreach (var c in Clone.GetComponentsInChildren(type, true))
                {
                    if (c == null) continue;
                    UnityEngine.Object.DestroyImmediate(c);
                    removed++;
                }
            foreach (var col in Clone.GetComponentsInChildren<Collider>(true))
                if (col.isTrigger) { UnityEngine.Object.DestroyImmediate(col); removed++; }
            foreach (var body in Clone.GetComponentsInChildren<Rigidbody>(true))
                body.isKinematic = true;

            ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma 2 (casca) criada em {HouseCloner.Format(position)}; {removed} componentes removidos.");
        }

        static GameObject FindRoot(string name)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.parent == null && t.name == name && t.hideFlags == HideFlags.None)
                    return t.gameObject;
            return null;
        }
    }
}
