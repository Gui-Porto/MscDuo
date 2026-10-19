using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MSCLoader;

namespace MscDuo.Duplication
{
    internal static class WarehouseConfig
    {
        const string FileName = "galpao.txt";
        const string DevFileName = "pasta_dev.txt";

        static readonly string[][] Defaults =
        {
            new[] { "interruptor_x", "0", "afastamento extra da parede, em metros (o normal vem do interruptor da cozinha)" },
            new[] { "interruptor_portao1_x", "1.6", "interruptor à esquerda do portão: distância da parede esquerda" },
            new[] { "interruptor_portao2_x", "22.4", "interruptor à direita do portão: distância da parede esquerda" },
            new[] { "interruptor_y", "12.33", "segundo interruptor, na porta lateral: distância da frente (portões)" },
            new[] { "interruptor_altura", "1.3", "altura do chão" },
            new[] { "interruptor_giro", "0", "giro extra em graus, em volta do eixo vertical" },
            new[] { "luz_alta_intensidade", "1.4", "lâmpadas do salão alto" },
            new[] { "luz_alta_alcance", "14", "" },
            new[] { "luz_baixa_intensidade", "1.0", "lâmpadas embaixo do mezanino" },
            new[] { "luz_baixa_alcance", "7", "" },
            new[] { "luz_mezanino_intensidade", "1.0", "lâmpadas em cima do mezanino" },
            new[] { "luz_mezanino_alcance", "9", "" },
            new[] { "patio_arvores", "12", "metros sem árvores na frente dos portões" },
            new[] { "dinamometro1_fim", "10", "dinamômetro 1 (esquerda): onde termina a ponta do fundo, medido a partir do portão" },
            new[] { "dinamometro2_fim", "10", "dinamômetro 2 (direita): onde termina a ponta do fundo (a porta do banheiro fica em 12)" },
            new[] { "cacaniquel_x", "14.4", "caça-níquel no mezanino, encostado no fundo: distância da parede esquerda" },
            new[] { "elevador_y", "6", "elevador de carro (centro): distância do meio dele até o portão" },
            new[] { "luz_externa_sol", "0.1", "luz externa no automático: acende quando o sol fica abaixo desta altura (0 = horizonte, maior = acende mais cedo)" },
            new[] { "portao_velocidade", "0.45", "velocidade dos portões de enrolar, em metros por segundo" },
            new[] { "portao_volume", "1", "volume dos sons dos portões (0 a 2)" },
        };

        static readonly Dictionary<string, float> values = new Dictionary<string, float>();

        static string Folder => MscDuo.Instance.SettingsFolder;

        internal static string DevFolder { get; private set; }

        internal static void Load()
        {
            values.Clear();
            foreach (var d in Defaults) values[d[0]] = float.Parse(d[1], CultureInfo.InvariantCulture);
            var path = Path.Combine(Folder, FileName);
            if (!File.Exists(path)) Write(path);
            foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                float value;
                if (float.TryParse(line.Substring(eq + 1).Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    values[line.Substring(0, eq).Trim()] = value;
            }

            DevFolder = null;
            var dev = Path.Combine(Folder, DevFileName);
            if (File.Exists(dev))
            {
                var folder = File.ReadAllText(dev, Encoding.UTF8).Trim();
                if (Directory.Exists(folder)) DevFolder = folder;
                else ModConsole.Warning($"[MSC Duo] pasta de desenvolvimento '{folder}' não existe; usando o galpão da DLL.");
            }
        }

        internal static float Get(string key)
        {
            float value;
            return values.TryGetValue(key, out value) ? value : 0f;
        }

        static void Write(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# MSC Duo - ajustes do galpão. Mude um número e rode duo_reload no console.");
            foreach (var d in Defaults)
            {
                if (d[2].Length > 0) sb.AppendLine("# " + d[2]);
                sb.AppendLine(d[0] + " = " + d[1]);
            }
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
    }
}
