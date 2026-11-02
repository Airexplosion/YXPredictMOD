import test from 'node:test';
import assert from 'node:assert/strict';
import {Rooms} from './rooms.mjs';
import {PredictionWeb} from './web.mjs';
import {HourlyEvents,hourlySlot,eligibleLobby,publicSummary} from './hourly.mjs';
import {qqCommand,serviceAccess} from './qq.mjs';
import http from 'node:http';
import {createHmac} from 'node:crypto';
import {createHandler} from './server.mjs';

const user={subject:'a'.repeat(64),name:'认证观众',scope:'qq-prediction',groupId:'599092307'};
function fixture(save){
 let now=Date.UTC(2026,10,1,0,0);const rooms=new Rooms(undefined,save,()=>now),web=new PredictionWeb(rooms,{now:()=>now});
 const hourly=new HourlyEvents(rooms,web),info={roomId:'native-hourly',round:1,isPrivate:false,avgRankScore:3001,playerCount:8};
 const snapshot={id:'code:1234567',gameRoomId:info.roomId,round:1,closedThrough:0,open:true,ended:false,
  players:Array.from({length:8},(_,i)=>({uid:'private-player-'+i,name:'选手'+(i+1)})),ranks:Array(8).fill(0),active:Array(8).fill(true),
  baseRates:Array(64).fill(40000),horseTitles:Array(8).fill('上等马'),states:Array.from({length:8},()=>({life:100,wins:0,losses:0}))};
 return {rooms,web,hourly,info,snapshot,now:()=>now,time:t=>now=t,open:()=>hourly.open(info,snapshot)};
}
test('Beijing hourly window excludes 02 through 06 inclusive, stops searching after five minutes and has stable day keys',()=>{
 for(let hour=0;hour<24;hour++){const at=Date.UTC(2026,10,1,hour-8);assert.equal(!!hourlySlot(at),!(hour>=2&&hour<=6));}
 assert.equal(hourlySlot(Date.UTC(2026,10,1,0,4,59)),'2026110108');assert.equal(hourlySlot(Date.UTC(2026,10,1,0,5)),null);
 assert.equal(hourlySlot(Date.UTC(2026,10,1,16)),'2026110200');
});
test('selection uses actual public eight-player round 1/2 and native rank score, never an Elo default',()=>{
 const x=fixture();assert(eligibleLobby(x.info));
 for(const patch of [{isPrivate:true},{round:0},{round:3},{avgRankScore:3000},{avgRankScore:2999},{playerCount:7},{roomId:'../private'}])assert(!eligibleLobby({...x.info,...patch}));
 assert(!eligibleLobby({...x.info,avgRankScore:undefined,elo:9999}));
});
test('one authoritative hourly room, immutable 15% premium, restart deduplication and unchanged standard quotes',()=>{
 const x=fixture(),r=x.open(),code=r.code;assert.equal(r.players[0].quoteRate,46000);assert.equal(r.bonusBps,11500);
 assert.equal(x.open().code,code);assert.equal(Object.keys(x.rooms.state.rooms).length,1);
 const restored=new Rooms(structuredClone(x.rooms.state),undefined,x.now),h=new HourlyEvents(restored,new PredictionWeb(restored,{now:x.now}));
 assert.equal(h.open(x.info,x.snapshot).code,code);
 x.time(x.now()+3600000);assert.throws(x.open,/上一场/);
 const key=x.rooms.register('普通房主').token,normal=x.rooms.create(key,{...x.snapshot,id:'another',gameRoomId:'another-native'});
 assert.equal(normal.quoteRates[0],40000);assert.equal(normal.bonusBps,10000);
 x.snapshot.round=2;x.snapshot.baseRates.fill(500000);x.hourly.sync(code,x.snapshot);assert.equal(x.rooms.state.rooms[code].quoteRates[0],575000);
});
test('QQ and website use one wallet, duplicate group commands across room changes cannot wager again; expiry does not cancel a locked payout',()=>{
 const x=fixture(),r=x.open();x.web.openAccount(user);const accounts=Object.keys(x.rooms.state.accounts).length;
 const command={text:'/下注 1 100',messageId:'one-message',messageAt:x.now()};
 const first=qqCommand(x.web,x.hourly,user,command);assert.equal(x.web.me(user).playBalance,900);assert.equal(Object.keys(x.rooms.state.accounts).length,accounts);
 const key=x.web.playKey(user);assert.equal(x.rooms.state.rooms[r.code].positions[key][0].claim,460);
 assert.deepEqual(qqCommand(x.web,x.hourly,user,command),first);assert.equal(x.web.me(user).playBalance,900);
 assert.throws(()=>qqCommand(x.web,x.hourly,user,{...command,text:'/下注 2 100'}),/内容不一致/);
  const pending=qqCommand(x.web,x.hourly,user,{...command,messageId:'new-message'});assert(pending.lines.some(s=>s.includes('尚未执行')));assert.equal(x.web.me(user).playBalance,900);
  qqCommand(x.web,x.hourly,user,{action:'ack',receiptId:first.receiptId});
 x.snapshot.round=10;x.hourly.sync(r.code,x.snapshot);assert.throws(()=>qqCommand(x.web,x.hourly,user,{...command,messageId:'late-message'}),/封盘/);
 x.snapshot.round=18;x.snapshot.ended=true;x.snapshot.ranks=[1,2,3,4,5,6,7,8];x.hourly.sync(r.code,x.snapshot);
 assert.equal(x.web.me(user).playBalance,1360);x.hourly.sync(r.code,x.snapshot);assert.equal(x.web.me(user).playBalance,1360);
 x.time(x.now()+3600000);x.snapshot.id='next-game';x.snapshot.gameRoomId='next-native';x.info.roomId='next-native';x.snapshot.ended=false;x.snapshot.round=1;x.snapshot.ranks.fill(0);x.open();
 assert.deepEqual(qqCommand(x.web,x.hourly,user,command),first);assert.equal(x.web.me(user).playBalance,1360);
 assert.throws(()=>qqCommand(x.web,x.hourly,user,{...command,messageId:'stale-message'}),/消息已过期/);
});
test('notifications are durable, leased, acknowledgement-gated and contain only public information',()=>{
 const x=fixture(),r=x.open();assert.equal(x.hourly.data().outbox.length,1);
 let item=x.hourly.claim();assert(item);assert.equal(x.hourly.claim(),null);assert.throws(()=>x.hourly.ack(item.id,'wrong'));
 x.hourly.ack(item.id,item.lease);assert.equal(x.hourly.claim(),null);
 x.snapshot.closedThrough=1;x.hourly.sync(r.code,x.snapshot);assert.equal(x.hourly.data().outbox.filter(e=>e.kind==='round').length,0);
 x.time(x.now()+45000);x.hourly.sync(r.code,x.snapshot);x.hourly.sync(r.code,x.snapshot);assert.equal(x.hourly.data().outbox.filter(e=>e.kind==='round').length,1);
 const view=x.web.watch(x.rooms.state.rooms[r.code]);Object.assign(view.players[0],{elo:9999,uid:'private-user',hand:['private-hand']});
 const summary=publicSummary(view).join('\n');assert(!/private-user|private-hand|9999|elo/i.test(summary));assert(summary.includes('上等马'));
 item=x.hourly.claim();x.time(x.now()+60001);assert.equal(x.hourly.claim().id,item.id);
});
test('actual native round ten permanently closes earlier delayed public rounds, and frozen models survive restart',()=>{
 const x=fixture(),model=x.snapshot.players.map(p=>({uid:p.uid,score:3400,found:true}));
 const view=x.hourly.open(x.info,x.snapshot,model);assert.deepEqual(x.hourly.status().active.model,model);
 x.snapshot.liveRound=10;x.hourly.sync(view.code,x.snapshot);assert.equal(x.web.watch(x.rooms.state.rooms[view.code]).open,false);
 x.snapshot.liveRound=2;x.hourly.sync(view.code,x.snapshot);assert.equal(x.rooms.state.rooms[view.code].tradingClosed,true);
 x.snapshot.closedThrough=1;x.snapshot.roundComplete=false;x.hourly.sync(view.code,x.snapshot);
 assert.equal(x.hourly.data().outbox.filter(e=>e.kind==='round').length,0);
 x.snapshot.roundComplete=true;x.hourly.sync(view.code,x.snapshot);x.time(x.now()+45000);x.hourly.sync(view.code,x.snapshot);assert.equal(x.hourly.data().outbox.filter(e=>e.kind==='round').length,1);
});
test('round announcement waits for public battle coverage and reports the next preparation state as that round result',()=>{
 const x=fixture(),r=x.open();x.snapshot.round=2;x.snapshot.closedThrough=1;x.snapshot.states=x.snapshot.states.map(()=>({life:80,wins:1,losses:0}));
 x.hourly.sync(r.code,x.snapshot);assert.equal(x.hourly.data().outbox.filter(e=>e.kind==='round').length,0);
 x.snapshot.battles=Array.from({length:4},(_,i)=>({round:1,first:x.snapshot.players[i*2].uid,second:x.snapshot.players[i*2+1].uid,winner:x.snapshot.players[i*2].uid,
  firstMirror:false,secondMirror:false,firstCards:[1000010],secondCards:[1000012],firstNames:['云剑'],secondNames:['防御'],firstLife:80,secondLife:80}));
 x.hourly.sync(r.code,x.snapshot);const event=x.hourly.data().outbox.find(e=>e.kind==='round');assert(event);assert.equal(event.round,1);
 assert(event.lines.some(line=>line.includes('命元80')));assert(!event.lines.some(line=>line.includes('命元100')));assert.equal(event.lines.filter(line=>line.includes(' 对 ')).length,4);
 x.hourly.sync(r.code,x.snapshot);assert.equal(x.hourly.data().outbox.filter(e=>e.kind==='round').length,1);
});
test('failed saving rolls back wallet, global QQ receipt, joining, account creation and event outbox together',()=>{
 let fail=false;const x=fixture(()=>{if(fail)throw new Error('disk unavailable')});const r=x.open(),before=structuredClone(x.rooms.state);fail=true;
 assert.throws(()=>qqCommand(x.web,x.hourly,user,{text:'/下注 1 100',messageId:'first-command',messageAt:x.now()}));assert.deepEqual(x.rooms.state,before);
 x.snapshot.closedThrough=1;assert.throws(()=>x.hourly.sync(r.code,x.snapshot));assert.deepEqual(x.rooms.state,before);
});
test('bot scope, dedicated service keys, exited players and invalid amounts fail closed',()=>{
 const x=fixture();x.open();assert.throws(()=>serviceAccess(undefined,''));assert.throws(()=>serviceAccess('Bearer '+'x'.repeat(32),'y'.repeat(32)));
 serviceAccess('Bearer '+'x'.repeat(32),'x'.repeat(32));
 const body={text:'/下注 1 100',messageId:'invalid-test',messageAt:x.now()};
 assert.throws(()=>qqCommand(x.web,x.hourly,{...user,scope:undefined},body));assert.throws(()=>qqCommand(x.web,x.hourly,{...user,groupId:'other'},body));
 for(const amount of ['-1','0','1.2','NaN','10001','1e3'])assert.throws(()=>qqCommand(x.web,x.hourly,user,{...body,text:'/下注 1 '+amount}));
 const r=x.rooms.websiteRoom();x.snapshot.active[0]=false;x.hourly.sync(r.code,x.snapshot);assert.throws(()=>qqCommand(x.web,x.hourly,user,body),/退出/);
});
test('real HTTP separates observer, bot and normal clients, and rejects expired or ordinary web tickets',async()=>{
 const x=fixture(),observerKey='o'.repeat(32),botKey='b'.repeat(32),bridgeKey='a'.repeat(64);
 const server=http.createServer(createHandler(x.rooms,{observerKey,botKey,bridgeKey,now:x.now}));
 await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 const url='http://127.0.0.1:'+server.address().port;
 const post=async(path,body,headers={})=>{const response=await fetch(url+path,{method:'POST',headers:{'content-type':'application/json',...headers},body:JSON.stringify(body)});return {status:response.status,...await response.json()};};
 const proof=p=>{const body=Buffer.from(JSON.stringify(p)).toString('base64url');return body+'.'+createHmac('sha256',Buffer.from(bridgeKey,'hex')).update(body).digest('base64url');};
 try{
  assert.equal((await post('/ops/hourly',{action:'status'})).status,401);
  assert.equal((await post('/ops/hourly',{action:'status'},{authorization:'Bearer '+botKey})).status,401);
  assert.equal((await post('/ops/hourly',{action:'open',info:x.info,snapshot:x.snapshot},{authorization:'Bearer '+observerKey})).status,200);
  const body={text:'/下注 1 100',messageId:'http-qq-message',messageAt:x.now()};
  for(const p of [{...user,expiresAt:x.now()-1},{...user,scope:undefined,expiresAt:x.now()+10000}])assert([401,403].includes((await post('/bot/command',body,{authorization:'Bearer '+botKey,'x-prediction-identity':proof(p)})).status));
  const ticket=proof({...user,expiresAt:x.now()+10000});assert.equal((await post('/bot/command',body,{authorization:'Bearer '+botKey,'x-prediction-identity':ticket})).status,200);
  assert.equal(x.web.me(user).playBalance,900);
  assert.equal((await post('/bot/events',{action:'claim'},{authorization:'Bearer '+observerKey})).status,401);
  assert((await post('/bot/events',{action:'claim'},{authorization:'Bearer '+botKey})).data);
 }finally{await new Promise(resolve=>server.close(resolve));}
});
