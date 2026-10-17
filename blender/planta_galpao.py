import os

from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "planta-galpao.png")

W, D = 24.0, 16.0
MEZ = 6.0
PX = 34
MARGIN = 90
GAP = 120

BG = (247, 245, 240)
INK = (30, 30, 34)
WALL = (40, 40, 46)
SOFT = (120, 120, 128)
STEEL = (20, 20, 22)
DYNO = (236, 196, 40)
LIFT = (240, 210, 70)
LOUNGE = (196, 170, 140)
GLASS = (90, 160, 210)
STAIR = (150, 150, 158)
WET = (170, 200, 220)
GATE = (220, 60, 50)

font = ImageFont.truetype("arialbd.ttf", 22)
mid = ImageFont.truetype("arialbd.ttf", 17)
small = ImageFont.truetype("arial.ttf", 15)
title = ImageFont.truetype("arialbd.ttf", 30)

panel_w = int(W * PX)
panel_h = int(D * PX)
img = Image.new("RGB", (MARGIN * 2 + panel_w * 2 + GAP, MARGIN * 2 + panel_h + 210), BG)
d = ImageDraw.Draw(img)


def frame(ox, oy):
    def p(x, y):
        return ox + x * PX, oy + (D - y) * PX
    return p


def rect(p, x0, y0, x1, y1, fill=None, outline=INK, width=2):
    a, b = p(x0, y1), p(x1, y0)
    d.rectangle([a, b], fill=fill, outline=outline, width=width)


def label(p, x, y, text, f=mid, color=INK):
    u, v = p(x, y)
    w, h = d.textbbox((0, 0), text, font=f)[2:]
    d.text((u - w / 2, v - h / 2), text, font=f, fill=color)


def shell(p, gates):
    rect(p, 0, 0, W, D, outline=WALL, width=7)
    for x0, x1 in gates:
        a, b = p(x0, 0), p(x1, 0)
        d.line([a, b], fill=BG, width=9)
        d.line([(a[0], a[1] + 10), (b[0], b[1] + 10)], fill=GATE, width=6)
    for x in (0, 6, 12, 18, 24):
        for y in (0, D):
            u, v = p(x, y)
            d.rectangle([u - 6, v - 6, u + 6, v + 6], fill=STEEL)
    for y in (6, 10):
        for x in (0, 24):
            u, v = p(x, y)
            d.rectangle([u - 6, v - 6, u + 6, v + 6], fill=STEEL)


def dims(p):
    a, b = p(0, 0), p(W, 0)
    y = a[1] + 46
    d.line([(a[0], y), (b[0], y)], fill=SOFT, width=2)
    for u in (a[0], b[0]):
        d.line([(u, y - 8), (u, y + 8)], fill=SOFT, width=2)
    d.text(((a[0] + b[0]) / 2 - 28, y + 6), "24 m", font=mid, fill=SOFT)
    a, b = p(0, 0), p(0, D)
    x = a[0] - 40
    d.line([(x, a[1]), (x, b[1])], fill=SOFT, width=2)
    for v in (a[1], b[1]):
        d.line([(x - 8, v), (x + 8, v)], fill=SOFT, width=2)
    d.text((x - 52, (a[1] + b[1]) / 2 - 10), "16 m", font=mid, fill=SOFT)


ox1, oy = MARGIN, MARGIN + 40
p1 = frame(ox1, oy)
d.text((ox1, oy - 70), "TÉRREO", font=title, fill=INK)
d.text((ox1 + 150, oy - 62), "pé-direito livre 3,2 m sob o mezanino, 6–8 m no resto", font=small, fill=SOFT)
gates = [(2.0, 7.0), (17.0, 22.0)]

rect(p1, 2.0, 1.0, 7.0, 9.5, fill=(250, 238, 200))
rect(p1, 3.1, 4.0, 5.9, 5.4, fill=DYNO)
rect(p1, 3.1, 6.8, 5.9, 7.6, fill=DYNO)
label(p1, 4.5, 8.6, "DINAMÔMETRO 1")
label(p1, 4.5, 2.2, "baia 5 x 8,5 m", small, SOFT)

rect(p1, 17.0, 1.0, 22.0, 9.5, fill=(250, 238, 200))
rect(p1, 18.1, 4.0, 20.9, 5.4, fill=DYNO)
rect(p1, 18.1, 6.8, 20.9, 7.6, fill=DYNO)
label(p1, 19.5, 8.6, "DINAMÔMETRO 2")
label(p1, 19.5, 2.2, "baia 5 x 8,5 m", small, SOFT)

rect(p1, 9.0, 1.5, 15.0, 8.5, fill=(245, 245, 245), outline=SOFT)
for x in (9.3, 14.7):
    for y in (1.8, 8.2):
        u, v = p1(x, y)
        d.rectangle([u - 7, v - 7, u + 7, v + 7], fill=STEEL)
rect(p1, 9.8, 2.0, 11.2, 8.0, fill=LIFT, outline=SOFT)
rect(p1, 12.8, 2.0, 14.2, 8.0, fill=LIFT, outline=SOFT)
label(p1, 12.0, 9.2, "ELEVADOR 4 COLUNAS", small)
label(p1, 12.0, 1.0, "área de manobra", small, SOFT)

