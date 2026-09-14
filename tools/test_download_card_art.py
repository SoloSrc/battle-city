"""Offline regression tests; no calls to Yugipedia."""
import hashlib
import json
import struct
import tempfile
import unittest
from pathlib import Path
from unittest.mock import Mock, patch

import download_card_art as art


class DownloaderTests(unittest.TestCase):
    def test_prefers_master_duel_over_other_games(self):
        self.assertEqual(art.choose_image(['X-TF04-EN-VG-artwork.png', 'X-MADU-JP-VG-artwork.png']),
                         'X-MADU-JP-VG-artwork.png')

    def test_rejects_full_card_scan(self):
        with self.assertRaises(art.DownloadError):
            art.choose_image(['X-LOD-EN-UR.png'])

    def test_resolves_preferred_file_without_gallery(self):
        client = Mock()
        client.api.return_value = {'query': {'pages': [{'imageinfo': [{'url': 'https://ms.yugipedia.com/art.png'}]}]}}
        self.assertEqual(art.discover(client, 'Airknight Parshath')[1],
                         'AirknightParshath-MADU-EN-VG-artwork.png')
        self.assertEqual(client.api.call_count, 1)

    def test_gallery_fallback(self):
        client = Mock()
        client.api.side_effect = [
            {'query': {'pages': [{'missing': True}]}},
            {'parse': {'images': ['X-TF04-EN-VG-artwork.png']}},
            {'query': {'pages': [{'imageinfo': [{'url': 'https://ms.yugipedia.com/x.png'}]}]}}]
        self.assertEqual(art.discover(client, 'X')[1], 'X-TF04-EN-VG-artwork.png')

    def test_rejects_non_image_response(self):
        with self.assertRaises(art.DownloadError):
            art.png_info(b'<html>Access denied</html>')

    def test_robots_denial_prevents_art_request(self):
        client = art.Client()
        client.raw = Mock(return_value=b'User-agent: *\nDisallow: /\n')
        with self.assertRaises(art.DownloadError):
            client.get('https://yugipedia.com/api.php')
        self.assertEqual(client.raw.call_count, 1)

    def test_missing_robots_allows_request_but_403_stops(self):
        client = art.Client()
        client.raw = Mock(side_effect=[art.HttpFailure(404, 'robots'), b'image'])
        self.assertEqual(client.get('https://ms.yugipedia.com/a.png'), b'image')
        client = art.Client()
        client.raw = Mock(side_effect=art.HttpFailure(403, 'robots'))
        with self.assertRaises(art.HttpFailure):
            client.get('https://ms.yugipedia.com/a.png')
        self.assertEqual(client.raw.call_count, 1)

    def test_external_host_rejected(self):
        with self.assertRaises(art.DownloadError):
            art.Client().raw('https://example.com/art.png')

    def test_cached_download_makes_no_network_requests(self):
        # Reuse a project-owned valid placeholder as the cache fixture.
        data = (art.ROOT / 'assets/cards/art/airknight_parshath.png').read_bytes()
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            (output / 'airknight_parshath.png').write_bytes(data)
            (output / 'airknight_parshath.json').write_text(json.dumps({'sha256': hashlib.sha256(data).hexdigest()}))
            with patch.object(art, 'OUTPUT', output), patch('sys.argv', ['download_card_art.py', '--first']), patch.object(art.Client, 'get') as get:
                self.assertEqual(art.main(), 0)
                get.assert_not_called()
            self.assertFalse((output / '.download.lock').exists())

if __name__ == '__main__':
    unittest.main()
