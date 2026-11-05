import {test} from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import {readFileSync} from 'node:fs';
import {randomUUID} from 'node:crypto';
import {Rooms} from './rooms.mjs';
import {PredictionWeb} from './web.mjs';

// Exercise the shipped client against the real in-memory engine. No browser,
// network, production account, third-party DOM dependency or persistent money.
class Element{
 constructor(tag='div'){this.tagName=tag;this.children=[];this.value='';this.textContent='';this.dataset={};this.hidden=false;this.disabled=false;this.className='';this.attrs={};this.classList={contains:n=>this.className.split(' ').includes(n),toggle:()=>{}};}
 append(...nodes){this.children.push(...nodes);}
 replaceChildren(...nodes){this.children=[...nodes];}
 setAttribute(k,v){this.attrs[k]=String(v);}
 scrollIntoView(){}
}
const flush=async()=>{for(let n=0;n<12;n++)await new Promise(r=>setImmediate(r));};
const clone=value=>JSON.parse(JSON.stringify(value));
const deferred=()=>{let resolve;const promise=new Promise(r=>resolve=r);return {promise,resolve};};
async function fixture(){
 const rooms=new Rooms(undefined,undefined,()=>100000),host=rooms.register('synthetic host').token;
 const snapshot={id:'website-order-test',round:2,closedThrough:1,open:true,ended:false,players:Array.from({length:8},(_,i)=>({uid:'private-test-'+i,name:'选手'+i})),ranks:Array(8).fill(0),baseRates:Array(64).fill(40000),states:Array.from({length:8},()=>({life:100,wins:1,losses:0}))};
 const code=rooms.create(host,snapshot).code,web=new PredictionWeb(rooms,{now:()=>100000}),user={subject:'a'.repeat(64),name:'synthetic viewer'};web.openAccount(user);
 const elements=new Map(),get=id=>{if(!elements.has(id))elements.set(id,new Element());return elements.get(id);};get('bet-amount').value='100';
 const storage=new Map(),listeners={},intervals=[],calls=[],notes=[],views=[],trades=[];
 const window={predictionMe:clone(web.me(user)),predictionReady:Promise.resolve(),addEventListener:(name,fn)=>{(listeners[name]??=[]).push(fn);}};
 const emit=(name,detail)=>{for(const fn of listeners[name]??[])fn({detail});};
 const response=body=>clone(web.play(user,body));
 const P=window.Prediction={tab:name=>emit('prediction-tab',name),renderMe:data=>{window.predictionMe=data;},note:message=>notes.push(message),openLogin:async()=>{},api:async(path,{body}={})=>{
  calls.push({path,body:clone(body??{})});
  if(path==='/web/rooms')return clone(web.listRooms());
  if(path.startsWith('/web/room?'))return clone(web.watch(web.publicRoom(code)));
  assert.equal(path,'/web/play');
  if(body.action==='view'&&views.length)return clone(await views.shift()(body));
  if(body.action==='trade'&&trades.length)return clone(await trades.shift()(body));
  return response(body);
 }};
 const document={hidden:false,activeElement:null,getElementById:get,createElement:tag=>new Element(tag),createElementNS:(_,tag)=>new Element(tag),querySelectorAll:()=>[],querySelector:()=>null};
 const sessionStorage={getItem:k=>storage.get(k)??null,setItem:(k,v)=>storage.set(k,v),removeItem:k=>storage.delete(k)};
 const context=vm.createContext({window,document,sessionStorage,crypto:{randomUUID},setInterval:fn=>{intervals.push(fn);return intervals.length;},console});
 new vm.Script(readFileSync(new URL('./web/spectate.js',import.meta.url),'utf8')).runInContext(context);
 await flush();assert.equal(get('bet-controls').hidden,false);
 return {rooms,web,user,host,snapshot,code,window,emit,get,storage,calls,notes,views,trades,response,
  click:async(id='bet-buy')=>{get(id).onclick();await flush();},poll:async()=>{intervals[0]();await flush();},
  orders:()=>calls.filter(c=>c.body.action==='trade'),balance:()=>web.me(user).playBalance,
  sync:patch=>{Object.assign(snapshot,patch);rooms.sync(code,host,snapshot);}};
}

