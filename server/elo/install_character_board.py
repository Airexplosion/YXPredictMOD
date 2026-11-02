"""Install the first character board into the existing Elo Caddy overlay.

Run from a staged copy of server/elo. Leaves existing API services and Caddy
configuration intact. Refuses to overwrite an existing character page.
"""
import hashlib
import os
import pathlib
import re
import shutil
import subprocess
import tempfile
import time
import json
from publish_character_scores import publish

BASE=pathlib.Path('/opt/yx-elo')
SOURCE=pathlib.Path(__file__).resolve().parent
UNIT='yx-character-board-feed'
CARD='<a class="hub-card" href="elo/characters/"><h2>角色 Elo 强度榜</h2><div class="en">Character rankings</div><p>10·29 平衡窗口与实时榜，按积分分段和副职比较角色。</p><div class="path">/elo/characters</div></a>'

def overlay():
    cfg=pathlib.Path('/etc/caddy/Caddyfile').read_text()
    block=cfg.split('elo.kaigua.vip {',1)[1].split('\n}',1)[0]
    roots=re.findall(r'root \* (/opt/yx-elo/web-overlays/[0-9]+)',block)
    if len(set(roots))!=1 or '/elo/*' not in block:raise RuntimeError('Review current site routing')
    return pathlib.Path(roots[0])

def atomic(path,content,mode=0o644):
    with tempfile.NamedTemporaryFile(dir=path.parent,delete=False) as f:
        temp=pathlib.Path(f.name);f.write(content)
    try:temp.chmod(mode);temp.replace(path)
    finally:temp.unlink(missing_ok=True)

def main():
    webroot=overlay()
    dest=webroot/'elo/characters'
    if dest.exists():raise RuntimeError('Existing page requires a reviewed update')
    hub=webroot/'index.html'
    original=hub.read_bytes()
    anchor=b'<div class="hub-grid">'
    assert original.count(anchor)==1 and b'href="elo/characters/"' not in original
    backup=pathlib.Path('/var/backups/yx-elo')/('character-page-'+str(time.time_ns()))
    backup.mkdir(mode=0o700,parents=True)
    changes={hub:original.replace(anchor,anchor+b'\n'+CARD.encode())}
    old_hashes={'elo.js':'bb61b20340eac3bbba7016517d4e1000237748878fb4abec4dae7da9e8fc558d',
                'elo.css':'6ce118c045cdb1f114b4c11d7ffd16c9af0b5895e6334849cadc01aae4a4e931'}
    for name,old_hash in old_hashes.items():
        for folder in [webroot,BASE/'web']:
            path=folder/name
            assert hashlib.sha256(path.read_bytes().replace(b'\r\n',b'\n')).hexdigest()==old_hash, path
            changes[path]=(SOURCE/'web'/name).read_bytes()
    publisher=BASE/'publish_character_scores.py'
    units=pathlib.Path('/etc/systemd/system')
    service,timer=(units/(UNIT+ext) for ext in ('.service','.timer'))
    assert not any(p.exists() for p in (publisher,service,timer))
    changes[publisher]=(SOURCE/'publish_character_scores.py').read_bytes()
    changes[service]=(f"[Unit]\nDescription=Refresh public aggregate character scores\n[Service]\nType=oneshot\nExecStart=/usr/bin/python3 {publisher} {dest}\nNoNewPrivileges=true\nProtectSystem=strict\nProtectHome=true\nReadWritePaths={dest}\nTimeoutStartSec=45\n").encode()
    changes[timer]=(f"[Unit]\nDescription=Refresh Elo character board every two minutes\n[Timer]\nOnActiveSec=2min\nOnUnitInactiveSec=2min\nAccuracySec=3s\nUnit={UNIT}.service\n[Install]\nWantedBy=timers.target\n").encode()
    originals={p:p.read_bytes() if p.exists() else None for p in changes}
    assert originals[hub]==original
    for p,old in originals.items():
        if old is not None:(backup/(str(p).strip('/').replace('/','__'))).write_bytes(old)
    staged=pathlib.Path(tempfile.mkdtemp(prefix='.character-board-',dir=dest.parent))
    moved=False;written=[]
    try:
        staged.chmod(0o755)
        for name in ('index.html','board.js','board.css','window.json'):
            atomic(staged/name,(SOURCE/'web/characters'/name).read_bytes())
        publish(staged/'live.json',staged/'window.json')
        assert overlay()==webroot and not dest.exists()
        staged.rename(dest);moved=True
        for p,content in changes.items():
            assert (p.read_bytes() if p.exists() else None)==originals[p],p
            atomic(p,content);written.append(p)
        subprocess.run(['systemctl','daemon-reload'],check=True)
        subprocess.run(['systemctl','enable','--now',timer.name],check=True)
    except BaseException:
        if timer in written:subprocess.run(['systemctl','disable','--now',timer.name],capture_output=True)
        if service in written:subprocess.run(['systemctl','stop',service.name],capture_output=True)
        for p in reversed(written):
            if p.read_bytes()!=changes[p]:continue
            if originals[p] is None:p.unlink()
            else:atomic(p,originals[p])
        subprocess.run(['systemctl','daemon-reload'])
        if moved:dest.rename(backup/'unpublished-board')
        raise
    finally:
        if staged.exists():shutil.rmtree(staged)
    print(json.dumps(dict(path=str(dest),backup=str(backup),historical=json.loads((dest/'window.json').read_text())['games'],
                          live=json.loads((dest/'live.json').read_text())['games'])))
if __name__=='__main__':main()
