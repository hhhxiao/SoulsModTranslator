import sys, os
from deep_translator import GoogleTranslator, MicrosoftTranslator, MyMemoryTranslator

if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("use python text_translator.py [folder]")

    translated = GoogleTranslator(source="en", target="zh-CN").translate_file(
        sys.argv[1]
    )
    print(translated)
