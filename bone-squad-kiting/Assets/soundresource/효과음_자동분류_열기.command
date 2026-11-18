#!/usr/bin/python3
import io
import json
import os
import plistlib
import re
import secrets
import shutil
import subprocess
import tempfile
import time
import webbrowser
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path
from urllib.parse import parse_qs, unquote, urlencode, urlsplit

ROOT = Path(__file__).resolve().parent
DOWNLOADS = Path.home() / 'Downloads'
PAGE = (ROOT / '00_효과음_제작실.html').read_text()
CUES = {c['id']: c for c in json.loads(PAGE.split('const cues=', 1)[1].split(';\n', 1)[0])}
TOKEN = secrets.token_urlsafe(32)

def places(c):
    folder = ROOT / c['path']
    return folder / '후보' / c['stem'], folder / (c['stem'] + '.mp3')

def audio_path(c, name):
    if not re.fullmatch(re.escape(c['stem']) + r'_후보_\d+\.mp3', name):
        raise ValueError('후보 파일명이 올바르지 않습니다.')
    return places(c)[0] / name

def save(c, stream):
    folder, _ = places(c)
    folder.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(dir=folder, delete=False) as out:
        tmp = Path(out.name)
        try:
            shutil.copyfileobj(stream, out)
            out.flush()
            if not tmp.stat().st_size:
                raise ValueError('빈 파일입니다.')
            for n in range(1, 10000):
                dest = folder / (c['stem'] + '_후보_%02d.mp3' % n)
                try:
                    os.link(tmp, dest)
                    return
                except FileExistsError:
                    continue
            raise ValueError('후보 파일 수가 너무 많습니다.')
        finally:
            tmp.unlink(missing_ok=True)

def source_matches(p):
    if 'elevenlabs' in p.name.lower() or 'eleven_labs' in p.name.lower():
        return True
    try:
        raw = subprocess.run(['/usr/bin/xattr','-px','com.apple.metadata:kMDItemWhereFroms',str(p)], capture_output=True, timeout=3)
        if raw.returncode:
            return False
        urls = plistlib.loads(bytes.fromhex(raw.stdout.decode('ascii')))
        return any(re.search(r'(?:^|[./])elevenlabs\.io(?:[/:]|$)', str(url), re.I) for url in urls)
    except (OSError, ValueError, subprocess.TimeoutExpired, plistlib.InvalidFileException):
        return False

class Inbox:
    def __init__(self):
        self.active = ''
        self.seen = {}
        self.pending = {}
        self.scan()

    def scan(self):
        try:
            paths = list(DOWNLOADS.iterdir())
        except OSError:
            return
        for p in paths:
            if p.suffix.lower() not in ('.mp3', '.crdownload', '.part', '.download'):
                continue
            try:
                st = p.stat()
                if not p.is_file():
                    continue
                stamp = (st.st_ino, st.st_size, st.st_mtime_ns)
                if self.seen.get(p) == stamp:
                    continue
                self.seen[p] = stamp
                final = p if p.suffix.lower()=='.mp3' else p.with_suffix('')
                if final.suffix.lower() != '.mp3':
                    continue
                if final not in self.pending:
                    self.pending[final] = [self.active, time.monotonic()]
                self.pending[final][1] = time.monotonic()
            except OSError:
                continue
        for p,(cue,changed) in list(self.pending.items()):
            if time.monotonic()-changed < 1.1 or not p.is_file():
                continue
            if cue and source_matches(p):
                try:
                    with p.open('rb') as src:
                        save(CUES[cue], src)
                except OSError:
                    continue
            del self.pending[p]

def state():
    files = {}
    for k,c in CUES.items():
        folder, final = places(c)
        files[k] = [{'name':p.name, 'url':'/audio?'+urlencode({'id':k,'name':p.name}), 'hasFinal':final.exists()}
                    for p in sorted(folder.glob('*_후보_*.mp3'))]
    return {'active':inbox.active, 'files':files}

class Handler(BaseHTTPRequestHandler):
    def log_message(self,*args):
        pass
    def reply(self, status, data, kind):
        self.send_response(status)
        self.send_header('Content-Type',kind)
        self.send_header('Content-Length',str(len(data)))
        self.send_header('Cache-Control','no-store')
        self.send_header('X-Content-Type-Options','nosniff')
        self.end_headers()
        self.wfile.write(data)
    def do_GET(self):
        if self.headers.get('Host') != server.host:
            return self.send_error(403)
        u = urlsplit(self.path)
        if u.path=='/':
            server.last = time.monotonic()
            return self.reply(200,PAGE.replace('__SESSION_TOKEN__',TOKEN).encode(),'text/html; charset=utf-8')
        if u.path=='/audio':
            try:
                q=parse_qs(u.query)
                p=audio_path(CUES[q['id'][0]],q['name'][0])
                return self.reply(200,p.read_bytes(),'audio/mpeg')
            except (KeyError,ValueError,OSError):
                pass
        self.send_error(404)
    def do_POST(self):
        if self.headers.get('Host')!=server.host or not secrets.compare_digest(self.headers.get('X-Session',''),TOKEN):
            return self.send_error(403)
        server.last=time.monotonic()
        try:
            size=int(self.headers.get('Content-Length','0'))
            if not 0 < size <= 64*1024*1024:
                raise ValueError('64 MB 이하의 파일을 선택하세요.')
            url=urlsplit(self.path)
            data=self.rfile.read(size)
            if url.path=='/import':
                c=CUES[parse_qs(url.query)['id'][0]]
                save(c,io.BytesIO(data))
            else:
                obj=json.loads(data)
                if url.path=='/state':
                    inbox.scan()
                    return self.reply(200,json.dumps(state()).encode(),'application/json')
                c=CUES[obj['id']]
                if url.path=='/arm':
                    inbox.scan()
                    inbox.active=c['id']
                elif url.path=='/select':
                    p=audio_path(c,obj['name'])
                    dest=places(c)[1]
                    with tempfile.NamedTemporaryFile(dir=dest.parent,delete=False) as f:
                        tmp=Path(f.name)
                    try:
                        shutil.copyfile(p,tmp)
                        tmp.replace(dest)
                    finally:
                        tmp.unlink(missing_ok=True)
                else:
                    raise ValueError('알 수 없는 요청입니다.')
            self.reply(200,b'{}','application/json')
        except (ValueError,KeyError,IndexError,OSError) as e:
            self.reply(400,str(e).encode(),'text/plain; charset=utf-8')

if __name__=='__main__':
    inbox=Inbox()
    with HTTPServer(('127.0.0.1',0),Handler) as server:
        server.host='127.0.0.1:'+str(server.server_port)
        server.last=time.monotonic()
        server.timeout=1
        address='http://'+server.host+'/'
        print('효과음 제작실이 열렸습니다. 종료하려면 이 창에서 Ctrl+C를 누르세요.\n'+address,flush=True)
        webbrowser.open(address)
        try:
            while time.monotonic()-server.last < 180:
                server.handle_request()
                inbox.scan()
        except KeyboardInterrupt:
            pass
