import copy
import unittest
from season_scores import MODEL, STARTS, public_board, validate

class SeasonScoreTests(unittest.TestCase):
    def fixture(self):
        cats={"1":"甲","2":"乙"};ratings={b:{"1":3010.5,"2":2989.5} for b in ("mid","high")}
        eras=[dict(start=s,end=end,first=s+1,last=end-1,ratings={MODEL:ratings},
            support={"games":18,"users":24,"perHero":{b:{"1":[4,2],"2":[8,7]} for b in ratings}},
            internal=[{"uid":"secret"}]) for s,end in zip(STARTS[:2],STARTS[1:3])]
        c=STARTS[2]
        live={b:[dict(hero=int(h),score=v,appearances=12,players=9,ignored="secret") for h,v in scores.items()] for b,scores in ratings.items()}
        index=dict(seasonOnlySelection={"model":MODEL},seasonStart=STARTS[0],preseasonGames=0,
                   firstTwoEras=eras,currentArchive={"start":c},livePlayers=25,
                   liveScope=dict(version="001.0008.0004",strictStart=c+100,firstBegin=c+200,latestEnd=c+400,stopAt=None,asOf=c+500,games=20),
                   chosenLiveScores=live)
        return index,cats

    def fourth(self):
        index,cats=self.fixture();d=STARTS[3]
        old=copy.deepcopy(index["liveScope"]);old["stopAt"]=d
        index["closedPatches"]=[dict(start=STARTS[2],scope=old,players=index["livePlayers"],scores=copy.deepcopy(index["chosenLiveScores"]))]
        index["currentArchive"]={"start":d}
        index["liveScope"].update(version="001.0008.0005",strictStart=d,firstBegin=d+1,latestEnd=d+2,asOf=d+3)
        return index,cats

    def test_fourth_period_keeps_history_and_excludes_private_fields(self):
        index,cats=self.fourth();board=public_board(index,cats);validate(board,set(cats.values()))
        self.assertEqual([p["start"] for p in board["periods"]],list(STARTS))
        self.assertEqual(board["periods"][2]["end"],STARTS[3])
        self.assertNotIn("secret",str(board))

    def test_new_period_can_wait_for_first_verified_match(self):
        index,cats=self.fourth()
        index["liveScope"].update(games=0,firstBegin=None,latestEnd=None);index["livePlayers"]=0
        for rows in index["chosenLiveScores"].values():
            for row in rows:row.update(score=3000,appearances=0,players=0)
        board=public_board(index,cats);validate(board,set(cats.values()))
        board["periods"][-1]["heroes"][0]["mid"]["score"]=3010
        with self.assertRaises(ValueError):validate(board,set(cats.values()))

    def test_fourth_rejects_old_version_and_missing_third(self):
        index,cats=self.fourth()
        for edit in (lambda x:x["liveScope"].update(version="001.0008.0004"),
                     lambda x:x.update(closedPatches=[]),
                     lambda x:x["closedPatches"][0]["scope"].update(stopAt=STARTS[3]-1)):
            changed=copy.deepcopy(index);edit(changed)
            with self.assertRaises(ValueError):public_board(changed,cats)

    def test_allowlisted_eras_exclude_internal_fields(self):
        index,cats=self.fixture()
        board=public_board(index,cats);validate(board,set(cats.values()))
        self.assertNotIn("secret",str(board));self.assertEqual([p["games"] for p in board["periods"]],[18,18,20])
        self.assertEqual(board["periods"][-1]["heroes"][0]["mid"],{"score":3010.5,"appearances":12,"players":9})
        extra=copy.deepcopy(board);extra["periods"][-1]["heroes"][0]["mid"]["uid"]="no"
        with self.assertRaises(ValueError):validate(extra,set(cats.values()))

    def test_wrong_recipe_boundary_or_missing_catalog_aborts(self):
        index,cats=self.fixture()
        for edit in (lambda x:x["seasonOnlySelection"].update(model="joint"),
                     lambda x:x["firstTwoEras"][0].update(end=STARTS[2]),
                     lambda x:x["chosenLiveScores"]["mid"].pop()):
            changed=copy.deepcopy(index);edit(changed)
            with self.assertRaises(ValueError):public_board(changed,cats)

if __name__=="__main__":unittest.main()
