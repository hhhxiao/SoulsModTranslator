import sys, os, json
from ermt import translator

UNPACK_ROOT = "./AscenElden_Ascended"


def pack(t):
    d = UNPACK_ROOT + "\\" + t + "\\trans\\"
    for f in os.listdir(d):
        os.system("chcp 65001 && Yabber " + d + "\\" + f)

    cfg = {}
    with open(UNPACK_ROOT + "\\" + "config.json") as f:
        cfg = json.load(f)
    mod_msg_root = cfg["root"]
    internal_path = cfg["internal"]
    dst_path = mod_msg_root + "\\{}-msgbnd-dcx".format(t) + "\\" + internal_path
    for f in os.listdir(d):
        cmd = "move {}  {}".format(d + "\\" + f, dst_path)
        os.system(cmd)
    print(dst_path)
    os.system("chcp 65001 && Yabber " + mod_msg_root + "\\{}-msgbnd-dcx".format(t))


def translator_type(t: str):
    if t != "menu" and t != "item":
        print("不合法的的类型,类型只能是 menu 或者 item")
        return

    root = UNPACK_ROOT + "/" + t

    files = [f for f in os.listdir(root) if f.endswith("xml")]

    if len(files) == 0:
        print("找不到解包的文本文件,请使用Yabbr.exe解包dcx文件")
        exit()

    for f in files:
        print("找到文件 " + f)

    translated_path = root + "/trans/"

    if not os.path.exists(translated_path):
        os.mkdir(translated_path)

    group = translator.TranslatorGroup()
    group.add_translator(translator.IngoreErrorTranslator())
    group.add_translator(translator.VanillaTranslator())

    vanilla_glossary = translator.Glossary("data/vanilla_glossary.json")
    # mod_glossary = translator.Glossary(UNPACK_ROOT + "/" + "glossary.json")

    group.add_translator(
        translator.MachineTranslator(
            root + "/" + "key_table.txt",
            root + "/" + "value_table.txt",
            [vanilla_glossary],
            "load",
        )
    )

    for f in files:
        translator.tralslate_file(root + "/" + f, translated_path + "/" + f, group)
    pass


if __name__ == "__main__":
    translator_type("menu")
    pack("menu")
    translator_type("item")
    pack("item")
    pass
