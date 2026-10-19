using System;
using System.IO;
using MSCLoader;
using MscDuo.Duplication;
using UnityEngine;

namespace MscDuo.Tools
{
    internal class PhotoCommand : ConsoleCommand
    {
        const int Width = 1280;
        const int Height = 720;

        public override string Name => "duo_foto";
        public override string Help => "duo_foto [luz | aqui]  fotografa o interruptor do galpão de vários ângulos (ou a sua visão) e salva em dumps/fotos";

        public override void Run(string[] args)
        {
            var folder = Path.Combine(Path.Combine(Path.Combine(MscDuo.Instance.SettingsFolder, "dumps"), "fotos"), DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(folder);
            var what = args.Length > 0 ? args[0] : "luz";
            if (what == "aqui")
            {
                var main = Camera.main;
                if (main == null) { ModConsole.Error("[MSC Duo] câmera não encontrada"); return; }
                Save(folder, "visao.png", main.transform.position, main.transform.rotation, false);
                ModConsole.Print($"<color=orange>[MSC Duo]</color> foto salva em {folder}");
                return;
            }

            var target = Warehouse.LightSwitch;
            if (target == null) { ModConsole.Error("[MSC Duo] interruptor do galpão não existe"); return; }
            var center = Center(target);
            var walls = Warehouse.Root != null ? Warehouse.Root.transform.Find("Galpao_Paredes") : null;
            var shots = new[]
            {
                new { name = "1_frente_perto", dir = Vector3.forward, distance = 0.5f, hideWalls = false },
                new { name = "2_frente_longe", dir = Vector3.forward, distance = 1.5f, hideWalls = false },
                new { name = "3_lado_esquerdo", dir = (Vector3.forward + Vector3.left).normalized, distance = 0.7f, hideWalls = false },
                new { name = "4_lado_direito", dir = (Vector3.forward + Vector3.right).normalized, distance = 0.7f, hideWalls = false },
                new { name = "5_sem_parede_frente", dir = Vector3.forward, distance = 0.5f, hideWalls = true },
                new { name = "6_sem_parede_atras", dir = Vector3.back, distance = 0.5f, hideWalls = true },
            };
            foreach (var shot in shots)
            {
                var wasActive = walls != null && walls.gameObject.activeSelf;
                if (shot.hideWalls && walls != null) walls.gameObject.SetActive(false);
                var position = center + shot.dir * shot.distance;
                Save(folder, shot.name + ".png", position, Quaternion.LookRotation(center - position), true);
                if (shot.hideWalls && walls != null) walls.gameObject.SetActive(wasActive);
            }
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            var info = $"interruptor em {HouseCloner.Format(target.position)}, centro visual {HouseCloner.Format(center)}, rotação {HouseCloner.Format(target.eulerAngles)}, ativo {target.gameObject.activeInHierarchy}";
            foreach (var r in renderers)
                info += $"\n  {r.name}: ligado {r.enabled}, ativo {r.gameObject.activeInHierarchy}, tamanho {HouseCloner.Format(r.bounds.size)}, centro {HouseCloner.Format(r.bounds.center)}, material {(r.sharedMaterial != null ? r.sharedMaterial.name + " / " + r.sharedMaterial.shader.name : "nenhum")}, camada {LayerMask.LayerToName(r.gameObject.layer)}";
            File.WriteAllText(Path.Combine(folder, "info.txt"), info);
            ModConsole.Print($"<color=orange>[MSC Duo]</color> {shots.Length} fotos do interruptor salvas em {folder}");
        }

        static Vector3 Center(Transform t)
        {
            var first = true;
            var bounds = new Bounds(t.position, Vector3.zero);
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            return bounds.center;
        }

        static void Save(string folder, string file, Vector3 position, Quaternion rotation, bool lamp)
        {
            var go = new GameObject("MscDuo_PhotoCamera");
            var rt = new RenderTexture(Width, Height, 24);
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.fieldOfView = 50f;
                cam.nearClipPlane = 0.02f;
                cam.farClipPlane = 500f;
                cam.transform.position = position;
                cam.transform.rotation = rotation;
                cam.targetTexture = rt;
                if (lamp)
                {
                    var light = go.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.range = 4f;
                    light.intensity = 1.5f;
                }
                cam.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(Path.Combine(folder, file), tex.EncodeToPNG());
                UnityEngine.Object.Destroy(tex);
            }
            finally
            {
                UnityEngine.Object.Destroy(go);
                rt.Release();
                UnityEngine.Object.Destroy(rt);
            }
        }
    }
}
