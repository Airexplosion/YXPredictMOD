"""Publish the selected model, with independent archives for each balance period."""
import json
import os
import tempfile
from pathlib import Path

STATE=Path("/var/lib/yx-patch-rating-study")
SEASON=STATE/"season11"
STARTS=(1792033200000,1792648800000,1793253600000,1793858400000,1794463200000)
VERSIONS=("001.0008.0004","001.0008.0005","001.0008.0006")

def build_index(base, archived, live):
    a,b,c=STARTS[:3]
    if (base["earlierPatchDataUsed"]!=0 or [r["start"] for r in base["eras"]]!=[a,b,c]
            or base["selection"]["model"]!="within"):
        raise ValueError("Unexpected historical study")
    archives=[archived] if isinstance(archived,dict) else archived
    if not 1<=len(archives)<=2:raise ValueError("Missing archived patches")
    for i,report in enumerate(archives):
        old=report["scope"];start,end=STARTS[i+2:i+4]
        if (old["version"]!=VERSIONS[i] or old["strictStart"]<start or not start<old["stopAt"]<=end
                or old["latestEnd"]>=old["stopAt"]):raise ValueError("Mismatched archived period")
    current=STARTS[len(archives)+2];new=live["scope"]
    if new["version"]!=VERSIONS[len(archives)] or new["strictStart"]!=current:
        raise ValueError("Mismatched balance periods")
    chosen=base["selection"]["model"]
    def scores(report):
        return {band:report["heroScores"][band][chosen] for band in ("mid","high")}
    return dict(seasonStart=a,preseasonGames=0,firstTwoEras=base["eras"][:2],
                seasonOnlySelection=base["selection"],
                closedPatches=[dict(start=STARTS[i+2],scope=r["scope"],players=r["players"],scores=scores(r)) for i,r in enumerate(archives)],
                currentArchive={"start":current},liveScope=new,livePlayers=live["players"],
                chosenLiveScores=scores(live))

def build():
    read=lambda p:json.loads(p.read_text())
    result=build_index(read(SEASON/"starting-eras.json"),[read(SEASON/"phase3-frozen.json"),read(SEASON/"phase4-frozen.json")],
                       read(STATE/"20261112/latest.json"))
    with tempfile.NamedTemporaryFile(dir=SEASON,delete=False) as f:
        tmp=Path(f.name);f.write(json.dumps(result,ensure_ascii=False,allow_nan=False,separators=(",",":")).encode())
    try:tmp.replace(SEASON/"latest.json")
    finally:tmp.unlink(missing_ok=True)

if __name__=="__main__":
    os.umask(0o077);build();print("updated")
