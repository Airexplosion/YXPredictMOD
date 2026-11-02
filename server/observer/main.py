"""One dedicated account, one public hourly match, no player/private actions."""
import argparse,hashlib,json,logging,math,pathlib,signal,time
import requests
from public_lobby import PublicLobby,LobbyError,read_token,room_filter
from projection import PublicProjection
from transport import Spectator,public_mode
from quotes import champion_rates,sources,titles

LOG=logging.getLogger('duma-observer')
STOP=False
def stop(*_):
    global STOP
    STOP=True

def eligible(info):
    return info.get('name')=='game_room' and info.get('status')==2 and info.get('isPrivate')is False and info.get('playerCount')==8 and 1<=info.get('round',0)<=2 and info.get('avgRankScore',0)>3000

class Backend:
    def __init__(self,url,key):self.url=url.rstrip('/');self.key=key;self.http=requests.Session()
    def call(self,action,**body):
        r=self.http.post(self.url+'/ops/hourly',json={'action':action,**body},headers={'Authorization':'Bearer '+self.key},timeout=12,allow_redirects=False)
        if not r.ok:raise LobbyError('Prediction service rejected '+action+' ('+str(r.status_code)+')')
        data=r.json()
        if data.get('ok')is not True:raise LobbyError('Prediction service did not confirm '+action)
        return data['data']

def ratings_for(roster):
    uids=[p['userInfo']['uid'] for p in roster if not p['isAI']]
    if uids:
        r=requests.get('https://elo.kaigua.vip/api/v1/ratings',params=[('uid',uid)for uid in uids],timeout=12,allow_redirects=False)
        r.raise_for_status();data=r.json()
        if not isinstance(data,dict)or set(data)-set(uids):raise LobbyError('Invalid private quote input')
    else:data={}
    result=[]
    for p in roster:
        uid=p['userInfo']['uid'];row=data.get(uid)
        if row is None:result.append({'uid':uid,'score':3000,'found':False});continue
        score=row.get('rating');sigma=row.get('sigma')
        if type(score)not in (int,float)or not math.isfinite(score)or not 0<=score<=10000 or type(sigma)not in (int,float)or not math.isfinite(sigma)or not 0<=sigma<=2000:raise LobbyError('Invalid private quote input')
        result.append({'uid':uid,'score':score,'found':True})
    return result

class SnapshotBuilder:
    def __init__(self,info,roster,model,card_names=None):
        self.info=info;self.roster=roster;self.players=[{'uid':p['userInfo']['uid'],'name':p['userInfo']['username'],'isAI':p['isAI']}for p in roster]
        by_uid={p['uid']:p for p in model}
        if len(by_uid)!=8 or set(by_uid)!={p['uid']for p in self.players}:raise LobbyError('Frozen quote model disagrees with public roster')
        self.ratings=[by_uid[p['uid']]for p in self.players];self.card_names=card_names or {};self.last_rates=[0]*64;self.game_id=None
    def build(self,p,live_round):
        states=p.public_state();game_id=p.game_id
        if not game_id or len(states)!=8:raise LobbyError('Public snapshot is not ready')
        if self.game_id not in (None,game_id):raise LobbyError('Frozen model cannot move to another game')
        self.game_id=game_id
        allocated=sources(game_id,self.players);ratings=[self.ratings[i]for i in allocated]
        quote_ready=True
        try:self.last_rates=champion_rates(ratings,states,p.round)
        except ValueError:
            # Missing final ranks never creates a quote or a fabricated result.
            quote_ready=False
        battles=[]
        for battle in p.battles.values():
            if not max(1,p.round-1)<=battle['round']<=p.round:continue
            a,z=battle['first'],battle['second'];cards=lambda q:(q.get('lastRound')or {}).get('usedCards')
            first,second=cards(a),cards(z)
            names=lambda ids:None if ids is None else [self.card_names.get(str(i),'未知牌' if i else '空位')for i in ids]
            battles.append({'round':battle['round'],'first':a['uid'],'second':z['uid'],'winner':battle['winner'],
                'firstMirror':a['isMirror'],'secondMirror':z['isMirror'],'firstCards':first,'secondCards':second,
                'firstNames':names(first),'secondNames':names(second),'firstLife':a['life'],'secondLife':z['life']})
        if len(battles)>16:raise LobbyError('Public battle budget exceeded')
        elimination=[min(p.round,s['wins']+s['losses'])if s['life']<=0 else 0 for s in states]
        complete=p.ended or p.round_complete
        return {'id':game_id,'gameRoomId':self.info['roomId'],'round':p.round,'liveRound':max(p.round,live_round),
            'closedThrough':p.closed_through,'open':quote_ready and not p.ended and complete and p.remaining_timer()>0 and p.closed_through<p.round and live_round<10,
            'ended':p.ended,'players':[{k:v for k,v in player.items()if k!='isAI'}for player in self.players],
            'states':[{k:v for k,v in s.items()if k!='rank'}for s in states],'ranks':[s['rank']for s in states],
            'active':[s['life']>0 for s in states],'baseRates':self.last_rates,'horseShuffle':True,
            'horseTitles':titles(game_id,self.players,self.ratings),'eliminationRounds':elimination,'roundComplete':complete,'battles':battles}

