"""Read verified inputs and enforce a single balance period for the shadow rating study."""
import collections
import datetime as dt
import hashlib
import json
from pathlib import Path
import sqlite3
import time
import urllib.request
import zipfile
import zlib
from contextlib import closing

START = 1793260800000  # 2026-10-29 16:00 Asia/Shanghai
VERSION = "001.0008.0004"
STORE = Path("/var/lib/yx-replay")
FROZEN = Path("/root/yx-replay/character-elo-v1/window-20261029-bjt")
PILOT = Path("/root/yx-replay/observer-pilot-20261029/anonymous-34224197.zip")
ANCHOR_NEWS="1847920302233330"
NEWS="https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=1948800&count=50&maxlength=200"

def configure(directory):
    """Each period has its own version cache, forecasts and explicit reviewed scope."""
    global START, VERSION, ANCHOR_NEWS
    config=json.loads((directory/"period.json").read_text())
    if (set(config)!={"start","version","anchorNews"} or type(config["start"]) is not int
            or config["start"]<1793260800000 or version_tuple(config["version"]) is None
            or not str(config["anchorNews"]).isdigit()):
        raise ValueError("Invalid reviewed period configuration")
    START,VERSION,ANCHOR_NEWS=config["start"],config["version"],str(config["anchorNews"])

def notice_candidates(items):
    official=[r for r in items if r.get("feedname")=="steam_community_announcements"]
    anchor=next((r for r in official if str(r["gid"])==ANCHOR_NEWS),None)
    if anchor is None:raise ValueError("Balance announcement outside the verified news feed")
    zone=dt.timezone(dt.timedelta(hours=8))
    found={}
    for r in official:
        if r["date"]<=anchor["date"]:continue
        # A notice may precede the actual change. Stop at midnight Beijing time
        # until reviewed, including if the replay format version did not change.
        date=dt.datetime.fromtimestamp(r["date"],zone).replace(hour=0,minute=0,second=0,microsecond=0)
        found[str(r["gid"])]=int(date.timestamp()*1000)
    return found

def notice_boundary(directory):
    with urllib.request.urlopen(NEWS,timeout=15) as response:
        if response.geturl()!=NEWS:raise ValueError("Unexpected news source")
        raw=response.read(512_001)
    if len(raw)>512_000:raise ValueError("Oversized news feed")
    feed=json.loads(raw)["appnews"]
    if feed["appid"]!=1948800:raise ValueError("Wrong game in news feed")
    candidates=notice_candidates(feed["newsitems"])
    # A removed notice must not silently reopen a period. Only reviewed unrelated
    # notices may be explicitly excluded by placing their IDs in this private file.
    with closing(sqlite3.connect(directory/"version-cache.sqlite3")) as db:
        db.execute("CREATE TABLE IF NOT EXISTS notices(gid TEXT PRIMARY KEY,begin INTEGER)")
        db.executemany("INSERT OR IGNORE INTO notices VALUES(?,?)",candidates.items())
        db.commit()
        known=dict(db.execute("SELECT gid,begin FROM notices"))
    file=directory/"reviewed-unrelated-news.txt"
    ignored=set(file.read_text().split()) if file.exists() else set()
    reviewed=directory/"reviewed-news-boundaries.json"
    overrides=json.loads(reviewed.read_text()) if reviewed.exists() else {}
    for gid,begin in overrides.items():
        if gid not in known or type(begin) is not int or begin<=START:
            raise ValueError("Invalid reviewed announcement boundary")
        known[gid]=begin
    blocked=sorted(known.keys()-ignored)
    return min((known[k] for k in blocked),default=None),blocked

def readonly(path):
    return closing(sqlite3.connect(path.resolve().as_uri()+"?mode=ro", uri=True, timeout=15))

def version_tuple(v):
    parts = v.split(".")
    return tuple(map(int, parts)) if len(parts)==3 and all(x.isascii() and x.isdigit() for x in parts) else None

def pilot_version(game):
    with zipfile.ZipFile(PILOT) as z:
        manifest = json.loads(z.read("manifest.json"))
        assert manifest["codeId"] == game["code"] and len(manifest["views"]) == 8
        versions = set()
        for view in manifest["views"]:
            value = z.read(view["file"])
            assert hashlib.sha256(value).hexdigest() == view["sha256"]
            record = json.loads(value)
            assert (record["codeId"],record["gameId"],record["beginTs"]) == (game["code"],game["gameId"],game["begin"])
            versions.add(record.get("version",""))
        return versions

