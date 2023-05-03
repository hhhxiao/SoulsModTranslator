import os

MOD_ROOT = "D:\\mods\\Ascended\\ModEngine\\mod\\msg\\"
TARGET = ".\\AscenElden_Ascended\\"


def try_create(folder: str):
    if not os.path.exists(folder):
        os.mkdir(folder)
    pass


if __name__ == "__main__":
    try_create(TARGET + "\\item")
    try_create(TARGET + "\\menu")
    types = ["item", "menu"]
    for t in types:
        file_name = MOD_ROOT + "\\engus\\" + t + ".msgbnd.dcx"
        os.system("chcp 65001 && Yabber.exe " + file_name)
        fmg_path = (
            MOD_ROOT
            + "\\engus\\"
            + t
            + "-msgbnd-dcx\\GR\\data\\INTERROOT_win64\\msg\\engUS"
        )
        files = [f for f in os.listdir(fmg_path) if f.endswith(".fmg")]
        for f in files:
            path = fmg_path + "\\" + f
            os.system("chcp 65001 && Yabber.exe " + path)
            cmd = "move " + path + ".xml" + " " + TARGET + "\\" + t
            print(cmd)
            os.system(cmd)
            pass
    pass
