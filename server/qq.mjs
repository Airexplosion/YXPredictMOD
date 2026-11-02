import {createHash,timingSafeEqual} from 'node:crypto';
import {RoomError} from './rooms.mjs';
import {publicSummary} from './hourly.mjs';
import {publicPresentation} from './public-build.mjs';
import {syncQqWallet,transferQqCoins} from './qq-wallet.mjs';

export function serviceAccess(header,key){
  if(typeof key!=='string'||key.length<32)throw new RoomError('此服务入口尚未启用',503);
  if(typeof header!=='string'||!timingSafeEqual(createHash('sha256').update(header).digest(),createHash('sha256').update('Bearer '+key).digest()))throw new RoomError('服务身份无效',401);
}
const ensure=(ok,message,status=400)=>{if(!ok)throw new RoomError(message,status)};
const help=[
  '/马币：查询余额，未开户自动开通。',
  '/马币帮助：查看本指令表。',
  '/马币签到：每日领取200马币，与网站共用签到记录，北京时间0点刷新。',
  '/马币转账 @某人 200：转账200马币；收款人未开户自动开通。转账不计输赢，转空余额不触发破产救济。',
  '/赌马：查看本场；/选手 1：查看指定选手，也支持完整昵称。',
  '/下注 1 200 2 300：一条命令多笔下注，可重复选手。',
  '/退半 1：减半；/清仓 1：清仓；/赌马 查单：核对最近扣款回执。',
  '签到、转账和下注需有效认证；只猜冠军，第10回合起封盘，第二返25%、第三返10%。'
];
const instructions='群内赌马：/马币帮助 查看全部指令 · /马币 · /马币签到 · /马币转账 @某人 200 · /赌马 · /下注 1 200 2 300 · /选手 1 · /退半 1 · /清仓 1 · /赌马 查单。';
export function isPredictionCommand(text){return typeof text==='string'&&/^\/(赌马|下注|选手|马币帮助|马币签到|马币转账|马币|退半|清仓)(\s|$)/.test(text.trim());}
function saveReceipt(web,user,operation,requestDigest,reply,code=null){
  const receipts=web.rooms.state.qqReceipts??={};
  for(const [id,r]of Object.entries(receipts))if(r.at<web.rooms.now()-90*86400000)delete receipts[id];
  ensure(Object.keys(receipts).length<100000,'群交易记录已达容量，请联系维护者',503);
  receipts[operation]={code,at:web.rooms.now(),owner:user.subject,acknowledged:false,requestDigest,result:reply};
}

