const assert=require('node:assert/strict');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const url=process.env.CHARACTER_BOARD_URL||'https://elo.kaigua.vip/elo/characters/';
const origin=new URL(url).origin;
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.WEB_TEST_BROWSER||'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 try{
  const page=await browser.newPage({viewport:{width:1440,height:1000}}),errors=[],requests=[],seen={};
  page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>requests.push(r.url()));
  page.on('response',async r=>{
   const name=new URL(r.url()).pathname.split('/').pop();
   if(['window.json','live.json'].includes(name)){
    const d=await r.json();seen[name]=d;
   }
  });
  const ready=()=>page.locator('#rows tr').first().waitFor();
  assert.equal((await page.goto(url)).status(),200);await ready();
  assert.equal(await page.locator('#rows tr').count(),24);assert.match(await page.locator('#rows tr').first().innerText(),/林小月/);
  assert.equal(await page.locator('#games').innerText(),'20,806');assert.equal(await page.locator('#players').innerText(),'14,540');
  await page.locator('[data-band=high]').click();assert.match(await page.locator('#rows tr').first().innerText(),/谭舒雁/);
  await page.locator('#career').selectOption('阵法师');await page.locator('#search').fill('祁忘忧');
  assert.equal(await page.locator('#rows tr').count(),1);
  const projected=seen['window.json'].heroes.find(h=>h.name==='祁忘忧').bands.high.careers['阵法师'];
  assert.equal(await page.locator('#rows tr td').nth(2).innerText(),projected.games?projected.score.toFixed(1):'待首局');
  await page.locator('#search').fill('不存在的角色');assert(await page.locator('#empty').isVisible());
  await page.locator('#search').fill('');await page.locator('[data-band=overall]').click();
  await page.locator('[data-source=live]').click();await ready();
  assert.equal(await page.locator('#rows tr').count(),24);assert.match(await page.locator('#modeLabel').innerText(),/自动更新/);
  await page.locator('[data-source=window]').click();await ready();
  await page.locator('details summary').click();assert.match(await page.locator('#notes').innerText(),/约9小时/);
  await page.reload();await ready();
  for(const width of [390,320]){
   await page.setViewportSize({width,height:840});
   assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'mobile overflow at '+width);
  }
  for(const d of Object.values(seen)){
   assert.equal(d.heroes.length,24);
   for(const h of d.heroes){
    assert.deepEqual(Object.keys(h).sort(),['bands','name','overall']);
    assert.deepEqual(Object.keys(h.overall).sort(),['appearances','games','playerCount','score']);
    for(const v of Object.values(h.bands)){
     assert.equal(Object.keys(v.careers).length,7);
     for(const c of Object.values(v.careers))assert.deepEqual(Object.keys(c).sort(),['appearances','games','playerCount','score']);
    }
   }
   assert(!/\b(?:uid|codeId|gameId|roomId|replayCode|lastCode|[a-f0-9]{24})\b/i.test(JSON.stringify(d)));
  }
  assert(seen['window.json']&&seen['live.json']);
  assert(requests.every(s=>new URL(s).origin===origin));
  assert(!requests.some(s=>/\/api\/|\/data\/elo\//.test(new URL(s).pathname)));
  assert.deepEqual(errors,[]);console.log(JSON.stringify({windowGames:seen['window.json'].games,liveGames:seen['live.json'].games,heroCount:24,tiers:true,careerAndSearch:true,mobile:true,publicOnly:true}));
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exit(1)});
