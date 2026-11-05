import fs from 'node:fs';
import {DatabaseSync} from 'node:sqlite';
import {decodeState} from './state-store.mjs';

const object=value=>value!==null&&typeof value==='object'&&!Array.isArray(value);
const put=(target,key,value)=>Object.defineProperty(target,key,{value,writable:true,enumerable:true,configurable:true});

// Records are accounts, rooms, receipt entries and individual outbox notices.
// The manifest preserves empty containers and the exact outbox ordering.
function records(state){
  const result=new Map(),manifest={};
  const save=(path,value)=>result.set(JSON.stringify(path),JSON.stringify(value));
  for(const [key,value]of Object.entries(state)){
    if(key==='hourly'&&object(value)){
      manifest[key]={kind:'hourly',fields:{}};
      for(const [field,entry]of Object.entries(value)){
        if(field==='outbox'&&Array.isArray(entry)){
          const ids=entry.map(row=>row.id);
          if(ids.some(id=>typeof id!=='string')||new Set(ids).size!==ids.length)throw new Error('Invalid outbox identities');
          manifest[key].fields[field]={kind:'outbox',ids};
          for(const row of entry)save([key,field,row.id],row);
        }else if(field==='slots'&&object(entry)){
          manifest[key].fields[field]={kind:'map',keys:Object.keys(entry)};
          for(const [id,row]of Object.entries(entry))save([key,field,id],row);
        }else{manifest[key].fields[field]={kind:'value'};save([key,field],entry);}
      }
    }else if(object(value)){
      manifest[key]={kind:'map',keys:Object.keys(value)};
      for(const [id,row]of Object.entries(value))if(row!==undefined)save([key,id],row);
      manifest[key].keys=manifest[key].keys.filter(id=>value[id]!==undefined);
    }else if(value!==undefined){manifest[key]={kind:'value'};save([key],value);}
  }
  result.set('manifest',JSON.stringify(manifest));return result;
}
function restore(rows){
  const map=new Map(rows.map(r=>[r.key,r.value]));
  if(!map.has('manifest'))throw new Error('Missing ledger manifest');
  const load=path=>{const key=JSON.stringify(path);if(!map.has(key))throw new Error('Missing ledger record');return JSON.parse(map.get(key));};
  const state={},manifest=JSON.parse(map.get('manifest'));
  for(const [key,spec]of Object.entries(manifest)){
    if(spec.kind==='value')put(state,key,load([key]));
    else if(spec.kind==='map'){
      const value={};for(const id of spec.keys)put(value,id,load([key,id]));put(state,key,value);
    }else if(spec.kind==='hourly'){
      const value={};for(const [field,entry]of Object.entries(spec.fields)){
        if(entry.kind==='outbox')put(value,field,entry.ids.map(id=>load([key,field,id])));
        else if(entry.kind==='map'){const slots={};for(const id of entry.keys)put(slots,id,load([key,field,id]));put(value,field,slots);}
        else if(entry.kind==='value')put(value,field,load([key,field]));
        else throw new Error('Invalid ledger manifest');
      }put(state,key,value);
    }else throw new Error('Invalid ledger manifest');
  }
  if(!object(state.accounts)||!object(state.rooms))throw new Error('Invalid ledger state');
  return state;
}
export function readIncrementalState(file){
  const db=new DatabaseSync(file,{readOnly:true});
  try{return restore(db.prepare('SELECT key,value FROM records').all());}finally{db.close();}
}
export class IncrementalStore{
  constructor(file){
    this.file=file+'.sqlite';this.db=new DatabaseSync(this.file);
    try{
      fs.chmodSync(this.file,0o600);
      this.db.exec('PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA busy_timeout=5000; CREATE TABLE IF NOT EXISTS records(key TEXT PRIMARY KEY,value TEXT NOT NULL);');
      this.cache=new Map(this.db.prepare('SELECT key,value FROM records').all().map(r=>[r.key,r.value]));
      this.upsert=this.db.prepare('INSERT INTO records(key,value) VALUES(?,?) ON CONFLICT(key) DO UPDATE SET value=excluded.value');
      this.remove=this.db.prepare('DELETE FROM records WHERE key=?');
      if(!this.cache.size)this.save(fs.existsSync(file)?decodeState(fs.readFileSync(file)):{accounts:{},rooms:{}});
      this.state=restore([...this.cache].map(([key,value])=>({key,value})));
    }catch(error){this.db.close();throw error;}
  }
  save(state){
    const next=records(state),changed=[...next].filter(([key,value])=>this.cache.get(key)!==value),removed=[...this.cache.keys()].filter(key=>!next.has(key));
    if(!changed.length&&!removed.length){this.lastWrite={records:0,bytes:0};return false;}
    this.db.exec('BEGIN IMMEDIATE');
    try{
      for(const [key,value]of changed)this.upsert.run(key,value);
      for(const key of removed)this.remove.run(key);
      this.db.exec('COMMIT');
    }catch(error){this.db.exec('ROLLBACK');throw error;}
    this.cache=next;this.lastWrite={records:changed.length+removed.length,bytes:changed.reduce((n,[,v])=>n+Buffer.byteLength(v),0)};return true;
  }
  close(){if(!this.closed){this.db.close();this.closed=true;}}
}
