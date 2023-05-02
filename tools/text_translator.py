import sys, os
from deep_translator import (
    GoogleTranslator,
    MicrosoftTranslator,
    PonsTranslator,
    LingueeTranslator,
    MyMemoryTranslator,
    YandexTranslator,
    PapagoTranslator,
    DeeplTranslator,
    QcriTranslator,
    single_detection,
    batch_detection,
)

if __name__ == "__main__":
    s = "戈弗雷 is the first lord of 交界地"

    print(
        "Google:      ",
        GoogleTranslator(
            source="english", target="chinese (simplified)"
        ).translate_file("a.docx"),
    )
