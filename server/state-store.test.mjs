import {test} from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {encodeState,decodeState,readState,writeState} from './state-store.mjs';
import {Rooms} from './rooms.mjs';

const fixture=()=>({accounts:{token:{balance:12345,name:'测试道友',transferIn:20}},rooms:{game:{positions:{token:[{stake:200,claim:123456,locked:true}]},receipts:{'op-one':true},settled:false}},hourly:{outbox:[{id:'opening',lease:'old-lease',deliveredGroups:{group:123},presentation:{players:[{name:'示例',cards:[1,2]}]}}]},compensations:{once:{amount:200}}});

test('compressed state preserves the entire ledger and reads legacy plain JSON',()=>{
  const state=fixture();assert.deepEqual(decodeState(encodeState(state)),state);
  assert.deepEqual(decodeState(Buffer.from(JSON.stringify(state))),state);
});
test('repeated public histories use much less disk without discarding any history',()=>{
  const state=fixture();state.hourly.outbox=Array.from({length:150},(_,i)=>({...state.hourly.outbox[0],id:'event-'+i,lines:['公开角色卡组与仙命'.repeat(100)]}));
  const raw=JSON.stringify(state),encoded=encodeState(state);
  assert.ok(Buffer.byteLength(encoded)<Buffer.byteLength(raw)/3);assert.deepEqual(decodeState(encoded),state);
});
test('corrupted content, wrong sizes and invalid checksums fail closed',()=>{
  const saved=JSON.parse(encodeState(fixture()));
  for(const change of [{sha256:'0'.repeat(64)},{bytes:saved.bytes+1},{bytes:0},{bytes:257*1024*1024},{payload:'not base64!'},
    {payload:Buffer.from('broken compressed bytes').toString('base64')}])assert.throws(()=>decodeState(JSON.stringify({...saved,...change})));
});
test('atomic saves migrate legacy state and a failed rename preserves old disk and wallet state',()=>{
  const directory=fs.mkdtempSync(path.join(os.tmpdir(),'yxp-storage-')),file=path.join(directory,'rooms.json');
  try{
    const state=fixture();fs.writeFileSync(file,JSON.stringify(state));assert.deepEqual(readState(file),state);
    writeState(file,state);const committed=fs.readFileSync(file);assert.deepEqual(readState(file),state);
    const failing={...fs,renameSync:()=>{throw new Error('disk rename failed');}};
    const rooms=new Rooms(state,next=>writeState(file,next,failing));
    assert.throws(()=>rooms.commit(()=>{rooms.state.accounts.token.balance-=100;}),/disk rename failed/);
    assert.equal(rooms.state.accounts.token.balance,12345);assert.deepEqual(fs.readFileSync(file),committed);assert.deepEqual(readState(file),fixture());
    writeState(file,rooms.state);assert.deepEqual(readState(file),fixture());
  }finally{fs.rmSync(directory,{recursive:true,force:true});}
});
test('fsync completes before rename and fsync failure never publishes an unconfirmed save',()=>{
  const events=[],io={openSync:()=>{events.push('open');return 1;},writeFileSync:()=>events.push('write'),fsyncSync:()=>events.push('fsync'),closeSync:()=>events.push('close'),renameSync:()=>events.push('rename')};
  writeState('unused',fixture(),io);assert.deepEqual(events,['open','write','fsync','close','rename']);
  events.length=0;io.fsyncSync=()=>{events.push('fsync');throw new Error('disk sync failed');};
  assert.throws(()=>writeState('unused',fixture(),io),/disk sync failed/);assert.deepEqual(events,['open','write','fsync','close']);
});
