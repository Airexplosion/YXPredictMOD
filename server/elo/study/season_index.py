"""Publish the selected model, with independent archives for each balance period."""
import json
import os
import tempfile
from pathlib import Path

STATE=Path("/var/lib/yx-patch-rating-study")
SEASON=STATE/"season11"
STARTS=(1792033200000,1792648800000,1793253600000,1793858400000)

def build_index(base, archived, live):
    a,b,c,d=STARTS
    if (base["earlierPatchDataUsed"]!=0 or [r["start"] for r in base["eras"]]!=[a,b,c]
            or base["selection"]["model"]!="within"):
        raise ValueError("Unexpected historical study")
    old=archived["scope"];new=live["scope"]
    if (old["version"]!="001.0008.0004" or old["strictStart"]<c or old["stopAt"]!=d
            or old["latestEnd"]>=d or new["version"]!="001.0008.0005" or new["strictStart"]!=d):
        raise ValueError("Mismatched balance periods")
    chosen=base["selection"]["model"]
    def scores(report):
        return {band:report["heroScores"][band][chosen] for band in ("mid","high")}
    return dict(seasonStart=a,preseasonGames=0,firstTwoEras=base["eras"][:2],
                seasonOnlySelection=base["selection"],
                closedPatches=[dict(start=c,scope=old,players=archived["players"],scores=scores(archived))],
                currentArchive={"start":d},liveScope=new,livePlayers=live["players"],
                chosenLiveScores=scores(live))

def build():
    read=lambda p:json.loads(p.read_text())
    result=build_index(read(SEASON/"starting-eras.json"),read(SEASON/"phase3-frozen.json"),
                       read(STATE/"20261105/latest.json"))
    with tempfile.NamedTemporaryFile(dir=SEASON,delete=False) as f:
        tmp=Path(f.name);f.write(json.dumps(result,ensure_ascii=False,allow_nan=False,separators=(",",":")).encode())
    try:tmp.replace(SEASON/"latest.json")
    finally:tmp.unlink(missing_ok=True)

if __name__=="__main__":
    os.umask(0o077);build();print("updated")
