using System.Collections.Generic;
using UnityEngine;

namespace MscDuo.Duplication
{
    internal class PixelCanvas
    {
        internal readonly int Width;
        internal readonly int Height;
        internal readonly Texture2D Texture;
        readonly Color32[] pixels;

        static readonly Dictionary<char, string> Font = new Dictionary<char, string>
        {
            { 'A', ".#.#.#####.##.#" }, { 'B', "##.#.###.#.###." }, { 'C', ".###..#..#...##" }, { 'D', "##.#.##.##.###." }, { 'E', "####..##.#..###" }, { 'F', "####..##.#..#.." }, { 'G', ".###..#.##.#.##" }, { 'H', "#.##.#####.##.#" }, { 'I', "###.#..#..#.###" }, { 'J', "..#..#..##.#.#." }, { 'K', "#.##.###.#.##.#" }, { 'L', "#..#..#..#..###" }, { 'M', "#.########.##.#" }, { 'N', "##.#.##.##.##.#" }, { 'O', ".#.#.##.##.#.#." }, { 'P', "##.#.###.#..#.." }, { 'Q', ".#.#.##.###..##" }, { 'R', "##.#.###.#.##.#" }, { 'S', ".###...#...###." }, { 'T', "###.#..#..#..#." }, { 'U', "#.##.##.##.####" }, { 'V', "#.##.##.##.#.#." }, { 'W', "#.##.########.#" }, { 'X', "#.##.#.#.#.##.#" }, { 'Y', "#.##.#.#..#..#." }, { 'Z', "###..#.#.#..###" }, { '0', "####.##.##.####" }, { '1', ".#.##..#..#.###" }, { '2', "##...#.#.#..###" }, { '3', "##...#.#...###." }, { '4', "#.##.####..#..#" }, { '5', "####..##...###." }, { '6', ".###..####.####" }, { '7', "###..#.#..#..#." }, { '8', "####.#####.####" }, { '9', "####.####..###." }, { '-', "......###......" }, { ':', "....#.....#...." }, { '!', ".#..#..#.....#." }, { '.', ".............#." },
        };

        internal PixelCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            pixels = new Color32[width * height];
            Texture = new Texture2D(width, height, TextureFormat.RGB24, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        }

        internal void Clear(Color32 c)
        {
            for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
        }

        internal void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            pixels[(Height - 1 - y) * Width + x] = c;
        }

        internal void Rect(int x, int y, int w, int h, Color32 c)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++) Set(i, j, c);
        }

        internal void Sprite(int x, int y, string[] rows, Color32 c, int scale = 1)
        {
            for (int j = 0; j < rows.Length; j++)
                for (int i = 0; i < rows[j].Length; i++)
                    if (rows[j][i] == '#') Rect(x + i * scale, y + j * scale, scale, scale, c);
        }

        internal int TextWidth(string text, int scale = 1) => text.Length * 4 * scale - scale;

        internal void Text(int x, int y, string text, Color32 c, int scale = 1)
        {
            foreach (var ch in text.ToUpperInvariant())
            {
                string glyph;
                if (Font.TryGetValue(ch, out glyph))
                    for (int k = 0; k < 15 && k < glyph.Length; k++)
                        if (glyph[k] == '#') Rect(x + (k % 3) * scale, y + (k / 3) * scale, scale, scale, c);
                x += 4 * scale;
            }
        }

        internal void Centered(int y, string text, Color32 c, int scale = 1) => Text((Width - TextWidth(text, scale)) / 2, y, text, c, scale);

        internal void Noise(System.Random rng)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                var v = (byte)rng.Next(256);
                pixels[i] = new Color32(v, v, v, 255);
            }
        }

        internal Color Average()
        {
            long r = 0, g = 0, b = 0;
            for (int i = 0; i < pixels.Length; i += 7) { r += pixels[i].r; g += pixels[i].g; b += pixels[i].b; }
            var n = (pixels.Length + 6) / 7 * 255f;
            return new Color(r / n, g / n, b / n);
        }

        internal void Apply()
        {
            Texture.SetPixels32(pixels);
            Texture.Apply(false);
        }
    }
}
