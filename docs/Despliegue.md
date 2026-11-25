# Despliegue en servidor Linux con Nginx y balanceo de carga

La API se despliega en un **VPS de Hostinger con Ubuntu 24.04**:

- **Docker** corre SQL Server y **dos instancias** de la imagen de Docker Hub `marianasilcam05/promelec-api:1.0`.
- **Nginx**, instalado en el servidor, recibe el tráfico en los puertos 80 y 443, fuerza HTTPS, agrega las cabeceras de seguridad y **reparte las peticiones** entre las dos instancias.

```
Internet ──► Nginx :80  ──► 301 a HTTPS
Internet ──► Nginx :443 (TLS + cabeceras de seguridad)
                  │ upstream promelec_api (round robin)
          ┌───────┴────────┐
          ▼                ▼
  promelec-api-1     promelec-api-2       ← imagen de Docker Hub, solo en 127.0.0.1
  127.0.0.1:5000     127.0.0.1:5001
          └───────┬────────┘
                  ▼
           promelec-sql (SQL Server 2022, sin puerto público)
```

| Archivo | Va en el servidor en | Qué hace |
|---|---|---|
| [`deploy/docker-compose.yml`](../deploy/docker-compose.yml) | `/opt/promelec/docker-compose.yml` | SQL Server + 2 instancias de la API, con verificación de salud y llaves compartidas |
| [`deploy/nginx/promelec.conf`](../deploy/nginx/promelec.conf) | `/etc/nginx/sites-available/promelec` | Proxy inverso, HTTPS, cabeceras de seguridad y balanceo (`upstream`) |
| [`deploy/.env.example`](../deploy/.env.example) | (solo referencia) | Variables que necesita el `.env` del servidor; el real se genera en el paso 4 |

La configuración se probó antes en una simulación local con Nginx 1.24, la misma versión de Ubuntu 24.04. Resultados en la sección 4.

---

## 1. Antes de empezar

- La **IP pública** del VPS: hPanel → VPS → *Información general*. En esta guía aparece como `TU_IP`.
- La **contraseña de root** del VPS, o una llave SSH configurada.
- Si en hPanel → VPS → *Firewall* tienes reglas activas, permite los puertos **22, 80 y 443**.
- Sube a Docker Hub la versión más reciente de la imagen. Desde tu PC, en la carpeta `PromElec`:

```powershell
docker push marianasilcam05/promelec-api:1.0
docker push marianasilcam05/promelec-api:latest
```

---

## 2. Pasos en el servidor

Los comandos marcados como **[PC]** se ejecutan en PowerShell en tu computadora. Los marcados como **[VPS]** se ejecutan en el servidor, dentro de la sesión SSH, como `root`.

### Paso 1 · Conectarse por SSH **[PC]**

```powershell
ssh root@TU_IP
```

La primera vez pregunta si confías en el servidor: escribe `yes`. Luego escribe la contraseña de root (no se ve al escribir).

### Paso 2 · Revisar el servidor **[VPS]**

```bash
lsb_release -ds; uname -m; free -h | head -2; nproc
ss -tlnp | grep -E ':(80|443|5000|5001)\b' || echo "Puertos 80, 443, 5000 y 5001 libres"
docker --version; docker compose version
```

Qué debe salir:

- `Ubuntu 24.04...`, la arquitectura **`x86_64`** (SQL Server no corre en ARM) y **4 GB** de RAM o más.
- `Puertos ... libres`. Si aparece otro programa en el 80 o el 443 (por ejemplo `apache2`), **detente** y revisa qué es antes de seguir: podría ser otro sitio tuyo.
- Si `docker --version` muestra una versión, Docker ya está instalado; en el paso 3 usa la opción B.

### Paso 3 · Instalar Docker, Nginx y el firewall **[VPS]**

**Opción A**, si Docker NO está instalado:

```bash
apt update && apt upgrade -y
apt install -y docker.io docker-compose-v2 nginx openssl ufw
systemctl enable --now docker nginx
```

**Opción B**, si Docker YA está instalado (plantilla "Ubuntu 24.04 with Docker"):

```bash
apt update && apt upgrade -y
apt install -y nginx openssl ufw
systemctl enable --now nginx
```

Firewall: solo SSH, HTTP y HTTPS. Los puertos 5000 y 5001 **no** se abren, porque solo los usa Nginx dentro del servidor.

```bash
ufw allow OpenSSH
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable
ufw status
```

