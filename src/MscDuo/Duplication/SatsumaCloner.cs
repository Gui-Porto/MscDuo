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
        static readonly string[] CarGlobals =
        {
            "RPM", "SpeedKMH", "EngineTemp", "CarVelocity", "CarMass", "ClutchWear", "Fuel",
            "EnginePower", "EnginePowerMultiplier", "EnginePowerPerCylinder", "EngineTorqPerCylinder", "EngineTorqueRPM",
        };

        internal static GameObject Clone { get; private set; }
        internal static GameObject Parts { get; private set; }
        internal static GameObject Database { get; private set; }
        internal static Dictionary<string, NamedVariable> Variables { get; private set; }
        internal static Dictionary<Object, Object> Map { get; private set; }
        internal static GameObject Original { get; private set; }
        internal static GameObject OriginalDatabase { get; private set; }

        const float ReadyTimeout = 60f;
        const float SettleDelay = 5f;

        internal static void SpawnWhenReady(GameObject house1, GameObject house2)
        {
            var runner = new GameObject("MscDuo_SatsumaSpawner").AddComponent<Spawner>();
            runner.StartCoroutine(WaitAndSpawn(house1, house2, runner.gameObject));
        }

        class Spawner : MonoBehaviour { }

        static IEnumerator WaitAndSpawn(GameObject house1, GameObject house2, GameObject runner)
        {
            var started = Time.realtimeSinceStartup;
            var car = FindRoot(CarName);
            PlayMakerFSM setup = null;
            if (car != null)
                foreach (var fsm in car.GetComponents<PlayMakerFSM>())
                    if (fsm.FsmName == "Setup") setup = fsm;
            while (car != null && (setup != null && !Settled(setup) || PlayerInside(car)) && Time.realtimeSinceStartup - started < ReadyTimeout)
                yield return new WaitForSeconds(0.5f);
            yield return new WaitForSeconds(SettleDelay);
            var waited = Time.realtimeSinceStartup - started;
            ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma 1 pronta depois de {waited:0.0} s (estado '{(setup != null ? setup.ActiveStateName : "-")}'); clonando a Satsuma 2.");
            try { Spawn(house1, house2); }
            catch (System.Exception e) { ModConsole.Error($"[MSC Duo] Satsuma 2 falhou: {e}"); }
            Object.Destroy(runner);
        }

        static bool PlayerInside(GameObject car)
        {
            var player = GameObject.Find("PLAYER");
            return player != null && player.transform.IsChildOf(car.transform);
        }

        static bool Settled(PlayMakerFSM setup) =>
            setup.ActiveStateName == "Lod ON" || setup.ActiveStateName == "Lod OFF";

        internal static void Spawn(GameObject house1, GameObject house2) =>
            Spawn(house2.transform.localToWorldMatrix * house1.transform.worldToLocalMatrix, house2.transform.rotation * Quaternion.Inverse(house1.transform.rotation), true, house1.transform.position);

        static void Spawn(Matrix4x4 move, Quaternion turn, bool snap, Vector3 home)
        {

            var car1 = FindRoot(CarName);
            var parts1 = FindRoot("CARPARTS");
            var db1 = FindRoot("Database");
            if (car1 == null || parts1 == null || db1 == null)
            {
                ModConsole.Warning("[MSC Duo] Satsuma, CARPARTS ou Database não encontrados; Satsuma 2 não criada.");
                return;
            }

            SeedSave(move, turn, home);
            ModParts.Reset();
            var map = Map = new Dictionary<Object, Object>();
            Original = car1;
            OriginalDatabase = db1;

            Clone = CloneRoot(car1, "SATSUMA_P2", move, turn, map);
            foreach (var joint in Clone.GetComponents<FixedJoint>()) Object.DestroyImmediate(joint);
            Parts = CloneRoot(parts1, "CARPARTS_P2", move, turn, map);
            Database = new GameObject("Database_P2");
            foreach (var name in DatabaseParts)
            {
                var child = db1.transform.Find(name);
                if (child == null) continue;
                var copy = CloneRoot(child.gameObject, name, Matrix4x4.identity, Quaternion.identity, map);
                copy.transform.parent = Database.transform;
            }

            var lift = snap ? SnapToGround() : 0f;
            var locals = Variables = CarLocals();
            int fsms = 0, remapped = 0, saves = 0, failed = 0, separated = 0, confined = 0, bodies = 0;
            foreach (var root in new[] { Clone, Parts, Database })
            {
                foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
                {
                    try
                    {
                        FsmTools.EnsureLoaded(fsm);
                        remapped += Remap(fsm, map);
                        saves += RedirectSaves(fsm);
                        foreach (var pair in locals) separated += FsmTools.RedirectGlobal(fsm, pair.Key, pair.Value);
                        bodies += PointJointsAtClone(fsm, car1);
                        if (root == Clone) confined += FsmTools.ConfineBroadcasts(fsm, Clone);
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

            ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma 2: {map.Count} objetos clonados, {fsms} FSMs ({failed} com erro), {remapped} referências redirecionadas, {saves} saves em {SaveFile}, {separated} usos de variáveis do carro separados, {confined} avisos restritos à Satsuma 2, {bodies} juntas apontadas para a Satsuma 2, altura ajustada em {lift:0.00} m pela base de '{lowest}'.");
            if (ModParts.Mods.Count > 0)
                ModConsole.Print($"<color=orange>[MSC Duo]</color> Satsuma 2 sem peças de mods: {ModParts.Removed} objetos tirados ({string.Join(", ", new List<string>(ModParts.Mods).ToArray())}): {string.Join("; ", ModParts.Names.ToArray())}.");
            CurrentVehicle.Setup(car1, parts1, Clone, Parts);
        }

        const float SeedReach = 30f;

        static void SeedSave(Matrix4x4 move, Quaternion turn, Vector3 home)
        {
            var folder = Application.persistentDataPath;
            var p2 = System.IO.Path.Combine(folder, SaveFile);
            var game = System.IO.Path.Combine(folder, GameSaveFile);
            if (System.IO.File.Exists(p2) || !System.IO.File.Exists(game)) return;
            try
            {
                System.IO.File.Copy(game, p2);
                var moved = MoveSeeded(move, turn, home);
                ModConsole.Print($"<color=orange>[MSC Duo]</color> {SaveFile} não existia; o jogador 2 começa com uma cópia do estado do jogador 1 ({moved} posições levadas da casa 1 para a casa 2).");
            }
            catch (System.Exception e)
            {
                if (System.IO.File.Exists(p2)) System.IO.File.Delete(p2);
                ModConsole.Warning($"[MSC Duo] não foi possível criar {SaveFile}: {e.Message}");
            }
        }

        static int MoveSeeded(Matrix4x4 move, Quaternion turn, Vector3 home)
        {
            var transformHash = ES2TypeManager.GetES2Type(typeof(Transform)).hash;
            var tags = new List<string>();
            using (var reader = ES2Reader.Create(SaveFile))
                foreach (var pair in reader.ReadAllHeaders())
                    if (pair.Value.collectionType == ES2Keys.Key._Null && pair.Value.valueType == transformHash) tags.Add(pair.Key);
            var probe = new GameObject("MscDuo_seed");
            var moved = 0;
            try
            {
                var t = probe.transform;
                foreach (var tag in tags)
                {
                    ES2.Load<Transform>(SaveFile + "?tag=" + tag, t);
                    var flat = t.position - home;
                    flat.y = 0f;
                    if (flat.magnitude > SeedReach) continue;
                    t.position = move.MultiplyPoint3x4(t.position);
                    t.rotation = turn * t.rotation;
                    ES2.Save(t, SaveFile + "?tag=" + tag);
                    moved++;
                }
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
            return moved;
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

        static string lowest = "";

        static bool TryBottom(GameObject car, out float bottom, out Bounds footprint)
        {
            bottom = float.MaxValue;
            footprint = new Bounds();
            var found = false;
            foreach (var renderer in car.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                var b = renderer.bounds;
                if (!found) footprint = b; else footprint.Encapsulate(b);
                if (b.min.y < bottom) { bottom = b.min.y; lowest = renderer.name; }
                found = true;
            }
            return found;
        }

        internal static GameObject CloneRoot(GameObject original, string name, Matrix4x4 move, Quaternion turn, Dictionary<Object, Object> map)
        {
            var position = move.MultiplyPoint3x4(original.transform.position);
            var rotation = turn * original.transform.rotation;
            var copy = (GameObject)Object.Instantiate(original, position, rotation);
            copy.name = name;
            MapHierarchy(original.transform, copy.transform, map);
            ModParts.Strip(copy);
            return copy;
        }

        internal static void MapHierarchy(Transform a, Transform b, Dictionary<Object, Object> map)
        {
            map[a.gameObject] = b.gameObject;
            var ca = a.GetComponents<Component>();
            var cb = b.GetComponents<Component>();
            for (int i = 0; i < ca.Length && i < cb.Length; i++)
                if (ca[i] != null && cb[i] != null) map[ca[i]] = cb[i];
            for (int i = 0; i < a.childCount && i < b.childCount; i++)
                MapHierarchy(a.GetChild(i), b.GetChild(i), map);
        }

        static int PointJointsAtClone(PlayMakerFSM fsm, GameObject car1)
        {
            var count = 0;
            foreach (var state in fsm.FsmStates)
                foreach (var action in state.Actions)
                {
                    if (action == null || !action.GetType().Name.Contains("Joint")) continue;
                    foreach (var field in action.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (!field.Name.ToLowerInvariant().Contains("connectedbody") && !field.Name.ToLowerInvariant().Contains("rigidbody")) continue;
                        var value = field.GetValue(action);
                        if (value is FsmGameObject go && !go.UseVariable && go.Value != null && (go.Value == car1 || go.Value.name == car1.name))
                        {
                            go.Value = Clone;
                            count++;
                        }
                        else if (value is FsmObject obj && !obj.UseVariable && obj.Value != null && (obj.Value == car1 || obj.Value == car1.GetComponent<Rigidbody>() || obj.Value.name == car1.name))
                        {
                            obj.Value = Clone.GetComponent<Rigidbody>();
                            count++;
                        }
                        else if (value is GameObject raw && (raw == car1 || raw.name == car1.name))
                        {
                            field.SetValue(action, Clone);
                            count++;
                        }
                    }
                }
            return count;
        }

        static Dictionary<string, NamedVariable> CarLocals()
        {
            var locals = new Dictionary<string, NamedVariable> { { "TheCar", new FsmGameObject("MscDuoTheCar") { Value = Clone } } };
            foreach (var name in CarGlobals)
            {
                var global = FsmVariables.GlobalVariables.FindVariable(name);
                if (global == null)
                {
                    ModConsole.Warning($"[MSC Duo] Satsuma 2: variável global '{name}' não encontrada.");
                    continue;
                }
                var type = global.GetType();
                var local = (NamedVariable)System.Activator.CreateInstance(type, "MscDuo" + name);
                var value = type.GetProperty("Value");
                if (value != null) value.SetValue(local, value.GetValue(global, null), null);
                locals[name] = local;
            }
            return locals;
        }

        internal static int Remap(PlayMakerFSM fsm, Dictionary<Object, Object> map)
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
                            case FsmProperty property:
                                if (property.TargetObject != null && !property.TargetObject.UseVariable) count += Swap(property.TargetObject, map);
                                if (property.ObjectParameter != null && !property.ObjectParameter.UseVariable) count += Swap(property.ObjectParameter, map);
                                if (property.GameObjectParameter != null && !property.GameObjectParameter.UseVariable) count += Swap(property.GameObjectParameter, map);
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

        internal static int RemapLists(GameObject root, Dictionary<Object, Object> map)
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

        internal static int RedirectSaves(PlayMakerFSM fsm)
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

        internal static int RedirectAutoSaves(GameObject root)
        {
            var count = 0;
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().Name != "ES2Auto") continue;
                var type = behaviour.GetType();
                var field = type.GetField("saveFile");
                if (field == null) continue;
                field.SetValue(behaviour, SaveFile);
                var init = type.GetMethod("InitializeSettings", BindingFlags.NonPublic | BindingFlags.Instance);
                if (init != null) init.Invoke(behaviour, null);
                count++;
            }
            return count;
        }

        internal static GameObject FindRoot(string name)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t.parent == null && t.name == name && t.hideFlags == HideFlags.None)
                    return t.gameObject;
            return null;
        }
    }
}
