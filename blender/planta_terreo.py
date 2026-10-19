import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
S = 62
X0, Y0 = 60, 110
W, Y_FRONT, Y_BACK = 24.0, 0.0, 16.0

img = Image.new("RGB", (int(W * S) + 120, int((Y_BACK - Y_FRONT) * S) + 300), (246, 244, 239))
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



d.text((X0, 25), "TÉRREO — proposta", font=TITLE, fill=(30, 30, 34))
d.text((X0, 68), "portão embaixo; mezanino a partir de y=10 (linha tracejada); cinza = o que já existe, colorido = novo", font=SMALL, fill=(110, 110, 118))

rect(0, 0, W, 16, (252, 251, 248), width=4)
a, b = p(2, 0), p(22, 0)
d.line([a, b], fill=(220, 60, 60), width=8)
text(12, -0.5, "portão de enrolar 20 m", SMALL, (200, 50, 50))
for x in range(0, 24):
    a, b = p(x, 10), p(x + 0.5, 10)
    d.line([a, b], fill=(150, 150, 160), width=2)
text(19.0, 10.3, "projeção do mezanino", SMALL, (120, 120, 130))

OLD = (205, 205, 210)
rect(2.75, 2.2, 6.25, 10.0, OLD); text(4.5, 6, "DINAMÔMETRO 1", MID, (90, 90, 96))
rect(10.0, 3.75, 14.0, 8.25, OLD); text(12, 6, "ELEVADOR", MID, (90, 90, 96))
rect(17.75, 2.2, 21.25, 10.0, OLD); text(19.5, 6, "DINAMÔMETRO 2", MID, (90, 90, 96))
rect(22.6, 4.0, 23.8, 10.0, (150, 150, 158)); text(23.2, 7, "escada", SMALL)
rect(18.0, 12.0, 21.0, 16, OLD); text(19.5, 14, "BANHEIRO", SMALL)
rect(21.0, 12.0, 24, 16, OLD); text(22.5, 14, "DEPÓSITO", SMALL)
rect(0.7, 15.03, 4.4, 15.77, OLD); text(2.55, 14.75, "bancada", SMALL)
rect(4.56, 15.25, 5.94, 15.75, OLD); text(5.25, 14.9, "armário", SMALL)
rect(9.3, 13.0, 12.7, 15.6, OLD); text(11, 14.3, "sofá grande", SMALL)
for x in (6, 12, 18):
    rect(x - 0.15, 10, x + 0.15, 10.3, (40, 40, 44))
a, b = p(0, 12.5), p(0, 13.5)
d.line([a, b], fill=(252, 251, 248), width=8)
text(1.0, 13.0, "porta lateral", SMALL)

NEW_W, NEW_G, NEW_B, NEW_Y = (200, 120, 60), (90, 150, 90), (90, 120, 190), (210, 170, 60)
rect(0.25, 10.8, 0.85, 12.2, NEW_W); text(1.5, 11.5, "compressor + mangueira", SMALL, NEW_W)
rect(6.3, 14.9, 7.4, 15.75, NEW_W); text(6.85, 14.5, "lavadora de peças", SMALL, NEW_W)
rect(6.4, 11.4, 7.3, 12.3, NEW_W); text(6.85, 11.0, "cavalete de motor", SMALL, NEW_W)
d.ellipse([p(2.1, 15.0), p(2.4, 14.75)], fill=NEW_Y); text(2.25, 14.4, "carregador de bateria (do jogo)", SMALL, NEW_Y)
rect(3.6, 11.0, 4.3, 11.6, NEW_Y); text(3.95, 10.75, "macaco (do jogo)", SMALL, NEW_Y)
rect(0.25, 1.5, 0.8, 5.5, NEW_G); text(1.4, 3.5, "rack de pneus", SMALL, NEW_G)
for y in (6.4, 7.1):
    d.ellipse([p(0.3, y + 0.3), p(0.9, y - 0.3)], fill=NEW_G)
text(1.5, 6.75, "tambores de óleo", SMALL, NEW_G)
rect(0.25, 8.3, 0.45, 8.6, (220, 40, 40)); text(1.2, 8.45, "extintor", SMALL, (200, 40, 40))
rect(0.3, 9.0, 0.7, 9.4, NEW_G); text(1.1, 9.2, "lixeira", SMALL, NEW_G)
rect(9.0, 12.4, 13.0, 15.7, None, outline=NEW_B, width=3); text(11, 12.65, "tapete + mesinha + luminária", SMALL, NEW_B)
text(11, 15.95, "relógio de parede (hora do jogo)", SMALL, NEW_B)
for i in range(4):
    rect(14.0 + i * 0.55, 15.3, 14.5 + i * 0.55, 15.75, NEW_B)
text(15.1, 14.95, "armários (lockers)", SMALL, NEW_B)
rect(14.2, 12.2, 16.6, 12.6, NEW_B); text(15.4, 11.9, "banco", SMALL, NEW_B)
rect(16.6, 15.7, 17.8, 15.78, NEW_B); text(17.2, 15.4, "quadro de serviços", SMALL, NEW_B)
rect(22.7, 4.4, 23.7, 9.6, None, outline=NEW_G, width=3); text(22.0, 9.0, "pallets e galões", SMALL, NEW_G)
rect(23.4, 0.4, 23.75, 0.8, (220, 40, 40)); text(22.9, 1.1, "extintor", SMALL, (200, 40, 40))
rect(22.4, 0.4, 22.9, 1.0, NEW_G); text(22.6, 1.6, "lixeira", SMALL, NEW_G)
text(12, 1.1, "pôsteres de carros/peças nas paredes laterais", SMALL, (140, 90, 160))

ly = Y0 + int(16 * S) + 70
legend = [
    ("já temos", "dinamômetros, elevador, bancada, armário, sofá grande, plantas, banheiro, depósito"),
    ("do próprio jogo", "carregador de bateria (funciona) e macaco hidráulico da oficina (funciona)"),
    ("faço por código", "rack de pneus, tambores, extintores, lixeiras, tapete, relógio com a hora do jogo, armários, banco, quadro, pallets"),
    ("você baixa", "compressor de ar, lavadora de peças, cavalete de motor, pôsteres (imagens)"),
]
for i, (k, v) in enumerate(legend):
    d.text((X0, ly + i * 28), k + ":", font=font(15, True), fill=(40, 40, 46))
    d.text((X0 + 160, ly + i * 28), v, font=SMALL, fill=(70, 70, 78))
img.save(os.path.join(HERE, "planta-terreo.png"))
