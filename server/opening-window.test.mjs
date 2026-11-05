import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Rooms} from './rooms.mjs';
import {PredictionWeb} from './web.mjs';
import {HourlyEvents} from './hourly.mjs';
import {qqCommand} from './qq.mjs';

function fixture(save){
 let clock=Date.UTC(2026,10,5,12),serial=0;
 const now=()=>clock,rooms=new Rooms(undefined,save,now),web=new PredictionWeb(rooms,{now}),hourly=new HourlyEvents(rooms,web);
 const snapshot={id:'opening-window-game',gameRoomId:'opening-native',round:2,liveRound:2,closedThrough:1,open:true,ended:false,sourcePhase:'preparation',
  players:Array.from({length:8},(_,i)=>({uid:'secret-uid-'+i,name:'选手'+i})),ranks:Array(8).fill(0),baseRates:Array(64).fill(40000),horseTitles:Array(8).fill('上等马'),
  states:Array.from({length:8},()=>({life:100,wins:0,losses:0})),builds:Array.from({length:8},()=>({characterId:1000001,characterName:'青松',careerId:1,careerName:'符师',sectId:1,sectName:'剑宗',realmId:1,realmName:'炼气',talents:[],baijia:[],fates:[],resonance:null,selectionRound:0,selectionsKnown:false,cards:null,cardNames:null}))};
 const view=hourly.open({roomId:snapshot.gameRoomId,round:2,isPrivate:false,playerCount:8,avgRankScore:6500},snapshot),code=view.code;
 const user={subject:'b'.repeat(64),qqSubject:'b'.repeat(64),scope:'qq-racing',groupId:'613486881',name:'道友',boundAccount:false};
 const call=(text,extra={},who=user)=>qqCommand(web,hourly,who,{text,messageId:'opening-command-'+(++serial),messageAt:now(),...extra});
 call('注册 测试道友');
 const ack=row=>{for(const t of row.targets)hourly.ack(row.id,row.lease,t.groupId);};
 return {rooms,web,hourly,snapshot,code,user,call,now,time:t=>clock=t,advance:t=>clock+=t,
  room:()=>rooms.state.rooms[code],key:()=>web.playKey(user),ack,start:()=>{const row=hourly.claim();assert.equal(row.kind,'start');ack(row);return row;},
  sync:patch=>{Object.assign(snapshot,patch);return hourly.sync(code,snapshot);}};
}

test('opening all-in starts at the first confirmed forward, not room creation, native round, claim or failed send',()=>{
 const x=fixture(),r=x.room(),created=x.now(),before=structuredClone(x.rooms.state);
 assert.equal(x.web.watch(r).openingAllIn.state,'pending');
 assert.equal(x.rooms.allInMultiplier(r),1);
 assert.throws(()=>x.call('全猜 3'),/发布确认/);assert.deepEqual(x.rooms.state,before);
 let row=x.hourly.claim();x.advance(61000);
 row=x.hourly.claim();assert.equal(row.kind,'start');assert.equal(r.openingAllIn.startedAt,null);
 assert.throws(()=>x.hourly.ack(row.id,'bad-lease'),/租约/);assert.equal(r.openingAllIn.startedAt,null);
 x.ack(row);
 assert.equal(x.room().openingAllIn.startedAt,created+61000);assert.equal(x.room().openingAllIn.endsAt,created+181000);
 x.sync({});const reply=x.call('全猜 3');
 assert.match(reply.lines[0],/额外×3/);assert.equal(x.room().positions[x.key()][2].claim,5807);
 assert.equal(x.room().positions[x.key()][2].cashoutLocked,true);
});

test('first successful destination opens exactly once, and a delayed second destination or restart never extends the deadline',()=>{
 const x=fixture(),r=x.room(),row=x.hourly.claim();
 // A historically mirrored notice still starts on the first real destination ACK.
 row.targets.push({groupId:'599092307'});
 const saved=x.hourly.data().outbox.find(e=>e.id===row.id);saved.deliveryTargets=row.targets;
 x.hourly.ack(row.id,row.lease,'613486881');const deadline=r.openingAllIn.endsAt;
 x.advance(45000);
 const rooms=new Rooms(JSON.parse(JSON.stringify(x.rooms.state)),undefined,x.now),h=new HourlyEvents(rooms,new PredictionWeb(rooms,{now:x.now}));
 h.ack(row.id,row.lease,'599092307');
 assert.equal(rooms.state.rooms[x.code].openingAllIn.endsAt,deadline);
 h.ack(row.id,row.lease,'599092307');
 assert.equal(rooms.state.rooms[x.code].openingAllIn.endsAt,deadline);
 assert.equal(h.data().outbox.filter(e=>e.kind==='opening-countdown').length,3);
});