rect(p1, 0.0, 10.0, W, D, fill=None, outline=SOFT, width=1)
for i in range(int(W / 0.8)):
    x = i * 0.8
    a, b = p1(x, 10.0), p1(min(x + 0.4, W), 10.0)
    d.line([a, b], fill=SOFT, width=2)
label(p1, 18.5, 10.45, "projeção do mezanino", small, SOFT)

rect(p1, 22.6, 4.0, 23.8, 9.8, fill=STAIR, outline=SOFT)
for i in range(16):
    y = 4.0 + i * 0.36
    a, b = p1(22.6, y), p1(23.8, y)
    d.line([a, b], fill=(110, 110, 118), width=1)
u, v = p1(23.2, 4.3)
u2, v2 = p1(23.2, 9.6)
d.line([(u, v), (u2, v2)], fill=INK, width=3)
d.polygon([(u2, v2), (u2 - 7, v2 + 12), (u2 + 7, v2 + 12)], fill=INK)
label(p1, 22.1, 7.0, "sobe", small)

rect(p1, 0.0, 10.0, 6.0, D, fill=(225, 225, 228))
label(p1, 3.0, 13.6, "BANCADA")
label(p1, 3.0, 12.6, "ferramentas na parede", small)

rect(p1, 7.0, 10.8, 15.0, 15.4, fill=LOUNGE)
label(p1, 11.0, 13.6, "LOUNGE")
label(p1, 11.0, 12.6, "sofás, mesa, tapete", small)

rect(p1, 18.0, 12.0, 21.0, D, fill=WET)
label(p1, 19.5, 14.3, "BANHEIRO", small)
rect(p1, 21.0, 12.0, W, D, fill=(210, 210, 214))
label(p1, 22.5, 14.3, "DEPÓSITO", small)

u, v = p1(0, 13.0)
d.rectangle([u - 5, v - 18, u + 5, v + 18], fill=BG)
label(p1, 1.6, 15.4, "porta lateral", small, SOFT)

shell(p1, gates)
for x0, x1 in gates:
    label(p1, (x0 + x1) / 2, -1.6, "PORTÃO 5 m", mid, GATE)
dims(p1)

ox2 = MARGIN + panel_w + GAP
p2 = frame(ox2, oy)
d.text((ox2, oy - 70), "MEZANINO  (+3,2 m)", font=title, fill=INK)
d.text((ox2 + 330, oy - 62), "só o fundo; a frente fica em vão livre", font=small, fill=SOFT)

rect(p2, 0.0, 0.0, W, 10.0, fill=(236, 236, 232), outline=None, width=0)
label(p2, 12.0, 5.5, "VÃO LIVRE (vista para o térreo)", font, SOFT)
label(p2, 12.0, 4.4, "ventiladores industriais e luminárias no teto", small, SOFT)

rect(p2, 0.0, 10.0, W, D, fill=(250, 250, 248))
a, b = p2(0.0, 10.0), p2(22.6, 10.0)
d.line([a, b], fill=GLASS, width=7)
label(p2, 11.0, 9.4, "guarda-corpo de vidro", small, GLASS)

rect(p2, 22.6, 4.0, 23.8, 10.0, fill=STAIR, outline=SOFT)
label(p2, 22.1, 7.0, "chega", small)

rect(p2, 1.0, 11.0, 9.0, 15.2, fill=LOUNGE)
label(p2, 5.0, 13.6, "SALA / TV")
label(p2, 5.0, 12.6, "sofá em L, TV na parede", small)
rect(p2, 10.0, 11.0, 15.0, 15.2, fill=(220, 210, 230))
label(p2, 12.5, 13.6, "JOGOS")
label(p2, 12.5, 12.6, "fliperama, sinuca", small)
rect(p2, 16.0, 11.0, 21.5, 15.2, fill=(210, 225, 210))
label(p2, 18.75, 13.6, "ESCRITÓRIO")
label(p2, 18.75, 12.6, "mesa, quadro de peças", small)

shell(p2, [])
dims(p2)

ly = oy + panel_h + 110
items = [
    (DYNO, "dinamômetro (clonado do jogo)"),
    (LIFT, "elevador de 4 colunas"),
    (LOUNGE, "área de estar"),
    (STAIR, "escada metálica 1,2 m, 16 degraus"),
    (GLASS, "guarda-corpo de vidro"),
    (STEEL, "pilares de aço a cada 6 m"),
    (GATE, "portões seccionais 5 x 4,5 m"),
]
x = MARGIN
for color, text in items:
    d.rectangle([x, ly, x + 22, ly + 22], fill=color, outline=INK)
    d.text((x + 30, ly + 1), text, font=small, fill=INK)
    x += 30 + d.textbbox((0, 0), text, font=small)[2] + 34
d.text((MARGIN, ly + 44), "Frente (portões) virada para as duas casas.  Telhado em duas águas: 6 m no beiral, 8 m na cumeeira.  Escala: 1 quadrado de pilar = 6 m.", font=small, fill=SOFT)
img.save(OUT)
print(OUT, img.size)