export function qqCommand(web,hourly,user,body){
  ensure(user.groupId==='599092307'&&(user.scope==='qq-prediction'||user.scope==='qq-wallet'&&typeof body.text==='string'&&['/马币','/马币帮助'].includes(body.text.trim())),'未认证账号只能开通和查询马币、查看帮助；签到、转账和下注请先完成有效认证',403);
  if(body.action==='ack'){
    ensure(user.scope==='qq-prediction','未认证账号只能开通和查询马币',403);
    const receipt=web.rooms.state.qqReceipts?.[body.receiptId];ensure(receipt?.owner===user.subject,'操作回执不属于当前账号',403);
    return web.rooms.commit(()=>{receipt.acknowledged=true;return {acknowledged:true};});
  }
  ensure(isPredictionCommand(body.text),'无法识别赌马命令');
  const [command,...args]=body.text.trim().split(/\s+/);
  if(command==='/马币帮助'||command==='/赌马'&&args[0]==='帮助')return {lines:help,forward:false};
  const unconfirmed=Object.entries(web.rooms.state.qqReceipts??{}).find(([,r])=>r.owner===user.subject&&!r.acknowledged);
  if(command==='/赌马'&&args[0]==='查单'){
    const own=Object.entries(web.rooms.state.qqReceipts??{}).filter(([,r])=>r.owner===user.subject).sort((a,b)=>b[1].at-a[1].at)[0];
    return own?{...own[1].result,receiptId:own[0]}:{lines:['暂无已确认的群内扣款记录。'],forward:false};
  }
  let operation,requestDigest,recipient;
  if(command==='/马币转账'){
    recipient=web.identity(body.recipientTicket);
    ensure(recipient.groupId==='599092307'&&['qq-wallet','qq-prediction'].includes(recipient.scope)&&/^[a-f0-9]{64}$/.test(recipient.qqSubject??''),'收款人身份无效',403);
  }
  if(['/下注','/退半','/清仓','/马币转账'].includes(command)){
    ensure(typeof body.messageId==='string'&&/^[a-zA-Z0-9-]{1,64}$/.test(body.messageId),'群消息标识无效');
    operation='qq-'+createHash('sha256').update(user.subject+':'+user.groupId+':'+body.messageId).digest('hex');
    requestDigest=createHash('sha256').update(recipient?JSON.stringify([body.text.trim(),recipient.qqSubject]):body.text.trim()).digest('hex');
    const receipt=web.rooms.state.qqReceipts?.[operation];
    if(receipt){ensure(receipt.requestDigest===requestDigest,'重复消息的内容不一致',409);return receipt.result;}
    if(unconfirmed)return {...unconfirmed[1].result,receiptId:unconfirmed[0],lines:[...unconfirmed[1].result.lines,'这是上一笔的回执；本条新命令尚未执行。核对后可重新发送新命令。']};
    ensure(Number.isSafeInteger(body.messageAt)&&Math.abs(web.rooms.now()-body.messageAt)<=300000,'消息已过期，请重新发送命令',409);
  }
  if(command==='/马币签到'){
    ensure(args.length===0,'用法：/马币签到');
    return web.rooms.commit(()=>{
      syncQqWallet(web,user);
      const result=web.checkIn({...user,subject:web.rooms.state.qqBindings?.[user.qqSubject]??user.subject});
      return {lines:[`${result.checkInReceipt.duplicate?'今天已签到，网站与群内不能重复领取。':'签到成功，已领取200马币。'}可用${result.playBalance}马币。`],forward:false};
    });
  }
  if(command==='/马币转账'){
    ensure(args.length===1&&/^[1-9][0-9]{0,12}$/.test(args[0]),'用法：/马币转账 @某人 正整数金额');
    return web.rooms.commit(()=>{
      const result=transferQqCoins(web,user,recipient,Number(args[0]),operation);
      const reply={lines:[`转账成功：${result.amount}马币已到账。${result.recipientCreated?'已为收款人自动开通马币账户。':''}你的可用余额${result.balance}马币。`],forward:false,receiptId:operation};
      saveReceipt(web,user,operation,requestDigest,reply);return reply;
    });
  }
  if(command==='/马币'){
    ensure(args.length===0,'用法：/马币');
    const result=syncQqWallet(web,user),me=result.me;
    return {lines:[`${result.created?'已自动开通马币账户。':result.linked||result.synced?'已自动同步游戏马币账户。':''}${me.name}：可用${me.playBalance}马币${me.accounts>1&&me.balance!==me.playBalance?`，名下合计${me.balance}马币`:''}，累计净收益${me.net}马币。${user.scope==='qq-wallet'?(user.boundAccount?'认证已到期，续期后可参与下注。':'尚未绑定游戏账号，暂不能下注；完成认证后再次发送 /马币 即可自动同步。'):''}`],forward:false};
  }
  const room=web.rooms.websiteRoom();ensure(room?.eventSlot,'目前没有整点赌马专场',404);
  const view=web.watch(room);
  if(command==='/赌马')return {lines:[instructions,...publicSummary(view)],presentation:publicPresentation(view),forward:true};
  ensure(args.length>=1,'请提供选手编号1～8或完整昵称');
  const resolve=arg=>{
    if(/^[1-8]$/.test(arg))return Number(arg)-1;
    const found=view.players.map((p,i)=>p.name===arg?i:-1).filter(i=>i>=0);ensure(found.length===1,found.length?'昵称重复，请使用选手编号':'没有找到这位选手，请使用编号1～8');return found[0];
  };
  const index=resolve(args[0]);
  if(command==='/选手')return {lines:publicSummary(view,{player:index}),presentation:publicPresentation(view,{player:index}),forward:true};
  const kind=command==='/下注'?'buy':command==='/退半'?'half':'sell';
  ensure(kind==='buy'?args.length>=2&&args.length%2===0:args.length===1,kind==='buy'?'用法：/下注 选手编号 马币数 [选手编号 马币数 …]':'用法：'+command+' 选手编号');
  ensure(kind!=='buy'||args.length<=64,'一次最多下注32笔');
  const orders=[];
  if(kind==='buy')for(let at=0;at<args.length;at+=2){
    const stake=/^[1-9][0-9]{0,4}$/.test(args[at+1])?Number(args[at+1]):0;
    ensure(stake>=1&&stake<=10000,'每笔投入1～10000马币');orders.push({index:resolve(args[at]),stake});
  }else orders.push({index,stake:0});
  // One QQ/guide subject has one shared wallet selection. All mutations use the
  // existing room transaction and receipt path, including retries and settlement.
  return web.rooms.commit(()=>{
    if(kind==='buy')syncQqWallet(web,user);
    else ensure(web.owned(user).length,'尚无马币账户或持仓');
    const key=web.playKey(user);
    if(!room.members.includes(key))web.play(user,{action:'join',code:room.code});
    const total=orders.reduce((sum,o)=>sum+o.stake,0);
    if(kind==='buy')ensure(total<=web.rooms.state.accounts[key].balance,'马币不足，本条共需'+total+'马币');
    const lines=[];let result;
    for(let at=0;at<orders.length;at++){
      const order=orders[at],previous=room.positions[key]?.[order.index]?.claim??0;
      result=web.play(user,{action:'trade',code:room.code,kind,player:order.index,rank:1,stake:order.stake,version:room.version,operation:orders.length===1?operation:operation+'-'+(at+1)});
      const p=result.wallet.positions[order.index];
      lines.push(orders.length>1?`${order.index+1}号 ${view.players[order.index].name}：投入${order.stake}马币，本笔冠军锁定返还${p.claim-previous}马币。`:`${view.players[index].name}：${kind==='buy'?'投入已确认':kind==='half'?'减半已确认':'清仓已确认'}${p?`，剩余冠军锁定返还${p.claim}马币`:''}。可用${result.wallet.balance}马币。`);
    }
    if(orders.length>1)lines.unshift(`批量下注已确认：${orders.length}笔，共投入${total}马币。可用${result.wallet.balance}马币。`);
    const reply={lines,forward:false,receiptId:operation};
    saveReceipt(web,user,operation,requestDigest,reply,room.code);
    return reply;
  });
}
