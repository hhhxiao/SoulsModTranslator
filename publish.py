import os, zipfile

publish_path = "./bin/Release/net7.0-windows/publish"


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
copy_files = ["db/", "changelog.md", "oo2core_6_win64.dll"]
for file in copy_files:
    os.system("cp -r {} {}".format(file, publish_path))
zip_folder(publish_path, "SoulsModTranslator.zip")
