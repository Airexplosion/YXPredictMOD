import { test } from 'node:test';
import assert from 'node:assert/strict';
import http from 'node:http';
import { Rooms } from './rooms.mjs';
import { createHandler } from './server.mjs';
function setup(save) {
  let time = 100000; const rooms = new Rooms(undefined, save, () => time);
  const a = rooms.register('甲').token, b = rooms.register('乙').token;
  const snapshot = {id:'game:1', round:1, closedThrough:0, open:true, ended:false,
    players:Array.from({length:8},(_,i)=>({uid:String(i),name:'玩家'+i})), ranks:Array(8).fill(0)};
  const code = rooms.create(a, snapshot).code; rooms.join(code,b,snapshot.id);
  const trade = (key, player, rank, stake, kind='buy', operation='operation-'+String(Math.random()).slice(2)) => rooms.trade(code,key,{kind,player,rank,stake,operation,version:rooms.view(code,key).version});
  return {rooms,a,b,snapshot,code,trade,advance: ms => time += ms};
}
test('shared pools fluctuate and final settlement conserves all points',()=>{
  const x=setup(); const {rooms,a,b,code,snapshot,trade}=x;
  trade(a,0,1,100); trade(b,0,2,300);
  assert.deepEqual(rooms.view(code,a).totals[0],[100,300,0,0,0,0,0,0]);
  snapshot.ended=true; snapshot.ranks=[1,2,3,4,5,6,7,8];rooms.sync(code,a,snapshot);
  assert.equal(rooms.view(code,a).balance,1300);assert.equal(rooms.view(code,b).balance,700);
  rooms.sync(code,a,snapshot);assert.equal(rooms.view(code,a).balance,1300);
});
test('round close, heartbeat timeout, restart and rewind cannot reopen trades',()=>{
  const x=setup();x.advance(15001);assert.throws(()=>x.trade(x.a,0,1,10));
  x.rooms.sync(x.code,x.a,x.snapshot);x.trade(x.a,0,1,10);
  x.snapshot.closedThrough=1;x.rooms.sync(x.code,x.a,x.snapshot);assert.throws(()=>x.trade(x.a,1,2,10));
  x.snapshot.closedThrough=0;x.rooms.sync(x.code,x.a,x.snapshot);assert.throws(()=>x.trade(x.a,1,2,10));
  x.snapshot.round=2;x.rooms.sync(x.code,x.a,x.snapshot);x.trade(x.a,1,2,10);
  const reloaded=new Rooms(structuredClone(x.rooms.state));assert.equal(reloaded.view(x.code,x.a).open,false);
  assert.throws(()=>x.rooms.sync(x.code,x.a,{...x.snapshot,round:1}));
});
test('host authorization, same game, unique ranks and member authorization',()=>{
  const x=setup();assert.throws(()=>x.rooms.sync(x.code,x.b,x.snapshot));
  const c=x.rooms.register('丙').token;assert.throws(()=>x.rooms.view(x.code,c));
  assert.throws(()=>x.rooms.join(x.code,c,'other'));
  assert.throws(()=>x.rooms.sync(x.code,x.a,{...x.snapshot,ranks:[1,1,0,0,0,0,0,0]}));
  assert.equal(x.rooms.view(x.code,x.a).ranks[0],0);
});
test('stale quotes reject; operation retries cannot charge twice',()=>{
  const x=setup();const version=x.rooms.view(x.code,x.a).version;
  const input={operation:'repeat-123',kind:'buy',player:0,rank:1,stake:10,version};
  x.rooms.trade(x.code,x.a,input);x.rooms.trade(x.code,x.a,input);
  assert.equal(x.rooms.view(x.code,x.a).balance,990);
  assert.throws(()=>x.rooms.trade(x.code,x.a,{...input,operation:'another-123'}));
});
test('switch rolls back refund and balances if replacement purchase fails',()=>{
  const x=setup();x.trade(x.a,0,1,100);assert.throws(()=>x.trade(x.a,0,2,10000,'switch'));
  assert.equal(x.rooms.view(x.code,x.a).positions[0].stake,100);assert.equal(x.rooms.view(x.code,x.a).balance,900);
  x.trade(x.a,0,2,100,'switch');assert.equal(x.rooms.view(x.code,x.a).balance,897);
  assert.equal(x.rooms.view(x.code,x.a).positions[0].rank,2);
});
test('half/full refunds do not create points; unknown winners refund pools',()=>{
  const x=setup();x.trade(x.a,0,2,101);x.trade(x.a,0,0,0,'half');x.trade(x.a,0,0,0,'sell');
  assert.equal(x.rooms.view(x.code,x.a).balance,996);
  x.trade(x.a,1,1,100);x.snapshot.ended=true;x.snapshot.ranks=[1,2,3,4,5,6,7,8];x.rooms.sync(x.code,x.a,x.snapshot);
  assert.equal(x.rooms.view(x.code,x.a).balance,996);
});
test('incomplete final state locks and waits, then settles once with reordered UID roster',()=>{
  const x=setup();x.trade(x.a,0,1,50);x.snapshot.ended=true;x.rooms.sync(x.code,x.a,x.snapshot);
  assert.equal(x.rooms.view(x.code,x.a).settled,false);assert.throws(()=>x.trade(x.a,0,1,50));
  x.snapshot.players.reverse();x.snapshot.ranks=[8,7,6,5,4,3,2,1];x.rooms.sync(x.code,x.a,x.snapshot);
  assert.equal(x.rooms.view(x.code,x.a).settled,true);assert.equal(x.rooms.view(x.code,x.a).balance,1000);
});
test('failed durable save rolls back entire mutation',()=>{
  let fail=false;const x=setup(()=>{if(fail)throw Error('disk full');});fail=true;
  assert.throws(()=>x.trade(x.a,0,1,100));assert.equal(x.rooms.view(x.code,x.a).balance,1000);
  assert.equal(x.rooms.view(x.code,x.a).positions[0],null);
});
test('HTTP two clients join and trade with bearer credentials',async()=>{
  const x=setup();const server=http.createServer(createHandler(x.rooms));
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  const url=`http://127.0.0.1:${server.address().port}/v1/room`;
  try {
    const call=async(token,body)=>{const r=await fetch(url,{method:'POST',headers:{authorization:'Bearer '+token,'content-type':'application/json'},body:JSON.stringify(body)});return {status:r.status,body:await r.json()};};
    const r=await call(x.a,{action:'trade',code:x.code,kind:'buy',player:0,rank:1,stake:10,operation:'http-test-123',version:x.rooms.view(x.code,x.a).version});assert.equal(r.body.ok,true);
    const v=await call(x.b,{action:'view',code:x.code});assert.equal(v.body.data.totals[0][0],10);
    assert.equal((await call('wrong',{action:'view',code:x.code})).status,401);
  } finally {await new Promise(resolve=>server.close(resolve));}
});

