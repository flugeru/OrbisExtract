"""Check the static Pages site and optionally stage only its public web files."""
from argparse import ArgumentParser
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urlsplit
import shutil
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SITE = ROOT / 'docs'
FILES = ['index.html', 'en.html', 'privacy-policy.html', 'terms.html', 'support.html',
         '404.html', 'robots.txt', 'sitemap.xml', '.nojekyll', 'assets/site.css', 'assets/icon.png']
BASE = '/OrbisExtract/'

class Document(HTMLParser):
    def __init__(self, text):
        super().__init__(convert_charrefs=True)
        self.ids = set()
        self.links = []
        self.h1 = 0
        self.lang = False
        self.title = False
        self.viewport = False
        self.feed(text)

    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        if 'id' in a:
            assert a['id'] not in self.ids, f'Duplicate id: {a["id"]}'
            self.ids.add(a['id'])
        if tag == 'html': self.lang = bool(a.get('lang'))
        if tag == 'title': self.title = True
        if tag == 'meta' and a.get('name') == 'viewport': self.viewport = True
        if tag == 'h1': self.h1 += 1
        if tag == 'img': assert 'alt' in a, 'Missing image alt'
        for field in ('href', 'src'):
            if field in a: self.links.append(a[field])

parser = ArgumentParser()
parser.add_argument('--output', type=Path)
args = parser.parse_args()
for f in FILES: assert (SITE / f).is_file(), f'Missing file: {f}'
docs = {f: Document((SITE / f).read_text()) for f in FILES if f.endswith('.html')}
for name, doc in docs.items():
    assert doc.lang and doc.title and doc.viewport and doc.h1 == 1, name
    for link in doc.links:
        u = urlsplit(link)
        if u.scheme:
            assert u.scheme in ('https', 'mailto'), (name, link)
            continue
        assert not u.netloc, (name, link)
        target = unquote(u.path)
        if target.startswith(BASE): target = target[len(BASE):]
        elif target.startswith('/'): raise AssertionError((name, link))
        if not target: target = name
        if target.endswith('/'): target += 'index.html'
        assert target in FILES, (name, link)
        if u.fragment:
            assert target in docs and unquote(u.fragment) in docs[target].ids, (name, link)
for name in ('privacy-policy.html', 'terms.html', 'support.html'):
    text = (SITE / name).read_text()
    assert 'mailto:fluugell@gmail.com' in text and 'id="english"' in text, name
ET.parse(SITE / 'sitemap.xml')
if args.output:
    args.output.mkdir(parents=True, exist_ok=True)
    for name in FILES:
        target = args.output / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(SITE / name, target)
print(f'Checked {len(docs)} HTML pages, local links/anchors, assets and sitemap; {len(FILES)} public files.')
