import fs from 'node:fs';
import {createHash} from 'node:crypto';
import {brotliCompressSync,brotliDecompressSync,constants} from 'node:zlib';

const FORMAT='yx-prediction-state-br-v1',MAX_BYTES=256*1024*1024;
const hash=bytes=>createHash('sha256').update(bytes).digest('hex');

export function encodeState(state){
  const raw=Buffer.from(JSON.stringify(state));
  if(raw.length>MAX_BYTES)throw new Error('Room state exceeds storage limit');
  const compressed=brotliCompressSync(raw,{params:{[constants.BROTLI_PARAM_QUALITY]:1}});
  return JSON.stringify({format:FORMAT,bytes:raw.length,sha256:hash(raw),payload:compressed.toString('base64')});
}

export function decodeState(serialized){
  const document=JSON.parse(serialized.toString());
  // Existing uncompressed ledgers migrate only on their next successful save.
  if(document?.format!==FORMAT)return document;
  const {bytes,sha256,payload}=document;
  if(!Number.isSafeInteger(bytes)||bytes<1||bytes>MAX_BYTES||typeof sha256!=='string'||!/^[a-f0-9]{64}$/.test(sha256)
    ||typeof payload!=='string'||payload.length>Math.ceil(MAX_BYTES/3)*4)throw new Error('Invalid compressed room state');
  const compressed=Buffer.from(payload,'base64');
  if(compressed.toString('base64')!==payload)throw new Error('Invalid compressed room state');
  const raw=brotliDecompressSync(compressed,{maxOutputLength:bytes});
  if(raw.length!==bytes||hash(raw)!==sha256)throw new Error('Room state checksum mismatch');
  return JSON.parse(raw.toString('utf8'));
}

export function readState(file){return decodeState(fs.readFileSync(file));}

export function writeState(file,state,io=fs){
  // Compression happens before opening the temporary file. Persistence still
  // confirms only after fsync + atomic rename; callers retain their rollback.
  const serialized=encodeState(state),temporary=file+'.tmp',fd=io.openSync(temporary,'w',0o600);
  try{io.writeFileSync(fd,serialized);io.fsyncSync(fd);}finally{io.closeSync(fd);}
  io.renameSync(temporary,file);
}
