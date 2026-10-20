import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
S = 120
X_MIN, X_MAX, Y_MIN, Y_MAX = 8.5, 21.2, 9.6, 16.0
X0, Y0 = 60, 110

img = Image.new("RGB", (int((X_MAX - X_MIN) * S) + 120, int((Y_MAX - Y_MIN) * S) + 300), (246, 244, 239))
d = ImageDraw.Draw(img)


def font(size, bold=False):
    for name in (("segoeuib.ttf" if bold else "segoeui.ttf"), "arial.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


TITLE, MID, SMALL = font(30, True), font(18, True), font(15)


def p(x, y):
    return X0 + (x - X_MIN) * S, Y0 + (Y_MAX - y) * S


def rect(x0, y0, x1, y1, fill, outline=(60, 60, 66), width=2):
    a, b = p(x0, y1), p(x1, y0)
    d.rectangle([a, b], fill=fill, outline=outline, width=width)


def circle(x, y, r, fill):
    a, b = p(x - r, y + r), p(x + r, y - r)
    d.ellipse([a, b], fill=fill, outline=(60, 60, 66), width=2)


def text(x, y, s, f=SMALL, fill=(40, 40, 46)):
    u, v = p(x, y)
    w = d.textlength(s, font=f)
    d.text((u - w / 2, v - f.size / 2), s, font=f, fill=fill)


d.text((X0, 25), "CANTO DO BAR — proposta (térreo, embaixo do mezanino)", font=TITLE, fill=(30, 30, 34))
d.text((X0, 68), "parede do fundo em cima; pé-direito 2,95 m; cinza = o que já existe, colorido = novo", font=SMALL, fill=(110, 110, 118))

rect(X_MIN, Y_MIN, X_MAX, 15.8, (252, 251, 248), outline=None)
a, b = p(X_MIN, 15.8), p(X_MAX, 15.8)
d.line([a, b], fill=(60, 60, 66), width=6)
text(14.8, 15.95, "parede do fundo", SMALL, (110, 110, 118))
for x in range(9, 22):
    a, b = p(x, 10), p(x + 0.5, 10)
    d.line([a, b], fill=(150, 150, 160), width=2)
text(10.2, 9.8, "borda do mezanino (pilares a cada 6 m)", SMALL, (120, 120, 130))
for x in (12, 18):
    rect(x - 0.15, 10, x + 0.15, 10.3, (40, 40, 44))

OLD = (205, 205, 210)
rect(9.0, 11.6, 13.0, 15.7, (225, 214, 196), outline=(170, 150, 120))
rect(9.3, 13.0, 12.7, 15.6, OLD); text(11, 14.4, "sofá grande", SMALL)
rect(10.5, 11.8, 11.5, 12.8, OLD); text(11, 12.3, "mesinha", SMALL)
circle(8.65, 14.9, 0.18, OLD)
circle(13.3, 15.3, 0.28, (120, 170, 110)); text(13.3, 14.85, "planta", SMALL)
text(11, 11.35, "LOUNGE (já existe)", MID, (110, 110, 118))
rect(18.0, 12.0, 21.0, 15.8, OLD); text(19.5, 14.0, "BANHEIRO", MID, (110, 110, 118))
rect(18.0, 12.0, 18.12, 15.8, (90, 90, 96), outline=None)
a, b = p(18.9, 12.06), p(19.8, 12.06)
d.line([a, b], fill=(252, 251, 248), width=6)
text(19.35, 11.75, "porta", SMALL)

WOOD, BAR, NEON, STOOL, FRIDGE = (196, 160, 118), (120, 80, 50), (230, 60, 170), (170, 40, 40), (60, 150, 200)
rect(13.9, 13.0, 17.85, 15.8, (232, 214, 190), outline=(180, 150, 110))
text(15.9, 15.62, "piso de tábuas só na área do bar", SMALL, (150, 120, 80))
rect(14.1, 15.35, 16.9, 15.78, BAR)
text(15.5, 15.15, "bar de fundo: balcão 0,9 m + 2 prateleiras de garrafas e espelho", SMALL, (90, 60, 40))
rect(17.05, 15.0, 17.8, 15.78, FRIDGE)
text(17.43, 14.75, "geladeira de", SMALL, (40, 100, 150))
text(17.43, 14.55, "cerveja (gela)", SMALL, (40, 100, 150))
rect(14.1, 13.85, 16.6, 14.45, BAR)
text(15.35, 14.15, "balcão 1,1 m (com chopeira)", SMALL, (250, 240, 230))
circle(14.9, 14.15, 0.09, (200, 200, 205))
for x in (14.5, 15.35, 16.2):
    circle(x, 13.45, 0.19, STOOL)
text(15.35, 13.0, "3 banquetas altas", SMALL, (150, 40, 40))
text(16.95, 14.6, "entrada", SMALL, (110, 110, 118))
text(16.95, 14.4, "do balcão", SMALL, (110, 110, 118))
a, b = p(14.3, 15.79), p(16.7, 15.79)
d.line([a, b], fill=NEON, width=8)
text(15.5, 16.25, "neon \"BAR\" na parede, acima das prateleiras", SMALL, NEON)
for x in (14.6, 15.35, 16.1):
    circle(x, 14.15, 0.06, (250, 210, 90))
text(15.35, 13.65, "3 pendentes sobre o balcão", SMALL, (180, 140, 40))

for x in range(9, 22):
    u, v = p(x, Y_MIN)
    d.line([(u, v + 20), (u, v + 28)], fill=(120, 120, 128), width=2)
    d.text((u - 8, v + 30), str(x), font=SMALL, fill=(120, 120, 128))

ly = Y0 + int((Y_MAX - Y_MIN) * S) + 80
legend = [
    ("faço por código", "balcão, bar de fundo com prateleiras e garrafas, espelho, chopeira, banquetas, neon BAR, pendentes, piso de tábuas"),
    ("do que já temos", "geladeira da Pepsi (2ª unidade) gelando cerveja, com porta de vidro e luz interna"),
    ("passagem", "0,9 m livre atrás do balcão; uns 3 m livres na frente das banquetas; lounge continua ao lado"),
]
for i, (k, v) in enumerate(legend):
    d.text((X0, ly + i * 30), k + ":", font=font(16, True), fill=(40, 40, 46))
    d.text((X0 + 170, ly + i * 30), v, font=SMALL, fill=(70, 70, 78))
img.save(os.path.join(HERE, "planta-bar.png"))
