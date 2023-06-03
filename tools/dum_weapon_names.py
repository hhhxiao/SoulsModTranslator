import json
from collections import Counter

WEAPON_NAME_XML = "..\\db\\partial\\WeaponName.fmg.xml.json"


CHANGES = ["厚重", "锋利", "优质", "火焰", "焰术", "雷电", "神圣", "魔力", "寒冷", "毒", "血", "神秘"]


def read_data():
    data = {}
    with open(WEAPON_NAME_XML, encoding="utf-8") as f:
        data = json.load(f)
        f.close()
    return data


if __name__ == "__main__":
    data = read_data()

    weapon_glossay = {"phases": {}}

    start = False
    for k, v in data.items():
        k = str(k)
        if k.startswith("[error]") or "箭" in v or v == "%null%":
            continue

        if k == "dagger":
            start = True
        if start:
            con = False
            for change in CHANGES:
                if str(v).startswith(change):
                    con = True
                    break
            if not con:  # 排除质变武器
                weapon_glossay["phases"][k] = v
    with open("vanilla_weapn.json", "w", encoding="utf-8") as f:
        json.dump(weapon_glossay, f, ensure_ascii=False, indent=2)