test('website refreshes an unrelated room revision without rejecting an unchanged reviewed quote',async()=>{
 const x=await fixture(),other=x.rooms.register('synthetic second viewer').token;
 x.rooms.join(x.code,other,x.snapshot.id);
 await x.click();assert.equal(x.orders().length,1);assert.equal(x.balance(),900);
 assert.equal(x.get('play-balance').textContent,'900');assert.match(x.get('bet-reason').textContent,/操作已确认/);
});

test('website requires another click for a changed quote and never silently executes at the new rate',async()=>{
 const x=await fixture();x.sync({baseRates:Array(64).fill(70000)});
 await x.click();assert.equal(x.orders().length,0);assert.equal(x.balance(),1000);
 assert.match(x.get('bet-reason').textContent,/核对后再次确认/);assert.equal(x.storage.size,0);
 await x.click();assert.equal(x.orders().length,1);assert.equal(x.balance(),900);
 const key=x.web.playKey(x.user);assert.equal(x.rooms.state.rooms[x.code].positions[key][0].claim,700);
});

test('website detects an independently changed position before allowing another purchase',async()=>{
 const x=await fixture(),key=x.web.playKey(x.user);
 x.rooms.trade(x.code,key,{kind:'buy',rank:1,player:0,stake:100,operation:'other-channel-test',version:x.rooms.state.rooms[x.code].version});
 await x.click();assert.equal(x.orders().length,0);assert.equal(x.balance(),900);assert.match(x.get('bet-reason').textContent,/持仓已更新/);
 await x.click();assert.equal(x.orders().length,1);assert.equal(x.balance(),800);
});

test('website cutoff, insufficient funds and invalid amount never submit a financial request',async()=>{
 const closed=await fixture();closed.sync({round:10});
 await closed.click();assert.equal(closed.orders().length,0);assert.equal(closed.balance(),1000);assert.equal(closed.get('bet-buy').disabled,true);assert.match(closed.get('bet-reason').textContent,/截止/);
 for(const value of ['1001','10001','0','1.5']){
  const x=await fixture();x.get('bet-amount').value=value;await x.click();assert.equal(x.orders().length,0);assert.equal(x.balance(),1000);assert.equal(x.storage.size,0);assert.match(x.get('bet-reason').textContent,/不能超过|整数/);
 }
});

test('preflight read failures stay visible and do not invent a pending debit',async()=>{
 const x=await fixture();x.views.push(async()=>{throw Error('network unavailable');});
 await x.click();assert.equal(x.orders().length,0);assert.equal(x.storage.size,0);
 assert.match(x.get('bet-reason').textContent,/network unavailable.*尚未提交/);
 await x.poll();assert.match(x.get('bet-reason').textContent,/network unavailable/);
});

test('unconfirmed execution retries the identical stored order after cutoff and never debits twice',async()=>{
 const x=await fixture();x.trades.push(async body=>{x.response(body);throw Error('response lost');});
 await x.click();assert.equal(x.balance(),900);const pending=JSON.parse(x.storage.get('prediction-pending-order'));
 assert(pending);assert.match(x.get('bet-reason').textContent,/确认上一笔/);
 x.sync({round:10});await x.click('bet-retry');
 assert.equal(x.orders().length,2);assert.deepEqual(x.orders()[0].body,x.orders()[1].body);assert.equal(x.balance(),900);assert.equal(x.storage.size,0);
});

test('a late background view cannot overwrite the balance of a newly confirmed purchase',async()=>{
 const x=await fixture(),d=deferred(),old=x.response({action:'view',code:x.code});
 x.views.push(async()=>{await d.promise;return old;});await x.poll();
 await x.click();assert.equal(x.balance(),900);assert.equal(x.get('play-balance').textContent,'900');
 d.resolve();await flush();assert.equal(x.get('play-balance').textContent,'900');assert.equal(x.orders().length,1);
});

test('changing amount or logging out while a fresh quote is loading prevents submission',async()=>{
 for(const logout of [false,true]){
  const x=await fixture(),d=deferred(),fresh=x.response({action:'view',code:x.code});
  x.views.push(async()=>{await d.promise;return fresh;});await x.click();
  if(logout){x.window.predictionMe=null;x.emit('prediction-logout');}else x.get('bet-amount').value='200';
  d.resolve();await flush();assert.equal(x.orders().length,0);assert.equal(x.balance(),1000);assert.equal(x.storage.size,0);
 }
});
