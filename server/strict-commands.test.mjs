import {test} from 'node:test';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {Rooms} from './rooms.mjs';
import {PredictionWeb} from './web.mjs';
import {HourlyEvents} from './hourly.mjs';
import {qqCommand,isPredictionCommand} from './qq.mjs';
import {normalizeRaceCommand} from './race-policy.mjs';

const retired=['转账','转账 200','下注 1 100','清仓 1','退半 1','盘口','赔率','押注 1 100','赌马','赌马注册 小白','赛马','赛马注册 小白','观战注册 小白','赛马绑定 SM-test','观战绑定 SM-test','赛马改名 小白','观战改名 小白','马币帮助','马币签到','马币转账 200'];
function fixture(){
 const now=()=>Date.UTC(2026,10,5,12),rooms=new Rooms(undefined,undefined,now),web=new PredictionWeb(rooms,{now}),hourly=new HourlyEvents(rooms,web);
 const snapshot={id:'strict-quiz-game',gameRoomId:'strict-native',round:1,closedThrough:0,open:true,ended:false,players:Array.from({length:8},(_,i)=>({uid:'secret-'+i,name:'选手'+i})),ranks:Array(8).fill(0),baseRates:Array(64).fill(40000)};
 const view=hourly.open({roomId:snapshot.gameRoomId,round:1,isPrivate:false,playerCount:8,avgRankScore:6500},snapshot);
 const user={subject:'b'.repeat(64),qqSubject:'c'.repeat(64),bindingDigest:createHash('sha256').update('[]').digest('hex'),gameUidRef:null,scope:'qq-prediction',groupId:'613486881',name:'观众'};
 const call=text=>qqCommand(web,hourly,user,{text,messageId:'unchanged-message-id',messageAt:now()});
 return {rooms,web,hourly,view,user,call};
}
test('only the current quiz allowlist is public; retired commands cannot match bare or slash forms',()=>{
 for(const command of retired)for(const prefix of ['','/']){
  assert.equal(normalizeRaceCommand(prefix+command),null,command);assert.equal(isPredictionCommand(prefix+command),false,command);
 }
 for(const name of ['注册','绑定','改名','帮助','签到','赠送','余额','查单','马币排行','全猜','竞猜','观战','走势','减半','撤回','马币','选手']){
  assert.ok(normalizeRaceCommand(name));assert.ok(normalizeRaceCommand('/'+name));
 }
 for(const text of ['下注1 100','竞猜错误','全猜猫','清仓选手'])assert.equal(normalizeRaceCommand(text),null);
});
test('direct backend requests with retired commands cannot create wallets, trade, sell or alter a pending receipt',()=>{
 const x=fixture();x.call('竞猜 1 100');
 const before=structuredClone(x.rooms.state);
 for(const command of retired)for(const prefix of ['','/']){
  assert.throws(()=>x.call(prefix+command),/无法识别/);assert.deepEqual(x.rooms.state,before);
 }
 assert.equal(x.web.me(x.user).playBalance,900);
});
test('new public command retries preserve historical receipt digests and never re-debit; old words remain rejected',()=>{
 const x=fixture(),reply=x.call('竞猜 1 100'),receipt=x.rooms.state.qqReceipts[reply.receiptId];
 // This digest is the persisted pre-rename contract, not a second public alias.
 assert.equal(receipt.requestDigest,createHash('sha256').update('/下注 1 100').digest('hex'));
 const before=structuredClone(x.rooms.state);
 assert.deepEqual(x.call('/竞猜 1 100'),reply);assert.deepEqual(x.rooms.state,before);
 assert.throws(()=>x.call('/下注 1 100'),/无法识别/);assert.deepEqual(x.rooms.state,before);
 assert.equal(x.web.me(x.user).playBalance,900);
});
