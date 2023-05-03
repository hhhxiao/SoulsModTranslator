import sys, os
from deep_translator import GoogleTranslator

if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Use: python translatr [input] [output]")

    print("Input: ", sys.argv[1])
    print("Output: ", sys.argv[2])

    text = GoogleTranslator(
        source="english", target="chinese (simplified)"
    ).translate_file(sys.argv[1])

    with open(sys.argv[2], "w", encoding="utf-8") as f:
        f.write(text)
    f.close()
