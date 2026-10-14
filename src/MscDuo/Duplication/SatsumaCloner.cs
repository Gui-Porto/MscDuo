using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using MSCLoader;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal static class SatsumaCloner
    {
        internal const string SaveFile = "MscDuo_P2.txt";
        const string GameSaveFile = "defaultES2File.txt";
        const float GroundProbe = 0.5f;
        const string CarName = "SATSUMA(557kg, 248)";

        static readonly string[] DatabaseParts = { "DatabaseBody", "DatabaseMechanics", "DatabaseMotor", "DatabaseWiring", "PartsStatus" };
        static readonly HashSet<string> CarFsmsKept = new HashSet<string> { "Assembly", "Removal", "BoltCheck", "Screw", "Paint" };

        internal static GameObject Clone { get; private set; }
        internal static GameObject Parts { get; private set; }
        internal static GameObject Database { get; private set; }

        internal static void Spawn(GameObject house1, GameObject house2)
        {
            var car1 = FindRoot(CarName);
            var parts1 = FindRoot("CARPARTS");
            var db1 = FindRoot("Database");
            if (car1 == null || parts1 == null || db1 == null)
            {
                ModConsole.Warning("[MSC Duo] Satsuma, CARPARTS ou Database não encontrados; Satsuma 2 não criada.");
                return;
            }

            var move = house2.transform.localToWorldMatrix * house1.transform.worldToLocalMatrix;
            var turn = house2.transform.rotation * Quaternion.Inverse(house1.transform.rotation);
            var map = new Dictionary<Object, Object>();

            Clone = CloneRoot(car1, "SATSUMA_P2", move, turn, map);
            Parts = CloneRoot(parts1, "CARPARTS_P2", move, turn, map);
            Database = new GameObject("Database_P2");
            foreach (var name in DatabaseParts)
            {
                var child = db1.transform.Find(name);
                if (child == null) continue;
                var copy = CloneRoot(child.gameObject, name, Matrix4x4.identity, Quaternion.identity, map);
                copy.transform.parent = Database.transform;
            }

            var lift = SnapToGround();
            var stripped = StripCarSystems(Clone);
            var carRef = new FsmGameObject("MscDuoTheCar") { Value = Clone };
            int fsms = 0, remapped = 0, saves = 0, failed = 0;
            foreach (var root in new[] { Clone, Parts, Database })
            {
                foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    try
                    {
                        FsmTools.EnsureLoaded(fsm);
                        remapped += Remap(fsm, map);
                        saves += RedirectSaves(fsm);
                        FsmTools.RedirectGlobal(fsm, "TheCar", carRef);
                        fsms++;
                    }
                    catch (System.Exception e)
                    {
                        failed++;
                        if (failed <= 5) ModConsole.Warning($"[MSC Duo] Satsuma 2: '{fsm.gameObject.name}/{fsm.FsmName}' não preparada: {e.Message}");
                    }
                }
                remapped += RemapLists(root, map);
                saves += RedirectAutoSaves(root);
            }

            ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma 2: {map.Count} objetos clonados, {fsms} FSMs ({failed} com erro), {remapped} referências redirecionadas, {saves} saves em {SaveFile}, {stripped} sistemas do carro desligados, altura ajustada em {lift:0.00} m.");
        }

        static float SnapToGround()
        {
            float bottom;
            Bounds footprint;
            if (!TryBottom(Clone, out bottom, out footprint)) return 0f;
            var ground = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector3(
                    Mathf.Lerp(footprint.min.x, footprint.max.x, (i & 1) == 0 ? 0.25f : 0.75f),
                    bottom,
                    Mathf.Lerp(footprint.min.z, footprint.max.z, (i & 2) == 0 ? 0.25f : 0.75f));
                float y;
                if (Ground.TryHeight(corner, GroundProbe, out y, Clone, Parts) && y > ground) ground = y;
            }
            if (ground == float.MinValue) return 0f;
            var shift = ground - bottom;
            Clone.transform.position += Vector3.up * shift;
            return shift;
        }

        static bool TryBottom(GameObject car, out float bottom, out Bounds footprint)
        {
            bottom = float.MaxValue;
            footprint = new Bounds();
            var found = false;
            foreach (var collider in car.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger || !collider.enabled) continue;
                var b = collider.bounds;
                if (!found) footprint = b; else footprint.Encapsulate(b);
                if (b.min.y < bottom) bottom = b.min.y;
                found = true;
            }
            return found;
        }

        static GameObject CloneRoot(GameObject original, string name, Matrix4x4 move, Quaternion turn, Dictionary<Object, Object> map)
        {
            var position = move.MultiplyPoint3x4(original.transform.position);
            var rotation = turn * original.transform.rotation;
            var copy = (GameObject)Object.Instantiate(original, position, rotation);
            copy.name = name;
            MapHierarchy(original.transform, copy.transform, map);
            return copy;
        }

        static void MapHierarchy(Transform a, Transform b, Dictionary<Object, Object> map)
        {
            map[a.gameObject] = b.gameObject;
            var ca = a.GetComponents<Component>();
            var cb = b.GetComponents<Component>();
            for (int i = 0; i < ca.Length && i < cb.Length; i++)
                if (ca[i] != null && cb[i] != null) map[ca[i]] = cb[i];
            for (int i = 0; i < a.childCount && i < b.childCount; i++)
                MapHierarchy(a.GetChild(i), b.GetChild(i), map);
        }

        static int StripCarSystems(GameObject car)
        {
            var removed = 0;
            foreach (var fsm in car.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (CarFsmsKept.Contains(fsm.FsmName)) continue;
                Object.DestroyImmediate(fsm);
                removed++;
            }
            foreach (var behaviour in car.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;
                var type = behaviour.GetType().Name;
                if (type.StartsWith("PlayMaker") || type == "ES2Auto") continue;
                Object.DestroyImmediate(behaviour);
                removed++;
            }
            foreach (var source in car.GetComponentsInChildren<AudioSource>(true))
            {
                Object.DestroyImmediate(source);
                removed++;
            }
            var body = car.GetComponent<Rigidbody>();
            if (body != null) body.isKinematic = true;
            return removed;
        }

        static int Remap(PlayMakerFSM fsm, Dictionary<Object, Object> map)
        {
            var count = 0;
            foreach (var v in fsm.FsmVariables.GameObjectVariables) count += Swap(v, map);
            foreach (var v in fsm.FsmVariables.ObjectVariables) count += Swap(v, map);

            foreach (var state in fsm.FsmStates)
                foreach (var action in state.Actions)
                {
                    if (action == null) continue;
                    foreach (var field in action.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        var value = field.GetValue(action);
                        switch (value)
                        {
                            case FsmGameObject go:
                                if (!go.UseVariable) count += Swap(go, map);
                                break;
                            case FsmObject obj:
                                if (!obj.UseVariable) count += Swap(obj, map);
                                break;
                            case FsmOwnerDefault owner:
                                if (owner.OwnerOption == OwnerDefaultOption.SpecifyGameObject && owner.GameObject != null && !owner.GameObject.UseVariable)
                                    count += Swap(owner.GameObject, map);
                                break;
                            case FsmEventTarget target:
                                if (target.gameObject != null && target.gameObject.OwnerOption == OwnerDefaultOption.SpecifyGameObject && target.gameObject.GameObject != null && !target.gameObject.GameObject.UseVariable)
                                    count += Swap(target.gameObject.GameObject, map);
                                break;
                            case FsmGameObject[] array:
                                foreach (var item in array) if (item != null && !item.UseVariable) count += Swap(item, map);
                                break;
                            case GameObject raw:
                                Object mapped;
                                if (map.TryGetValue(raw, out mapped)) { field.SetValue(action, mapped); count++; }
                                break;
                        }
                    }
                }
            return count;
        }

        static int Swap(FsmGameObject variable, Dictionary<Object, Object> map)
        {
            Object mapped;
            if (variable == null || variable.Value == null || !map.TryGetValue(variable.Value, out mapped)) return 0;
            variable.Value = (GameObject)mapped;
            return 1;
        }

        static int Swap(FsmObject variable, Dictionary<Object, Object> map)
        {
            Object mapped;
            if (variable == null || variable.Value == null || !map.TryGetValue(variable.Value, out mapped)) return 0;
            variable.Value = mapped;
            return 1;
        }

        static int RemapLists(GameObject root, Dictionary<Object, Object> map)
        {
            var count = 0;
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;
                var type = behaviour.GetType();
                if (!type.Name.StartsWith("PlayMakerArrayListProxy") && !type.Name.StartsWith("PlayMakerHashTableProxy")) continue;
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    var list = field.GetValue(behaviour) as IList;
                    if (list == null || list.IsReadOnly || list.IsFixedSize) continue;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var item = list[i] as Object;
                        Object mapped;
                        if (item == null || !map.TryGetValue(item, out mapped)) continue;
                        list[i] = mapped;
                        count++;
                    }
                }
            }
            return count;
        }

        static int RedirectSaves(PlayMakerFSM fsm)
        {
            var count = 0;
            foreach (var s in fsm.FsmVariables.StringVariables)
                if (s.Value == GameSaveFile) { s.Value = SaveFile; count++; }

            foreach (var state in fsm.FsmStates)
                foreach (var action in state.Actions)
                {
                    if (action == null) continue;
                    foreach (var field in action.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        var s = field.GetValue(action) as FsmString;
                        if (s == null || s.UseVariable || s.Value != GameSaveFile) continue;
                        s.Value = SaveFile;
                        count++;
                    }
                }
            return count;
        }

        static int RedirectAutoSaves(GameObject root)
        {
            var count = 0;
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().Name != "ES2Auto") continue;
                var field = behaviour.GetType().GetField("saveFile");
                if (field == null) continue;
                field.SetValue(behaviour, SaveFile);
                count++;
            }
            return count;
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
