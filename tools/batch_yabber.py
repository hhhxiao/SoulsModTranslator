import os, sys

if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("use python batch_yabber [root_folder]")
        exit()
    input = sys.argv[1]
    files = [f for f in os.listdir(input) if f.endswith(".fmg")]
    for f in files:
        os.system('yabber.exe "{}"'.format(os.path.join(input, f)))
