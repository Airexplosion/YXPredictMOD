"""API-only Elo service. Reload atomically published data; no collector/game calls."""
import argparse
import json
import pathlib
import re
import threading
import time
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
from urllib.parse import urlparse,parse_qs


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


def handler(snapshot):
    class Handler(BaseHTTPRequestHandler):
        def log_message(self,*args):pass
        def do_GET(self):
            try:
                url=urlparse(self.path);args=parse_qs(url.query);data=snapshot.get();status=200
                if url.path in ('/','/healthz','/api/v1/status'):
                    result={'ok':True,'mode':'api-only',**data['meta'],'stale':int(time.time()*1000)-data['meta']['sourceAt']>3600000}
                elif url.path=='/api/v1/ratings':
                    uids=args.get('uid',[])
                    if not 1<=len(uids)<=8 or any(not re.fullmatch('[0-9a-f]{24}',u) for u in uids):raise ValueError('Provide 1 to 8 raw 24-character UIDs')
                    result={u:data['players'][u] for u in dict.fromkeys(uids) if u in data['players']}
                elif re.fullmatch('/data/elo/p/[0-9a-f]{2}.json',url.path):
                    suffix=url.path[-7:-5];result={u:p for u,p in data['players'].items() if u.endswith(suffix)}
                else:status=404;result={'error':'API route not found'}
            except ValueError as e:status=400;result={'error':str(e)}
            except (OSError,KeyError):status=503;result={'error':'Rating snapshot unavailable'}
            body=json.dumps(result,ensure_ascii=False,separators=(',',':')).encode()
            self.send_response(status);self.send_header('Content-Type','application/json; charset=utf-8')
            self.send_header('Cache-Control','public, max-age=30' if status==200 else 'no-store')
            self.send_header('X-Content-Type-Options','nosniff');self.send_header('Content-Length',str(len(body)));self.end_headers();self.wfile.write(body)
    return Handler


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--snapshot',required=True);p.add_argument('--port',type=int,default=8124);a=p.parse_args()
    server=ThreadingHTTPServer(('127.0.0.1',a.port),handler(Snapshot(a.snapshot)));server.daemon_threads=True;server.serve_forever()
