export const RACE_GROUP='613486881',LEGACY_GROUP='599092307';
export const TRANSITION_SLOT=process.env.RACE_TRANSITION_SLOT??'2026110314';
export const isRaceGroup=group=>group===RACE_GROUP||group===LEGACY_GROUP;
export function normalizeRaceCommand(value){
 if(typeof value!=='string')return null;
 const input=value.trim(),match=/^\/?(领取补偿|注册|绑定|改名|帮助|签到|赠送|余额|查单|马币排行|全猜|竞猜|助威|观战|走势|减半|撤回|马币|选手)(?=\s|$)/.exec(input);
 if(!match)return null;
 const aliases={余额:'马币',助威:'竞猜'};
 const name=aliases[match[1]]??match[1],args=input.slice(match[0].length).trim();
 return '/'+name+(args?' '+args:'');
}
export function raceNoticeTargets(slot,kind){
 const targets=[{groupId:RACE_GROUP,notice:null}];
 if(typeof slot==='string'&&slot.slice(0,8)===TRANSITION_SLOT.slice(0,8)&&slot<=TRANSITION_SLOT){
  const notice=slot===TRANSITION_SLOT&&['start','settled'].includes(kind)?'迁群提醒：以后整点竞猜将在观战群 '+RACE_GROUP+' 进行。无需认证，加入后发送“注册 昵称”即可参与；钱包绑定QQ，之后认证游戏账号可同步马币。':null;
  targets.push({groupId:LEGACY_GROUP,notice});
 }
 return targets;
}
