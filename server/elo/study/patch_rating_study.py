"""Patch-isolated, sequential shadow rating study. Public output is anonymous aggregates."""
import argparse
import collections
import datetime as dt
import gzip
import hashlib
import json
import math
import os
from pathlib import Path
import tempfile
import time

from patch_study_inputs import START, VERSION, load, notice_boundary
import patch_study_inputs as inputs

DAY=86_400_000
BJT=dt.timezone(dt.timedelta(hours=8))
RECIPES=("player","context","joint","mastery","within","recent3d")
MODEL="cold-patch-1"
STEPS={"player":(.23,12.),"ctx":(.06,30.),"hero":(.10,24.),"bandHero":(.065,32.),"mastery":(.055,10.)}

def calendar(t):
    return dt.datetime.fromtimestamp(t/1000,BJT).date().isoformat()

def sigmoid(x):
    return 1/(1+math.exp(-max(-35.,min(35.,x))))

class Rating:
    def __init__(self,recipe):
        self.recipe=recipe
        self.v={}
        self.n=collections.Counter()
        self.mix=collections.defaultdict(collections.Counter)

    def value(self,k,t):
        v,last=self.v.get(k,(0.,t))
        if self.recipe=="recent3d" and k[0] in ("hero","bandHero"):
            v*=2**(-max(0,t-last)/(3*DAY))
        return v

    def hero(self,h,b,t):
        return self.value(("hero",h),t)+self.value(("bandHero",b,h),t)

    def start(self,players,t):
        skills=[];features=[]
        for p in players:
            u,h,b=p["uid"],p["hero"],p["band"]
            keys=[("player",u)]
            if self.recipe!="player":keys.append(("ctx",b,p["mode"],p["random"]))
            if self.recipe not in ("player","context"):
                keys.extend([("hero",h),("bandHero",b,h)])
            if self.recipe=="mastery":keys.append(("mastery",u,h))
            skills.append(sum(self.value(k,t) for k in keys))
            features.append(keys)
        return skills,features

    def finish(self,players,prediction,t):
        skills,features=prediction;n=len(players)
        gradients=collections.Counter();exposure=collections.Counter()
        for i,(p,keys) in enumerate(zip(players,features)):
            surprise=sum((p["rank"]<q["rank"])-sigmoid(skills[i]-skills[j]) for j,q in enumerate(players) if i!=j)/(n-1)
            for k in keys:gradients[k]+=surprise;exposure[k]+=1
            if self.recipe=="within":
                # Attribute half of the shared-role signal to a contrast against the
                # player's prior choices in the same environment. Single-role users
                # consequently supply less information about differences between roles.
                group=(p["uid"],p["band"],p["mode"])
                past=self.mix[group] or {p["hero"]:1}
                total=sum(past.values())
                for h,uses in past.items():
                    weight=.5*uses/total
                    for k in (("hero",h),("bandHero",p["band"],h)):
                        gradients[k]-=weight*surprise
                        exposure.setdefault(k,1)
        for k,grad in gradients.items():
            step,prior=STEPS[k[0]];current=self.value(k,t)
            rate=step/math.sqrt(1+self.n[k]/35)
            self.v[k]=(current+rate*(grad/exposure[k]-current/prior),t)
            self.n[k]+=exposure[k]
        if self.recipe=="within":
            for p in players:self.mix[p["uid"],p["band"],p["mode"]][p["hero"]]+=1

def fit_scale(hist):
    if sum(hist.values())<500:return 1.
    data=[(k/40,count*k/40) for k,count in hist.items() if k]
    lo,hi=0.,8.
    for _ in range(24):
        mid=(lo+hi)/2
        if sum(wx*sigmoid(-mid*x) for x,wx in data)>0:lo=mid
        else:hi=mid
    return (lo+hi)/2

def calibration(history,day,include_today=False):
    dates=sorted(d for d in history if d<day or include_today and d==day)[-2:]
    return {k:fit_scale(sum((history[d][k] for d in dates),collections.Counter())) for k in RECIPES}

