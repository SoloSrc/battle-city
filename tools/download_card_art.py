#!/usr/bin/env python3
"""Optional local Yugipedia artwork downloader. Python 3.10+, standard library only."""
import argparse
import hashlib
import json
import re
import struct
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import urllib.robotparser
from datetime import datetime, timezone
from email.utils import parsedate_to_datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'local-card-art'
API = 'https://yugipedia.com/api.php'
AGENT = 'SOLOSRC-CardArtDownloader/1.0 (+https://github.com/SoloSrc/battle-city)'
HOSTS = {'yugipedia.com', 'ms.yugipedia.com'}
MAX_BYTES = 20 * 1024 * 1024

class DownloadError(Exception):
    pass

class HttpFailure(DownloadError):
    def __init__(self, status, url):
        self.status = status
        super().__init__(f'HTTP {status}: {url}')

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise DownloadError('Redirect refused; review source URL: ' + newurl)

class Client:
    def __init__(self, interval=5):
        self.interval = max(5, interval)
        self.last = 0
        self.robots = {}
        self.opener = urllib.request.build_opener(NoRedirect())

    def raw(self, url):
        parts = urllib.parse.urlsplit(url)
        if parts.scheme != 'https' or parts.netloc not in HOSTS:
            raise DownloadError('Unapproved source host: ' + url)
        for attempt in range(3):
            time.sleep(max(0, self.interval - (time.monotonic() - self.last)))
            self.last = time.monotonic()
            try:
                req = urllib.request.Request(url, headers={'User-Agent': AGENT})
                with self.opener.open(req, timeout=30) as response:
                    body = response.read(MAX_BYTES + 1)
                    if len(body) > MAX_BYTES:
                        raise DownloadError('Response exceeds 20 MiB limit')
                    return body
            except urllib.error.HTTPError as exc:
                if exc.code not in (429, 500, 502, 503, 504) or attempt == 2:
                    raise HttpFailure(exc.code, url) from exc
                delay = 30 * 2 ** attempt
                retry = exc.headers.get('Retry-After')
                if retry:
                    try:
                        delay = max(delay, float(retry))
                    except ValueError:
                        delay = max(delay, (parsedate_to_datetime(retry) - datetime.now(timezone.utc)).total_seconds())
                if delay > 300:
                    raise DownloadError(f'Server requests {delay:.0f}s pause; stop and retry later')
                print(f'Throttled/unavailable; waiting {delay:.0f}s', flush=True)
                time.sleep(delay)
            except (urllib.error.URLError, TimeoutError) as exc:
                raise DownloadError(f'Network failure: {exc}') from exc

    def get(self, url):
        parts = urllib.parse.urlsplit(url)
        origin = f'{parts.scheme}://{parts.netloc}'
        if origin not in self.robots:
            parser = urllib.robotparser.RobotFileParser()
            try:
                rules = self.raw(origin + '/robots.txt').decode('utf-8').splitlines()
            except HttpFailure as exc:
                if exc.status not in (404, 410):
                    raise
                rules = []  # No robots file; still enforce pacing and access errors.
            parser.parse(rules)
            self.robots[origin] = parser
            self.interval = max(self.interval, parser.crawl_delay(AGENT) or 0)
            rate = parser.request_rate(AGENT)
            if rate:
                self.interval = max(self.interval, rate.seconds / rate.requests)
        if not self.robots[origin].can_fetch(AGENT, url):
            raise DownloadError('robots.txt disallows: ' + url)
        return self.raw(url)

    def api(self, **params):
        url = API + '?' + urllib.parse.urlencode(dict(format='json', formatversion=2, maxlag=5, **params))
        try:
            data = json.loads(self.get(url))
        except (ValueError, UnicodeError) as exc:
            raise DownloadError('API returned non-JSON (possibly an access challenge)') from exc
        if 'error' in data:
            raise DownloadError('API error: ' + json.dumps(data['error']))
        return data

