// Champion predictions pay 100%, 25%, 10% of the locked claim to the podium.
// For ascending claims the worst result puts the smallest claims on the podium.
// Every ascending vector is a nonnegative sum of suffix indicator vectors.
// Thus checking the cheapest n, n-1, n-2 reciprocal-price sums covers ALL
// allocations, not merely equal stakes or eight equal claims.
const PODIUM = [1, 0.25, 0.1];
export function coverageReturn(rates, multiplier = 1) {
  if (!Array.isArray(rates) || rates.length < 2 || rates.length > 8 ||
      !rates.every(x => Number.isSafeInteger(x) && x > 0) ||
      ![1, 3].includes(multiplier)) throw new Error('Invalid coverage prices');
  const costs = rates.map(x => 10000 / (x * multiplier)).sort((a, b) => a - b);
  let worst = 0;
  for (let skipped = 0; skipped < Math.min(3, costs.length); skipped++) {
    const cost = costs.slice(0, costs.length - skipped).reduce((a, b) => a + b, 0);
    const payout = PODIUM.slice(skipped, Math.min(3, costs.length)).reduce((a, b) => a + b, 0);
    worst = Math.max(worst, payout / cost);
  }
  return worst;
}

// A small margin absorbs integer-price/claim rounding. Never round back up to
// a 1.01 minimum after this step: that would reintroduce a guaranteed return.
export function capCoverage(rates, multiplier = 1) {
  const factor = Math.min(1, 0.98 / coverageReturn(rates, multiplier));
  return rates.map(x => Math.max(1, Math.floor(x * factor)));
}