class Audit:
    def __init__(self):
        self.totals=collections.defaultdict(lambda:[0.,0.,0.,0,0])
        self.paired=collections.defaultdict(lambda:[0.,0.,0.,0.,0.,0])
        self.people=collections.defaultdict(lambda:collections.defaultdict(lambda:[0.,0]))
        self.fit=collections.defaultdict(lambda:[0.,0.,0])
        self.previous={}

    def score(self,g,skills,scales,role):
        r=g["rows"];n=len(r)
        cells=collections.defaultdict(lambda:[0.,0.,0.,0])
        neutral=[0.]*n
        for i,p in enumerate(r):
            for j in range(i+1,n):
                q=r[j]
                groups=["all"]
                if p["band"]==q["band"] and p["band"] in ("mid","high"):groups.append(p["band"])
                if p["mode"]==q["mode"]:groups.append(p["mode"])
                if p["random"] and q["random"]:groups.append("bothRandom")
                for k,v in skills.items():
                    z=(v[i]-v[j])*scales[k];prob=sigmoid(z)
                    if k=="context":neutral[i]+=prob;neutral[j]+=1-prob
                    loss=math.log1p(math.exp(-max(-35,min(35,z))))
                    correct=(z>0)+.5*(z==0);brier=(1-prob)**2
                    for group in groups:
                        s=cells[group,k];s[0]+=loss;s[1]+=correct;s[2]+=brier;s[3]+=1
        for (group,k),v in cells.items():
            cell=self.totals[group,k]
            for j,x in enumerate(v):cell[j]+=x
            cell[4]+=1
            baseline=cells[group,"context"]
            delta=v[0]-baseline[0];size=v[3]
            a=self.paired[group,k]
            for j,x in enumerate((delta,size,delta**2,delta*size,size**2,1)):a[j]+=x
        for i,p in enumerate(r):
            if p["band"] not in ("mid","high"):continue
            key=(p["uid"],p["band"],p["mode"])
            actual=(n-1-i-neutral[i])/(n-1)
            prev=self.previous.get(key)
            if prev and prev[0]<=g["begin"] and prev[1]!=p["hero"] and actual!=prev[2]:
                for k in ("joint","mastery","within","recent3d"):
                    new=role[k,p["band"],p["hero"]]*scales[k];old=role[k,p["band"],prev[1]]*scales[k]
                    diff=sigmoid(new)-sigmoid(old)
                    result=((actual-prev[2])*diff>0)+.5*(diff==0)
                    for group in ("all",p["band"],p["mode"]):
                        a=self.people[group,k][p["uid"]];a[0]+=result;a[1]+=1
                    if diff:
                        bucket="under2pp" if abs(diff)<.02 else "2to5pp" if abs(diff)<.05 else "atLeast5pp"
                        values=self.fit[p["band"],k,bucket]
                        values[0]+=abs(diff);values[1]+=math.copysign(1,diff)*(actual-prev[2]);values[2]+=1
            self.previous[key]=(g["end"],p["hero"],actual)

    def public(self):
        scores={};contrast={}
        for (group,k),(loss,correct,brier,n,rooms) in self.totals.items():
            d,num,dd,dn,nn,numrooms=self.paired[group,k]
            change=d/num
            se=math.sqrt(max(0,dd-2*change*dn+change**2*nn)*numrooms/(numrooms-1))/num if numrooms>1 else None
            scores.setdefault(group,{})[k]=dict(rooms=rooms,pairs=n,accuracy=correct/n,logLoss=loss/n,
                brier=brier/n,deltaVsContext=change,pairedRoom95=[change-1.96*se,change+1.96*se] if se is not None else None)
        for (group,k),accounts in self.people.items():
            n=sum(y for x,y in accounts.values());value=sum(x for x,y in accounts.values())/n;count=len(accounts)
            se=math.sqrt(count/(count-1)*sum((x-value*y)**2 for x,y in accounts.values()))/n if count>1 else None
            contrast.setdefault(group,{})[k]=dict(pairs=n,players=count,accuracy=value,
                player95=[value-1.96*se,value+1.96*se] if se is not None else None)
        fit={}
        for (band,k,bucket),(p,o,n) in self.fit.items():
            fit.setdefault(band,{}).setdefault(k,{})[bucket]=dict(pairs=n,predicted=p/n,observed=o/n)
        return dict(prediction=scores,withinPlayer=contrast,strengthGap=fit)

