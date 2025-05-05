import os, zipfile
import shutil

publish_path = "./bin/Release/net7.0-windows/publish"
extra_folders = ["db", "glossaries"] #do not add / to the end
extra_files = ["../changelog.md", "oo2core_6_win64.dll"]



def zip_folder(folder_path, output_path, compression=zipfile.ZIP_DEFLATED):
    with zipfile.ZipFile(output_path, "w", compression) as zipf:
        for root, dirs, files in os.walk(folder_path):
            for file in files:
                file_path = os.path.join(root, file)
                zipf.write(file_path, os.path.relpath(file_path, folder_path))
    zipf.close()


code = 0
if os.system("dotnet publish") != 0:
    exit(-1)



for file in extra_files:
    base_name = os.path.basename(file)
    print("copy file: " ,file)
    shutil.copyfile(file, os.path.join(publish_path, base_name))

for folder in extra_folders:
    base_name = os.path.basename(folder)
    full_path = os.path.join(publish_path, base_name)
    if os.path.exists(full_path):
        shutil.rmtree(full_path)
    print("copy folder: " ,folder)
    shutil.copytree(folder, full_path)
zip_folder(publish_path, "../SoulsModTranslator.zip")
print("Create archive finished.")