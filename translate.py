import os, sys, json
from typing import Any
import xml.etree.ElementTree as ET
import re


class MachineTranslator:
    def get_variant(s: str):
        if s.isupper():
            return [s]
        return [s, s.capitalize(), s.upper()]

    def __init__(self, key_path: str, value_path: str, mode: str) -> None:
        self.mode = mode
        self.key_file = None
        self.value_file = None
        self.machin_table = {}
        self.glossary = {}

        if mode == "load":
            self.key_file = open(key_path, "r", encoding="utf-8")
            self.value_file = open(value_path, "r", encoding="utf-8")
            eng = self.key_file.read().split("\n\n")
            chs = self.value_file.read().split("\n\n")
            for i in range(len(eng)):
                self.machin_table[eng[i].strip()] = chs[i].strip()
        elif self.mode == "save":
            self.key_file = open(key_path, "w", encoding="utf-8")
            pass
        with open("data/glossary.json", encoding="utf-8") as g:
            gs = json.load(g)
            for k, v in gs.items():
                vars = MachineTranslator.get_variant(k)
                for var in vars:
                    self.glossary[var] = v

    def try_replace(self, match):
        token = match.group()
        if token in self.glossary:
            return self.glossary[token]
        return token

    def __call__(self, s: str):
        key = ""
        if s in self.glossary:
            key = self.get_variant[s]
        else:
            key = re.sub(r"[a-zA-Z]+", self.try_replace, s)
        if self.mode == "save":
            self.key_file.write(key + "\n\n")
            return True, s
        elif self.mode == "load":
            if key in self.machin_table:
                return True, self.machin_table[key]
            else:
                print("Can not find key: ", key)
                return True, s


class IngoreErrorTranslator:
    def __init__(self) -> None:
        self.black_list = ["%null%", "[ERROR]", "_"]

    def __call__(self, s: str):
        if s in self.black_list:
            return True, s
        else:
            return False, s


class VanillaTranslator:
    def __init__(self) -> None:
        self.item_db = {}
        self.menu_db = {}
        self.load_db()

    def load_db(self):
        with open("data/item.json", encoding="utf-8") as item:
            self.item_db = json.load(item)
            item.close()
        with open("data/menu.json", encoding="utf-8") as menu:
            self.menu_db = json.load(menu)
            menu.close()

    def __call__(self, s: str):
        s = s.strip()
        if s in self.item_db:
            return True, self.item_db[s]
        if s in self.menu_db:
            return True, self.menu_db[s]
        return False, s


class TranslatorGroup:
    def __init__(self) -> None:
        self.translators = []
        pass

    def add_translator(self, translator):
        self.translators.append(translator)

    def phrase_translate(self, phrase: str):
        for translator in self.translators:
            ok, res = translator(phrase)
            if ok:
                return res

        print("无法翻译: ", phrase)
        return phrase

    def translate(self, s: str) -> str:
        if s.strip() == "_":
            return "_"
        seqs = [i.strip() for i in s.split("\n\n") if len(i.strip()) > 0]
        res = ""
        for s in seqs:
            res += self.phrase_translate(s) + "\n\n"
        return res.strip()


def tralslate_file(file_name: str, output_name: str, ts: TranslatorGroup):
    kv = {}
    tree = ET.parse(file_name)
    root = tree.getroot()
    entries = root[3]
    for t in entries:
        kv[int(t.get("id"))] = t.text

    trans_kv = {}
    for id, english in kv.items():
        if english is None:
            trans_kv[id] = english
        else:
            trans_kv[id] = ts.translate(english)
    for t in entries:
        t.text = trans_kv.get(int(t.get("id")))
    tree.write(output_name, encoding="utf-8")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("use python translator.py [folder]")
        exit()

    root = sys.argv[1]
    files = [f for f in os.listdir(root) if f.endswith("xml")]
    if len(files) == 0:
        print("找不到Xml文件,请使用Yabbr解包dcx文件")
        exit()

    translated_path = root + "/trans/"
    if not os.path.exists(translated_path):
        os.mkdir(translated_path)

    group = TranslatorGroup()
    group.add_translator(IngoreErrorTranslator())
    group.add_translator(VanillaTranslator())
    group.add_translator(
        MachineTranslator(
            root + "/" + "key_table.txt", root + "/" + "value_table.txt", "load"
        )
    )

    for f in files:
        tralslate_file(root + "/" + f, translated_path + "/" + f, group)