def roles(models,heroes,t):
    return {(k,b,h):s.hero(h,b,t) for k,s in models.items() for b in ("mid","high") for h in heroes}

def study(games):
    models={k:Rating(k) for k in RECIPES};pending={};history=collections.defaultdict(lambda:collections.defaultdict(collections.Counter))
    books={};temps={};day_roles={};heroes={p["hero"] for g in games for p in g["rows"]}
    times=sorted((t,kind,g["code"],i) for i,g in enumerate(games) for t,kind in ((g["begin"],1),(g["end"],0)))
    for t,kind,_,i in times:
        g=games[i];players=g["rows"];day=calendar(t)
        if kind:
            if day not in books:
                books[day]=Audit();temps[day]=calibration(history,day);day_roles[day]=roles(models,heroes,t)
            state=pending[i]={k:s.start(players,t) for k,s in models.items()}
            books[day].score(g,{k:v[0] for k,v in state.items()},temps[day],day_roles[day])
        else:
            saved=pending.pop(i)
            for k,s in models.items():
                skills=saved[k][0]
                for j,a in enumerate(skills):
                    for b in skills[j+1:]:history[day][k][round(40*(a-b))]+=1
                s.finish(players,saved[k],t)
    if pending:raise RuntimeError("Unsettled training input")
    return models,history,{d:b.public() for d,b in books.items()},temps

def atomic(path,payload):
    with tempfile.NamedTemporaryFile(dir=path.parent,delete=False) as f:
        tmp=Path(f.name);f.write(payload)
    try:tmp.replace(path)
    finally:tmp.unlink(missing_ok=True)

def generation():
    digest=hashlib.sha256()
    for p in (Path(__file__),Path(__file__).with_name("patch_study_inputs.py")):digest.update(p.read_bytes())
    digest.update(json.dumps([START,VERSION,inputs.ANCHOR_NEWS]).encode())
    return digest.hexdigest()

def nominate(days,origin):
    # Lock the choice along with the parameters. Only completed dates following
    # the first warm-up day can nominate a challenger for a future checkpoint.
    past=[d for d in sorted(days)[1:] if d<calendar(origin)][-3:]
    qualified=[]
    if len(past)>=2:
        for model in ("joint","mastery","within","recent3d"):
            deltas=[];direction=0.;pairs=0
            for date in past:
                audit=days[date];scores=audit["prediction"]
                mid=scores.get("mid",{}).get(model,{})
                high=scores.get("high",{}).get(model,{})
                switches=audit["withinPlayer"].get("all",{}).get(model,{})
                if mid.get("pairs",0)<1000 or high.get("pairs",0)<250 or switches.get("pairs",0)<100:break
                deltas.append((mid["deltaVsContext"],high["deltaVsContext"]))
                direction+=switches["accuracy"]*switches["pairs"];pairs+=switches["pairs"]
            if len(deltas)==len(past) and pairs and direction/pairs>.5:
                # Equal importance for both target bands; reject a persistent loss in either.
                avg=[sum(x[i] for x in deltas)/len(deltas) for i in (0,1)]
                if max(avg)<=0:qualified.append((sum(avg)/2,model))
    return dict(model=min(qualified)[1] if qualified else None,dates=past,
                status="locked-candidate" if qualified else "insufficient-evidence")

def hero_report(games,models,scales,at,catalog=None):
    appearances=collections.Counter();people=collections.defaultdict(set);mix=collections.defaultdict(set)
    for g in games:
        for p in g["rows"]:
            b,h,u,m=p["band"],p["hero"],p["uid"],p["mode"]
            appearances[b,h]+=1;people[b,h].add((u,m));mix[b,u,m].add(h)
    observed={h for _,h in appearances}
    heroes=sorted(observed if catalog is None else catalog);public={}
    if not heroes or not observed.issubset(heroes):raise ValueError("Unexpected character catalog")
    for b in ("mid","high"):
        public[b]={}
        for k in ("joint","within","recent3d","mastery"):
            raw={h:models[k].hero(h,b,at) for h in heroes};center=sum(raw.values())/len(heroes)
            public[b][k]=[dict(hero=h,score=round(3000+(v-center)*scales[k]*400/math.log(10),1),appearances=appearances[b,h],
                players=len({u for u,m in people[b,h]}),switchers=len({u for u,m in people[b,h] if len(mix[b,u,m])>1})) for h,v in raw.items()]
    return public

