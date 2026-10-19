import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
S = 70
X0, Y0 = 60, 110
W, Y_FRONT, Y_BACK = 24.0, 10.0, 16.0

img = Image.new("RGB", (int(W * S) + 120, int((Y_BACK - Y_FRONT) * S) + 330), (246, 244, 239))
d = ImageDraw.Draw(img)


def font(size, bold=False):
    for name in (("segoeuib.ttf" if bold else "segoeui.ttf"), "arial.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


TITLE, MID, SMALL = font(30, True), font(17, True), font(14)


def p(x, y):
    return X0 + x * S, Y0 + (Y_BACK - y) * S


def rect(x0, y0, x1, y1, fill, outline=(60, 60, 66), width=2):
    a, b = p(x0, y1), p(x1, y0)
    d.rectangle([a, b], fill=fill, outline=outline, width=width)


def text(x, y, s, f=SMALL, fill=(40, 40, 46)):
    u, v = p(x, y)
    w = d.textlength(s, font=f)
    d.text((u - w / 2, v - f.size / 2), s, font=f, fill=fill)


d.text((X0, 25), "MEZANINO — proposta", font=TITLE, fill=(30, 30, 34))
d.text((X0, 68), "parede do fundo em cima; guarda-corpo de vidro embaixo; tudo agrupado em 3 ambientes com tapete", font=SMALL, fill=(110, 110, 118))

rect(0, Y_FRONT, W, Y_BACK, (252, 251, 248), width=4)
rect(22.6, Y_FRONT, 23.8, 12.0, (150, 150, 158))
text(23.2, 11.0, "escada", SMALL)
a, b = p(0, Y_FRONT), p(22.6, Y_FRONT)
d.line([a, b], fill=(90, 150, 210), width=8)
text(11.3, 9.75, "guarda-corpo de vidro (vista para a oficina)", SMALL, (70, 120, 180))

WAIN = (120, 90, 60)
a, b = p(0.2, Y_BACK - 0.2), p(22.6, Y_BACK - 0.2)
d.line([a, b], fill=WAIN, width=6)
text(11.3, 16.25, "lambri de madeira até 1,1 m em todas as paredes do mezanino + pôsteres", SMALL, WAIN)

RUG = (196, 160, 130)
rect(1.4, 11.3, 6.4, 14.6, (225, 205, 180), outline=RUG, width=3)
rect(8.6, 11.0, 13.6, 14.6, (205, 200, 225), outline=(150, 140, 190), width=3)
rect(16.4, 10.9, 21.2, 13.4, (205, 222, 205), outline=(140, 175, 140), width=3)

SOFA, WOOD, METAL, GAME = (170, 150, 120), (150, 110, 75), (130, 135, 145), (190, 70, 70)
rect(2.5, 11.5, 5.2, 12.3, SOFA)
rect(2.5, 12.3, 3.3, 13.2, SOFA)
text(3.85, 11.9, "sofá em L", SMALL)
rect(3.4, 12.9, 4.6, 13.6, WOOD)
text(4.0, 13.25, "mesinha", SMALL)
rect(3.0, 15.3, 4.8, 15.75, WOOD)
text(3.9, 15.0, "rack + TV na parede", SMALL)
rect(1.5, 15.3, 2.6, 15.75, METAL)
rect(5.2, 15.3, 6.3, 15.75, METAL)
text(2.05, 14.95, "estante", SMALL)
text(5.75, 14.95, "estante", SMALL)
rect(0.35, 14.95, 1.15, 15.75, (60, 150, 200))
text(0.75, 14.6, "geladeira", SMALL)
d.ellipse([p(5.5, 12.1), p(5.9, 11.7)], fill=(240, 200, 90), outline=(60, 60, 66))
text(5.7, 11.45, "luminária", SMALL)
text(3.9, 10.55, "SALA / TV", MID)

rect(9.9, 12.25, 12.0, 13.4, (40, 120, 70))
text(10.95, 12.83, "sinuca", SMALL, (240, 240, 240))
a, b = p(10.2, 12.83), p(11.7, 12.83)
d.line([a, b], fill=(240, 200, 90), width=4)
text(10.95, 13.75, "pendente de sinuca em cima", SMALL)
for i, (name, color) in enumerate((("fliperama", GAME), ("caça-níquel", (200, 60, 40)), ("fliperama 2", (80, 80, 160)))):
    x = 9.0 + i * 0.95
    rect(x, 15.0, x + 0.8, 15.75, color)
    text(x + 0.4, 14.75 - (i % 2) * 0.3, name, SMALL)
text(10.4, 15.95, "letreiro neon acima dos jogos", SMALL, (200, 60, 160))
d.ellipse([p(13.0, 15.75), p(13.4, 15.55)], fill=(30, 30, 30))
text(13.2, 15.3, "dardos", SMALL)
d.ellipse([p(13.2, 11.7), p(13.9, 11.0)], fill=WOOD, outline=(60, 60, 66))
for sx, sy in ((12.9, 11.35), (14.2, 11.35)):
    d.ellipse([p(sx - 0.18, sy + 0.18), p(sx + 0.18, sy - 0.18)], fill=METAL)
text(13.55, 10.65, "mesa alta + banquetas", SMALL)
text(11.1, 10.35, "JOGOS", MID)

rect(17.4, 11.4, 19.4, 12.2, WOOD)
text(18.4, 11.8, "mesa (vista p/ oficina)", SMALL)
d.ellipse([p(18.2, 12.75), p(18.6, 12.35)], fill=(40, 40, 40))
text(18.4, 12.95, "cadeira", SMALL)
rect(15.6, 15.3, 19.2, 15.75, METAL)
text(17.4, 14.95, "prateleiras de peças", SMALL)
rect(19.6, 15.55, 21.4, 15.75, (235, 235, 235))
text(20.5, 15.25, "quadro de peças", SMALL)
rect(21.6, 14.9, 22.4, 15.75, METAL)
text(22.0, 14.6, "arquivo", SMALL)
text(18.8, 10.35, "ESCRITÓRIO", MID)

for x in range(0, 25, 2):
    u, v = p(x, Y_FRONT)
    d.line([(u, v + 25), (u, v + 33)], fill=(120, 120, 128), width=2)
    d.text((u - 6, v + 36), str(x), font=SMALL, fill=(120, 120, 128))

ly = Y0 + int((Y_BACK - Y_FRONT) * S) + 85
legend = [
    ("já temos", "sofá em L, TV, geladeira, sinuca, fliperama, caça-níquel"),
    ("faço por código", "lambri, tapetes, pendente de sinuca, estantes e prateleiras de aço, mesa alta e banquetas, letreiro neon"),
    ("você baixa", "mesinha de centro, rack de TV, luminária de pé, mesa e cadeira de escritório, quadro, arquivo, alvo de dardos, pôsteres, 2º fliperama (opcional)"),
]
for i, (k, v) in enumerate(legend):
    d.text((X0, ly + i * 28), k + ":", font=font(15, True), fill=(40, 40, 46))
    d.text((X0 + 150, ly + i * 28), v, font=SMALL, fill=(70, 70, 78))

img.save(os.path.join(HERE, "planta-mezanino.png"))
