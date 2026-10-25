export function wagerFactorBps(selected, total) {
  if (total <= 0) return 10000;
  return Math.max(7500, Math.min(12500, Math.round(10000 * (1 + 0.25 * (1 - 8 * selected / total)))));
}
export function quoteRates(base, totals) {
  const factors=Array(64).fill(10000),rates=Array(64).fill(0);
  for(let i=0;i<8;i++){
    const total=totals[i].reduce((a,b)=>a+b,0);
    for(let rank=0;rank<8;rank++){const at=i*8+rank;factors[at]=wagerFactorBps(totals[i][rank],total);rates[at]=base[at]<=0?0:Math.max(10100,Math.min(500000,Math.floor(base[at]*factors[at]/10000)));}
  }
  return {factors,rates};
}

export function sale(claim, rate, half) {
  const removed=half?Math.floor(claim/2):claim;
  return rate<=0?0:Number(BigInt(removed)*10000n*97n/(BigInt(rate)*100n));
}
export function returnFor(claim, predicted, finalRank){return predicted===finalRank?claim:Math.abs(predicted-finalRank)===1?Math.floor(claim/4):0;}
