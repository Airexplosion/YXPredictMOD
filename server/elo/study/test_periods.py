import collections
import hashlib
import json
from pathlib import Path
import sqlite3
import tempfile
import unittest
from unittest.mock import patch
import zlib

import patch_study_inputs as inputs
import patch_rating_study as study
from season_index import build_index, STARTS

class PeriodTests(unittest.TestCase):
    def test_configure_is_explicit_and_separate(self):
        with tempfile.TemporaryDirectory() as temp:
            folder=Path(temp)
            with patch.multiple(inputs,START=inputs.START,VERSION=inputs.VERSION,ANCHOR_NEWS=inputs.ANCHOR_NEWS):
                config=dict(start=STARTS[3],version="001.0008.0005",anchorNews="1848557880481669")
                (folder/"period.json").write_text(json.dumps(config))
                inputs.configure(folder)
                self.assertEqual((inputs.START,inputs.VERSION,inputs.ANCHOR_NEWS),tuple(config.values()))
                self.assertEqual(inputs.notice_candidates([dict(gid=config["anchorNews"],date=1793847299,feedname="steam_community_announcements")]),{})
                with self.assertRaises(ValueError):inputs.notice_candidates([])

    def test_reviewed_notice_uses_actual_update_hour(self):
        with tempfile.TemporaryDirectory() as temp:
            folder=Path(temp);gid="1848557880481669"
            (folder/"reviewed-news-boundaries.json").write_text(json.dumps({gid:STARTS[3]}))
            class Response:
                def __enter__(self):return self
                def __exit__(self,*args):pass
                def geturl(self):return inputs.NEWS
                def read(self,n):return json.dumps(dict(appnews=dict(appid=1948800,newsitems=[]))).encode()
            with patch.object(inputs.urllib.request,"urlopen",return_value=Response()),patch.object(inputs,"notice_candidates",return_value={gid:1793808000000}):
                self.assertEqual(inputs.notice_boundary(folder),(STARTS[3],[gid]))

    def test_loader_filters_version_boundary_conflict_retraction_and_future(self):
        with tempfile.TemporaryDirectory() as temp:
            folder=Path(temp);frozen=folder/"frozen";frozen.mkdir();(folder/"character-elo-v1").mkdir()
            start=STARTS[3];now=start+10000
            def game(code,begin,end):
                return dict(code=code,gameId=str(code),begin=begin,end=end,rows=[dict(uid="a",rank=1),dict(uid="b",rank=2)])
            pilot=game(1,start-200,start-100)
            (frozen/"pilot-private.json").write_text(json.dumps([pilot]))
            with sqlite3.connect(frozen/"window-private.sqlite3") as db:db.execute("create table events(code integer,event text)")
            db.close()
            games=[pilot,game(2,start-50,start+500),game(3,start+100,start+500),
                   game(4,start+200,start+600),game(5,start+300,start+700),
                   game(6,start+400,start+800),game(7,start+500,start+900),game(8,start+600,now+1)]
            with sqlite3.connect(folder/"character-elo-v1/inputs.sqlite3") as db:
                db.execute("create table inputs(code integer,event text)")
                db.executemany("insert into inputs values(?,?)",[(g["code"],json.dumps(g) if g["code"]!=7 else None) for g in games])
            db.close()
            with sqlite3.connect(folder/"rank-identities.sqlite3") as db:
                db.execute("create table link_rooms(code_id integer,created integer,status text)")
                db.execute("create table link_conflicts(code_id integer)")
                db.execute("insert into link_conflicts values(5)")
                db.execute("create table link_replays(code_id integer,view_index integer,game_id text,begin_ts integer,sha text,payload blob)")
                for g in games:
                    code=g["code"];db.execute("insert into link_rooms values(?,?,'verified')",(code,start))
                    versions=["001.0008.0004"] if code==4 else ["001.0008.0005"]
                    if code==6:versions.append("001.0008.0004")
                    for idx,version in enumerate(versions):
                        payload=zlib.compress(json.dumps(dict(codeId=code,gameId=g["gameId"],beginTs=g["begin"],version=version)).encode())
                        db.execute("insert into link_replays values(?,?,?,?,?,?)",(code,idx,g["gameId"],g["begin"],hashlib.sha256(payload).hexdigest(),payload))
            db.close()
            with patch.multiple(inputs,START=start,VERSION="001.0008.0005",STORE=folder,FROZEN=frozen),patch.object(inputs.time,"time_ns",return_value=now*1000000):
                accepted,scope=inputs.load(folder)
                self.assertEqual([g["code"] for g in accepted],[3])
                self.assertEqual(scope["games"],1)
                accepted,scope=inputs.load(folder,start+400)
                self.assertEqual(accepted,[]);self.assertIsNone(scope["firstBegin"])

    def test_empty_and_sparse_samples_keep_full_catalog(self):
        models={k:study.Rating(k) for k in study.RECIPES};scales={k:1 for k in study.RECIPES}
        report=study.hero_report([],models,scales,STARTS[3],{1,2,3})
        for band in report.values():
            self.assertEqual([x["score"] for x in band["within"]],[3000,3000,3000])
            self.assertEqual([x["appearances"] for x in band["within"]],[0,0,0])
        g=dict(rows=[dict(uid="a",hero=1,band="mid",mode="rank",random=False,rank=1),dict(uid="b",hero=2,band="mid",mode="rank",random=False,rank=2)])
        for model in models.values():model.finish(g["rows"],model.start(g["rows"],STARTS[3]),STARTS[3]+1)
        report=study.hero_report([g],models,scales,STARTS[3]+1,{1,2,3})
        self.assertEqual(len(report["mid"]["within"]),3)
        self.assertGreater(report["mid"]["within"][0]["score"],report["mid"]["within"][1]["score"])

    def test_index_rejects_cross_period_inputs(self):
        base=dict(earlierPatchDataUsed=0,eras=[dict(start=s) for s in STARTS[:3]],selection={"model":"within"})
        scores={b:{"within":[]} for b in ("mid","high")}
        old=dict(scope=dict(version="001.0008.0004",strictStart=STARTS[2]+1,stopAt=STARTS[3],latestEnd=STARTS[3]-1),players=1,heroScores=scores)
        new=dict(scope=dict(version="001.0008.0005",strictStart=STARTS[3]),players=1,heroScores=scores)
        self.assertEqual(build_index(base,old,new)["currentArchive"]["start"],STARTS[3])
        old["scope"]["latestEnd"]=STARTS[3]
        with self.assertRaises(ValueError):build_index(base,old,new)

if __name__=="__main__":unittest.main()
