# 读取的游戏的所有中英文文本，并建立一一对应的数据库
# 目录格式必须是root/engUS/menu/*  以及roots/zhoCN/menu格式的
import os ,sys
import json
import xml.etree.ElementTree as ET

def read_fmg_xml(path: str):
    kv = {} 
    tree = ET.parse(path)
    root = tree.getroot()
    entries =  root[3]
    for t in entries:
        kv[int(t.get('id'))] = t.text
    return kv





def split_msg(english: str,chinese:str):
    english = english.strip()
    chinese = chinese.strip()
    
    es = [s.strip() for s in english.split('\n\n') if len(s.strip()) > 0]
    cs = [s.strip() for s in chinese.split('\n\n') if len(s.strip()) > 0]
    if len(es) != len(cs):
        print(" --------------- ")
        print("文本段数不一致:")
        print("英文: ")
        print(english)
        print("中文")
        print(chinese)
        return {
            english: chinese
        }
    
    m = {}
    for i in range(len(es)):
        m[es[i]] = cs[i]
    return m


def create_db(root: str,msg_type :str,output: str):
    eng_dir =  root +'/engUS/' + msg_type +'/'
    zh_dir =  root +'/zhoCN/' + msg_type +'/'
    print(eng_dir)
    if (not os.path.exists(eng_dir)) or (not os.path.exists(zh_dir)):
        raise Exception('非法的文本类型: ' + msg_type)
    
    eng_files = [ f for f in os.listdir(eng_dir) if f.endswith('.xml')]
    zh_files = [ f for f in os.listdir(zh_dir) if f.endswith('.xml')]


    db  = {}
    for file in eng_files:
        if file not in zh_files:
            print('在中文文本中找不到英文文本 {}'.format(file))
        # 读取数库
        table = {}
        eng_kv = read_fmg_xml(eng_dir +'/' + file)
        zh_kv = read_fmg_xml(zh_dir+'/' + file)
        for id,english in eng_kv.items():
            if english != "%null%" and english != "[ERROR]":
                if id in zh_kv:
                    chinese = zh_kv.get(id)
                    table[int(id)] = split_msg(english,chinese)
                else:
                    print("文件{}中文本id为{}的文本发生缺失")
        db[file] = table
    
    
    with open(output +'/'+msg_type+".json", "w",encoding='utf-8') as f:
        json.dump(db, f,ensure_ascii=False,indent=2)

    
if __name__ == "__main__":
    
    if len(sys.argv) != 2:
        print("use python db_creator [root_folder]")
        exit()

    create_db(sys.argv[1],'menu','data')



    
