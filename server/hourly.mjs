import {RoomError} from './rooms.mjs';
import {randomBytes} from 'node:crypto';
import {buildLines,publicPresentation} from './public-build.mjs';

export const EVENT_BONUS_BPS=11500;
const ensure=(ok,message,status=409)=>{if(!ok)throw new RoomError(message,status)};
export function hourlySlot(now){
  const t=new Date(now+8*3600000),hour=t.getUTCHours();
  if(hour>=2&&hour<=6||t.getUTCMinutes()>=5)return null;
  return t.toISOString().slice(0,13).replace(/[-T:]/g,'');
}
export function eligibleLobby(info){
  return !!info&&info.isPrivate===false&&info.playerCount===8&&Number.isSafeInteger(info.round)&&info.round>=1&&info.round<=2
    &&Number.isSafeInteger(info.avgRankScore)&&info.avgRankScore>=6000&&typeof info.roomId==='string'&&/^[a-zA-Z0-9_-]{1,128}$/.test(info.roomId);
}
const rate=n=>(n/10000).toFixed(2)+'倍';
export function publicSummary(view,{round=view.round,player=null}={}){
  const frame=view.frames?.find(f=>f.round===round+1)??view.frames?.find(f=>f.round===round),players=player===null?view.players.map((_,i)=>i):[player];
  const lines=[`整点赌马 #${view.code} · 第${round}回合${view.settled?' · 已结算':view.tradingClosed?' · 已封盘':''}`];
  for(const i of players){
    const p=view.players[i],state=frame?.states?.[i]??(round===view.round?p.state:null),rank=round>=view.round-1?p.rank:frame?.ranks?.[i]??0;
    const active=frame?(state?state.life>0:rank===0):p.active;
    const build=view.frames?.find(f=>f.round===round)?.builds?.[i]??(round===view.round?p.build:null);
    lines.push(`${i+1}号 ${p.name} · ${p.horseTitle||'马匹档位待同步'}\n${state?`命元${state.life} · ${state.wins}胜${state.losses}负`:'公开战况待同步'}${rank?` · 第${rank}名`:!active?' · 已退出':''}${round===view.round&&p.quoteRate>0?` · 冠军${rate(p.quoteRate)}`:''}\n${buildLines(build).join('\n')}`);
  }
  for(const b of view.battles??[]){
    if(b.round!==round||player!==null&&b.first!==player&&b.second!==player)continue;
    const a=view.players[b.first],z=view.players[b.second];
    lines.push(`${a.name}${b.firstMirror?'（镜像）':''} 对 ${z.name}${b.secondMirror?'（镜像）':''}\n${a.name}：${b.firstNames?.filter(Boolean).join('、')||'公开牌桌待同步'}\n${z.name}：${b.secondNames?.filter(Boolean).join('、')||'公开牌桌待同步'}\n${b.winner===null?'胜负待确认':view.players[b.winner].name+'胜'}`);
  }
  if(view.settled&&view.settlement){lines.push(view.settlement.closing);for(const r of view.settlement.rows)lines.push(`${r.name}：结算返还${r.payout}马币${r.known?`，出售回收${r.sold}，累计投入${r.invested}，净收益${r.net>0?'+':''}${r.net}`:'；历史流水不完整，净收益待核对'}。`);}
  return lines;
}

