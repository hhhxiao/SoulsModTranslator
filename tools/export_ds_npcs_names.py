import os
import os, sys
import json
import xml.etree.ElementTree as ET
from bs4 import BeautifulSoup

DSC_TEXT_PATH = "C:\\Users\\xhy\\dev\\Dark-Souls-Documents\\"


def save_dict(path: str, dict):
    with open(path, "w", encoding="utf8") as f:
        json.dump(dict, f, ensure_ascii=False, indent=4)
        f.close()


def read_dialog(game: int):
    dialogue_html = os.path.join(DSC_TEXT_PATH, "dialogue{}.html".format(game))
    # lines = None
    content = None
    with open(dialogue_html, "r", encoding="utf8") as f:
        content = f.read()

    soup = BeautifulSoup(content, "html.parser")
    dic = {}
    for link in soup.find_all("a"):
        eng = str(link.img.get("src"))
        eng = eng.split("/")[-1].replace(".webp", "")
        chs = link.span.string
        dic[eng] = chs
    return dic


d1 = read_dialog(1)
d2 = read_dialog(2)
d3 = read_dialog(3)
ds = d1 | d2 | d3
save_dict(os.path.join("../glossaries/ds", "npc.json"), ds)
