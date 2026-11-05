"""Export or mirror the public season-11 character board, with no participant records."""
import argparse
import json
import math
from pathlib import Path
import re
import subprocess
import sys
import tempfile

STARTS = (1792033200000, 1792648800000, 1793253600000, 1793858400000)
VERSIONS = {STARTS[2]: "001.0008.0004", STARTS[3]: "001.0008.0005"}
BANDS = ("mid", "high")
MODEL = "within"
SCHEMA = "season11-character-elo-v1"
INDEX = Path("/var/lib/yx-patch-rating-study/season11/latest.json")
NAMES = Path("/root/yx-replay/character-elo-v1/characters.json")
SSH_DIR = Path("/etc/yx-character-board")

def count(v):
    if type(v) is not int or v < 0:
        raise ValueError("Invalid count or time")
    return v

def cell(score, appearances, players):
    if type(score) not in (int, float) or not math.isfinite(score) or not 0 <= score <= 10000:
        raise ValueError("Invalid role score")
    if count(players) > count(appearances) or bool(players) != bool(appearances):
        raise ValueError("Invalid role sample")
    return {"score": score, "appearances": appearances, "players": players}

def public_board(index, catalog):
    if (index["seasonOnlySelection"]["model"] != MODEL or index["preseasonGames"] != 0
            or index["seasonStart"] != STARTS[0] or len(index["firstTwoEras"]) != 2
            or index["currentArchive"]["start"] not in VERSIONS):
        raise ValueError("Not the selected season-only index")
    periods = []
    for i, era in enumerate(index["firstTwoEras"]):
        if (era["start"], era["end"]) != STARTS[i:i+2]:
            raise ValueError("Historical period boundary mismatch")
        support = era["support"]
        ratings = era["ratings"][MODEL]
        if any(set(ratings[b]) != set(catalog) for b in BANDS):
            raise ValueError("Incomplete historical role catalog")
        heroes = []
        for h, name in catalog.items():
            bands = {b: cell(ratings[b][h], *support["perHero"][b].get(h,[0,0])) for b in BANDS}
            heroes.append({"name": name, **bands})
        periods.append(dict(start=era["start"], end=era["end"], first=era["first"], settled=era["last"],
                            asOf=era["last"], games=support["games"], players=support["users"], heroes=heroes))
    current=index["currentArchive"]["start"]
    closed=index.get("closedPatches",[])
    if [p["start"] for p in closed]!=list(STARTS[2:STARTS.index(current)]):
        raise ValueError("Missing closed balance period")
    for patch in closed:
        if patch["scope"]["stopAt"]!=STARTS[STARTS.index(patch["start"])+1]:
            raise ValueError("Historical patch boundary mismatch")
        periods.append(patch_period(patch["start"],patch["scope"],patch["scores"],patch["players"],catalog))
    periods.append(patch_period(current,index["liveScope"],index["chosenLiveScores"],index["livePlayers"],catalog))
    return dict(schema=SCHEMA,model=MODEL,periods=periods)

def patch_period(start,scope,scores,players,catalog):
    if scope["version"] != VERSIONS[start] or scope["strictStart"] < start:
        raise ValueError("Unexpected patch scope")
    if scope["firstBegin"] is not None and scope["firstBegin"]<=scope["strictStart"]:
        raise ValueError("Match outside strict patch scope")
    ratings = {}
    for b in BANDS:
        raw = scores[b]
        rows = {str(row["hero"]): cell(row["score"],row["appearances"],row["players"]) for row in raw}
        if len(rows) != len(raw) or set(rows) != set(catalog):
            raise ValueError("Incomplete live role catalog")
        ratings[b] = rows
    heroes = [dict(name=name, **{b: ratings[b][h] for b in BANDS}) for h,name in catalog.items()]
    return dict(start=start, end=scope["stopAt"], first=scope["firstBegin"], settled=scope["latestEnd"],
                asOf=scope["asOf"], games=scope["games"], players=players, heroes=heroes)

