"""Fetch a bounded read-only export and atomically publish a complete Elo generation."""
import argparse
import json
import os
import pathlib
import subprocess
import tempfile
import time
from ratings import build


def main():
    p=argparse.ArgumentParser();p.add_argument('--seed',required=True);p.add_argument('--source-command',required=True);p.add_argument('--output',required=True);a=p.parse_args()
    command=json.loads(pathlib.Path(a.source_command).read_text())
    started=time.monotonic()
    raw=subprocess.run(command,check=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE,timeout=180).stdout
    if len(raw)>100*1024*1024:raise ValueError('Source export exceeds budget')
    source=json.loads(raw);del raw
    print(json.dumps({'phase':'source-loaded','games':len(source['games']),'seconds':round(time.monotonic()-started,2)}),flush=True)
    seed=json.loads(pathlib.Path(a.seed).read_text(encoding='utf-8'))
    print(json.dumps({'phase':'seed-loaded','players':len(seed['players']),'seconds':round(time.monotonic()-started,2)}),flush=True)
    result=build(seed,source,copy_seed=False)
    destination=pathlib.Path(a.output);destination.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.NamedTemporaryFile(mode='w',encoding='utf-8',dir=destination.parent,delete=False) as f:
        json.dump(result,f,ensure_ascii=False,separators=(',',':'));f.flush();os.fsync(f.fileno());name=f.name
    os.replace(name,destination)
    print(json.dumps(result['meta'],ensure_ascii=False))


if __name__=='__main__':main()