def load(directory, end=None):
    asof = time.time_ns()//1000000
    with readonly(FROZEN/"window-private.sqlite3") as db:
        games = {code:json.loads(raw) for code,raw in db.execute("SELECT code,event FROM events")}
    pilot = json.loads((FROZEN/"pilot-private.json").read_text())
    assert len(pilot)==1
    p = pilot[0]
    games[p["code"]]=p
    # The live cache can retract previously eligible inputs. Its entire time range
    # supersedes the historical snapshot; rooms captured earlier remain in the archive.
    with readonly(STORE/"character-elo-v1/inputs.sqlite3") as db:
        live=dict(db.execute("SELECT code,event FROM inputs"))
    with readonly(STORE/"rank-identities.sqlite3") as source:
        source.execute("BEGIN")
        current_rooms = dict(source.execute("SELECT code_id, MIN(created) FROM link_rooms WHERE code_id IS NOT NULL GROUP BY code_id"))
        floor = 1793382346629  # eligibility cutoff used by the live cache
        for code in list(games):
            if code!=p["code"] and current_rooms.get(code,0)>=floor:del games[code]
        for code,raw in live.items():
            if raw:games[code]=json.loads(raw)
            else:games.pop(code,None)
        conflicts={r[0] for r in source.execute("SELECT code_id FROM link_conflicts")}
        eligible={r[0] for r in source.execute("SELECT code_id FROM link_rooms WHERE code_id IS NOT NULL GROUP BY code_id HAVING count(*)=1 AND min(status)='verified'")}
        meta=collections.defaultdict(list)
        for code,idx,gid,begin,sha in source.execute("SELECT code_id,view_index,game_id,begin_ts,sha FROM link_replays WHERE begin_ts>?",(START,)):
            if code in games:meta[code].append((idx,gid,begin,sha))
        source.commit()  # never retain a WAL reader while decoding payloads
        with closing(sqlite3.connect(directory/"version-cache.sqlite3")) as cache:
            cache.execute("CREATE TABLE IF NOT EXISTS views(sha TEXT PRIMARY KEY,code INTEGER,gid TEXT,begin INTEGER,version TEXT)")
            known={sha:(code,gid,begin,v) for sha,code,gid,begin,v in cache.execute("SELECT * FROM views")}
            versions={};new=0
            for code,g in games.items():
                if code in conflicts or (code!=p["code"] and code not in eligible):continue
                if not START<g["begin"]<g["end"]<=asof:continue
                if end is not None and g["end"]>=end:continue
                if code==p["code"] and not meta[code]:
                    versions[code]=pilot_version(g)
                    continue
                found=set()
                for idx,gid,begin,sha in meta[code]:
                    if gid!=g["gameId"] or begin!=g["begin"]:
                        found.add("");continue
                    if sha not in known:
                        row=source.execute("SELECT payload FROM link_replays WHERE code_id=? AND view_index=? AND sha=?",(code,idx,sha)).fetchone()
                        if row is None:
                            found.add("");continue
                        blob=row[0]
                        if hashlib.sha256(blob).hexdigest()!=sha:raise ValueError("Replay digest mismatch")
                        data=json.loads(zlib.decompress(blob))
                        stamp=(data.get("codeId"),data.get("gameId"),data.get("beginTs"),data.get("version") or "")
                        if stamp[:3]!=(code,gid,begin):raise ValueError("Replay header mismatch")
                        known[sha]=stamp
                        cache.execute("INSERT INTO views VALUES(?,?,?,?,?)",(sha,*stamp))
                        new+=1
                        if new%2000==0:cache.commit()
                    cc,gg,bb,v=known[sha]
                    if (cc,gg,bb)!=(code,gid,begin):raise ValueError("Conflicting cached view")
                    found.add(v)
                versions[code]=found
            cache.commit()
    first_new=min((games[code]["begin"] for code,vs in versions.items() for v in vs
                   if version_tuple(v) and version_tuple(v)>version_tuple(VERSION)),default=None)
    with closing(sqlite3.connect(directory/"version-cache.sqlite3")) as cache:
        cache.execute("CREATE TABLE IF NOT EXISTS boundaries(begin INTEGER PRIMARY KEY)")
        if first_new is not None:cache.execute("INSERT OR IGNORE INTO boundaries VALUES(?)",(first_new,))
        first_new=cache.execute("SELECT min(begin) FROM boundaries").fetchone()[0]
        cache.commit()
    stop=min(v for v in (first_new,end,asof+1) if v is not None)
    accepted=[games[c] for c,vs in versions.items() if vs=={VERSION} and games[c]["end"]<stop]
    assert len({g["gameId"] for g in accepted})==len(accepted)
    for g in accepted:
        rows=g["rows"]
        assert 2<=len(rows)<=8 and len({p["uid"] for p in rows})==len(rows)
        assert len({p["rank"] for p in rows})==len(rows)
        g["rows"]=sorted(rows,key=lambda x:x["rank"])
    return accepted,dict(asOf=asof,strictStart=START,version=VERSION,stopAt=min([x for x in (first_new,end) if x is not None],default=None),
                         versionChecked=len(versions),wrongOrMissingVersion=sum(vs!={VERSION} for vs in versions.values()),
                         firstBegin=min((g["begin"] for g in accepted),default=None),latestEnd=max((g["end"] for g in accepted),default=None),games=len(accepted))