test('two-minute triple all-in spans later rounds and live battles, while normal guesses and reductions keep their phase rules',()=>{
 const x=fixture();x.start();x.advance(50000);
 x.sync({round:3,liveRound:3,closedThrough:3,open:false,sourcePhase:'battle',roundComplete:false,eliminationRounds:Array(8).fill(0)});
 assert.equal(x.web.watch(x.room()).allInOpen,true);assert.equal(x.web.watch(x.room()).open,false);
 const result=x.call('全猜 3'),p=x.room().positions[x.key()][2];assert.equal(p.claim,5807);assert.equal(p.cashoutLocked,true);
 qqCommand(x.web,x.hourly,x.user,{action:'ack',receiptId:result.receiptId});
 x.sync({round:4,liveRound:4,closedThrough:3,open:true,sourcePhase:'preparation',roundComplete:true});
 for(const text of ['减半 3','撤回 3'])assert.throws(()=>x.call(text),/锁定至结算/);
 for(const kind of ['half','sell','switch'])assert.throws(()=>x.rooms.trade(x.code,x.key(),{kind,player:2,rank:1,stake:1,operation:'opening-lock-'+kind,version:x.room().version}),/锁定至结算/);
});

test('the exact deadline removes only the extra triple, and ordinary all-in retains its old phase and withdrawal rules',()=>{
 const x=fixture();x.start();const start=x.now();x.time(start+119999);x.sync({});
 const first=x.call('全猜 3');qqCommand(x.web,x.hourly,x.user,{action:'ack',receiptId:first.receiptId});
 assert.equal(x.room().positions[x.key()][2].claim,5807);
 x.time(start+120000);x.sync({round:3,liveRound:3,closedThrough:2});
 const second={...x.user,subject:'c'.repeat(64),qqSubject:'c'.repeat(64)};
 x.call('注册 第二道友',{},second);const normalClaim=x.room().quoteRates[24]/10,full=x.call('全猜 4',{},second),key=x.web.playKey(second);
 assert.equal(x.room().positions[key][3].claim,normalClaim);assert(!x.room().positions[key][3].cashoutLocked);
 assert(!full.lines[0].includes('额外×3'));qqCommand(x.web,x.hourly,second,{action:'ack',receiptId:full.receiptId});
 x.call('撤回 4',{},second);assert.equal(x.rooms.state.accounts[key].balance,970);
 assert.throws(()=>x.call('撤回 3'),/锁定至结算/);
 x.sync({round:4,liveRound:4,closedThrough:4,open:false,sourcePhase:'battle'});
 assert.equal(x.rooms.isOpen(x.room(),true),false);
});

test('countdowns fire at 60 and 90 seconds, end at 120 seconds, and release queued reports only after the end notice is acknowledged',()=>{
 const x=fixture();x.snapshot.round=2;x.start();const start=x.now(),r=x.room();
 const report=x.hourly.data().outbox;
 // A ready settlement stands in for a complete public report and must also wait.
 x.sync({round:3,liveRound:3,closedThrough:3,open:false,ended:true,ranks:[1,2,3,4,5,6,7,8]});
 assert(report.some(e=>e.kind==='settled'));assert.equal(x.hourly.claim(),null);
 x.time(start+59999);assert.equal(x.hourly.claim(),null);
 x.time(start+60000);let row=x.hourly.claim();assert.equal(row.remainingSeconds,60);assert(row.lines[0].includes('剩余1分钟'));x.ack(row);assert.equal(x.hourly.claim(),null);
 x.time(start+90000);row=x.hourly.claim();assert.equal(row.remainingSeconds,30);assert(row.lines[0].includes('剩余30秒'));x.ack(row);
 x.time(start+119999);assert.equal(x.hourly.claim(),null);
 x.time(start+120000);row=x.hourly.claim();assert.equal(row.remainingSeconds,0);assert.equal(row.textOnly,true);
 assert.equal(x.hourly.claim(),null);x.ack(row);
 const released=x.hourly.claim();assert.equal(released.kind,'settled');assert.equal(r.settled,true);
});

