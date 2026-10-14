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
        public override string Help => "duo_car  compara as variáveis da Satsuma 1 e da Satsuma 2";

        public override void Run(string[] args)
        {
            if (SatsumaCloner.Variables == null) { ModConsole.Error("Satsuma 2 não está ativa"); return; }
            var text = new StringBuilder("<color=orange>[MSC Duo]</color> Satsuma 1 | Satsuma 2");
            foreach (var pair in SatsumaCloner.Variables)
            {
                if (pair.Key == "TheCar") continue;
                var global = FsmVariables.GlobalVariables.FindVariable(pair.Key);
                text.Append($"\n  {pair.Key}: {Value(global)} | {Value(pair.Value)}");
            }
            text.Append($"\n  massa física: {Mass(GameObject.Find("SATSUMA(557kg, 248)"))} | {Mass(SatsumaCloner.Clone)}");
            ModConsole.Print(text.ToString());
        }

        static string Value(NamedVariable variable)
        {
            if (variable == null) return "-";
            var property = variable.GetType().GetProperty("Value");
            var value = property != null ? property.GetValue(variable, null) : null;
            return value is float f ? f.ToString("0.##") : value != null ? value.ToString() : "-";
        }

        static string Mass(GameObject car)
        {
            var body = car != null ? car.GetComponent<Rigidbody>() : null;
            return body != null ? body.mass.ToString("0") + " kg" : "-";
        }
    }
}