> ⚠️ Es importante ejecutar `ufw allow OpenSSH` **antes** de `ufw --force enable`, o perderías la conexión SSH.

### Paso 4 · Carpeta del proyecto y secretos **[VPS]**

```bash
mkdir -p /opt/promelec && cd /opt/promelec
IP=$(hostname -I | awk '{print $1}'); echo "IP detectada: $IP"
```

Revisa que la IP detectada sea la misma de hPanel. Si no, escríbela a mano: `IP=TU_IP`. Luego genera el `.env` con valores **aleatorios**:

```bash
cat > .env <<EOF
SERVER_HOST=$IP
MSSQL_SA_PASSWORD=Sql-$(openssl rand -hex 12)-Aa9
JWT_KEY=$(openssl rand -base64 48 | tr -d '\n')
ADMIN_PASSWORD=Adm-$(openssl rand -hex 8)-Bb7
EOF
chmod 600 .env
cut -d= -f1 .env
```

El último comando muestra solo los **nombres** de las variables, no los valores. El archivo solo lo puede leer root (`chmod 600`).

### Paso 5 · Copiar la configuración al servidor **[PC]**

En **otra** ventana de PowerShell (no en la de SSH), desde la carpeta `PromElec`:

```powershell
scp deploy/docker-compose.yml root@TU_IP:/opt/promelec/
scp deploy/nginx/promelec.conf root@TU_IP:/etc/nginx/sites-available/promelec
```

### Paso 6 · Levantar los contenedores **[VPS]**

```bash
cd /opt/promelec
docker compose pull
docker compose up -d
docker compose ps
```

La primera vez descarga unos 2 GB. Espera a que `promelec-sql`, `promelec-api-1` y `promelec-api-2` digan **(healthy)**; repite `docker compose ps` cada 30 segundos. Luego comprueba cada instancia directamente:

```bash
curl -s http://localhost:5000/health; echo
curl -s http://localhost:5001/health; echo
```

Las dos deben responder `Healthy`.

### Paso 7 · Certificado HTTPS autofirmado para la IP **[VPS]**

```bash
mkdir -p /etc/nginx/ssl
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout /etc/nginx/ssl/promelec.key -out /etc/nginx/ssl/promelec.crt \
  -subj "/CN=$IP" -addext "subjectAltName=IP:$IP"
chmod 600 /etc/nginx/ssl/promelec.key
```

Si abriste una sesión SSH nueva, vuelve a definir la IP antes: `IP=$(grep SERVER_HOST /opt/promelec/.env | cut -d= -f2)`.

### Paso 8 · Activar el sitio en Nginx **[VPS]**

```bash
ln -sf /etc/nginx/sites-available/promelec /etc/nginx/sites-enabled/promelec
rm -f /etc/nginx/sites-enabled/default
nginx -t && systemctl reload nginx
```

`nginx -t` debe decir `syntax is ok` y `test is successful`.

---

## 3. Verificación

**En el servidor [VPS]:**

```bash
# HTTP redirige a HTTPS (301)
curl -sI http://$IP/api/Product | head -3

# La API responde por HTTPS (-k acepta el certificado autofirmado)
curl -sk https://$IP/api/Product/2; echo

# Balanceo: X-Upstream alterna entre 127.0.0.1:5000 y 127.0.0.1:5001
for i in 1 2 3 4 5 6; do curl -sk -o /dev/null -D - https://$IP/api/Product/1 | grep -i x-upstream; done

# Cabeceras de seguridad
curl -skI https://$IP/ | grep -iE 'strict-transport|x-frame|x-content|referrer|permissions|content-security|server'
```

**Tolerancia a fallas [VPS]:** se apaga una instancia y la API sigue respondiendo con la otra.

```bash
docker stop promelec-api-2
for i in 1 2 3 4; do curl -sk -o /dev/null -D - https://$IP/api/Product/1 | grep -iE '^HTTP|x-upstream'; done
docker start promelec-api-2
```

**Desde tu PC:**

- Navegador: `https://TU_IP/`. Aparece una advertencia por el certificado autofirmado: *Configuración avanzada → Continuar*. Debe verse el catálogo.
- PowerShell: `curl.exe -k https://TU_IP/api/Product/2` y `curl.exe -k -i https://TU_IP/api/Order` (este último debe dar 401).
- Contraseña del Admin, para iniciar sesión **[VPS]**: `grep ADMIN_PASSWORD /opt/promelec/.env`.

