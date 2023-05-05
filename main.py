import sys, os, logging, time, shutil
import subprocess
import json
from typing import Any
import xml.etree.ElementTree as ET
import re
from typing import List

from PyQt6.QtWidgets import (
    QWidget,
    QPushButton,
    QGridLayout,
    QApplication,
    QLineEdit,
    QListWidget,
    QLabel,
    QFileDialog,
    QMessageBox,
    QTextBrowser,
)
from PyQt6.QtGui import QFont

SPLITOR = "------"

# ------------------------------- TOOLS ---------------------------


def init_logger():
    if os.path.exists("./logs"):
        shutil.rmtree("./logs")
    os.mkdir("./logs")
    logging.basicConfig(
        handlers=[
            logging.FileHandler("logs/" + str(int(time.time())) + ".log"),
            logging.StreamHandler(sys.stdout),
        ],
        encoding="utf-8",
        level=logging.DEBUG,
        format="[%(asctime)s - %(levelname)s] - %(message)s",
        datefmt="%m/%d/%Y %I:%M:%S %p",
    )


def read_kv_file(s: str):
    try:
        with open(s, "r", encoding="utf-8") as f:
            content = str(f.read())
            return [text.strip() for text in content.split(SPLITOR.strip())]
    except:
        return []


# ------------------------------- CONFIG ---------------------------
class GlobalConfig:
    """
    {
    "inter_root": "GR\\data\\INTERROOT_win64\\msg",
    "source_lang": "engUS",
    "vanilla_db_path": ".\\data\\",
    "export_as_docx": false,
    "tmp_path": ".\\.tmp\\"
    }
    """

    def __init__(self) -> None:
        self.inter_root = "GR\\data\\INTERROOT_win64\\msg"
        self.source_lang = "engUS"
        self.export_as_docx = False
        self.tmp_path = "tmp"

    def loadConfig(self, path: str):
        try:
            data = {}
            with open(path, "r") as f:
                data = json.load(f)

            self.inter_root = str(data["inter_root"])
            self.source_lang = str(data["source_lang"])
            self.export_as_docx = str(data["export_as_docx"])
            self.tmp_path = str(data["tmp_path"])
            self.yabber_bin = str(data["yabber_bin"])
            self.vanilla_db_path = str(data["yabber_bin"])
            logging.info("config:")
            logging.info(" - Inter root: %s", self.inter_root)
            logging.info(" - Soruce lang: %s", self.source_lang)
            logging.info(" - Temp path: %s", self.tmp_path)
            logging.info(" - Yabber path: %s", self.yabber_bin)
            logging.info(" - Vanialla DB path: %s", self.vanilla_db_path)

            return True
        except:
            logging.error("Can not parse logger file %s", path)
            return False


CONFIG = GlobalConfig()


# ------------------------------- Yabber ---------------------------
def yabber(path: str):
    result = subprocess.run(
        [os.path.join(CONFIG.yabber_bin, "Yabber.exe"), path],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )

    info = result.stdout.decode("utf-8")
    err = result.stderr.decode("utf-8")
    log = (info + err).replace("\n\n", "\n")
    if "Press any key to exit" in log:
        logging.error(log)
    else:
        logging.info(log)
    return result.returncode == 0


# ------------------------------- 翻译模块 ---------------------------
class Glossary:
    def __init__(self, name: str) -> None:
        # 对于word表，使用re.sub来替换单个单词
        # 对于phase表，使用s.replace来整个替换,phase表的优先级更高

        self.word_table = {}
        self.phase_table = {}

        try:
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
        except:
            logging.error("Can not open glossary %s", name)

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
        return self.lookup_word_table(self.lookup_phase_table(s)).lower()


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
            eng = read_kv_file(key_path)
            chs = read_kv_file(value_path)
            if len(eng) != len(chs):
                logging.error(
                    "Mismatched translation files %s[%d] -> %s[%d]",
                    key_path,
                    len(eng),
                    value_path,
                    len(chs),
                )
            else:
                for i in range(len(eng)):
                    # logging.debug("%s -> %s", eng[i].strip(), chs[i].strip())
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
            self.key_file.write(s + "\n" + SPLITOR + "\n")
            return True, s
        elif self.mode == "load":
            if s in self.machin_table:
                logging.debug("Translate: <%s> -> <%s>", s, self.machin_table[s])
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
        with open(
            os.path.join(CONFIG.vanilla_db_path, "item.json"), encoding="utf-8"
        ) as item:
            self.item_db = json.load(item)
            item.close()
        with open(
            os.path.join(CONFIG.vanilla_db_path, menu.json), encoding="utf-8"
        ) as menu:
            self.menu_db = json.load(menu)
            menu.close()

        for k in self.menu_db.items():
            if k in self.item_db:
                logging.warn(
                    "Find duplicated key in vanilla DB",
                    k,
                    self.menu_db[k],
                    self.item_db[k],
                )

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

        logging.error("Can not translate: %s", phrase)
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