def refresh_info(reader,info):
    cursor=info.get('createdTs')
    if type(cursor)is not int:raise LobbyError('Missing public room cursor')
    rows=reader._http('POST',reader.game_endpoint+'/game/fetchRooms',headers=reader._headers(),json=room_filter(info.get('gameMode',0),cursor+1))
    if not isinstance(rows,list):raise LobbyError('Invalid public room refresh')
    row=next((r for r in rows if r.get('roomId')==info['roomId']),None)
    if row is None:return None
    if row.get('isPrivate')is not False or row.get('playerCount')!=8 or row.get('name')!='game_room':raise LobbyError('Room is no longer a public eight-player game')
    return row

def observe(config,backend,status):
    import fcntl
    reader=PublicLobby(config['tokenCache'],gap=1.4,timeout=12);spectator=None;lock=None
    _,uid=read_token(reader.token_cache)
    lockpath=pathlib.Path(config['lockDirectory'])/('public-observer-'+hashlib.sha256(uid.encode()).hexdigest()[:20]+'.lock')
    # Shared with the existing short-code collector; never displace its session.
    lock=lockpath.open('r');fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
    try:
        active=status['active'];info=active['info']if active else None
        if info is None:
            rooms=[]
            for game_mode in (0,3):
                listed,_=reader.list_rooms(game_mode,max_pages=4);rooms.extend(listed)
            candidates=[r for r in rooms if eligible(r)]
            if not candidates:LOG.info('No eligible newly started public match');return
            candidates.sort(key=lambda r:(r['round'],-r['createdTs']))
            info=candidates[0]
        else:reader.public_ids.add(info['roomId'])
        roster=reader.roster(info['roomId'])['players']
        if len(roster)!=8 or len({p['userInfo']['uid']for p in roster})!=8 or any(p['identityKind']=='unknown'for p in roster)or uid in {p['userInfo']['uid']for p in roster}:raise LobbyError('Public eight-player roster unavailable')
        model=active['model']if active else ratings_for(roster)
        if not model:raise LobbyError('Frozen quote model is missing; operator review required')
        card_names=json.loads(pathlib.Path(config['cardNames']).read_text(encoding='utf8'))if config.get('cardNames')else {}
        builder=SnapshotBuilder(info,roster,model,card_names);projection=PublicProjection(roster)
        mode=public_mode(reader);spectator=Spectator(reader,info,mode);spectator.join()
        code=active['code']if active else None;live_round=info['round'];last_meta=0;focus_index=0;meta_ready=False;started=time.monotonic()
        LOG.info('Public spectator connected (%s; %s)',mode,'recovering'if code else'candidate')
        while not STOP:
            if time.monotonic()-last_meta>=4:
                meta=refresh_info(reader,info);last_meta=time.monotonic()
                meta_ready=meta is not None
                if meta:live_round=max(live_round,meta['round'])
                elif not code:raise LobbyError('Candidate is no longer listed')
            raw=spectator.read(projection.request_round()if mode=='delayed'else -1,timeout=10)
            ready=projection.accept(raw)if mode=='delayed'else projection.accept_ordinary(raw)
            if mode=='ordinary'and ready:
                visible=[p['userInfo']['uid']for p in roster if projection.players[p['userInfo']['uid']]['life']>0]
                if visible:
                    target=visible[focus_index%len(visible)];focus_index+=1
                    result=spectator.focus_public(target,projection.expected)
                    if result:projection.ordinary_event({'type':'BattleResult','data':result})
            for event in spectator.events:projection.ordinary_event(event)
            spectator.events.clear();projection.tick()
            if not ready and not projection.game_id:
                if time.monotonic()-started>90:raise LobbyError('Waiting for public spectator data exceeded budget')
                time.sleep(2);continue
            snapshot=builder.build(projection,live_round)
            if not meta_ready:snapshot['open']=False
            if active and snapshot['id']!=active['gameId']:raise LobbyError('Recovered game identity disagrees')
            if code is None:
                # Fresh metadata + observed round must both still satisfy opening rules.
                if live_round>2 or projection.round>2:LOG.info('Candidate passed opening window');return
                view=backend.call('open',info=info,snapshot=snapshot,model=model);code=view['code'];LOG.info('Hourly market opened')
            else:view=backend.call('sync',code=code,snapshot=snapshot)
            if view.get('settled'):LOG.info('Hourly market fully settled');return
            if time.monotonic()-started>3*3600:raise LobbyError('Public match watch budget exceeded; positions retained')
            time.sleep(2)
    finally:
        try:
            if spectator:spectator.close()
            else:reader.close()
        finally:
            if lock:lock.close()

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--config',required=True);parser.add_argument('--once',action='store_true');args=parser.parse_args()
    config=json.loads(pathlib.Path(args.config).read_text(encoding='utf8'));backend=Backend(config['backend'],config['key'])
    logging.basicConfig(level=logging.INFO,format='%(asctime)s %(levelname)s %(message)s')
    signal.signal(signal.SIGTERM,stop);signal.signal(signal.SIGINT,stop)
    while not STOP:
        try:
            status=backend.call('status')
            if status['active']or status['available']and not status['opened']:observe(config,backend,status)
        except BlockingIOError:LOG.warning('Dedicated spectator account is busy; preserving existing session')
        except Exception as error:LOG.warning('Observation paused (%s); positions retained',str(error)if isinstance(error,LobbyError)else type(error).__name__)
        if args.once:break
        for _ in range(15):
            if STOP:break
            time.sleep(1)

if __name__=='__main__':main()
