import http from 'http';
import https from 'https';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const ROOT = path.join(path.dirname(fileURLToPath(import.meta.url)), 'src');
const BFF  = 'ext.smartpropdata.org.uk';
const PORT = 3001;

const MIME = {
  '.html':'text/html', '.js':'application/javascript',
  '.css':'text/css',   '.png':'image/png', '.svg':'image/svg+xml',
};

http.createServer((req, res) => {
  if (req.url.startsWith('/demo-api/') || req.url.startsWith('/webhook')) {
    const opts = { hostname: BFF, path: req.url, method: req.method,
      headers: { ...req.headers, host: BFF } };
    const proxy = https.request(opts, r => {
      res.writeHead(r.statusCode, r.headers);
      r.pipe(res);
    });
    proxy.on('error', () => { res.writeHead(502); res.end('proxy error'); });
    req.pipe(proxy);
    return;
  }

  const filePath = path.join(ROOT, req.url === '/' ? 'index.html' : req.url);
  fs.readFile(filePath, (err, data) => {
    if (err) { res.writeHead(404); res.end('not found'); return; }
    res.writeHead(200, { 'Content-Type': MIME[path.extname(filePath)] || 'text/plain' });
    res.end(data);
  });
}).listen(PORT, () => console.log(`dev server → http://localhost:${PORT}  (BFF proxied to ${BFF})`));
