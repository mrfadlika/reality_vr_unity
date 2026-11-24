// Cloudflare Pages Worker untuk 1Eagle VR Yard Editor
// 1. Menyimpan & menyajikan /data/denah.json lintas perangkat (Laptop/HP lain)
// 2. Mem-proxy /api/getMasterBlock & /api/getCtrLoc agar bebas masalah CORS

let memoryDenahJson = null;

export default {
  async fetch(request, env, ctx) {
    const url = new URL(request.url);

    // CORS Preflight
    if (request.method === 'OPTIONS') {
      return new Response(null, {
        status: 204,
        headers: {
          'Access-Control-Allow-Origin': '*',
          'Access-Control-Allow-Methods': 'GET, POST, PUT, OPTIONS',
          'Access-Control-Allow-Headers': 'Content-Type'
        }
      });
    }

    // 1. Endpoint Autosave & Load /data/denah.json lintas perangkat
    if (url.pathname === '/data/denah.json' || url.pathname.endsWith('/data/denah.json')) {
      const cacheKey = new Request(url.origin + '/data/denah.json', { method: 'GET' });
      const cache = caches.default;

      if (request.method === 'PUT' || request.method === 'POST') {
        try {
          const body = await request.text();
          if (body && body.trim().startsWith('{')) {
            memoryDenahJson = body;

            // Simpan ke Cloudflare KV jika binding DENAH_KV tersedia
            if (env && env.DENAH_KV) {
              try { await env.DENAH_KV.put('denah.json', body); } catch (e) {}
            }

            // Simpan ke Cloudflare Edge Cache
            const cacheResp = new Response(body, {
              status: 200,
              headers: {
                'Content-Type': 'application/json; charset=utf-8',
                'Cache-Control': 'public, max-age=31536000',
                'Access-Control-Allow-Origin': '*'
              }
            });
            await cache.put(cacheKey, cacheResp);

            return new Response(JSON.stringify({ status: 'ok', saved: true }), {
              status: 200,
              headers: {
                'Content-Type': 'application/json; charset=utf-8',
                'Access-Control-Allow-Origin': '*'
              }
            });
          }
        } catch (err) {
          return new Response(JSON.stringify({ status: 'error', message: String(err) }), {
            status: 500,
            headers: { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' }
          });
        }
      }

      if (request.method === 'GET') {
        // Prioritas A: Cloudflare KV (jika dikonfigurasi)
        if (env && env.DENAH_KV) {
          try {
            const kvData = await env.DENAH_KV.get('denah.json');
            if (kvData && kvData.trim().startsWith('{')) {
              return new Response(kvData, {
                status: 200,
                headers: {
                  'Content-Type': 'application/json; charset=utf-8',
                  'Cache-Control': 'no-store, no-cache, must-revalidate',
                  'Access-Control-Allow-Origin': '*'
                }
              });
            }
          } catch (e) {}
        }

        // Prioritas B: Cloudflare Edge Cache (Zero-config)
        try {
          const cached = await cache.match(cacheKey);
          if (cached) {
            const text = await cached.text();
            if (text && text.trim().startsWith('{')) {
              return new Response(text, {
                status: 200,
                headers: {
                  'Content-Type': 'application/json; charset=utf-8',
                  'Cache-Control': 'no-store, no-cache, must-revalidate',
                  'Access-Control-Allow-Origin': '*'
                }
              });
            }
          }
        } catch (e) {}

        // Prioritas C: In-memory isolate
        if (memoryDenahJson && memoryDenahJson.trim().startsWith('{')) {
          return new Response(memoryDenahJson, {
            status: 200,
            headers: {
              'Content-Type': 'application/json; charset=utf-8',
              'Cache-Control': 'no-store, no-cache, must-revalidate',
              'Access-Control-Allow-Origin': '*'
            }
          });
        }

        // Fallback: File statis /data/denah.json bawaan folder build
        const assetReq = new Request(url.origin + '/data/denah.json', { method: 'GET' });
        return env.ASSETS.fetch(assetReq);
      }
    }

    // 2. Dynamic Universal API Proxy & .env Reader (Mendukung domain API pelabuhan mana pun tanpa perlu ubah kode)
    // A. Endpoint /api/proxy?url=https://domain-pelabuhan-lain.com/...
    if (url.pathname === '/api/proxy') {
      const targetUrl = url.searchParams.get('url');
      if (targetUrl && (targetUrl.startsWith('http://') || targetUrl.startsWith('https://'))) {
        try {
          const apiResp = await fetch(targetUrl, {
            method: 'GET',
            headers: { 'Accept': 'application/json', 'User-Agent': '1Eagle-VR-Proxy/1.0' }
          });
          const text = await apiResp.text();
          return new Response(text, {
            status: apiResp.status,
            headers: { 'Content-Type': 'application/json; charset=utf-8', 'Access-Control-Allow-Origin': '*' }
          });
        } catch (err) {
          return new Response(JSON.stringify({ status: 'error', message: String(err) }), {
            status: 502,
            headers: { 'Content-Type': 'application/json', 'Access-Control-Allow-Origin': '*' }
          });
        }
      }
    }

    // B. Helper membaca URL dari /data/.env jika memanggil /api/getMasterBlock atau /api/getCtrLoc
    async function resolveEnvApiUrl(envKey, fallbackUrl) {
      try {
        const envReq = new Request(url.origin + '/data/.env', { method: 'GET' });
        const envResp = await env.ASSETS.fetch(envReq);
        if (envResp && envResp.ok) {
          const envText = await envResp.text();
          const lines = envText.split(/\r?\n/);
          for (const rawLine of lines) {
            const line = rawLine.trim();
            if (!line || line.startsWith('#') || line.startsWith(';')) continue;
            const eqIdx = line.indexOf('=');
            if (eqIdx <= 0) continue;
            const k = line.substring(0, eqIdx).trim().toUpperCase();
            let v = line.substring(eqIdx + 1).trim().replace(/^["']|["']$/g, '');
            if (k === envKey && (v.startsWith('http://') || v.startsWith('https://'))) {
              return v;
            }
          }
        }
      } catch (e) {}
      return fallbackUrl;
    }

    if (url.pathname === '/api/getMasterBlock' || url.pathname === '/api/vr/getMasterBlock') {
      try {
        const target = await resolveEnvApiUrl('API_MASTER_BLOCK', 'https://apps.globalhalalhub.co.id/global-halal-api/api/vr/getMasterBlock');
        const apiResp = await fetch(target);
        const text = await apiResp.text();
        return new Response(text, {
          status: apiResp.status,
          headers: { 'Content-Type': 'application/json; charset=utf-8', 'Access-Control-Allow-Origin': '*' }
        });
      } catch (e) {}
    }

    if (url.pathname === '/api/getCtrLoc' || url.pathname === '/api/vr/getCtrLoc') {
      try {
        const target = await resolveEnvApiUrl('API_CONTAINER_LOC', 'https://apps.globalhalalhub.co.id/global-halal-api/api/vr/getCtrLoc');
        const apiResp = await fetch(target);
        const text = await apiResp.text();
        return new Response(text, {
          status: apiResp.status,
          headers: { 'Content-Type': 'application/json; charset=utf-8', 'Access-Control-Allow-Origin': '*' }
        });
      } catch (e) {}
    }

    // 3. File statis lainnya
    return env.ASSETS.fetch(request);
  }
};
