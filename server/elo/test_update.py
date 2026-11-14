import contextlib
import io
import json
import pathlib
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import update


class UpdateTests(unittest.TestCase):
    def run_update(self, directory, timeout, runner):
        root = pathlib.Path(directory)
        (root/'command.json').write_text(json.dumps(['export-facts']))
        (root/'seed.json').write_text(json.dumps({'players': {}}))
        args = ['update', '--seed', str(root/'seed.json'), '--source-command',
                str(root/'command.json'), '--output', str(root/'snapshot.json'),
                '--source-timeout', str(timeout)]
        with patch('sys.argv', args), patch('update.subprocess.run', runner), \
                patch('update.build', return_value={'meta': {'complete': True}}), \
                contextlib.redirect_stdout(io.StringIO()):
            update.main()

    def test_custom_export_timeout_can_publish(self):
        with tempfile.TemporaryDirectory() as directory:
            calls = []
            def export(command, **kwargs):
                calls.append((command, kwargs['timeout']))
                return subprocess.CompletedProcess(command, 0, b'{"games":[]}')
            self.run_update(directory, 900, export)
            self.assertEqual(calls, [(['export-facts'], 900)])
            self.assertTrue(json.loads((pathlib.Path(directory)/'snapshot.json').read_text())['meta']['complete'])

    def test_export_timeout_preserves_published_snapshot(self):
        with tempfile.TemporaryDirectory() as directory:
            output = pathlib.Path(directory)/'snapshot.json'
            original = b'{"meta":{"generation":"published"}}'
            output.write_bytes(original)
            def timeout(command, **kwargs):
                raise subprocess.TimeoutExpired(command, kwargs['timeout'])
            with self.assertRaises(subprocess.TimeoutExpired):
                self.run_update(directory, 900, timeout)
            self.assertEqual(output.read_bytes(), original)


if __name__ == '__main__':
    unittest.main()