def validate(d, names):
    if set(d) != {"schema","model","periods"} or d["schema"] != SCHEMA or d["model"] != MODEL or len(d["periods"]) not in (3,4):
        raise ValueError("Unexpected public board schema")
    if not names or any(not re.fullmatch(r"[\u3400-\u9fff·]{1,18}", n) for n in names):
        raise ValueError("Unrecognized character name")
    for i,p in enumerate(d["periods"]):
        if set(p) != {"start","end","first","settled","asOf","games","players","heroes"} or p["start"] != STARTS[i]:
            raise ValueError("Unexpected period fields")
        if count(p["asOf"])<p["start"] or bool(count(p["games"]))!=bool(count(p["players"])):
            raise ValueError("Invalid period sample")
        if p["games"]:
            if not p["start"] <= count(p["first"]) < count(p["settled"]) <= p["asOf"]:
                raise ValueError("Invalid period timestamps")
        elif p["first"] is not None or p["settled"] is not None or i!=len(d["periods"])-1:
            raise ValueError("Invalid empty period")
        if (i<len(d["periods"])-1 and p["end"] != STARTS[i+1]
                or p["end"] is not None and (count(p["end"])<=p["start"] or p["games"] and p["settled"]>=p["end"])):
            raise ValueError("Unexpected period end")
        if {h["name"] for h in p["heroes"]} != names or len(p["heroes"]) != len(names):
            raise ValueError("Unexpected public catalog")
        for h in p["heroes"]:
            if set(h) != {"name",*BANDS}:
                raise ValueError("Unexpected character fields")
            for b in BANDS:
                if set(h[b]) != {"score","appearances","players"}:
                    raise ValueError("Unexpected score fields")
                cell(**h[b])
                if not p["games"] and (h[b]["appearances"] or h[b]["players"] or h[b]["score"]!=3000):
                    raise ValueError("Empty period must start at 3000")

def encode(data):
    return json.dumps(data,ensure_ascii=False,separators=(",",":"),allow_nan=False).encode()

def mirror(folder):
    # The remote key is bound to export only this anonymous feed.
    command = ["/usr/bin/ssh","-F","/dev/null","-T","-i",str(SSH_DIR/"source_key"),
               "-o","BatchMode=yes","-o","IdentitiesOnly=yes","-o","StrictHostKeyChecking=yes",
               "-o","UserKnownHostsFile="+str(SSH_DIR/"known_hosts"),"-o","ConnectTimeout=8","root@154.9.252.167"]
    raw = subprocess.run(command,capture_output=True,timeout=25,check=True).stdout
    if len(raw) > 160000:
        raise ValueError("Oversized season feed")
    data=json.loads(raw)
    names={h["name"] for h in json.loads((folder/"window.json").read_text(encoding="utf-8"))["heroes"]}
    validate(data,names)
    content=encode(data)
    target=folder/"season.json"
    if target.exists():
        previous=target.read_bytes()
        if previous==content:return
        old=json.loads(previous)["periods"]
        if len(old)>len(data["periods"]) or old[-1]["asOf"]>data["periods"][-1]["asOf"]:
            raise ValueError("Refusing an older source generation")
    with tempfile.NamedTemporaryFile(dir=folder,delete=False) as f:
        temp=Path(f.name);f.write(content)
    try:
        temp.chmod(0o644);temp.replace(target)
    finally:temp.unlink(missing_ok=True)

if __name__=="__main__":
    parser=argparse.ArgumentParser()
    parser.add_argument("--mirror",type=Path)
    args=parser.parse_args()
    if args.mirror:
        mirror(args.mirror)
    else:
        names={h:v["name"] for h,v in json.loads(NAMES.read_text(encoding="utf-8")).items()}
        public=public_board(json.loads(INDEX.read_text(encoding="utf-8")),names)
        validate(public,set(names.values()))
        sys.stdout.buffer.write(encode(public))
