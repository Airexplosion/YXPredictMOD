const assert=require('node:assert/strict');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const base=process.env.ELO_ORIGIN||'https://elo.kaigua.vip';
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.WEB_TEST_BROWSER||'C:/Program Files/Google/Chrome/Application/chrome.exe'});
 try{
  const page=await browser.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));
  // The personal page must remain usable even if the old dynamic API is unavailable.
  await page.route('**/api/v1/**',route=>route.abort());
  await page.goto(base+'/elo/');await page.locator('.anon-score').first().waitFor();
  assert.equal(await page.locator('.anon-score').count(),100);
  const feed=await (await page.request.get(base+'/elo/characters/season.json')).json();
  await page.clock.install();let fail=false,calls=0;
  await page.route('**/elo/characters/season.json',route=>{
   calls++;return fail?route.abort():route.fulfill({json:feed});
  });
  await page.goto(base+'/elo/characters/?band=high');await page.locator('#rows tr').first().waitFor();
  await page.locator('#search').fill('聆羽');const original=await page.locator('#rows tr').innerText();
  const current=feed.periods.at(-1),count=current.games;
  current.games=count+1;current.asOf=Date.now();
  const got=page.waitForResponse(r=>r.url().endsWith('/season.json'));
  await page.clock.fastForward(61000);await got;
  await page.waitForFunction(n=>document.getElementById('games').textContent===n,(count+1).toLocaleString('zh-CN'));
  assert.equal(await page.locator('#rows tr').innerText(),original);
  assert.equal(await page.locator('#search').inputValue(),'聆羽');
  assert.equal(await page.locator('[data-band="high"]').getAttribute('aria-pressed'),'true');
  fail=true;await page.clock.fastForward(61000);
  await page.waitForFunction(()=>document.getElementById('status').textContent.includes('刷新失败'));
  assert.equal(await page.locator('#rows tr').innerText(),original);
  fail=false;current.games=count+2;
  await page.locator('#status button').click();
  await page.waitForFunction(n=>document.getElementById('games').textContent===n,(count+2).toLocaleString('zh-CN'));
  current.asOf=Date.now()-21*60000;
  await page.clock.fastForward(61000);
  await page.waitForFunction(()=>document.getElementById('status').textContent.includes('更新已延迟'));
  await page.locator('[data-phase="1"]').click();assert.equal(await page.locator('#status').innerText(),'');
  current.games=0;current.players=0;current.first=null;current.settled=null;current.asOf=Date.now();
  for(const hero of current.heroes)for(const b of ['mid','high'])hero[b]={score:3000,appearances:0,players:0};
  await page.clock.fastForward(61000);await page.locator('[data-phase="'+feed.periods.length+'"]').click();
  await page.waitForFunction(()=>document.getElementById('games').textContent==='0');
  assert.match(await page.locator('#notes').textContent(),/等待已结算/);
  assert.match(await page.locator('#rows').innerText(),/待样本/);
  assert(!/1970|NaN/.test(await page.locator('#board').innerText()));
  assert.deepEqual(errors,[]);
  console.log(JSON.stringify({personalWithoutDynamicApi:true,automaticRefresh:true,keepsFilters:true,preservesOnFailure:true,retry:true,staleWarning:true,emptyNewPeriod:true,calls}));
 }finally{await browser.close()}
})().catch(error=>{console.error(error);process.exit(1)});
