import os,sys,json
from typing import Any
import xml.etree.ElementTree as ET






class IngoreErrorTranslator():
    
    def __init__(self) -> None:
        self.black_list = ["%null%","[ERROR]"]    

    def __call__(self,s: str):
        if s in self.black_list:
            return True,s
        else:
            return False,''


class VanillaTranslator():
    def __init__(self) -> None:
        self.item_db = {}
        self.menu_db = {}

    def load_db(self):
        with open('data/item.json',encoding='utf-8') as item:
            self.item_db  = json.load(item)
            item.close()
        with open('data/menu.json',encoding='utf-8') as menu:
            self.menu_db  = json.load(menu)
            menu.close()



    def __call__(self,s: str):
        s = s.strip()
        if s in self.item_db:
            return True, self.item_db[s]
        if s in self.menu_db:
            return True, self.menu_db[s]
        return False,s

class TranslatorGroup():

    def __init__(self) -> None:
        self.translators = []
        pass
    def add_translator(self,translator):
        self.translators.append(translator)

    def phrase_translate(self, phrase:str):
        for translator in self.translators:
            ok,res = translator(phrase)
            if ok:
                return res
        
        print("无法翻译: ",phrase)
        return phrase

    def translate(self,s: str)->str:
        seqs = [i.strip() for i in s.split("\n\n") if len(i.strip()) > 0]
        res = ''
        for s in seqs:
            res += (self.phrase_translate(s) + '\n\n')
     

def tralslate_file(file_name:str, output_name:str,ts:TranslatorGroup):
    print("file:",file_name, "new file: ",output_name)
    kv = {} 
    tree = ET.parse(file_name)
    root = tree.getroot()
    entries =  root[3]
    for t in entries:
        kv[int(t.get('id'))] = t.text

    trans_kv = {}
    for id,english in kv.items():
            if english is None:
                trans_kv[id] = english
            else:
                trans_kv[id] = ts.translate(english)

    for t in entries:
        t.text = trans_kv.get(int(t.get('id')))
    tree.write(output_name ,encoding='utf-8')

if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("use python translator.py [folder]")
        exit()
    

    root = sys.argv[1]
    files = [f for f in os.listdir(root) if f.endswith('xml')]
    if len(files) == 0:
        print("找不到Xml文件,请使用Yabbr解包dcx文件")
        exit()

    
    translated_path = root + '/trans/'
    if not os.path.exists(translated_path):
        os.mkdir(translated_path)

    group  = TranslatorGroup()
    v =  VanillaTranslator()
    v.load_db()
    group.add_translator(IngoreErrorTranslator())
    group.add_translator(v)

    for f in files:
        tralslate_file(root + '/' + f,translated_path+'/'+ f,group)




    