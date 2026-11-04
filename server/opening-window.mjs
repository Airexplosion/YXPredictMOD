export const OPENING_ALL_IN_MS=120000;
/** The clock starts only on a confirmed opening forward, never on room creation. */
export function openingAllIn(r,now){
 const w=r.openingAllIn;if(w?.version!==1)return null;
 const started=Number.isSafeInteger(w.startedAt)&&Number.isSafeInteger(w.endsAt)&&w.endsAt===w.startedAt+OPENING_ALL_IN_MS;
 return {state:!started?'pending':now<w.endsAt?'open':'ended',startedAt:started?w.startedAt:null,endsAt:started?w.endsAt:null,
  remainingSeconds:started?Math.max(0,Math.ceil((w.endsAt-now)/1000)):null};
}
