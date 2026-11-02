/* Browser spectating uses the room engine; no game client or game credentials here. */
(()=>{
const P=window.Prediction,$=id=>document.getElementById(id);
let page=0,room=null,wallet=null,selected=0,joined=false,visible='hall',polling=false,busy=false,pending=null,roundChoice='live';
try{pending=JSON.parse(sessionStorage.getItem('prediction-pending-order')||'null')}catch{}
const fmt=n=>Number(n).toLocaleString('zh-CN'),rate=n=>(n/10000).toFixed(2)+'×';
function node(tag,text,cls){const e=document.createElement(tag);if(text!==undefined)e.textContent=text;if(cls)e.className=cls;return e}
function setPending(value){pending=value;if(value)sessionStorage.setItem('prediction-pending-order',JSON.stringify(value));else sessionStorage.removeItem('prediction-pending-order')}
async function rooms(){
 $('rooms-refresh').disabled=true;
 try{const data=await P.api('/web/rooms?page='+page);$('rooms-count').textContent=data.total?'当前公开对局 · 持续观战':'等待下一场公开对局';$('room-list').replaceChildren();
  if(!data.rooms.length){const e=node('div',undefined,'hall-empty');e.append(node('span','候一阵风，等一局棋。','empty-title'),node('p','目前没有正在同步的公开房间。MOD房主公开创建房间后，会出现在这里。'));$('room-list').append(e)}
  for(const r of data.rooms){const card=node('article',undefined,'room-card'),head=node('div',undefined,'room-card-head');head.append(node('span',r.open?'可预测':'已封盘',r.open?'badge live':'badge'),node('span','第 '+r.round+' 回合','muted'));card.append(head,node('h3',r.hostName+' 的观战席'),node('p',r.names.slice(0,3).join(' · ')+' 等八位仙友','roster-preview'));const bottom=node('div',undefined,'room-card-bottom');bottom.append(node('span','#'+r.code+' · '+r.members+'人入席','muted'));const button=node('button','入席观战 ↗','text-button');button.onclick=()=>void enter(r.code);bottom.append(button);card.append(bottom);$('room-list').append(card)}
  $('rooms-page').textContent='第 '+(page+1)+' / '+Math.max(1,Math.ceil(data.total/12))+' 页';$('rooms-prev').disabled=page===0;$('rooms-next').disabled=(page+1)*12>=data.total;
 }catch(e){P.note(e.message);$('rooms-count').textContent='对局读取失败，请点击刷新'}finally{$('rooms-refresh').disabled=false}
}
async function enter(code){
 if(busy)return;joined=false;wallet=null;selected=0;roundChoice='live';
 try{room=await P.api('/web/room?code='+encodeURIComponent(code));P.tab('watch');draw();if(window.predictionMe?.linked)await join()}
 catch(e){if(window.predictionMe?.linked){try{const data=await P.api('/web/play',{privateRequest:true,body:{action:'join',code}});accept(data);P.tab('watch');return}catch(inner){P.note(inner.message);return}}P.note(e.message);if(!window.predictionMe)void P.openLogin()}
}
function accept(data){room=data.room;wallet=data.wallet;joined=true;if(data.me)P.renderMe(data.me);draw()}
async function join(){
 if(!window.predictionMe){await P.openLogin();return}
 if(!window.predictionMe.linked){P.tab('mine');P.note('开通网页马币，或绑定已有MOD账户后即可入席');return}
 if(!room)return;try{accept(await P.api('/web/play',{privateRequest:true,body:{action:'join',code:room.code}}))}catch(e){P.note(e.message)}
}
function draw(){
 if(!room)return;
 $('watch-code').textContent='仙府对局 · #'+room.code;$('watch-title').textContent=room.hostName+' 的观战席';
 $('watch-live').textContent=room.settled?'本局已结算':room.ended?'等待最终战果':!room.fresh?'战况来源离线':(room.round>=10||room.tradingClosed)?'第 '+room.round+' 回合 · 已封盘':'第 '+room.round+' 回合 · '+(room.open?'可预测':'等待修炼阶段');
 $('watch-live').className=room.fresh&&!room.ended?'live-text':'muted';
 $('watch-rule').textContent=(room.eventSlot?'整点专场 · 倍率额外×1.15 · ':'')+(room.horseShuffle?'本局随机交换2–3匹真人马的档位 · ':'只猜冠军 · ')+'第10回合封盘';$('horse-grid').replaceChildren();
 room.players.forEach((p,i)=>{const card=node('button',undefined,'horse-card c'+i+(i===selected?' selected':''));card.type='button';card.setAttribute('aria-pressed',String(i===selected));card.onclick=()=>{selected=i;draw()};
  const head=node('div',undefined,'horse-card-head');head.append(node('span',String(i+1).padStart(2,'0'),'horse-number'),node('span',p.rank?(p.rankMinimum<p.rankMaximum?'第 '+p.rankMinimum+'–'+p.rankMaximum+'名同出':'第 '+p.rank+' 名'):p.active?'在场':'已退出','horse-state'));card.append(head,node('h3',p.name),node('span',p.horseTitle||'','horse-tier'));
  const life=node('div',undefined,'horse-life');life.append(node('strong',p.state?String(p.state.life):'—'),node('span','命元'));card.append(life);
  card.append(node('p',p.state?p.state.wins+'胜 / '+p.state.losses+'负':'等待公开战况','muted'));
  const quote=node('div',undefined,'horse-quote');quote.append(node('span','冠军倍率'),node('strong',p.quoteRate?rate(p.quoteRate):'—'));card.append(quote,node('small',p.baseRate?'基础 '+rate(p.baseRate)+' × 系数 '+rate(p.factor)+(room.bonusBps>10000?' × 专场 '+rate(room.bonusBps):''):'等待基础倍率','muted'));
  const held=wallet?.positions[i];card.append(node('span',held?'已投入 '+fmt(held.stake)+' 马币':i===selected?'已选中 · 预测冠军':'选择这位仙友',held?'horse-held':'horse-select'));$('horse-grid').append(card)});
 drawBattles();drawChart();drawNotes();drawBet();
}
function drawBattles(){
 const rounds=[...new Set((room.battles||[]).map(b=>b.round))].sort((a,b)=>b-a),select=$('battle-round');select.replaceChildren();const follow=node('option','跟随最新回合');follow.value='live';select.append(follow);
 for(const number of rounds){const option=node('option','第 '+number+' 回合');option.value=String(number);select.append(option)}
 if(roundChoice!=='live'&&!rounds.includes(Number(roundChoice)))roundChoice='live';select.value=roundChoice;
 const chosen=roundChoice==='live'?rounds[0]:Number(roundChoice),battles=(room.battles||[]).filter(b=>b.round===chosen),target=$('battle-tables');target.replaceChildren();
 $('battle-summary').textContent=battles.length?'第 '+chosen+' 回合 · 已同步 '+battles.length+' 场对战':'等待观战端同步牌桌与战果。';
 if(!battles.length){target.append(node('p','战斗数据到达后会自动显示，可持续跟随，也可回看已同步回合。','chart-empty'));return}
 for(const battle of battles){const table=node('article',undefined,'duel-table'),winner=battle.winner;
  const title=node('div',undefined,'duel-title');title.append(node('span',room.players[battle.first].name+(battle.firstMirror?'（镜像）':'')),node('span',winner===null?'胜负待确认':room.players[winner].name+' 胜','duel-result'),node('span',room.players[battle.second].name+(battle.secondMirror?'（镜像）':'')));table.append(title);
  for(const side of ['first','second']){const line=node('div',undefined,'duel-side'),who=battle[side],cards=battle[side+'Cards'],names=battle[side+'Names'];
   const name=node('div',undefined,'duel-player');name.append(node('strong',room.players[who].name),node('small',(battle[side+'Mirror']?'镜像 · ':'')+'战后命元 '+battle[side+'Life']));line.append(name);
   const board=node('div',undefined,'card-board');if(cards===null){board.append(node('p','该方公开牌阵尚未同步','muted'))}else for(let i=0;i<8;i++){const id=cards[i]??0,card=node('div',undefined,'played-card'+(id?'':' empty-card'));card.append(node('small',String(i+1).padStart(2,'0'),'card-slot'),node('strong',id?(names?.[i]||'卡牌 '+id):'空位'));if(id)card.append(node('small','#'+id,'card-id'));board.append(card)}line.append(board);table.append(line);
  }target.append(table);
 }
}
function drawChart(){
 const target=$('life-chart');target.replaceChildren();$('chart-legend').replaceChildren();
 const frames=room.frames.filter(f=>f.states.some(Boolean));
 if(!frames.length){target.append(node('p','等待房主同步命元数据。旧版本房主仍可提供倍率与下注。','chart-empty'));return}
 const ns='http://www.w3.org/2000/svg',svg=document.createElementNS(ns,'svg');svg.setAttribute('viewBox','0 0 800 240');svg.setAttribute('role','img');svg.setAttribute('aria-label','八位选手的命元随回合变化');
 const max=Math.max(100,...frames.flatMap(f=>f.states.filter(Boolean).map(s=>s.life))),minRound=frames[0].round,maxRound=Math.max(minRound+1,frames.at(-1).round);
 for(let i=0;i<5;i++){const y=20+i*46,line=document.createElementNS(ns,'line');line.setAttribute('x1','40');line.setAttribute('x2','780');line.setAttribute('y1',y);line.setAttribute('y2',y);line.setAttribute('class','chart-grid');svg.append(line);const label=document.createElementNS(ns,'text');label.setAttribute('x','4');label.setAttribute('y',y+4);label.textContent=Math.round(max*(1-i/4));svg.append(label)}
 room.players.forEach((p,i)=>{let segments=[],segment=[];for(const frame of frames){const state=frame.states[i];if(!state){if(segment.length)segments.push(segment);segment=[];continue}segment.push((40+(frame.round-minRound)/(maxRound-minRound)*740).toFixed(1)+','+(204-Math.max(0,state.life)/max*184).toFixed(1))}if(segment.length)segments.push(segment);for(const points of segments){const line=document.createElementNS(ns,'polyline');line.setAttribute('points',points.join(' '));line.setAttribute('class','life-line c'+i);svg.append(line)}const label=node('span',p.name,'legend-item c'+i);$('chart-legend').append(label)});
 for(const [round,x]of [[minRound,40],[frames.at(-1).round,730]]){const label=document.createElementNS(ns,'text');label.setAttribute('x',x);label.setAttribute('y','233');label.textContent='第'+round+'回合';svg.append(label)}target.append(svg);
}
function drawNotes(){
 const list=$('round-notes');list.replaceChildren();const last=room.frames.at(-1),prev=room.frames.at(-2);
 room.players.forEach((p,i)=>{if(p.rank)list.append(node('li',p.name+' 已确定第 '+p.rank+' 名'));else if(last?.states[i]){const state=last.states[i],before=prev?.states[i];list.append(node('li',p.name+'：命元 '+(before?before.life+' → ':'')+state.life+'，累计 '+state.wins+'胜 '+state.losses+'负'))}});
 if(!list.children.length)list.append(node('li','尚未收到本轮公开战况。'));
}
function drawBet(){
 const p=room.players[selected],position=wallet?.positions[selected],amount=Number($('bet-amount').value);
 $('selected-horse').textContent=p.name;$('play-balance').textContent=wallet?fmt(wallet.balance):window.predictionMe?.playBalance!=null?fmt(window.predictionMe.playBalance):'登录后查看';$('play-account').textContent=wallet?'当前账户：'+wallet.name:'';
 $('watch-join').hidden=joined;$('watch-join').textContent=!window.predictionMe?'登录并入席':window.predictionMe.linked?'加入此房间':'开通马币后入席';$('bet-controls').hidden=!joined;
 $('bet-quote').textContent=p.quoteRate?rate(p.quoteRate):'—';const claim=Number.isSafeInteger(amount)&&amount>0?Math.floor(amount*p.quoteRate/10000):0;
 $('bet-return').textContent=fmt(claim)+' 马币';$('bet-partial').textContent=fmt(Math.floor(claim/4))+' / '+fmt(Math.floor(claim/10));
 $('held-position').textContent=position?fmt(position.stake)+' 马币':'暂无投入';$('held-claim').textContent=position?(wallet?.forfeited?'已离场放弃，不再返还':'锁定冠军返还 '+fmt(position.claim??0)+' 马币'+(position.resolved?' · 已结算':'')):'';
 let reason=wallet?.forfeited?'已离场放弃本局持仓，仅可查看，不再返还。':room.settled?'本局已结算，返还已进入账户。':room.ended?'本局已结束，等待最终战果确认。':!room.fresh?'战况来源暂时离线，已暂停交易。':(room.round>=10||room.tradingClosed)?'第10回合起已封盘，等待本局结算。':!room.open?'等待下一修炼阶段开放交易。':!p.active||p.rank?'这位选手已退出或确定名次，无法继续投入。':room.rules!==3?'旧房间仅保留原有持仓，请加入新的冠军预测房间。':'';
 if(pending)reason='上笔操作尚待确认，请先确认结果，避免重复投入。';$('bet-reason').textContent=reason;$('bet-retry').hidden=!pending||pending.owner!==window.predictionMe?.id;
 $('bet-buy').disabled=busy||!!reason||!Number.isSafeInteger(amount)||amount<1||amount>10000||amount>(wallet?.balance??0);
 $('bet-half').disabled=$('bet-sell').disabled=busy||!!reason||!position||position.resolved;
}
async function trade(kind,retry=false){
 if(busy||!joined)return;
 let order=pending;
 if(retry){if(!order||order.owner!==window.predictionMe?.id){P.note('请用发起该笔操作的账户登录');return}}
 else{if(pending){P.note('请先确认上一笔操作');return}order={owner:window.predictionMe.id,action:'trade',code:room.code,kind,rank:1,player:selected,stake:Number($('bet-amount').value),version:room.version,operation:crypto.randomUUID()};setPending(order)}
 busy=true;drawBet();try{const {owner,...body}=order;const data=await P.api('/web/play',{privateRequest:true,body});setPending(null);accept(data);P.note('操作已确认，余额已更新')}
 catch(e){if([400,409].includes(e.status)){setPending(null);P.note(e.message+'，本笔未成交');void poll()}else P.note(e.message+'。请点击“确认上一笔操作”核对结果。')}
 finally{busy=false;drawBet()}
}
async function poll(){
 if(polling||document.hidden||busy)return;polling=true;
 try{if(visible==='hall')await rooms();else if(visible==='watch'&&room){if(joined)accept(await P.api('/web/play',{privateRequest:true,body:{action:'view',code:room.code}}));else{room=await P.api('/web/room?code='+room.code);draw()}}}catch(e){if(room){room.fresh=false;room.open=false;draw()}P.note(e.message)}finally{polling=false}
}
 $('rooms-refresh').onclick=()=>void rooms();$('rooms-prev').onclick=()=>{if(page){page--;void rooms()}};$('rooms-next').onclick=()=>{page++;void rooms()};
 $('watch-back').onclick=()=>{P.tab('hall');void rooms()};$('watch-join').onclick=()=>void join();
 $('battle-round').onchange=()=>{roundChoice=$('battle-round').value;drawBattles()};$('bet-amount').oninput=drawBet;$('bet-buy').onclick=()=>void trade('buy');$('bet-half').onclick=()=>void trade('half');$('bet-sell').onclick=()=>void trade('sell');$('bet-retry').onclick=()=>void trade('',true);
 for(const b of document.querySelectorAll('[data-stake]'))b.onclick=()=>{$('bet-amount').value=b.dataset.stake==='half'?Math.max(1,Math.floor((wallet?.balance??0)/2)):b.dataset.stake;drawBet()};
 window.addEventListener('prediction-tab',e=>{visible=e.detail;if(visible==='hall')void rooms()});
 window.addEventListener('prediction-login',()=>{if(room){P.tab('watch');void join()}});window.addEventListener('prediction-logout',()=>{joined=false;wallet=null;if(room)draw()});
 setInterval(()=>void poll(),4000);void rooms();
})();
