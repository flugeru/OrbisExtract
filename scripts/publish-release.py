"""Publish existing successful main-branch artifacts; never rebuild or re-sign them."""
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import zipfile


def gh(*args, optional=False):
    p = subprocess.run(['gh', *map(str, args)], capture_output=True, text=True)
    if p.returncode and not optional:
        raise RuntimeError(p.stderr)
    return p.stdout if p.returncode == 0 else None


def one(root, pattern):
    found = list(root.rglob(pattern))
    assert len(found) == 1, (root, pattern, found)
    assert found[0].stat().st_size > 1024 * 1024, found[0]
    return found[0]


repo = os.environ['GITHUB_REPOSITORY']
run_id = os.environ['SOURCE_RUN_ID']
version = os.environ['RELEASE_VERSION']
assert repo == 'flugeru/OrbisExtract'
assert run_id.isdigit() and re.fullmatch(r'\d+\.\d+\.\d+', version)
notes_path = Path('packaging/release') / (version + '.md')
assert notes_path.is_file()
run = json.loads(gh('api', f'repos/{repo}/actions/runs/{run_id}'))
assert run['head_branch'] == 'main' and run['conclusion'] == 'success'
assert run['path'] == '.github/workflows/android-apk.yml'
source_sha = run['head_sha']
project_path = 'src/OrbisExtract.App/OrbisExtract.App.Android/OrbisExtract.App.Android.csproj'
import base64
source_project = json.loads(gh('api', f'repos/{repo}/contents/{project_path}?ref={source_sha}'))
project_text = base64.b64decode(source_project['content']).decode()
assert f'<ApplicationDisplayVersion>{version}</ApplicationDisplayVersion>' in project_text
artifacts = json.loads(gh('api', f'repos/{repo}/actions/runs/{run_id}/artifacts'))['artifacts']
required = {'OrbisExtract-Android-APK', 'orbisextract-windows-setup',
            'orbisextract-windows-exe', 'orbisextract-linux-appimage'}
selected = [a for a in artifacts if a['name'] in required]
assert {a['name'] for a in selected} == required and len(selected) == len(required)
assert not any(a['expired'] for a in selected)
root = Path(os.environ['RUNNER_TEMP']) / f'orbis-release-{version}'
root.mkdir()
fetched = root / 'fetched'
for name in sorted(required):
    gh('run', 'download', run_id, '--repo', repo, '--name', name, '--dir', fetched / name)
out = root / 'release'
out.mkdir()
apk = one(fetched / 'OrbisExtract-Android-APK', '*.apk')
setup = one(fetched / 'orbisextract-windows-setup', '*.exe')
appimage = one(fetched / 'orbisextract-linux-appimage', '*.AppImage')
portable = fetched / 'orbisextract-windows-exe'
assert (portable / 'OrbisExtract.App.Desktop.exe').is_file()
assert setup.read_bytes()[:2] == b'MZ'
assert appimage.read_bytes()[:4] == b'\x7fELF'
# No signing material belongs in any downloaded or public artifact.
for p in fetched.rglob('*'):
    assert p.suffix.lower() not in {'.jks', '.keystore', '.p12', '.pfx'}, p
sdk = Path(os.environ.get('ANDROID_SDK_ROOT', '/usr/local/lib/android/sdk'))
tools = sorted((sdk / 'build-tools').glob('*/apksigner'))
assert tools, 'Android build tools unavailable'
apksigner = tools[-1]
aapt = apksigner.parent / 'aapt'
cert = subprocess.check_output([str(apksigner), 'verify', '--verbose', '--print-certs', str(apk)], text=True)
info = subprocess.check_output([str(aapt), 'dump', 'badging', str(apk)], text=True)
assert f"versionName='{version}'" in info and "name='com.flugel.orbisextract'" in info
code = int(re.search(r"versionCode='(\d+)'", info).group(1))
assert code == 1050000 + run['run_number'], (code, run['run_number'])
with zipfile.ZipFile(apk) as z:
    assert z.testzip() is None
copy_names = [(apk, f'OrbisExtract-{version}-signed.apk'),
              (setup, f'OrbisExtract-{version}-windows-x64-setup.exe'),
              (appimage, f'OrbisExtract-{version}-x86_64.AppImage')]
for source, name in copy_names:
    shutil.copy2(source, out / name)
(out / f'OrbisExtract-{version}-x86_64.AppImage').chmod(0o755)
with zipfile.ZipFile(out / f'OrbisExtract-{version}-windows-x64-portable.zip', 'w', zipfile.ZIP_DEFLATED) as z:
    for p in sorted(portable.rglob('*')):
        if p.is_file(): z.write(p, p.relative_to(portable))
(out / 'android-signing-certificate.txt').write_text(cert)
(out / 'android-package-info.txt').write_text(info)
provenance = {'version': version, 'versionCode': code, 'source_commit': source_sha,
              'source_run_id': int(run_id), 'source_run_url': run['html_url'],
              'artifacts': [{'id': a['id'], 'name': a['name'], 'digest': a.get('digest')} for a in selected]}
(out / 'build-provenance.json').write_text(json.dumps(provenance, indent=2) + '\n')
checksums = '\n'.join(hashlib.sha256(p.read_bytes()).hexdigest() + '  ' + p.name
                       for p in sorted(out.iterdir())) + '\n'
(out / 'SHA256SUMS.txt').write_text(checksums)
notes = notes_path.read_text() + f'\n\nBuild: [{run_id}]({run["html_url"]}) · Commit: `{source_sha}`\n'
final_notes = root / 'release-notes.md'
final_notes.write_text(notes)
existing_text = gh('release', 'view', version, '--repo', repo, '--json', 'isDraft,body', optional=True)
if existing_text:
    existing = json.loads(existing_text)
    assert existing['isDraft'] and existing['body'].strip() == notes.strip(), 'Refusing to change an existing unrelated or published release'
else:
    gh('release', 'create', version, '--repo', repo, '--target', source_sha,
       '--title', f'OrbisExtract {version}', '--notes-file', final_notes, '--draft')
gh('release', 'upload', version, '--repo', repo, *sorted(out.iterdir()), '--clobber')
release = json.loads(gh('release', 'view', version, '--repo', repo, '--json', 'assets'))
expected = {p.name: p.stat().st_size for p in out.iterdir()}
assert {a['name']: a['size'] for a in release['assets']} == expected, 'Uploaded assets differ'
gh('release', 'edit', version, '--repo', repo, '--draft=false', '--latest')
print(gh('release', 'view', version, '--repo', repo, '--json', 'url,isDraft,tagName,assets'))