def png_info(data):
    if len(data) < 33 or data[:8] != b'\x89PNG\r\n\x1a\n' or data[12:16] != b'IHDR' or b'IEND' not in data[-12:]:
        raise DownloadError('Expected a complete PNG artwork file')
    width, height = struct.unpack('>II', data[16:24])
    if not 64 <= width <= 8192 or not 64 <= height <= 8192:
        raise DownloadError('Unexpected image dimensions')
    return width, height

def choose_image(names):
    candidates = [n for n in names if re.search(r'-VG-artwork\.png$', n, re.I)]
    if not candidates:
        raise DownloadError('No game artwork PNG found; generated art remains active')
    return sorted(candidates, key=lambda n: ('-MADU-' not in n, '-EN-' not in n, n))[0]

def discover(client, name):
    # Some cards have artwork files but no Card Artworks gallery page.
    # Ask MediaWiki for the preferred file URL; never guess CDN hash paths.
    filename = re.sub(r'[^A-Za-z0-9]', '', name) + '-MADU-EN-VG-artwork.png'
    result = client.api(action='query', titles='File:' + filename, prop='imageinfo', iiprop='url')
    for page in result['query']['pages']:
        if page.get('imageinfo'):
            return page['imageinfo'][0]['url'], filename
    title = 'Card Artworks:' + name
    result = client.api(action='parse', page=title, prop='images', redirects=1)
    filename = choose_image(result['parse']['images'])
    result = client.api(action='query', titles='File:' + filename, prop='imageinfo', iiprop='url')
    for page in result['query']['pages']:
        if page.get('imageinfo'):
            return page['imageinfo'][0]['url'], filename
    raise DownloadError('Image URL not found: ' + filename)

def atomic_write(path, data):
    temporary = path.with_suffix(path.suffix + '.part')
    temporary.write_bytes(data)
    temporary.replace(path)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group()
    group.add_argument('--card', help='Database card id')
    group.add_argument('--first', action='store_true', help='First card by database filename (smoke test)')
    parser.add_argument('--list', action='store_true', help='List selection without network access')
    parser.add_argument('--interval', type=float, default=5, help='Minimum seconds between requests (floor: 5)')
    args = parser.parse_args()
    cards = [json.loads(p.read_text()) for p in sorted((ROOT / 'data/cards').glob('*.json'))]
    if args.card:
        cards = [c for c in cards if c['id'] == args.card]
        if not cards:
            parser.error('Unknown database card id')
    elif args.first:
        cards = cards[:1]
    if args.list:
        for card in cards:
            print(card['id'], '-', card['name'])
        return 0
    OUTPUT.mkdir(exist_ok=True)
    (OUTPUT / '.gdignore').touch()  # Keep third-party files out of Godot imports/exports.
    lock = OUTPUT / '.download.lock'
    try:
        lock.touch(exist_ok=False)
    except FileExistsError:
        parser.error('Another download may be running; remove local-card-art/.download.lock only if it is stale')
    try:
        client = Client(args.interval)
        for card in cards:
            card_id = card['id']
            if not re.fullmatch(r'[a-z0-9_]+', card_id):
                raise DownloadError('Unsafe card id')
            target = OUTPUT / (card_id + '.png')
            metadata = OUTPUT / (card_id + '.json')
            if target.exists() and metadata.exists():
                data = target.read_bytes()
                png_info(data)
                if hashlib.sha256(data).hexdigest() == json.loads(metadata.read_text())['sha256']:
                    print('Cached:', card_id, flush=True)
                    continue
            print('Discovering:', card['name'], flush=True)
            url, filename = discover(client, card['name'])
            data = client.get(url)
            width, height = png_info(data)
            record = dict(card_id=card_id, filename=filename, url=url, width=width, height=height,
                          sha256=hashlib.sha256(data).hexdigest(), downloaded_at=datetime.now(timezone.utc).isoformat())
            atomic_write(target, data)
            atomic_write(metadata, (json.dumps(record, indent=2) + '\n').encode())
            print(f'Downloaded: {card_id} ({width}x{height})', flush=True)
    finally:
        lock.unlink(missing_ok=True)
    return 0

if __name__ == '__main__':
    try:
        sys.exit(main())
    except (DownloadError, OSError, ValueError, KeyError) as exc:
        print('Stopped:', exc, file=sys.stderr)
        sys.exit(1)