---

## 4. Resultados de la simulación previa (25/11/2026)

Antes de tocar el VPS, se levantó el mismo `deploy/docker-compose.yml` en local con un contenedor `nginx:1.24` y la misma configuración. Solo cambiaron las direcciones del `upstream`, porque ahí Nginx corría en Docker.

| Prueba | Resultado |
|---|---|
| `nginx -t` | Sintaxis correcta |
| SQL Server, instancia 1 e instancia 2 | Las tres en estado *healthy*; la instancia 2 arranca cuando la 1 ya terminó las migraciones |
| `http://` | 301 a `https://` |
| 8 peticiones seguidas | Alternan instancia 1, 2, 1, 2... (`X-Upstream`) |
| Cabeceras | HSTS, `X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy` y CSP, cada una **una sola vez**; `Server: nginx` sin versión |
| Login del Admin y `GET /api/Order` | 200; sin token, 401 |
| Formulario de Contacto con la página abierta en una instancia y enviado a la otra | Aceptado (200): las instancias comparten las llaves de Data Protection |
| Instancia 2 apagada | Todas las peticiones 200: Nginx reintenta en la instancia 1 |
| IP registrada en el log de la API | La del cliente, no la de Nginx (cabeceras `X-Forwarded-*`) |

Durante la simulación se encontraron y corrigieron dos detalles:

1. **Sin `zone` en el `upstream`**, las primeras peticiones caían todas en la instancia 1, porque cada proceso de Nginx llevaba su propio turno. Con `zone promelec_api 64k;` el turno se comparte y el balanceo alterna.
2. **El formulario de Contacto por HTTP plano respondía 500**, porque la cookie antiforgery exige HTTPS. Ahora la página tiene `[RequireHttps]` y redirige a HTTPS.

---

## 5. Seguridad del despliegue

| Medida | Dónde |
|---|---|
| Solo se abren los puertos 22, 80 y 443 | `ufw` (paso 3) |
| La API (5000 y 5001) y SQL Server no son accesibles desde internet | `127.0.0.1:` en `ports` y SQL sin `ports` |
| HTTP redirige siempre a HTTPS; TLS 1.2 y 1.3 | `promelec.conf` |
| Cabeceras de seguridad centralizadas en Nginx, sin versión del servidor | `add_header`, `proxy_hide_header`, `server_tokens off` |
| Peticiones de más de 1 MB rechazadas | `client_max_body_size 1m` |
| Secretos aleatorios en `.env` con permisos 600; certificado con permisos 600 | Pasos 4 y 7 |
| La API conoce la IP real del cliente (límite de intentos por IP) | `X-Forwarded-For` + `ASPNETCORE_FORWARDEDHEADERS_ENABLED` |
| Tolerancia a fallas | `max_fails`, `fail_timeout`, `proxy_next_upstream` |

---

## 6. Solución de problemas

| Síntoma | Causa probable | Solución |
|---|---|---|
| `ssh: connect ... timed out` | IP incorrecta o el firewall de Hostinger bloquea el puerto 22 | Revisa la IP y el firewall en hPanel |
| `docker.io : Conflicts: containerd.io` en el paso 3 | Docker ya venía instalado | Usa la opción B del paso 3 |
| Un contenedor `promelec-api-*` queda *unhealthy* o se reinicia | Error al arrancar | `docker compose logs api1` (o `api2`) |
| `promelec-sql` se detiene | Memoria insuficiente | `free -h`: SQL Server necesita al menos 2 GB libres |
| `400 Bad Request - Invalid Hostname` | `SERVER_HOST` no coincide con la IP que usas | Corrige `.env` y ejecuta `docker compose up -d` |
| `502 Bad Gateway` | Las instancias de la API no están arriba | `docker compose ps` y `docker compose logs` |
| `nginx: [emerg] bind() to 0.0.0.0:80 failed` | Otro programa usa el puerto 80 | `ss -tlnp | grep :80` para ver cuál es |

---

## 7. Mejoras posibles

- Con un dominio, cambiar el certificado autofirmado por uno gratuito y válido de **Let's Encrypt**: `apt install certbot python3-certbot-nginx` y `certbot --nginx -d tudominio.com`.
- Iniciar sesión por SSH con llave en lugar de contraseña, y desactivar el acceso con contraseña.
- `fail2ban` para bloquear IPs que intentan adivinar la contraseña de SSH.
- Respaldos automáticos del volumen de SQL Server.
