import pathlib,sys,unittest
from unittest.mock import Mock
import requests
sys.path.insert(0,str(pathlib.Path(__file__).parent))
from main import Backend,SyncError,upload_snapshot,push_frame,SnapshotBuilder,select_candidate,public_focus_targets
from public_lobby import PublicLobby,LobbyError
from projection import PublicProjection
from test_projection import ROSTER,UIDS,status,player,number,blob,string_field,encoded

class ContinuityTests(unittest.TestCase):
    def test_eliminated_player_remains_a_focus_target_until_the_final_public_battle_arrives(self):
        p=PublicProjection(ROSTER);p.accept_ordinary(encoded(status(4)))
        p.players[UIDS[0]].update(life=-3,wins=2,losses=2)
        self.assertIn(UIDS[0],public_focus_targets(p))
        battle={'round':4,'resultComplete':True,'first':{'uid':UIDS[0],'isMirror':True,'lastRound':{'usedCards':[1]}},'second':{'uid':UIDS[1],'isMirror':False}}
        p.battles['mirror']=battle;self.assertIn(UIDS[0],public_focus_targets(p))
        p.battles['actual']={**battle,'resultComplete':False,'first':{'uid':UIDS[0],'isMirror':False,'lastRound':{'usedCards':[1]}}}
        self.assertIn(UIDS[0],public_focus_targets(p));p.battles['actual']['resultComplete']=True
        self.assertNotIn(UIDS[0],public_focus_targets(p));self.assertIn(UIDS[1],public_focus_targets(p))

    def test_selection_is_highest_native_rank_among_recent_starts_then_newest_with_no_randomness(self):
        def room(code,score,created,round=1):return {'roomId':code,'avgRankScore':score,'createdTs':created,'round':round,'name':'game_room','status':2,'isPrivate':False,'playerCount':8}
        data=[room('newer-low',6500,900),room('older-high',8000,100,2),room('newest-high',8000,500),room('ineligible',9999,999,3)]
        for items in (data,list(reversed(data)),data[1:]+data[:1]):self.assertEqual(select_candidate(items)['roomId'],'newest-high')
        self.assertIsNone(select_candidate([room('too-low',5999,999)]));self.assertIsNone(select_candidate([room('same',8000,1),room('same',8000,1,3)]))
    def test_focused_battle_does_not_replace_authoritative_global_player_or_elimination_state(self):
        p=PublicProjection(ROSTER);p.accept_ordinary(encoded(status(4)));before=p.public_state()
        battle=blob(1,blob(1,player(0,life=-20,rank=7)))+blob(2,blob(1,player(1)))+number(7,4)+string_field(9,UIDS[1])
        p.ordinary_event({'type':'BattleResult','data':encoded(battle)})
        self.assertEqual(p.public_state(),before);self.assertEqual(next(iter(p.battles.values()))['first']['life'],-20)
    def test_rejection_preserves_specific_backend_reason_without_credentials(self):
        b=Backend('https://test.invalid','never-output-key');b.http=Mock();b.http.post.return_value=Mock(ok=False,status_code=400,json=lambda:{'error':'同轮公开牌桌冲突'})
        with self.assertRaises(SyncError)as e:b.call('sync',snapshot={'round':1})
        self.assertEqual(e.exception.reason,'同轮公开牌桌冲突');self.assertNotIn('never-output-key',str(e.exception))
    def test_display_only_failure_retries_original_authoritative_fields_and_not_bad_annotations(self):
        b=Mock();b.call.side_effect=[SyncError(400,'同轮公开牌桌冲突'),{'code':'123456'}]
        s={'id':'code:1','round':2,'open':False,'ranks':[0]*8,'baseRates':[12345]*64,'players':[{'uid':u}for u in UIDS],'builds':['invalid'],'battles':['invalid']}
        self.assertEqual(upload_snapshot(b,'sync',s,code='123456')['code'],'123456')
        retry=b.call.call_args.kwargs['snapshot'];self.assertEqual(retry,{k:v for k,v in s.items()if k not in('builds','battles')})
    def test_rejected_core_frame_keeps_same_backend_and_can_accept_the_next_full_frame(self):
        b=Mock();b.call.side_effect=[SyncError(400,'等待完整同轮出局名单'),{'ok':True},{'code':'123456'}]
        s={'id':'code:1','round':2};self.assertIsNone(push_frame(b,'123456',s,{},[]))
        self.assertEqual(b.call.call_args.args[0],'source');self.assertEqual(b.call.call_args.kwargs['state'],'verifying')
        self.assertEqual(push_frame(b,'123456',s,{},[])['code'],'123456')
    def test_backend_network_failure_is_not_a_game_connection_failure(self):
        b=Mock();b.call.side_effect=[requests.Timeout('network'),{'ok':True}]
        self.assertIsNone(push_frame(b,'123456',{'id':'code:1','round':2},{},[]));self.assertEqual(b.call.call_args.args[0],'source')
    def test_optional_lobby_refresh_can_use_one_short_attempt_without_sleeping_in_a_live_session(self):
        r=PublicLobby('unused-token-path');r._rate=Mock();r.http=Mock();r.http.request.side_effect=requests.Timeout()
        with self.assertRaises(LobbyError):r._http('GET','https://test.invalid',headers={},_attempts=1,_timeout=(1,2))
        self.assertEqual(r.http.request.call_count,1);self.assertEqual(r.http.request.call_args.kwargs['timeout'],(1,2))
    def test_snapshot_phase_distinguishes_preparation_from_battle(self):
        p=PublicProjection(ROSTER);p.accept_ordinary(encoded(status(1)));b=SnapshotBuilder({'roomId':'public'},ROSTER,[{'uid':u,'score':3000,'found':True}for u in UIDS])
        self.assertEqual(b.build(p,1)['sourcePhase'],'preparation');p.accept_ordinary(encoded(status(1).replace(number(2,30),number(2,0),1)));self.assertEqual(b.build(p,1)['sourcePhase'],'battle')
    def test_signed_native_damage_is_preserved_for_both_damage_directions(self):
        from public_snapshot import public_battles
        battle=blob(1,blob(1,player(0)))+blob(2,blob(1,player(1)))+number(6,-8)+number(7,1)+string_field(9,UIDS[1])
        self.assertEqual(public_battles('BattleResult',encoded(battle))[0]['lifeDamage'],-8)

if __name__=='__main__':unittest.main()
