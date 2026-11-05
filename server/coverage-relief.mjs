import {RoomError} from './rooms.mjs';
import {ownerOf} from './identity-links.mjs';
import {syncQqWallet} from './qq-wallet.mjs';
export const COVERAGE_RELIEF='coverage-triple-reserve-20261105-v1';
export function claimCoverageRelief(web,user){
 if(!/^[a-f0-9]{64}$/.test(user.qqSubject??''))throw new RoomError('请从本人QQ领取补偿',403);
 return web.rooms.commit(()=>{
  const s=web.rooms.state,c=s.compensationCampaigns?.[COVERAGE_RELIEF];
  if(!c)throw new RoomError('本次补偿尚未开放');
  syncQqWallet(web,user);
  const owner=ownerOf(s,s.qqBindings?.[user.qqSubject]??user.subject),identity={...user,subject:owner};
  const owned=web.owned(identity),keys=new Set(owned.map(([key])=>key));
  const previous=Object.values(c.claims).find(row=>row.qqSubject===user.qqSubject||ownerOf(s,row.owner)===owner||keys.has(row.key));
  if(previous)return {duplicate:true,amount:0};
  if(!c.eligibleKeys.some(key=>keys.has(key)))throw new RoomError('未查到你在本次受影响对局中的竞猜投入，无法领取此补偿');
  const key=web.playKey(identity),a=s.accounts[key],amount=c.amount;
  if(amount!==10000||!Number.isSafeInteger(a.compensationGranted??0)||!Number.isSafeInteger(a.balance+amount)||a.balance+amount>1e12)throw new RoomError('补偿金额或钱包余额需要核对');
  a.balance+=amount;a.compensationGranted=(a.compensationGranted??0)+amount;
  c.claims[user.qqSubject]={key,owner,qqSubject:user.qqSubject,amount,at:web.rooms.now()};
  return {duplicate:false,amount,balance:a.balance};
 });
}
