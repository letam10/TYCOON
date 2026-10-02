"""Write portable case results without machine paths or runner environment data."""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

parser = argparse.ArgumentParser()
parser.add_argument('output', type=Path)
parser.add_argument('results', nargs='+', type=Path)
args = parser.parse_args()
runs = []
for path in args.results:
    root = ET.parse(path).getroot()
    runs.append({'file': path.as_posix(), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                 'result': root.get('result'), 'total': int(root.get('total')),
                 'passed': int(root.get('passed')), 'failed': int(root.get('failed')),
                 'cases': [{'name': case.get('fullname'), 'result': case.get('result')}
                           for case in root.iter('test-case')]})
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps({'unity': '6000.6.3f1', 'renderer': 'Null Device',
                                  'scope': 'component logic and save tests only', 'runs': runs}, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'passed': sum(run['passed'] for run in runs), 'failed': sum(run['failed'] for run in runs)}))
