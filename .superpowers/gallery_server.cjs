const http = require('http');
const fs = require('fs');
const path = require('path');

const PORT = 52341;
const HOST = '0.0.0.0';
const CONTENT_DIR = path.join(__dirname, 'brainstorm', 'session', 'content');

if (!fs.existsSync(CONTENT_DIR)) {
  fs.mkdirSync(CONTENT_DIR, { recursive: true });
}

const MIME_TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.js': 'application/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.ico': 'image/x-icon',
  '.svg': 'image/svg+xml'
};

const server = http.createServer((req, res) => {
  const url = new URL(req.url, `http://${req.headers.host || 'localhost'}`);
  let pathname = decodeURIComponent(url.pathname);

  if (pathname === '/' || pathname === '/index.html') {
    const htmlFiles = fs.readdirSync(CONTENT_DIR)
      .filter(f => f.endsWith('.html'))
      .map(f => ({
        name: f,
        mtime: fs.statSync(path.join(CONTENT_DIR, f)).mtimeMs
      }))
      .sort((a, b) => b.mtime - a.mtime);

    if (htmlFiles.length > 0) {
      const latestPath = path.join(CONTENT_DIR, htmlFiles[0].name);
      let content = fs.readFileSync(latestPath, 'utf-8');
      res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
      res.end(content);
      return;
    } else {
      res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
      res.end('<!DOCTYPE html><html><body style="font-family:sans-serif;background:#0b0e14;color:#f3f4f6;padding:40px;text-align:center;"><h2>Local LLM Server Manager Visual Hub</h2><p style="color:#9ca3af;">Generating concepts...</p><script>setTimeout(()=>location.reload(),2000);</script></body></html>');
      return;
    }
  }

  const safeFilename = path.basename(pathname);
  const filePath = path.join(CONTENT_DIR, safeFilename);

  if (fs.existsSync(filePath) && fs.statSync(filePath).isFile()) {
    const ext = path.extname(filePath).toLowerCase();
    res.writeHead(200, { 'Content-Type': MIME_TYPES[ext] || 'application/octet-stream' });
    fs.createReadStream(filePath).pipe(res);
  } else {
    res.writeHead(404, { 'Content-Type': 'text/plain' });
    res.end('Not Found');
  }
});

server.listen(PORT, HOST, () => {
  console.log(`Visual Companion running at http://10.0.0.21:${PORT} and http://localhost:${PORT}`);
});