// Verify actual disk storage and process lock, not only the in-memory engine.
test('disk restart preserves account and room, resets trading heartbeat, rejects a second writer',async()=>{
  const fs=await import('node:fs'),os=await import('node:os'),path=await import('node:path');
  const {start}=await import('./server.mjs');
  const directory=fs.mkdtempSync(path.join(os.tmpdir(),'yxp-room-test-'));
  let server;
  try {
    server=start({directory,port:0});await new Promise(resolve=>server.once('listening',resolve));
    assert.throws(()=>start({directory,port:0}),/already in use/);
    let url=`http://127.0.0.1:${server.address().port}/v1/room`;
    const request=async(body,token)=>{const response=await fetch(url,{method:'POST',headers:{'content-type':'application/json',authorization:'Bearer '+(token??'')},body:JSON.stringify(body)});return (await response.json()).data;};
    const user=await request({action:'register',name:'restart'});
    const snapshot={id:'disk-game',round:1,closedThrough:0,open:true,ended:false,players:Array.from({length:8},(_,i)=>({uid:String(i),name:String(i)})),ranks:Array(8).fill(0)};
    const room=await request({action:'create',snapshot},user.token);
    await request({action:'trade',code:room.code,kind:'buy',player:0,rank:1,stake:100,version:room.version,operation:'disk-trade-001'},user.token);
    await new Promise(resolve=>server.close(resolve));server=null;
    server=start({directory,port:0});await new Promise(resolve=>server.once('listening',resolve));url=`http://127.0.0.1:${server.address().port}/v1/room`;
    const view=await request({action:'view',code:room.code},user.token);assert.equal(view.balance,900);assert.equal(view.positions[0].stake,100);assert.equal(view.open,false);
  }finally{
    if(server)await new Promise(resolve=>server.close(resolve));
    for(const name of ['rooms.json','rooms.json.tmp','server.lock']){const file=path.join(directory,name);if(fs.existsSync(file))fs.unlinkSync(file);}fs.rmdirSync(directory);
  }
});
test('retired rank stops new positions while remaining final ranks wait',()=>{
  const x=setup();x.snapshot.ranks[7]=8;x.rooms.sync(x.code,x.a,x.snapshot);
  assert.throws(()=>x.trade(x.a,7,8,10));assert.throws(()=>x.trade(x.a,0,8,10));
  x.trade(x.a,0,1,10);
});
test('unchanged host heartbeat does not invalidate member quotes',()=>{
  const x=setup();const before=x.rooms.view(x.code,x.b).version;
  x.rooms.sync(x.code,x.a,x.snapshot);assert.equal(x.rooms.view(x.code,x.b).version,before);
  x.rooms.trade(x.code,x.b,{kind:'buy',player:0,rank:1,stake:10,operation:'stable-heartbeat',version:before});
});
