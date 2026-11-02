"""Elo API and anonymous leaderboard. No collector or game calls."""
import argparse
import json
import pathlib
import re
import threading
import time
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
from urllib.parse import urlparse,parse_qs
from anonymous import AnonymousBoard


class Snapshot:
    def __init__(self,path):self.path=pathlib.Path(path);self.lock=threading.Lock();self.stamp=None;self.data=None
    def get(self):
        with self.lock:
            stamp=self.path.stat().st_mtime_ns
            if stamp!=self.stamp:
                data=json.loads(self.path.read_text(encoding='utf-8'))
                if not isinstance(data.get('players'),dict) or not data.get('meta'):raise ValueError('Invalid snapshot')
                self.data=data;self.stamp=stamp
            return self.data


def handler(snapshot,legacy=None):
    board=AnonymousBoard(snapshot,legacy)
    assets={'/':'index.html','/index.html':'index.html','/elo.js':'elo.js','/elo.css':'elo.css'}
    web=pathlib.Path(__file__).parent/'web'
    class Handler(BaseHTTPRequestHandler):
        def log_message(self,*args):pass
        def do_GET(self):
            try:
                url=urlparse(self.path);args=parse_qs(url.query);status=200
                if url.path in assets:
                    name=assets[url.path];body=(web/name).read_bytes()
                    kind='text/html' if name.endswith('.html') else 'text/css' if name.endswith('.css') else 'text/javascript'
                    self.send_response(200);self.send_header('Content-Type',kind+'; charset=utf-8')
                    self.send_header('Cache-Control','no-cache');self.send_header('X-Content-Type-Options','nosniff')
                    self.send_header('Referrer-Policy','no-referrer')
                    self.send_header('Content-Security-Policy',"default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; base-uri 'none'; frame-ancestors 'none'")
                    self.send_header('Content-Length',str(len(body)));self.end_headers();self.wfile.write(body);return
                data=snapshot.get()
                if url.path in ('/healthz','/api/v1/status'):
                    result={'ok':True,'mode':'anonymous-web-and-api','webAnonymous':True,**data['meta'],'stale':int(time.time()*1000)-data['meta']['sourceAt']>3600000}
                    if legacy is not None:result.update(legacy.status())
                elif url.path=='/api/v1/leaderboard':
                    if any(k not in ('offset','limit') or len(v)!=1 or not re.fullmatch('[0-9]{1,7}',v[0]) for k,v in args.items()):raise ValueError('Invalid page')
                    offset=int(args.get('offset',['0'])[0]);limit=int(args.get('limit',['100'])[0])
                    if not 0<=offset<=1000000 or not 1<=limit<=100:raise ValueError('Invalid page')
                    result=board.page(offset,limit)
                elif url.path=='/api/v1/ratings':
                    uids=args.get('uid',[])
                    if not 1<=len(uids)<=8 or any(not re.fullmatch('[0-9a-f]{24}',u) for u in uids):raise ValueError('Provide 1 to 8 raw 24-character UIDs')
                    result={}
                    old=None if legacy is None else legacy.data()
                    for uid in dict.fromkeys(uids):
                        row=data['players'].get(uid)
                        if legacy is not None:row=legacy.resolve(uid,row,old)
                        if row is not None:result[uid]=row
                elif re.fullmatch('/data/elo/p/[0-9a-f]{2}.json',url.path):
                    suffix=url.path[-7:-5];result={u:p for u,p in data['players'].items() if u.endswith(suffix)}
                    if legacy is not None:
                        old=legacy.data();result={u:legacy.resolve(u,p,old) for u,p in result.items()}
                else:status=404;result={'error':'API route not found'}
            except ValueError as e:status=400;result={'error':str(e)}
            except (OSError,KeyError):status=503;result={'error':'Rating snapshot unavailable'}
            body=json.dumps(result,ensure_ascii=False,separators=(',',':')).encode()
            self.send_response(status);self.send_header('Content-Type','application/json; charset=utf-8')
            self.send_header('Cache-Control','public, max-age=30' if status==200 else 'no-store')
            self.send_header('Referrer-Policy','no-referrer')
            self.send_header('X-Content-Type-Options','nosniff');self.send_header('Content-Length',str(len(body)));self.end_headers();self.wfile.write(body)
    return Handler


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--snapshot',required=True);p.add_argument('--port',type=int,default=8124)
    p.add_argument('--legacy-snapshot');p.add_argument('--uid-key-file');a=p.parse_args()
    legacy=None
    if bool(a.legacy_snapshot)!=bool(a.uid_key_file):p.error('Legacy snapshot and key file must be configured together')
    if a.legacy_snapshot:
        from legacy import Lookup,load_key
        legacy=Lookup(Snapshot(a.legacy_snapshot),load_key(a.uid_key_file))
    server=ThreadingHTTPServer(('127.0.0.1',a.port),handler(Snapshot(a.snapshot),legacy));server.daemon_threads=True;server.serve_forever()
