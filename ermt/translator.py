import os, sys, json
from typing import Any
import xml.etree.ElementTree as ET
import re
from typing import List


def read_key_file():
    pass


"""
机翻的翻译器,各种关键词的替换，查询都在这儿
"""


class Glossary:
    def __init__(self, name: str) -> None:
        # 对于word表，使用re.sub来替换单个单词
        # 对于phase表，使用s.replace来整个替换,phase表的优先级更高

        self.word_table = {}
        self.phase_table = {}

        data = {}
        with open(name, "r", encoding="utf-8") as f:
            data = json.load(f)
        # 不判空就是为了报错
        words = data["words"]
        for w, v in words.items():
            self.word_table[w] = v
            self.word_table[w.capitalize()] = v
            self.word_table[w.upper()] = v

        phases = data["phases"]

        for p, v in phases.items():
            self.phase_table[p] = v
            self.phase_table[p.lower()] = v
            self.phase_table[p.capitalize()] = v
            self.phase_table[p.upper()] = v

    def lookup_phase_table(self, s: str):
        for k, v in self.phase_table.items():
            s = s.replace(k, v)
        return s

    def try_replace(self, match):
        token = match.group()
        if token in self.word_table:
            return self.word_table[token]
        return token

    def lookup_word_table(self, s: str):
        return re.sub(r"[a-zA-Z]+", self.try_replace, s)

    def __call__(self, s: str) -> Any:
        return self.lookup_word_table(self.lookup_phase_table(s))


class MachineTranslator:
    def get_variant(s: str):
        if s.isupper():
            return [s]
        return [s, s.capitalize(), s.upper()]

    def __init__(
        self, key_path: str, value_path: str, glossaries: List[Glossary], mode: str
    ) -> None:
        self.mode = mode
        self.key_file = None
        self.value_file = None
        self.machin_table = {}
        self.glossaries = glossaries

        if mode == "load":
            self.key_file = open(key_path, "r", encoding="utf-8")
            self.value_file = open(value_path, "r", encoding="utf-8")
            eng = self.key_file.read().split("\n\n")
            chs = self.value_file.read().split("\n\n")
            for i in range(len(eng)):
                self.machin_table[eng[i].strip()] = chs[i].strip()
        elif self.mode == "save":
            self.key_file = open(key_path, "w", encoding="utf-8")

    def add_glossary(self, g: Glossary):
        self.add_glossary(g)

    # def __del__(self):
    #     pass

    def __call__(self, s: str):
        s = s.strip()
        for glossary in self.glossaries:
            s = glossary(s)

        if self.mode == "save":
            self.key_file.write(s + "\n\n")
            return True, s
        elif self.mode == "load":
            if s in self.machin_table:
                return True, self.machin_table[s]
            else:
                return False, s


class IngoreErrorTranslator:
    def __init__(self) -> None:
        self.black_list = ["%null%", "[ERROR]"]

    def __call__(self, s: str):
        if s in self.black_list:
            return True, s
        else:
            return False, s


"""
读取数据文件，并进行替换
"""


class VanillaTranslator:
    def __init__(self) -> None:
        self.item_db = {}
        self.menu_db = {}
        self.db = {}

        self.load_db()

    def load_db(self):
        with open("data/item.json", encoding="utf-8") as item:
            self.item_db = json.load(item)
            item.close()
        with open("data/menu.json", encoding="utf-8") as menu:
            self.menu_db = json.load(menu)
            menu.close()

        for k in self.menu_db.items():
            if k in self.item_db:
                print("发现的重复的键 ", k, self.menu_db[k], self.item_db[k])

    def __call__(self, s: str):
        s = s.strip()
        # 两个数据库有重的数据，下面的顺序最好不要换
        if s in self.menu_db:
            return True, self.menu_db[s]
        if s in self.item_db:
            return True, self.item_db[s]
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
