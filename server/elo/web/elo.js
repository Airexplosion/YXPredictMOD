/* Anonymous presentation adapter for the existing data site's EloTab entry. */
(()=>{
let ticket=0;
const host=()=>document.getElementById('view-elo');
function el(tag,text,cls){const node=document.createElement(tag);if(text!==undefined)node.textContent=text;if(cls)node.className=cls;return node}
async function render(){
 const root=host();if(!root)return;
 const mine=++ticket;root.replaceChildren();root.classList.add('anon-elo');
 const heading=el('div',undefined,'anon-heading'),copy=el('div');copy.append(el('h2','匿名 Elo 榜'));heading.append(copy);root.append(heading);
 const state=el('p','正在读取技术分…','anon-state');state.setAttribute('role','status');root.append(state);
 try{
  const response=await fetch('/api/v1/leaderboard?limit=100',{credentials:'omit',referrerPolicy:'no-referrer'});
  if(!response.ok)throw Error('榜单暂时无法读取');const data=await response.json();if(mine!==ticket)return;
  if(!Array.isArray(data.scores)||data.scores.some(s=>!Number.isFinite(s)))throw Error('榜单格式异常');
  state.remove();
  const frame=el('div',undefined,'anon-frame'),table=el('table'),head=el('thead'),tr=el('tr');tr.append(el('th','名次'),el('th','技术分'));head.append(tr);table.append(head);const rows=el('tbody');
  data.scores.forEach((score,index)=>{const row=el('tr');if(data.offset+index<3)row.className='anon-podium';row.append(el('td',String(data.offset+index+1).padStart(2,'0'),'anon-place'),el('td',score.toLocaleString('zh-CN'),'anon-score'));rows.append(row)});table.append(rows);frame.append(table);root.append(frame);
  if(!data.scores.length)root.append(el('p','这一页暂无分数。','anon-state'));
 }catch(error){if(mine!==ticket)return;state.textContent='榜单暂时无法读取，请稍后重试。';const retry=el('button','重新读取','anon-retry');retry.type='button';retry.onclick=()=>void render();root.append(retry)}
}
window.EloTab={render,tab:()=> '匿名 Elo',sub:()=> ''};
if(host()&&(!host().hidden||host().hasAttribute('data-anonymous-elo')))void render();
})();
