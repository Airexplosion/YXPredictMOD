"""Independent public-record recovery; no game websocket, login or wallet mutation."""
import argparse,json,logging,pathlib
from main import Backend
from public_archive import ArchivePending,records_for,recover_battles

def run(config):
    backend=Backend(config['backend'],config['key']);status=backend.call('status')
    names=json.loads(pathlib.Path(config['cardNames']).read_text(encoding='utf8'))
    for target in status.get('reports',[]):
        try:
            battles=recover_battles(target,records_for(config,target),names)
            result=backend.call('report',code=target['code'],gameId=target['gameId'],battles=battles)
            logging.info('Final public report recovered; round=%d tables=%d ready=%s',target['round'],len(battles),result.get('reportReady'))
        except Exception as error:logging.warning('Final public archive pending (%s): %s',type(error).__name__,str(error)if isinstance(error,ArchivePending)else'waiting for verified records')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--config',required=True);args=parser.parse_args()
    logging.basicConfig(level=logging.INFO,format='%(asctime)s %(levelname)s %(message)s')
    run(json.loads(pathlib.Path(args.config).read_text(encoding='utf8')))