# ------------------------------- 创建空翻译文件 ---------------------------


def create_empty_translate(mod_root_path: str):
    zhocn_path = os.path.join(mod_root_path, "msg", "zhocn")

    if os.path.exists(zhocn_path):
        logging.info(
            "Zhocn path %s already exist ,try rename it and create new one", zhocn_path
        )

        # 删除旧的bak
        bak_path = os.path.join(mod_root_path, "msg", "zhocn_bak")
        if os.path.exists(bak_path):
            shutil.rmtree(bak_path)

        os.rename(zhocn_path, os.path.join(mod_root_path, "msg", "zhocn_bak"))

    # 复制一份英文的
    src_path = os.path.join(mod_root_path, "msg", CONFIG.source_lang.lower())
    shutil.copytree(src_path, zhocn_path)
    # 开始解包并修改目录结构

    logging.info("Repacking files....")
    for t in ["item", "menu"]:
        file = os.path.join(zhocn_path, "{}.msgbnd.dcx".format(t))
        logging.info("Unpacking: %s", file)
        if not yabber(file):
            logging.info("Unpack %s Failure", file)
            return False

        inter_path = os.path.join(
            mod_root_path, "msg/zhocn/{}-msgbnd-dcx".format(t), CONFIG.inter_root
        )

        os.rename(
            os.path.join(inter_path, CONFIG.source_lang),
            os.path.join(inter_path, "zhoCN"),
        )
        logging.info(
            "Rename: %s ->%s",
            os.path.join(inter_path, CONFIG.source_lang),
            os.path.join(inter_path, "zhoCN"),
        )
        yabber_metadata = os.path.join(
            mod_root_path, "msg/zhocn/{}-msgbnd-dcx".format(t), "_yabber-bnd4.xml"
        )

        # 读出内容并替换并写入
        if not os.path.exists(yabber_metadata):
            logging.error("Can not find metadata file: %s", yabber_metadata)
            return False

        logging.info("ReWriting %s", yabber_metadata)

        try:
            content = ""
            with open(yabber_metadata, "r", encoding="utf-8") as f:
                content = f.read()
            f.close()

            new_contnet = content.replace(CONFIG.source_lang, "zhoCN")
            with open(yabber_metadata, "w", encoding="utf-8") as f:
                f.write(new_contnet)
        except:
            logging.info("ReWrite %s Failure!", yabber_metadata)
            return False
        # Repack

        repack_path = os.path.join(mod_root_path, "msg/zhocn/{}-msgbnd-dcx".format(t))
        logging.info("Repacking %s", repack_path)
        if not yabber(repack_path):
            logging.info("Repacking %s Failure", repack_path)
            return False
    return True


def buildTranslateGroup(
    glossaries: List[str], key_file: str, value_file: str, mode: str
):
    group = TranslatorGroup()
    group.add_translator(IngoreErrorTranslator())
    group.add_translator(VanillaTranslator())

    gls = [Glossary(i) for i in glossaries]
    group.add_translator(MachineTranslator(key_file, value_file, gls, mode))
    return group