export class HourlyEvents{
  constructor(rooms,web){this.rooms=rooms;this.web=web;}
  data(){return this.rooms.state.hourly??{slots:{},outbox:[]};}
  status(){
    const active=Object.values(this.rooms.state.rooms).find(r=>r.eventSlot&&!r.settled);
    const featured=this.rooms.websiteRoom(),slot=hourlySlot(this.rooms.now());
    return {slot,bonusBps:EVENT_BONUS_BPS,minimumRankScore:6000,maximumStartRound:2,searchMinutes:5,
      active:active?{code:active.code,gameId:active.gameId,gameRoomId:active.gameRoomId,info:this.data().slots[active.eventSlot]?.info??null,model:this.data().slots[active.eventSlot]?.model??null}:null,
      opened:slot?this.data().slots[slot]?.code??null:null,available:!!slot&&!active&&(!featured||featured.ended||featured.settled)};
  }
  open(info,snapshot,model=null){
    const slot=hourlySlot(this.rooms.now());ensure(slot,'当前不在整点开盘时段');
    const previous=this.data().slots[slot];
    if(previous){ensure(previous.gameId===snapshot?.id,'本整点已经开过一场');return this.web.watch(this.rooms.state.rooms[previous.code]);}
    ensure(this.status().available,'上一场公开观战尚未结束');
    ensure(eligibleLobby(info),'需要刚开始、原生平均段位分达到6000的公开八人对局');
    ensure(snapshot?.gameRoomId===info.roomId&&snapshot.round>=1&&snapshot.round<=2&&!snapshot.ended,'观战对局已不符合整点条件');
    ensure(Array.isArray(snapshot.baseRates)&&snapshot.baseRates.length===64&&snapshot.players?.every((_,i)=>snapshot.baseRates[i*8]>0),'等待八人冠军倍率就绪');
    ensure(model===null||(Array.isArray(model)&&model.length===8&&new Set(model.map(p=>p.uid)).size===8&&model.every(p=>snapshot.players.some(q=>q.uid===p.uid)&&typeof p.found==='boolean'&&typeof p.score==='number'&&Number.isFinite(p.score)&&p.score>=0&&p.score<=10000)),'观战报价模型格式无效');
    return this.rooms.commit(()=>{
      const data=this.rooms.state.hourly??={slots:{},outbox:[]};
      data.hostToken??=this.rooms.register('WHITE整点观战').token;
      this.rooms.state.accounts[data.hostToken].systemObserver=true;
      ensure(!Object.values(this.rooms.state.rooms).some(r=>!r.ended&&!r.settled&&(r.gameId===snapshot.id||r.gameRoomId===info.roomId)),'该对局已经有房间');
      const view=this.rooms.create(data.hostToken,snapshot,true,true,{slot,bonusBps:EVENT_BONUS_BPS});
      data.slots[slot]={code:view.code,gameId:snapshot.id,openedAt:this.rooms.now(),info:structuredClone(info),model:structuredClone(model),participants:{[snapshot.round]:snapshot.players.map((_,i)=>!snapshot.states?.[i]||snapshot.states[i].life>0)},noticeWait:{}};
      this.enqueue('start',this.rooms.state.rooms[view.code]);
      const slots=Object.keys(data.slots).sort();for(const old of slots.slice(0,Math.max(0,slots.length-2160)))delete data.slots[old];
      return this.web.watch(this.rooms.state.rooms[view.code]);
    });
  }
  sync(code,snapshot){
    const data=this.data(),r=this.rooms.state.rooms[code];ensure(r&&r.eventSlot&&r.host===data.hostToken,'不是本服务的整点房间',403);
    return this.rooms.commit(()=>{
      const slot=data.slots[r.eventSlot];slot.participants??={};slot.noticeWait??={};
      slot.participants[snapshot.round]??=r.players.map(p=>{const i=snapshot.players.findIndex(q=>q.uid===p.uid);return !snapshot.states?.[i]||snapshot.states[i].life>0;});
      this.rooms.sync(code,data.hostToken,snapshot);
      for(const frame of r.frames??[])if(frame.round<=r.closedThrough&&(frame.round<snapshot.round||snapshot.roundComplete!==false)){
        const covered=new Set();for(const b of r.battles??[])if(b.round===frame.round){if(!b.firstMirror)covered.add(b.first);if(!b.secondMirror)covered.add(b.second);}
        const expected=slot.participants[frame.round]??frame.states.map(p=>!!p&&p.life>0);
        slot.noticeWait[frame.round]??=this.rooms.now()+45000;
        if(r.settled||expected.every((active,i)=>!active||covered.has(i))||this.rooms.now()>=slot.noticeWait[frame.round])this.enqueue('round',r,frame.round);
      }
      if(r.settled)this.enqueue('settled',r);
      return this.web.watch(r);
    });
  }
  enqueue(kind,r,round=r.round){
    const data=this.data(),id=`${r.eventSlot}:${r.code}:${kind}:${kind==='round'?round:0}`;
    if(data.outbox.some(e=>e.id===id))return;
    const view=this.web.watch(r),lines=kind==='start'
      ?[`整点赌马开盘 #${r.code} · 倍率额外×1.15\n只猜冠军，第10回合封盘；第二25%、第三10%。\n/下注 选手编号 马币数 · /赌马 · /选手 编号\nhttps://duma.kaigua.vip/`,...publicSummary(view)]
      :publicSummary(view,{round});
    data.outbox.push({id,kind,code:r.code,round,lines,presentation:publicPresentation(view,{round}),createdAt:this.rooms.now(),deliveredAt:null});
    if(data.outbox.length>4000)data.outbox=data.outbox.filter(e=>!e.deliveredAt||e.deliveredAt>this.rooms.now()-7*86400000);
  }
  claim(){
    const row=this.data().outbox.find(e=>!e.deliveredAt&&(!e.leaseUntil||e.leaseUntil<=this.rooms.now()));if(!row)return null;
    return this.rooms.commit(()=>{if(!row.presentation&&this.rooms.state.rooms[row.code])row.presentation=publicPresentation(this.web.watch(this.rooms.state.rooms[row.code]),{round:row.round});row.lease=randomBytes(16).toString('hex');row.leaseUntil=this.rooms.now()+60000;return structuredClone(row);});
  }
  ack(id,lease){
    const row=this.data().outbox.find(e=>e.id===id);ensure(row&&row.lease===lease,'通知租约已过期');
    if(row.deliveredAt)return {ok:true};
    return this.rooms.commit(()=>{row.deliveredAt=this.rooms.now();return {ok:true};});
  }
}
