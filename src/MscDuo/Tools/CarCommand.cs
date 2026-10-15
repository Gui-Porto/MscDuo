using System.Text;
using HutongGames.PlayMaker;
using MSCLoader;
using MscDuo.Duplication;
using UnityEngine;

namespace MscDuo.Tools
{
    internal class CarCommand : ConsoleCommand
    {
        public override string Name => "duo_car";
        public override string Help => "duo_car [saves | fisica]  compara as variáveis da Satsuma 1 e da Satsuma 2, ou lista os saves automáticos";

        public override void Run(string[] args)
        {
            if (args.Length > 0 && args[0] == "saves") { ListAutoSaves(); return; }
            if (args.Length > 0 && args[0] == "fisica") { PhysicsDiff.Run(System.IO.Path.Combine(MscDuo.Instance.SettingsFolder, "dumps")); return; }
            if (SatsumaCloner.Variables == null) { ModConsole.Error("Satsuma 2 não está ativa"); return; }
            var text = new StringBuilder("<color=orange>[MSC Duo]</color> Satsuma 1 | Satsuma 2");
            foreach (var pair in SatsumaCloner.Variables)
            {
                if (pair.Key == "TheCar") continue;
                var global = FsmVariables.GlobalVariables.FindVariable(pair.Key);
                text.Append($"\n  {pair.Key}: {Value(global)} | {Value(pair.Value)}");
            }
            var car1 = GameObject.Find("SATSUMA(557kg, 248)");
            var current = CurrentVehicle.Current == 0 ? "nenhuma Satsuma" : "Satsuma " + CurrentVehicle.Current;
            text.Append($"\n  veículo atual: {current} (jogo diz '{CurrentVehicle.GlobalValue}')");
            text.Append($"\n  massa física: {Mass(car1)} | {Mass(SatsumaCloner.Clone)}");
            text.Append($"\n  física: {Physics(car1)} | {Physics(SatsumaCloner.Clone)}");
            ModConsole.Print(text.ToString());
        }

        static string Value(NamedVariable variable)
        {
            if (variable == null) return "-";
            var property = variable.GetType().GetProperty("Value");
            var value = property != null ? property.GetValue(variable, null) : null;
            return value is float f ? f.ToString("0.##") : value != null ? value.ToString() : "-";
        }

        static void ListAutoSaves()
        {
            var text = new StringBuilder("<color=orange>[MSC Duo]</color> ES2Auto na cena:");
            foreach (var behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            {
                if (behaviour == null || behaviour.GetType().Name != "ES2Auto" || behaviour.gameObject.hideFlags != HideFlags.None) continue;
                var type = behaviour.GetType();
                var file = type.GetField("saveFile").GetValue(behaviour);
                var tag = type.GetField("uniqueTag").GetValue(behaviour);
                var isPlayMaker = type.GetField("isPlayMaker").GetValue(behaviour);
                var onDisable = type.GetField("saveOnDisable").GetValue(behaviour);
                var settings = type.GetField("settings").GetValue(behaviour);
                text.Append($"\n  {Path(behaviour.transform)}  arquivo='{file}' tag='{tag}' playmaker={isPlayMaker} aoDesligar={onDisable} config={(settings != null)}");
            }
            ModConsole.Print(text.ToString());
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

        static string Physics(GameObject car)
        {
            var body = car != null ? car.GetComponent<Rigidbody>() : null;
            if (body == null) return "-";
            var joints = car.GetComponents<Joint>().Length;
            return $"kinematic={body.isKinematic} dormindo={body.IsSleeping()} travas={body.constraints} arrasto={body.drag:0.##} camada={LayerMask.LayerToName(car.layer)} tag={car.tag} juntas={joints} vel={body.velocity.magnitude:0.##}";
        }

        static string Mass(GameObject car)
        {
            var body = car != null ? car.GetComponent<Rigidbody>() : null;
            return body != null ? body.mass.ToString("0") + " kg" : "-";
        }
    }
}