def translateMod(mod_root_path, group: TranslatorGroup, mode: str):
    logging.info("Create temp dir")
    tmp_path = os.path.join(mod_root_path, "msg", CONFIG.tmp_path)
    if os.path.exists(tmp_path):
        shutil.rmtree(tmp_path)
    os.mkdir(tmp_path)
    for t in ["item", "menu"]:
        os.mkdir(os.path.join(tmp_path, t))
        fmg_files_path = os.path.join(
            mod_root_path,
            "msg/zhocn/{}-msgbnd-dcx".format(t),
            CONFIG.inter_root,
            "zhoCN",
        )
        print(fmg_files_path)
        # 复制文件到英文目录
        for f in os.listdir(fmg_files_path):
            if f.endswith(".fmg"):
                dst = os.path.join(tmp_path, t, f)
                logging.info("Copy %s -> %s", os.path.join(fmg_files_path, f), dst)
                shutil.copy(os.path.join(fmg_files_path, f), dst)
                if not yabber(dst):
                    logging.error("Invalid fmg fles: %s", dst)
                    return False
                os.remove(dst)

    # 开始翻译
    for t in ["item", "menu"]:
        xml_path = os.path.join(tmp_path, t)
        trans_path = os.path.join(xml_path, "trans")
        if os.path.exists(trans_path):
            shutil.rmtree(trans_path)
        os.mkdir(trans_path)
        for f in os.listdir(xml_path):
            if f.endswith(".fmg.xml"):
                logging.info("Translate %s", f)
                tralslate_file(
                    os.path.join(xml_path, f), os.path.join(trans_path, f), group
                )
    if mode == "save":
        logging.info("No need to reapck")
        return True
    # 打包并移动到对应位置
    for t in ["item", "menu"]:
        trans_path = os.path.join(tmp_path, t, "trans")
        for file in os.listdir(trans_path):
            if file.endswith(".fmg.xml"):
                full_file_path = os.path.join(trans_path, file)
                if not yabber(full_file_path):
                    logging.info("Repack %s Faiure", full_file_path)
                    return False
                logging.info("Repack %s", full_file_path)
                full_fmg_path = full_file_path.replace(".xml", "")
                dst_path = os.path.join(
                    mod_root_path,
                    "msg/zhocn/{}-msgbnd-dcx".format(t),
                    CONFIG.inter_root,
                    "zhoCN",
                )
                logging.info("Move %s -> %s", full_fmg_path, dst_path)
                # 删除旧的
                os.remove(os.path.join(dst_path, file.replace(".xml", "")))
                shutil.move(full_fmg_path, dst_path)

        if not yabber(os.path.join(mod_root_path, "msg/zhocn/{}-msgbnd-dcx".format(t))):
            logging.error(
                "can not pack: %s",
                os.path.join(mod_root_path, "msg/zhocn/{}-msgbnd-dcx".format(t)),
            )
            return False

    return True


# ------------------------------- GUI ---------------------------


