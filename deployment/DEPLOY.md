# Deploying Roofied to roofied.alibalib.com

Roofied joins the **alibalib.com** multi-app platform (DigitalOcean droplet
`162.243.174.107`, nginx + Docker Compose). It shares the existing
**`awblazor-sqlserver`** container (new database `Roofied`) and runs as its own
app container on **`127.0.0.1:8084`**. Nginx reverse-proxies the subdomain and
terminates TLS. Roofied is **public** (its own ASP.NET Identity login + roles).

Run the steps in order. `$` = on the droplet over SSH (use PuTTY — the DO web
console mangles long pasted lines).

---

## 1. DNS at Porkbun

Porkbun → **Domain Management → alibalib.com → DNS → Edit**. Add an **A** record (TTL 600):

| Type | Host | Answer |
|---|---|---|
| A | `roofied` | `162.243.174.107` |

Verify (must return the IP):
```bash
$ dig +short roofied.alibalib.com
```

## 2. Push the image to GHCR (from your machine)

Commit + push to `main`. GitHub Actions builds and publishes
`ghcr.io/capnbigal/roofied:latest`. Then make it pullable by the droplet:
- GitHub → profile → **Packages → roofied → Package settings → Change visibility → Public**, **or**
- keep it private and `docker login ghcr.io` on the droplet with a PAT.

## 3. Put the deploy files on the droplet

```bash
$ sudo git clone https://github.com/capnbigal/Roofied.git /opt/roofied
$ cd /opt/roofied
$ cp deployment/.env.template .env && sudo chmod 600 .env
$ sudo nano .env
#   - SA_PASSWORD: SAME value as /opt/awblazor/.env
#   - IP_HASH_SALT: a long random secret (e.g. `openssl rand -base64 32`)  <-- REQUIRED
#   - SEED_ADMIN_EMAIL / SEED_ADMIN_PASSWORD: optional initial admin account
$ grep SA_PASSWORD /opt/awblazor/.env /opt/roofied/.env   # confirm they match
```

> **Important:** Production startup **aborts** if `IP_HASH_SALT` is missing or
> shorter than 16 characters (`StartupValidation`). Set it before starting.

## 4. Start the container

```bash
$ cd /opt/roofied
$ sudo docker compose pull app
$ sudo docker compose up -d app
$ sudo docker compose logs -f app   # watch for: Now listening on: http://[::]:8080
```

On first start it creates the `Roofied` database, applies migrations, and seeds
roles, report/venue categories, the initial channels, starter resources, and
(if configured) the admin account. Smoke test on the box:
```bash
$ curl -sI http://127.0.0.1:8084 | head -1            # expect HTTP/1.1 200
$ curl -s  http://127.0.0.1:8084/health               # expect Healthy
```

## 5. Nginx site

```bash
$ sudo cp /opt/roofied/deployment/nginx-roofied.conf /etc/nginx/sites-available/roofied.conf
$ sudo ln -s /etc/nginx/sites-available/roofied.conf /etc/nginx/sites-enabled/
$ sudo nginx -t && sudo systemctl reload nginx
```

## 6. TLS

```bash
$ sudo certbot --nginx -d roofied.alibalib.com
$ sudo nginx -t && sudo systemctl reload nginx
```

## 7. Verify

```bash
$ curl -sI https://roofied.alibalib.com | head -1   # 200
```
Open it in a browser, confirm the interactive circuit connects (no persistent
"reconnecting" overlay), and sign in with the seeded admin if you set one.

---

## Routine updates later

Push to `main` → Actions rebuilds the image → on the droplet:
```bash
$ cd /opt/roofied && sudo docker compose pull app && sudo docker compose up -d app
```
Rollback: `APP_TAG=<short-sha> sudo docker compose up -d app`.

## Notes
- **Connection string** is injected by compose (env), overriding `appsettings*.json`.
- **Backups:** AWBlazor's instance-wide SQL backup includes the new `Roofied` DB automatically.
- **Logs:** Serilog writes a rolling file to `/app/logs` inside the container,
  persisted in the `roofied-logs` volume (plus stdout, visible via `docker compose logs`).
- **Email confirmation:** registration requires a confirmed account but the app
  ships a no-op email sender, so self-service signups can't confirm out of the box.
  Use the seeded admin (auto-confirmed) to manage the site, or wire a real
  `IEmailSender` before relying on public registration.
