import {createHash,timingSafeEqual} from 'node:crypto';
import {RoomError} from './rooms.mjs';
import {publicSummary} from './hourly.mjs';
import {syncQqWallet} from './qq-wallet.mjs';

export function serviceAccess(header,key){
  if(typeof key!=='string'||key.length<32)throw new RoomError('此服务入口尚未启用',503);
  if(typeof header!=='string'||!timingSafeEqual(createHash('sha256').update(header).digest(),createHash('sha256').update('Bearer '+key).digest()))throw new RoomError('服务身份无效',401);
}
const ensure=(ok,message,status=400)=>{if(!ok)throw new RoomError(message,status)};
const instructions='群内赌马：/赌马 查看本场 · /下注 1 100 · /选手 1（或昵称） · /马币 · /退半 1 · /清仓 1 · /赌马 查单。只猜冠军，第10回合封盘。';
export function isPredictionCommand(text){return typeof text==='string'&&/^\/(赌马|下注|选手|马币|退半|清仓)(\s|$)/.test(text.trim());}

export function qqCommand(web,hourly,user,body){
  ensure(user.groupId==='599092307'&&(user.scope==='qq-prediction'||user.scope==='qq-wallet'&&typeof body.text==='string'&&body.text.trim()==='/马币'),'未认证账号只能开通和查询马币；下注请先完成有效认证',403);
  if(body.action==='ack'){
    ensure(user.scope==='qq-prediction','未认证账号只能开通和查询马币',403);
    const receipt=web.rooms.state.qqReceipts?.[body.receiptId];ensure(receipt?.owner===user.subject,'操作回执不属于当前账号',403);
    return web.rooms.commit(()=>{receipt.acknowledged=true;return {acknowledged:true};});
  }
  ensure(isPredictionCommand(body.text),'无法识别赌马命令');
  const [command,...args]=body.text.trim().split(/\s+/);
  const unconfirmed=Object.entries(web.rooms.state.qqReceipts??{}).find(([,r])=>r.owner===user.subject&&!r.acknowledged);
  if(command==='/赌马'&&args[0]==='查单'){
    const own=Object.entries(web.rooms.state.qqReceipts??{}).filter(([,r])=>r.owner===user.subject).sort((a,b)=>b[1].at-a[1].at)[0];
    return own?{...own[1].result,receiptId:own[0]}:{lines:['暂无已确认的群内投注记录。'],forward:false};
  }
  let operation,requestDigest;
  if(['/下注','/退半','/清仓'].includes(command)){
    ensure(typeof body.messageId==='string'&&/^[a-zA-Z0-9-]{1,64}$/.test(body.messageId),'群消息标识无效');
    operation='qq-'+createHash('sha256').update(user.subject+':'+user.groupId+':'+body.messageId).digest('hex');
    requestDigest=createHash('sha256').update(body.text.trim()).digest('hex');
    const receipt=web.rooms.state.qqReceipts?.[operation];
    if(receipt){ensure(receipt.requestDigest===requestDigest,'重复消息的内容不一致',409);return receipt.result;}
    if(unconfirmed)return {...unconfirmed[1].result,receiptId:unconfirmed[0],lines:[...unconfirmed[1].result.lines,'这是上一笔的回执；本条新命令尚未执行。核对后可重新发送新命令。']};
    ensure(Number.isSafeInteger(body.messageAt)&&Math.abs(web.rooms.now()-body.messageAt)<=300000,'消息已过期，请重新发送投注命令',409);
  }
  if(command==='/赌马'&&args[0]==='帮助')return {lines:[instructions],forward:false};
  if(command==='/马币'){
    ensure(args.length===0,'用法：/马币');
    const result=syncQqWallet(web,user),me=result.me;
    return {lines:[`${result.created?'已自动开通马币账户。':result.linked||result.synced?'已自动同步游戏马币账户。':''}${me.name}：可用${me.playBalance}马币${me.accounts>1&&me.balance!==me.playBalance?`，名下合计${me.balance}马币`:''}，累计净收益${me.net}马币。${user.scope==='qq-wallet'?(user.boundAccount?'认证已到期，续期后可参与下注。':'尚未绑定游戏账号，暂不能下注；完成认证后再次发送 /马币 即可自动同步。'):''}`],forward:false};
  }
  const room=web.rooms.websiteRoom();ensure(room?.eventSlot,'目前没有整点赌马专场',404);
  const view=web.watch(room);
  if(command==='/赌马')return {lines:[instructions,...publicSummary(view)],forward:true};
  ensure(args.length>=1,'请提供选手编号1～8或完整昵称');
  let index;
  if(/^[1-8]$/.test(args[0]))index=Number(args[0])-1;
  else{const found=view.players.map((p,i)=>p.name===args[0]?i:-1).filter(i=>i>=0);ensure(found.length===1,found.length?'昵称重复，请使用选手编号':'没有找到这位选手，请使用编号1～8');index=found[0];}
  if(command==='/选手')return {lines:publicSummary(view,{player:index}),forward:true};
  const kind=command==='/下注'?'buy':command==='/退半'?'half':'sell';
  ensure(args.length===(kind==='buy'?2:1),kind==='buy'?'用法：/下注 选手编号 马币数':'用法：'+command+' 选手编号');
  const stake=kind==='buy'&&/^[1-9][0-9]{0,4}$/.test(args[1])?Number(args[1]):0;
  if(kind==='buy')ensure(stake>=1&&stake<=10000,'每次投入1～10000马币');
  // One QQ/guide subject has one shared wallet selection. All mutations use the
  // existing room transaction and receipt path, including retries and settlement.
  return web.rooms.commit(()=>{
    if(kind==='buy')syncQqWallet(web,user);
    else ensure(web.owned(user).length,'尚无马币账户或持仓');
    const key=web.playKey(user);
    if(!room.members.includes(key))web.play(user,{action:'join',code:room.code});
    const result=web.play(user,{action:'trade',code:room.code,kind,player:index,rank:1,stake,version:room.version,operation});
    const p=result.wallet.positions[index];
    const reply={lines:[`${view.players[index].name}：${kind==='buy'?'投入已确认':kind==='half'?'减半已确认':'清仓已确认'}${p?`，剩余冠军锁定返还${p.claim}马币`:''}。可用${result.wallet.balance}马币。`],forward:false,receiptId:operation};
    const receipts=web.rooms.state.qqReceipts??={};
    for(const [id,r]of Object.entries(receipts))if(r.at<web.rooms.now()-90*86400000)delete receipts[id];
    ensure(Object.keys(receipts).length<100000,'群交易记录已达容量，请联系维护者',503);
    receipts[operation]={code:room.code,at:web.rooms.now(),owner:user.subject,acknowledged:false,requestDigest,result:reply};
    return reply;
  });
}
