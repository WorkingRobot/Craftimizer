import shutil, os, subprocess, zipfile, json, sys

from itertools import chain

PROJECT_NAME = sys.argv[1]
OFFICIAL_ZIP = f"{PROJECT_NAME}/bin/x64/Release/{PROJECT_NAME}/latest.zip"
UNOFFICIAL_ZIP = f"{PROJECT_NAME}/bin/x64/Release/{PROJECT_NAME}/latestUnofficial.zip"

shutil.copy(OFFICIAL_ZIP, UNOFFICIAL_ZIP)

subprocess.check_call(['7z', 'd', UNOFFICIAL_ZIP, f"{PROJECT_NAME}.json"])

with zipfile.ZipFile(UNOFFICIAL_ZIP) as file:
	members = [member for member in file.namelist() if member in (f"{PROJECT_NAME}.dll", f"{PROJECT_NAME}.deps.json", f"{PROJECT_NAME}.json", f"{PROJECT_NAME}.pdb")]

subprocess.check_call(['7z', 'rn', UNOFFICIAL_ZIP] + list(chain.from_iterable((m, m.replace(PROJECT_NAME, f"{PROJECT_NAME}Unofficial")) for m in members)))

with open(f"{PROJECT_NAME}/bin/x64/Release/{PROJECT_NAME}/{PROJECT_NAME}.json") as file:
	manifest = json.load(file)

manifest['Punchline'] = f"Unofficial/uncertified build of {manifest['Name']}. {manifest['Punchline']}"
manifest['InternalName'] += 'Unofficial'
manifest['Name'] += ' (Unofficial)'
manifest['IconUrl'] = f"https://raw.githubusercontent.com/WorkingRobot/MyDalamudPlugins/main/icons/{manifest['InternalName']}.png"

with zipfile.ZipFile(UNOFFICIAL_ZIP, "a", zipfile.ZIP_DEFLATED, compresslevel = 7) as file:
	file.writestr(f"{PROJECT_NAME}Unofficial.json", json.dumps(manifest, indent = 2))