def freeze(directory,models,history,origin,nomination=None):
    day=calendar(origin);target=directory/(day+".forecast.gz")
    if target.exists():return
    item=dict(model=MODEL,version=VERSION,start=START,sourceHash=generation(),origin=origin,
              scale=calibration(history,day,True),nomination=nomination,
              values={k:[(list(key),v,last) for key,(v,last) in s.v.items()] for k,s in models.items()})
    content=gzip.compress(json.dumps(item,separators=(",",":"),allow_nan=False).encode(),compresslevel=2)
    with tempfile.NamedTemporaryFile(dir=directory,delete=False) as f:
        p=Path(f.name);f.write(content)
    try:os.link(p,target)  # immutable per calendar day, never overwrite an earlier forecast
    finally:p.unlink()

def forward(directory,games,asof):
    paths=sorted(directory.glob("*.forecast.gz"))[-3:];result=[]
    checkpoints=[json.loads(gzip.decompress(p.read_bytes())) for p in paths]
    for i,item in enumerate(checkpoints):
        begin=item["origin"];end=checkpoints[i+1]["origin"] if i+1<len(paths) else max(begin,asof)
        if (item["model"],item["version"],item["start"],item["sourceHash"])!=(MODEL,VERSION,START,generation()):
            result.append(dict(origin=begin,until=end,status="different-model-generation"));continue
        models={k:Rating(k) for k in RECIPES}
        for k,values in item["values"].items():models[k].v={tuple(key):(v,last) for key,v,last in values}
        later=sorted((g for g in games if begin<g["begin"]<end and g["end"]<=asof),key=lambda g:g["begin"])
        a=Audit();hs={p["hero"] for g in later for p in g["rows"]};initial=roles(models,hs,begin)
        for g in later:
            skills={k:s.start(g["rows"],g["begin"])[0] for k,s in models.items()}
            a.score(g,skills,item["scale"],initial)
        result.append(dict(origin=begin,until=end,games=len(later),nomination=item.get("nomination"),**a.public()))
    return result

def main():
    global START, VERSION
    os.umask(0o077)
    parser=argparse.ArgumentParser()
    parser.add_argument("directory",type=Path)
    args=parser.parse_args();folder=args.directory
    folder.mkdir(parents=True,exist_ok=True)
    if (folder/"period.json").exists():
        inputs.configure(folder);START,VERSION=inputs.START,inputs.VERSION
    catalog={int(h) for h in json.loads(Path("/root/yx-replay/character-elo-v1/characters.json").read_text())}
    limit=folder/"stop-at-ms"
    stop=int(limit.read_text().strip()) if limit.exists() else None
    if stop is not None and stop<=START:raise ValueError("Invalid next balance boundary")
    notice,unreviewed=notice_boundary(folder)
    end=min((v for v in (stop,notice) if v is not None),default=None)
    games,scope=load(folder,end)
    scope.update(officialNewsCheckedAt=time.time_ns()//1000000,unreviewedOfficialNotices=unreviewed)
    models,history,days,temps=study(games)
    # Timestamp only after all fitting has finished; future games cannot enter this state.
    origin=time.time_ns()//1000000
    nomination=nominate(days,origin)
    if scope["stopAt"] is None or origin<scope["stopAt"]:freeze(folder,models,history,origin,nomination)
    prospective=forward(folder,games,scope["asOf"])
    report=dict(model=MODEL,sourceHash=generation(),scope=scope,players=len({p["uid"] for g in games for p in g["rows"]}),
                days=days,scaleByDay=temps,lockedForward=prospective,
                heroScores=hero_report(games,models,calibration(history,calendar(origin),True),origin,catalog))
    raw=json.dumps(report,separators=(",",":"),allow_nan=False).encode()
    atomic(folder/(calendar(origin)+".report.json"),raw)
    atomic(folder/"latest.json",raw)
    print(json.dumps(dict(asOf=scope["asOf"],games=len(games),days=len(days),prospectiveGames=sum(x.get("games",0) for x in prospective),stopAt=scope["stopAt"])))

if __name__=="__main__":main()
