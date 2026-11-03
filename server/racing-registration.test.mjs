import {test} from 'node:test';
import assert from 'node:assert/strict';
import {createHmac} from 'node:crypto';
import {Rooms} from './rooms.mjs';
import {PredictionWeb} from './web.mjs';
import {HourlyEvents} from './hourly.mjs';
import {qqCommand} from './qq.mjs';
import {linkCandidates,syncQqWallet} from './qq-wallet.mjs';
import {normalizeRaceCommand,raceNoticeTargets,RACE_GROUP,LEGACY_GROUP,TRANSITION_SLOT} from './race-policy.mjs';
const bridgeKey='a'.repeat(64);
function fixture(save){
 let clock=Date.UTC(2026,10,3,6),now=()=>clock;const rooms=new Rooms(undefined,save,now),web=new PredictionWeb(rooms,{bridgeKey,now}),hourly=new HourlyEvents(rooms,web);
 const actor=(n='b')=>({subject:n.repeat(64),qqSubject:n.repeat(64),scope:'qq-racing',groupId:RACE_GROUP,name:'未绑定道友',boundAccount:false});
 const user=actor(),call=(text,extra={},who=user)=>qqCommand(web,hourly,who,{text,messageId:'race-message',messageAt:now(),...extra});
 const snapshot={id:'racing-new-group',gameRoomId:'native-race',round:1,closedThrough:0,open:true,ended:false,players:Array.from({length:8},(_,i)=>({uid:'native-'+i,name:'选手'+i})),ranks:Array(8).fill(0),baseRates:Array(64).fill(40000),horseTitles:Array(8).fill('上等马'),states:Array.from({length:8},()=>({life:100,wins:0,losses:0})),builds:Array.from({length:8},()=>({characterId:1000001,characterName:'青松',careerId:1,careerName:'符师',sectId:1,sectName:'剑宗',realmId:1,realmName:'炼气',talents:[],baijia:[],fates:[],resonance:null,selectionRound:0,selectionsKnown:false,cards:null,cardNames:null}))};
 const open=()=>hourly.open({roomId:'native-race',round:1,isPrivate:false,playerCount:8,avgRankScore:6500},snapshot);
 const sign=p=>{const payload=Buffer.from(JSON.stringify({...p,expiresAt:now()+60000})).toString('base64url');return payload+'.'+createHmac('sha256',Buffer.from(bridgeKey,'hex')).update(payload).digest('base64url');};
 return {rooms,web,hourly,user,actor,call,open,snapshot,now,time:n=>clock=n,sign};
}
test('slash and bare racing commands normalize once and anonymous registration requires an explicit leaderboard ID',()=>{
 for(const text of ['赛马注册','/赛马注册'])assert.equal(normalizeRaceCommand(text),'/赛马注册');
 assert.equal(normalizeRaceCommand(' 赛马注册 小白 '),'/赛马注册 小白');assert.equal(normalizeRaceCommand('普通聊天'),null);
 const x=fixture(),before=structuredClone(x.rooms.state);assert(x.call('赛马注册').lines.some(l=>l.includes('上榜ID')));assert.deepEqual(x.rooms.state,before);
 assert.throws(()=>x.call('下注 1 100'),/先发送/);assert.deepEqual(x.rooms.state,before);
 const r=x.call('赛马注册 小白');assert(r.lines.some(l=>l.includes('注册成功')));for(const word of ['下注','选手','清仓','签到','认证游戏账号'])assert(r.lines.join(' ').includes(word));
 assert.equal(x.web.me(x.user).playBalance,1000);assert.equal(x.web.leaderboard().items[0].name,'小白');
 for(let i=0;i<4;i++)x.call('/赛马注册');assert.equal(x.web.me(x.user).playBalance,1000);assert.equal(Object.keys(x.rooms.state.accounts).length,1);
});
test('a registered unlicensed QQ can buy, sign in and transfer; receipt retries remain atomic',()=>{
 const x=fixture();x.call('赛马注册 小白');x.call('马币签到');assert.equal(x.web.me(x.user).playBalance,1200);x.call('/马币签到');assert.equal(x.web.me(x.user).playBalance,1200);
 const room=x.open(),order={messageId:'unlicensed-bet'};const first=x.call('下注 1 100',order);assert.equal(x.web.me(x.user).playBalance,1100);assert.equal(x.rooms.state.rooms[room.code].positions[x.web.playKey(x.user)][0].claim,460);
 assert.deepEqual(x.call('/下注 1 100',order),first);assert.equal(x.web.me(x.user).playBalance,1100);
 x.call('赛马 查单');qqCommand(x.web,x.hourly,x.user,{action:'ack',receiptId:first.receiptId});
 const recipient=x.actor('c'),transfer=x.call('马币转账 200',{recipientTicket:x.sign(recipient),messageId:'registered-transfer'});
 assert.equal(x.web.me(x.user).playBalance,900);assert.equal(x.web.me(recipient).playBalance,1200);assert.deepEqual(x.call('/马币转账 200',{recipientTicket:x.sign(recipient),messageId:'registered-transfer'}),transfer);
 x.call('赛马注册 收款道友',{},recipient);assert.equal(x.web.me(recipient).playBalance,1200);assert.equal(x.web.me(recipient).name,'收款道友');
});
test('later game binding retains the registered name, money, grants and old message receipt without double buying',()=>{
 const x=fixture(),uid='1234567890abcdef12345678';x.call('赛马注册 小白');x.call('马币签到');const room=x.open(),reply=x.call('下注 1 100',{messageId:'before-binding'});
 const game=x.rooms.register('游戏账户').token;x.rooms.state.accounts[game].uid=uid;
 const bound={...x.user,subject:'d'.repeat(64),boundAccount:true,name:'游戏名',bindingDigest:linkCandidates(x.rooms).digest,gameUidRef:createHmac('sha256',Buffer.from(bridgeKey,'hex')).update('prediction-game-uid:'+uid).digest('hex')};
 syncQqWallet(x.web,bound);const balance=x.web.me(bound).balance;
 assert.equal(x.web.me(bound).name,'小白');assert.deepEqual(x.call('下注 1 100',{messageId:'before-binding'},bound),reply);assert.equal(x.web.me(bound).balance,balance);
 qqCommand(x.web,x.hourly,bound,{action:'ack',receiptId:reply.receiptId});
 x.snapshot.ended=true;x.snapshot.round=15;x.snapshot.ranks=[1,2,3,4,5,6,7,8];x.hourly.sync(room.code,x.snapshot);
 syncQqWallet(x.web,bound);assert.equal(x.web.playKey(bound),game);assert.equal(x.web.me(bound).playBalance,2560);assert.equal(x.web.me(bound).checkIn.claimed,true);
 assert.equal(x.rooms.state.profiles[bound.subject].name,'小白');
});
test('registration failure and malformed IDs never create partial accounts or duplicate opening grants',()=>{
 let fail=false;const x=fixture(()=>{if(fail)throw Error('disk failed');});const before=structuredClone(x.rooms.state);
 for(const name of ['x'.repeat(25),'bad'+String.fromCharCode(1)])assert.throws(()=>x.call('赛马注册 '+name));
 assert.deepEqual(x.rooms.state,before);fail=true;assert.throws(()=>x.call('赛马注册 小白'),/disk/);assert.deepEqual(x.rooms.state,before);
});
test('migration sends the exact transition hour to both groups and retains per-group confirmations on restart',()=>{
 for(const kind of ['start','round','settled']){
  const targets=raceNoticeTargets(TRANSITION_SLOT,kind);assert.deepEqual(targets.map(t=>t.groupId),[RACE_GROUP,LEGACY_GROUP]);
  assert.equal(!!targets[1].notice,kind!=='round');if(kind!=='round')assert(targets[1].notice.includes(RACE_GROUP));
 }
 assert.deepEqual(raceNoticeTargets('2026110315','start').map(t=>t.groupId),[RACE_GROUP]);
 const x=fixture();x.open();const event=x.hourly.claim();assert.equal(event.targets.length,2);
 const financial=JSON.stringify(x.rooms.state.accounts);x.hourly.ack(event.id,event.lease,RACE_GROUP);assert.equal(x.rooms.state.hourly.outbox[0].deliveredAt,null);
 x.time(x.now()+60001);const restored=new Rooms(JSON.parse(JSON.stringify(x.rooms.state)),undefined,x.now),h=new HourlyEvents(restored,new PredictionWeb(restored,{bridgeKey,now:x.now}));
 const retry=h.claim();assert.deepEqual(retry.targets.map(t=>t.groupId),[LEGACY_GROUP]);h.ack(retry.id,retry.lease,LEGACY_GROUP);assert(h.data().outbox[0].deliveredAt);assert.equal(JSON.stringify(restored.state.accounts),financial);
});
