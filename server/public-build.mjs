import {RoomError} from './rooms.mjs';
const valid=(ok)=>{if(!ok)throw new RoomError('公开角色或仙命资料格式无效')};
const integer=(n,min,max)=>Number.isSafeInteger(n)&&n>=min&&n<=max;
const text=s=>typeof s==='string'&&s.length>0&&s.length<=80&&!/[\x00-\x1f\x7f]/.test(s);
const choice=p=>{valid(p&&integer(p.id,1,2147483647)&&text(p.name));return {id:p.id,name:p.name};};
export function publicBuild(value,round=100000){
  if(value===null)return null;
  valid(value&&integer(value.characterId,1,2147483647)&&integer(value.careerId,0,32)&&integer(value.sectId,0,32)&&integer(value.realmId,0,16));
  for(const key of ['characterName','careerName','sectName','realmName'])valid(text(value[key]));
  for(const key of ['talents','baijia','fates'])valid(Array.isArray(value[key])&&value[key].length<=32);
  valid(integer(value.selectionRound,0,round)&&typeof value.selectionsKnown==='boolean');
  const cards=value.cards??null,cardNames=value.cardNames??null;
  valid(cards===null&&cardNames===null||Array.isArray(cards)&&cards.length<=8&&cards.every(id=>integer(id,0,2147483647))&&Array.isArray(cardNames)&&cardNames.length===cards.length&&cardNames.every(text));
  return {characterId:value.characterId,characterName:value.characterName,careerId:value.careerId,careerName:value.careerName,
    sectId:value.sectId,sectName:value.sectName,realmId:value.realmId,realmName:value.realmName,
    talents:value.talents.map(choice),baijia:value.baijia.map(choice),fates:value.fates.map(choice),resonance:value.resonance===null?null:choice(value.resonance),
    selectionRound:value.selectionRound,selectionsKnown:value.selectionsKnown,cards:cards&&[...cards],cardNames:cardNames&&[...cardNames]};
}
export function publicPresentation(view,{round=view.round,player=null}={}){
  const frame=view.frames?.find(f=>f.round===round),after=view.frames?.find(f=>f.round===round+1);
  const available=(view.battles??[]).filter(b=>b.round<=round&&(player===null||b.first===player||b.second===player));
  const battleRound=available.some(b=>b.round===round)?round:Math.max(0,...available.map(b=>b.round));
  const battles=available.filter(b=>b.round===battleRound);
  const players=(player===null?view.players.map((_,i)=>i):[player]).map(index=>{
    const p=view.players[index],build=frame?.builds?.[index]??(round===view.round?p.build:null);
    const battle=available.filter(b=>b.first===index&&!b.firstMirror||b.second===index&&!b.secondMirror).sort((a,z)=>z.round-a.round)[0],side=battle?.first===index?'first':'second';
    const board=battle&&battle[side+'Cards']?{round:battle.round,cards:battle[side+'Cards'],names:battle[side+'Names']??battle[side+'Cards'].map(id=>id?'未知牌':'空位')}:build?.cards&&build.selectionRound>0&&build.selectionRound<=round?{round:build.selectionRound,cards:build.cards,names:build.cardNames}:null;
    return {index,name:p.name,horseTitle:p.horseTitle,rank:round===view.round?p.rank:after?.ranks?.[index]??frame?.ranks?.[index]??0,
      state:after?.states?.[index]??frame?.states?.[index]??(round===view.round?p.state:null),quoteRate:round===view.round?p.quoteRate:0,build,board};
  });
  return structuredClone({kind:'duma-public-v1',code:view.code,round,settled:view.settled,tradingClosed:view.tradingClosed,settlement:view.settled?view.settlement??null:null,players,
    battles:battles.map(b=>({...b,firstName:view.players[b.first].name,secondName:view.players[b.second].name,firstCharacterId:view.players[b.first].build?.characterId??0,secondCharacterId:view.players[b.second].build?.characterId??0,winnerName:b.winner===null?null:view.players[b.winner].name}))});
}
export function buildLines(build){
  if(!build)return ['角色、职业及仙命资料待同步'];
  const names=rows=>rows.map(p=>p.name).join('、'),lines=[`角色：${build.characterName} · ${build.sectName} · ${build.realmName} · 副职：${build.careerName}`,
    '仙命：'+(names(build.talents)||'暂无公开仙命'),
    `百家仙命${build.selectionRound?'（公开至第'+build.selectionRound+'回合）':''}：`+(names(build.baijia)||(build.selectionsKnown?'暂无公开选取':'等待公开'))];
  if(build.resonance)lines.push('共鸣仙命：'+build.resonance.name);
  if(build.fates.length)lines.push('天衍仙命：'+names(build.fates));
  return lines;
}
