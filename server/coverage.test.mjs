import {test} from 'node:test';
import assert from 'node:assert/strict';
import {coverageReturn, capCoverage} from './coverage.mjs';
import {Rooms} from './rooms.mjs';

function fixture(){
  const rooms=new Rooms(undefined,undefined,()=>100000),key=rooms.register('test').token;
  rooms.state.accounts[key].balance=100000;
  const snapshot={id:'coverage',round:1,closedThrough:0,open:true,ended:false,
    players:Array.from({length:8},(_,i)=>({uid:'p'+i,name:'player'+i})),ranks:Array(8).fill(0),baseRates:Array(64).fill(70000)};
  const code=rooms.create(key,snapshot).code;
  let sequence=0;
  const trade=(player,stake=100,who=key,allIn=false)=>rooms.trade(code,who,{kind:'buy',rank:1,player,stake,allIn,operation:'coverage-order-'+(++sequence),version:rooms.state.rooms[code].version});
  return {rooms,key,code,snapshot,trade,room:()=>rooms.state.rooms[code]};
}

test('equal-claim coverage includes second and third returns', () => {
  assert(Math.abs(coverageReturn(Array(8).fill(70000)) - 1.18125) < 1e-12);
  const rates = capCoverage(Array(8).fill(70000));
  assert(coverageReturn(rates) <= 0.98 + 1e-12);
  assert.deepEqual(capCoverage(Array(8).fill(40000)), Array(8).fill(40000));
});

test('covering only six long shots can exploit third place despite safe eight-way reciprocal sum', () => {
  const rates = [10100, 10100, ...Array(6).fill(1000000)];
  assert(1.35 / rates.reduce((sum, r) => sum + 10000 / r, 0) < 1);
  assert(coverageReturn(rates) > 1);
  assert(coverageReturn(capCoverage(rates)) <= 0.98 + 1e-12);
});

test('calibration covers two to eight survivors, optional triple, and integer allocations', () => {
  let seed = 7147;
  const next = () => (seed = (seed * 16807) % 2147483647);
  for (let n = 2; n <= 8; n++) for (const bonus of [1, 3]) for (let k = 0; k < 120; k++) {
    const rates = capCoverage(Array.from({length:n}, () => 10100 + next() % 564901), bonus);
    assert(coverageReturn(rates, bonus) <= 0.98 + 1e-12);
    for (let attempt = 0; attempt < 15; attempt++) {
      const stakes = rates.map(() => next() % 10001);
      const claims = rates.map((r, i) => Math.floor(stakes[i] * r * bonus / 10000)).sort((a,b) => a-b);
      const minimum = claims[0] + Math.floor(claims[1] / 4) + (n >= 3 ? Math.floor(claims[2] / 10) : 0);
      assert(minimum <= stakes.reduce((a,b) => a+b, 0));
    }
  }
});

test('ordinary coverage is capped while independent triple promotion survives updates and restart',()=>{
  const x=fixture(),keys=[];
  assert.equal(x.room().quoteRates[0],58074);
  assert(coverageReturn(x.room().quoteRates.filter((_,i)=>i%8===0))<1);
  for(let i=0;i<8;i++){
    const key=x.rooms.register('wallet'+i).token;keys.push(key);x.rooms.join(x.code,key,x.snapshot.id);
    x.trade(i,1000,key,true);
  }
  const claims=keys.map((k,i)=>x.room().positions[k][i].claim);
  assert(claims.every(c=>c===17422));
  for(let a=0;a<8;a++)for(let b=0;b<8;b++)for(let c=0;c<8;c++)if(a!==b&&b!==c&&a!==c)
    assert(claims[a]+Math.floor(claims[b]/4)+Math.floor(claims[c]/10)>8000);
  x.snapshot.round=2;x.snapshot.baseRates.fill(500000);x.rooms.sync(x.code,x.key,x.snapshot);
  assert(coverageReturn(x.room().quoteRates.filter((_,i)=>i%8===0))<=0.98);
  assert.deepEqual(keys.map((k,i)=>x.room().positions[k][i].claim),claims);
  const restored=new Rooms(structuredClone(x.rooms.state),undefined,()=>100000);
  const settled={...x.snapshot,round:18,ended:true,ranks:[1,2,3,4,5,6,7,8]};
  restored.sync(x.code,x.key,settled);
  assert.equal(keys.reduce((sum,k)=>sum+restored.state.accounts[k].balance,0),17422+4355+1742);
});

test('same-round base updates, repeated syncs, roster order and survivor changes retain capped quotes',()=>{
  const x=fixture();
  x.snapshot.baseRates=Array.from({length:64},(_,i)=>i%8===0?10100+Math.floor(i/8)*60000:40000);
  x.rooms.sync(x.code,x.key,x.snapshot);const rates=x.room().quoteRates.slice(),version=x.room().version;
  x.rooms.sync(x.code,x.key,x.snapshot);assert.deepEqual(x.room().quoteRates,rates);assert.equal(x.room().version,version);
  const reversed={...x.snapshot,players:x.snapshot.players.slice().reverse(),ranks:x.snapshot.ranks.slice().reverse(),baseRates:[]};
  for(let i=7;i>=0;i--)reversed.baseRates.push(...x.snapshot.baseRates.slice(i*8,i*8+8));
  x.rooms.sync(x.code,x.key,reversed);assert.deepEqual(x.room().quoteRates,rates);
  for(let n=7;n>=2;n--){
    x.snapshot.round=2;x.snapshot.ranks=Array.from({length:8},(_,i)=>i<n?0:i+1);
    x.rooms.sync(x.code,x.key,x.snapshot);
    assert(coverageReturn(x.room().quoteRates.filter((_,i)=>i%8===0).slice(0,n))<=0.98+1e-12);
  }
});

test('cross-round coverage and a failed final leg roll back balances, claims and receipts atomically',()=>{
  const x=fixture();x.snapshot.round=2;x.rooms.sync(x.code,x.key,x.snapshot);
  // Historical locked fills may be higher than today's prices. They stay intact.
  x.room().positions[x.key]=Array.from({length:8},(_,i)=>i<6?{rank:1,stake:100,claim:1000}:null);
  const before=structuredClone(x.rooms.state);
  assert.throws(()=>x.rooms.commit(()=>{x.trade(6);x.trade(7);}),/保底返还/);
  assert.deepEqual(x.rooms.state,before);
});

test('old saved rooms retain quotes and locked claims after sync',()=>{
  const x=fixture();delete x.room().coverageVersion;x.room().quoteRates=x.snapshot.baseRates.slice();
  x.trade(0);const before=structuredClone(x.room().positions);
  x.rooms.sync(x.code,x.key,x.snapshot);
  assert.equal(x.room().quoteRates[0],70000);assert.deepEqual(x.room().positions,before);
});

test('removing the triple reserve raises only future quotes, preserving existing low-price claims and locks',()=>{
  const x=fixture();x.room().quoteRates.fill(19358);
  x.trade(0,100000,x.key,true);
  const before=structuredClone(x.room().positions[x.key]);
  x.rooms.sync(x.code,x.key,x.snapshot);
  assert.equal(x.room().quoteRates[0],58074);
  assert.deepEqual(x.room().positions[x.key],before);
  assert.equal(x.room().positions[x.key][0].cashoutLocked,true);
  assert.equal(x.rooms.state.accounts[x.key].balance,0);
});