test('ready round two reports stay queued while the observer keeps syncing and resume after the end message',()=>{
 const x=fixture();x.start();const started=x.now();
 const battles=Array.from({length:4},(_,i)=>({round:2,first:x.snapshot.players[i*2].uid,second:x.snapshot.players[i*2+1].uid,winner:x.snapshot.players[i*2].uid,firstMirror:false,secondMirror:false,firstCards:[1000010],secondCards:[1000010],firstNames:['云剑'],secondNames:['云剑'],firstLife:90,secondLife:80,lifeDamage:10,resultComplete:true}));
 const builds=x.snapshot.builds.map(b=>({...b,selectionRound:2,selectionsKnown:true,cards:[1000010],cardNames:['云剑']}));
 x.sync({round:3,liveRound:3,closedThrough:2,battles,builds});
 assert.equal(x.hourly.ready(x.room(),'round',2),true);assert.equal(x.hourly.claim(),null);assert.equal(x.room().round,3);
 x.time(started+120000);const end=x.hourly.claim();assert.equal(end.remainingSeconds,0);x.ack(end);
 const next=x.hourly.claim();assert.equal(next.kind,'round');assert.equal(next.round,2);
 assert(!JSON.stringify(next.presentation).includes('secret-uid-'));
});

test('missed countdowns do not replay stale time remaining after downtime; the end notice remains durable and retryable',()=>{
 const x=fixture();x.start();x.advance(125000);
 const rooms=new Rooms(JSON.parse(JSON.stringify(x.rooms.state)),undefined,x.now),h=new HourlyEvents(rooms,new PredictionWeb(rooms,{now:x.now}));
 let end=h.claim();assert.equal(end.remainingSeconds,0);assert.equal(h.claim(),null);
 assert.equal(h.data().outbox.filter(e=>e.skippedAt).length,2);
 x.advance(61000);end=h.claim();assert.equal(end.remainingSeconds,0);
 for(const t of end.targets)h.ack(end.id,end.lease,t.groupId);
 assert.equal(h.claim(),null);
});

test('promotion never bypasses stale or verifying sources, incomplete snapshots, eliminated targets or final settlement',()=>{
 for(const reason of ['offline','verifying','incomplete','eliminated','partial-elimination','settled']){
  const x=fixture();x.start();
  if(reason==='offline')x.advance(15001);
  if(reason==='verifying')x.hourly.source(x.code,x.room().gameId,'verifying',2);
  if(reason==='incomplete')x.sync({roundComplete:false});
  if(reason==='eliminated')x.sync({active:[true,true,false,true,true,true,true,true]});
  if(reason==='partial-elimination'){const states=structuredClone(x.snapshot.states);states[2].life=0;x.sync({states,sourcePhase:'battle',roundComplete:false,eliminationRounds:Array(8).fill(0)});}
  if(reason==='settled')x.sync({ended:true,ranks:[1,2,3,4,5,6,7,8]});
  const before=structuredClone(x.rooms.state);assert.throws(()=>x.call('全猜 3'),undefined,reason);assert.deepEqual(x.rooms.state,before);
 }
});

test('ack persistence failures roll back clock, countdowns, delivery confirmations and room version together',()=>{
 let fail=false;const x=fixture(()=>{if(fail)throw Error('disk failure');}),row=x.hourly.claim(),before=structuredClone(x.rooms.state);
 fail=true;assert.throws(()=>x.ack(row),/disk failure/);assert.deepEqual(x.rooms.state,before);
 fail=false;x.ack(row);assert.equal(x.room().openingAllIn.startedAt,x.now());
 assert.equal(x.hourly.data().outbox.filter(e=>e.kind==='opening-countdown').length,3);
});

test('a deadline-crossing duplicate keeps its original triple fill and payout, without consuming later grants or repricing holdings',()=>{
 const x=fixture();x.start();const body={text:'全猜 3',messageId:'opening-retry',messageAt:x.now()},first=qqCommand(x.web,x.hourly,x.user,body);
 x.call('签到');x.advance(120000);x.sync({round:4,liveRound:4,closedThrough:3});
 assert.deepEqual(qqCommand(x.web,x.hourly,x.user,body),first);
 const p=x.room().positions[x.key()][2];assert.equal(p.claim,5807);assert.equal(p.cashoutLocked,true);
 x.sync({round:20,liveRound:20,closedThrough:20,open:false,ended:true,ranks:[2,3,1,4,5,6,7,8]});
 assert.equal(x.rooms.state.accounts[x.key()].balance,6007);
 x.sync({});assert.equal(x.rooms.state.accounts[x.key()].balance,6007);
});

test('legacy saved rooms retain their round-based offer and existing locks without inventing a new timer',()=>{
 const x=fixture();delete x.room().openingAllIn;
 x.sync({round:2,liveRound:2});assert.equal(x.rooms.allInMultiplier(x.room()),1);
 assert.equal(x.web.watch(x.room()).openingAllIn,null);
 assert.equal(x.hourly.data().outbox.filter(e=>e.kind==='opening-countdown').length,0);
});