class TranslateGUI(QWidget):
    def MSG(self, s: str):
        QMessageBox.information(self, "提醒", s)

    def __init__(self):
        super().__init__()
        self.modPathEdit = QLineEdit()

        self.selectModPathButton = QPushButton("选择MOD路径")
        self.addGlossaryButton = QPushButton("添加术语表")
        self.removeGlossaryButton = QPushButton("删除选中的术语表")

        self.createEmptyFileButton = QPushButton("生成空翻译文件")

        self.exportButton = QPushButton("导出未翻译语句")

        self.translateButton = QPushButton("生成汉化文件")

        self.glossaryListView = QListWidget()

        # self.logWindow = QTextBrowser()
        # self.logWindow.setCurrentFont(QFont("微软雅黑", 8))

        self.initUI()
        # Event Binding
        self.selectModPathButton.clicked.connect(self.selectModPathEvent)
        self.addGlossaryButton.clicked.connect(self.addGlossaryEvent)
        self.removeGlossaryButton.clicked.connect(self.removeGlossaryEvent)
        self.glossaryListView.itemDoubleClicked.connect(self.displayItem)
        self.createEmptyFileButton.clicked.connect(self.createEmpty)
        self.exportButton.clicked.connect(self.exportEvent)
        self.translateButton.clicked.connect(self.translateEvent)
        # Status
        self.modPathEdit.setEnabled(False)
        self.exportButton.setEnabled(False)
        self.translateButton.setEnabled(False)
        self.createEmptyFileButton.setEnabled(False)

    def center(self):
        qr = self.frameGeometry()
        cp = self.screen().availableGeometry().center()

        qr.moveCenter(cp)
        self.move(qr.topLeft())

    def initUI(self):
        logging.debug("Init GUI")

        grid = QGridLayout()
        grid.setSpacing(10)
        grid.addWidget(self.modPathEdit, 0, 0, 1, 6)
        grid.addWidget(self.selectModPathButton, 0, 6, 1, 1)
        grid.addWidget(self.createEmptyFileButton, 0, 7, 1, 1)
        grid.addWidget(QLabel("术语表"), 1, 0, 1, 2)
        grid.addWidget(self.addGlossaryButton, 1, 2, 1, 3)
        grid.addWidget(self.removeGlossaryButton, 1, 5, 1, 3)

        grid.addWidget(self.glossaryListView, 2, 0, 3, 8)
        grid.addWidget(self.exportButton, 5, 0, 1, 5)
        grid.addWidget(self.translateButton, 5, 5, 1, 3)
        # grid.addWidget(self.logWindow, 6, 0, 1, 8)
        self.setLayout(grid)
        self.setWindowTitle("艾尔登法环MOD翻译器 v0.1")
        self.resize(800, 500)
        self.center()
        self.show()

    # Event
    def selectModPathEvent(self):
        path = QFileDialog.getExistingDirectory(self, caption="选择MOD根目录")
        if len(path.strip()) > 0:
            self.exportButton.setEnabled(True)
            self.translateButton.setEnabled(True)
            self.createEmptyFileButton.setEnabled(True)
            self.modPathEdit.setText(path)

    def addGlossaryEvent(self):
        file, x = QFileDialog.getOpenFileName(
            self,
            caption="选择术语表文件",
            filter="JSON files (*.json)",
        )
        items = [
            self.glossaryListView.item(i).text()
            for i in range(self.glossaryListView.count())
        ]

        if len(file.strip()) == 0:
            return

        if file not in items:
            self.glossaryListView.addItem(file)
        else:
            self.MSG("该术语表已存在")

    def removeGlossaryEvent(self):
        row = self.glossaryListView.currentRow()
        if row >= 0:
            current_item = self.glossaryListView.takeItem(row)
            del current_item

    def displayItem(self):
        f = self.glossaryListView.currentItem().text()
        if not os.path.exists(f):
            self.MSG("文件不存在")
        else:
            os.startfile(f)

    def getGlossairs(self):
        return [
            self.glossaryListView.item(i).text()
            for i in range(self.glossaryListView.count())
        ]

    def createEmpty(self):
        mod_path = self.modPathEdit.text()
        if not os.path.exists(mod_path):
            self.MSG("MOD目录不存在")
            return

        if not create_empty_translate(self.modPathEdit.text()):
            self.MSG("创建空翻译文件失败，详细原因请查询日志")
        else:
            self.MSG("创建空翻译: " + os.path.join(mod_path, "msg/zhocn") + "成功,之后无需再创建")

    def exportEvent(self):
        mod_path = self.modPathEdit.text()
        if not os.path.exists(mod_path):
            self.MSG("MOD目录不存在")
            return

        key_file, _ = QFileDialog.getSaveFileName(
            caption="保存翻译文件",
            directory=os.path.join(self.modPathEdit.text(), "msg/zhocn/key.txt"),
        )

        if len(key_file.strip()) == 0:
            return

        logging.info("save key files to %s", key_file)

        g = buildTranslateGroup(self.getGlossairs(), key_file, "wont_use", "save")
        if not translateMod(mod_path, g, "save"):
            self.MSG("导出未翻译语句失败，详细原因请查询日志")
        else:
            self.MSG("导出未翻译文件: " + key_file + "成功")

    def translateEvent(self):
        mod_path = self.modPathEdit.text()
        if not os.path.exists(mod_path):
            self.MSG("MOD目录不存在")
            return

        key_file, _ = QFileDialog.getOpenFileName(
            caption="选择未翻译文件",
            directory=os.path.join(self.modPathEdit.text(), "msg/zhocn/key.txt"),
        )

        value_file, _ = QFileDialog.getOpenFileName(
            caption="选择翻译文件",
            directory=os.path.join(self.modPathEdit.text(), "msg/zhocn/value.txt"),
        )

        if len(key_file.strip()) == 0 or len(value_file.strip()) == 0:
            return

        logging.info("Key file is %s", key_file)
        logging.info("Value file is %s", value_file)

        g = buildTranslateGroup(self.getGlossairs(), key_file, value_file, "load")
        if translateMod(mod_path, g, "load"):
            self.MSG("翻译成功，请打开游戏进行验证")
        else:
            self.MSG("翻译失败")


def main():
    init_logger()

    CONFIG.loadConfig("config.json")

    app = QApplication(sys.argv)
    app.setFont(QFont("微软雅黑", 14))
    ex = TranslateGUI()

    sys.exit(app.exec())


if __name__ == "__main__":
    main()
