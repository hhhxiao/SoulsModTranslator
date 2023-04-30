import os,sys

if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("use .\\unpack folder name")
        exit()

    d = sys.argv[1]
    for f in os.listdir(d):
        if f.endswith(".fmg"):
            os.system("chcp 65001 && Yabber " +d +"\\" + f)
