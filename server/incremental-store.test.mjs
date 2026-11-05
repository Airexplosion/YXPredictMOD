import{test}from'node:test';
import assert from'node:assert/strict';
import fs from'node:fs';import os from'node:os';import path from'node:path';
import{spawnSync}from'node:child_process';
import{IncrementalStore}from'./incremental-store.mjs';
import{readState,encodeState,writeState}from'./state-store.mjs';
import{Rooms}from'./rooms.mjs';

const fixture=()=>({accounts:{a:{balance:1000},b:{balance:200}},rooms:{x:{positions:{a:[{stake:100,claim:300,locked:true}]},history:['公开牌桌'.repeat(5000)]}},hourly:{slots:{hour:{code:'x'}},hostToken:'host',outbox:[{id:'start',lease:'lease',deliveredGroups:{g:123},lines:['开场']},{id:'round',deliveredAt:null,lines:['战况']}]},qqReceipts:{old:{acknowledged:false}},compensations:{once:{amount:100}},empty:{},optional:null});
function setup(t,state=fixture(),compressed=false){
 const directory=fs.mkdtempSync(path.join(os.tmpdir(),'yxp-incremental-')),file=path.join(directory,'rooms.json');
 fs.writeFileSync(file,compressed?encodeState(state):JSON.stringify(state));let store=new IncrementalStore(file);
 t.after(()=>{store.close();fs.rmSync(directory,{recursive:true,force:true});});
 return {file,get store(){return store;},restart(){store.close();store=new IncrementalStore(file);return store;}};
}
test('legacy and compressed ledgers migrate exactly, retaining all financial and notice records',t=>{
 for(const compressed of[false,true]){const x=setup(t,fixture(),compressed);assert.deepEqual(x.store.state,fixture());assert.deepEqual(readState(x.file),fixture());assert.deepEqual(x.restart().state,fixture());}
});
test('one account update writes one small record, unchanged queries perform no SQLite commit',t=>{
 const x=setup(t),state=x.store.state;state.accounts.a.balance-=50;assert.equal(x.store.save(state),true);
 assert.equal(x.store.lastWrite.records,1);assert.ok(x.store.lastWrite.bytes<100);
 const before=fs.statSync(x.file+'.sqlite-wal').size;
 assert.equal(x.store.save(state),false);assert.equal(fs.statSync(x.file+'.sqlite-wal').size,before);
 assert.equal(x.restart().state.accounts.a.balance,950);
});
test('outbox order, deletion, append, empty containers and account removal survive restart',t=>{
 const x=setup(t),state=x.store.state;
 state.hourly.outbox.reverse();state.hourly.outbox[0].deliveredAt=456;state.hourly.outbox.push({id:'new',lines:['结束']});
 delete state.accounts.b;delete state.optional;state.hourly.slots={};state.newEmpty={};
 x.store.save(state);assert.deepEqual(x.restart().state,state);
});
test('one failed SQL write rolls back every changed account and receipt, leaving the cache retryable',t=>{
 const x=setup(t),rooms=new Rooms(x.store.state,s=>x.store.save(s));
 x.store.db.exec(`CREATE TRIGGER reject_write BEFORE UPDATE ON records WHEN NEW.key='["accounts","b"]' BEGIN SELECT RAISE(ABORT,'injected failure'); END;`);
 const change=()=>{rooms.state.accounts.a.balance-=100;rooms.state.accounts.b.balance+=100;rooms.state.qqReceipts.new={amount:100};};
 assert.throws(()=>rooms.commit(change),/injected failure/);assert.deepEqual(readState(x.file),fixture());assert.deepEqual(rooms.state,fixture());
 x.store.db.exec('DROP TRIGGER reject_write');rooms.commit(change);assert.equal(readState(x.file).accounts.a.balance,900);assert.equal(readState(x.file).accounts.b.balance,300);
});
test('scoped wallet rollback restores new and existing wallet containers without cloning room history',()=>{
 const state=fixture(),room=state.rooms.x;let saves=0;const r=new Rooms(state,()=>{saves++;throw Error('save failed');});
 assert.throws(()=>r.commit(()=>{r.state.accounts.a.balance=1;r.state.profiles={a:{name:'new'}};},['accounts','profiles']),/save failed/);
 assert.equal(saves,1);assert.equal(r.state.accounts.a.balance,1000);assert.equal(r.state.profiles,undefined);assert.equal(r.state.rooms.x,room);
});
test('unchanged wallet sync skips serialization and saving while real wallet changes still commit',()=>{
 const r=new Rooms(fixture(),()=>{saves++;});let saves=0;
 const read=()=>r.state.accounts.a.balance;
 assert.equal(r.commit(read,['accounts','profiles']),1000);assert.equal(saves,0);assert.equal(r.revision,0);
 r.commit(()=>{r.state.accounts.a.balance=900;},['accounts','profiles']);assert.equal(saves,1);assert.equal(r.revision,1);
});
test('committed WAL survives abrupt process exit; SQLite remains authoritative over the old snapshot',t=>{
 const x=setup(t);x.store.close();
 const script=`import{IncrementalStore}from${JSON.stringify(new URL('./incremental-store.mjs',import.meta.url).href)};const s=new IncrementalStore(process.argv[1]);s.state.accounts.a.balance=777;s.save(s.state);process.exit(0);`;
 const child=spawnSync(process.execPath,['--input-type=module','-e',script,x.file],{encoding:'utf8'});assert.equal(child.status,0,child.stderr);
 assert.equal(readState(x.file).accounts.a.balance,777);assert.throws(()=>writeState(x.file,fixture()),/Incremental ledger exists/);
 x.restart();
});
