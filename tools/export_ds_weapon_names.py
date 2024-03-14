import os
import os, sys
import json
import xml.etree.ElementTree as ET


DSC_TEXT_PATH = "C:\\Users\\xhy\\dev\\Dark-Souls-Documents\\"


def read_fmg_xml(path: str):
    kv = {}
    tree = ET.parse(path)
    root = tree.getroot()
    for t in root.findall("./text"):
        kv[int(t.get("id"))] = t.text
    return kv


def save_dict(path: str, dict):
    with open(path, "w", encoding="utf8") as f:
        json.dump(dict, f, ensure_ascii=False, indent=4)
        f.close()


def dump_armor_name(game: int):
    eng_path = os.path.join(
        DSC_TEXT_PATH, "text", "eng{}".format(game), "armor_name.xml"
    )
    chs_path = os.path.join(
        DSC_TEXT_PATH, "text", "chn{}".format(game), "armor_name.xml"
    )
    chs_items = read_fmg_xml(chs_path)
    eng_items = read_fmg_xml(eng_path)
    res = {}
    for k, v in eng_items.items():
        if k in chs_items:
            res[v.replace("##", "")] = chs_items[k].replace("##", "")
        else:
            print("Can not found key: {}".format(k))
    return res


armor_1 = dump_armor_name(1)
armor_2 = dump_armor_name(2)
armor_3 = dump_armor_name(3)
armors = armor_1 | armor_2 | armor_3
save_dict(os.path.join("../glossaries/ds", "armor.json"), armors)


## weapons
def dump_weapon_name(game: int):
    eng_path = os.path.join(
        DSC_TEXT_PATH, "text", "eng{}".format(game), "weapon_name.xml"
    )
    chs_path = os.path.join(
        DSC_TEXT_PATH, "text", "chn{}".format(game), "weapon_name.xml"
    )
    chs_items = read_fmg_xml(chs_path)
    eng_items = read_fmg_xml(eng_path)
    res = {}
    for k, v in eng_items.items():
        if k in chs_items:
            res[v.replace("##", "")] = chs_items[k].replace("##", "")
        else:
            print("Can not found key: {}".format(k))
    return res


weapon_1 = dump_weapon_name(1)
weapon_2 = dump_weapon_name(2)
weapon_3 = dump_weapon_name(3)
weapons = weapon_1 | weapon_2 | weapon_3
save_dict(os.path.join("../glossaries/ds", "weapon.json"), weapons)
