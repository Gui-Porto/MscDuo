using UnityEngine;

namespace MscDuo.Sync
{
    internal class Avatar
    {
        const float BodyHeight = 1.75f;
        const float FeetBelowPivot = 0.5f;
        static readonly Color BodyColor = new Color(0.91f, 0.4f, 0.04f);

        readonly GameObject root;
        readonly Transform head;
        readonly TextMesh label;

        internal Avatar(string name)
        {
            root = new GameObject("MscDuo_Avatar_" + name);
            var material = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse")) { color = BodyColor };

            var body = Part(PrimitiveType.Capsule, root.transform, material);
            body.localScale = new Vector3(0.5f, BodyHeight / 2f * 0.85f, 0.5f);
            body.localPosition = new Vector3(0f, BodyHeight * 0.425f, 0f);

            head = new GameObject("Head").transform;
            head.parent = root.transform;
            head.localPosition = new Vector3(0f, BodyHeight * 0.92f, 0f);
            var skull = Part(PrimitiveType.Sphere, head, material);
            skull.localScale = Vector3.one * 0.28f;
            var nose = Part(PrimitiveType.Cube, head, new Material(material) { color = Color.white });
            nose.localScale = new Vector3(0.06f, 0.06f, 0.18f);
            nose.localPosition = new Vector3(0f, 0f, 0.16f);

            var tag = new GameObject("Name");
            tag.transform.parent = root.transform;
            tag.transform.localPosition = new Vector3(0f, BodyHeight + 0.35f, 0f);
            label = tag.AddComponent<TextMesh>();
            label.text = name;
            label.anchor = TextAnchor.MiddleCenter;
            label.characterSize = 0.018f;
            label.fontSize = 48;
            label.color = Color.white;
            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.font = font;
            tag.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        internal void Apply(Vector3 pivot, float yaw, float pitch)
        {
            root.transform.position = pivot - Vector3.up * FeetBelowPivot;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            head.localRotation = Quaternion.Euler(Mathf.Clamp(pitch, -80f, 80f), 0f, 0f);

            var camera = Camera.main;
            if (camera != null)
                label.transform.rotation = Quaternion.LookRotation(label.transform.position - camera.transform.position);
        }

        internal void Destroy()
        {
            if (root != null) Object.Destroy(root);
        }

        static Transform Part(PrimitiveType type, Transform parent, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.parent = parent;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go.transform;
        }
    }
}
