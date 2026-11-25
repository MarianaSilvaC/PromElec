# Despliegue en servidor Linux con Nginx y balanceo de carga

La API se despliega en un **VPS de Hostinger con Ubuntu 24.04** (1 vCPU, 3.8 GB de RAM). La imagen `marianasilcam05/promelec-api:1.1` corre en **dos instancias** detrás de **Nginx 1.30**, que hace de proxy inverso: fuerza HTTPS, agrega las cabeceras de seguridad y **reparte las peticiones** entre ellas (`upstream`).

El VPS ya tiene servicios en producción (Easypanel con Traefik en los puertos 80/443, n8n y otras aplicaciones). Por eso el despliegue es **aislado**: corre junto a ellos sin modificarlos.

```
Internet ──► :80 / :443  ──► Traefik (Easypanel) ──► servicios existentes     ← no se toca
Internet ──► :8088 (HTTP)  ──► promelec-nginx ──► 301 a https://IP:8443
Internet ──► :8443 (HTTPS) ──► promelec-nginx (TLS + cabeceras de seguridad)
                                     │ upstream promelec_api (round robin)
                              ┌──────┴───────┐
                              ▼              ▼
                       promelec-api-1   promelec-api-2      ← sin puertos públicos
                              └──────┬───────┘
                                     ▼
                         SQLite (volumen promelec-data)
```

### Decisiones de diseño

| Decisión | Motivo |
|---|---|
| **Puertos 8443 (HTTPS) y 8088 (HTTP)** en lugar de 443 y 80 | El 80 y el 443 ya los usa Traefik. Instalar Nginx ahí habría dejado sin servicio a las aplicaciones existentes |
| **Nginx en un contenedor** (`nginx:1.30-alpine`), no instalado en el sistema | No modifica paquetes ni la configuración del servidor; se quita con un solo comando |
| **SQLite en lugar de SQL Server** (`Database__Provider=Sqlite`) | SQL Server necesita al menos 2 GB de RAM y el VPS solo tenía 2.5 GB libres. Con SQLite todo el despliegue usa unos **130 MB**. Es el mismo motor que usan las 20 pruebas de integración |
| **Límites de memoria y CPU** (256 MB y 0.5 CPU por instancia, 64 MB Nginx) | Garantizan que PromElec nunca le quite recursos a los servicios en producción |
| **Proyecto, red y volúmenes propios** (`promelec-prod`) | Sin choques de nombres ni de red con lo que ya existe |
| **Certificado autofirmado para la IP** | No hay dominio, y Let's Encrypt necesita el puerto 80, que es de Traefik |
| **Sin `ufw`** | El firewall de Hostinger ya filtra los puertos, y activar `ufw` podría cortar la red de Docker Swarm que usa Easypanel |

| Archivo | Va en el VPS en |
|---|---|
| [`deploy/docker-compose.yml`](../deploy/docker-compose.yml) | `/opt/promelec/docker-compose.yml` |
| [`deploy/nginx/promelec.conf`](../deploy/nginx/promelec.conf) | `/opt/promelec/nginx/promelec.conf` |
| [`deploy/.env.example`](../deploy/.env.example) | (solo referencia: el `.env` real se genera en el paso 3) |

---

## 1. Pasos

Los pasos marcados como **[PC]** se ejecutan en PowerShell en tu computadora. Los marcados como **[VPS]** se ejecutan dentro de la sesión SSH. **[hPanel]** es el panel web de Hostinger.

### Paso 0 · Subir la imagen 1.1 a Docker Hub **[PC]**

```powershell
docker push marianasilcam05/promelec-api:1.1
docker push marianasilcam05/promelec-api:latest
```

### Paso 1 · Abrir los puertos 8443 y 8088 **[hPanel]**

En **VPS → Firewall**, agrega dos reglas y presiona **Sincronizar**:

| Acción | Protocolo | Puerto | Fuente |
|---|---|---|---|
| Accept | TCP | 8443 | Any |
| Accept | TCP | 8088 | Any |

### Paso 2 · Conectarse y comprobar que no hay nada con el mismo nombre **[PC] → [VPS]**

```powershell
ssh -i "C:\Users\Mariana\.ssh\promelec_vps" root@TU_IP
```

```bash
ls /opt/promelec 2>/dev/null && echo "YA EXISTE /opt/promelec: detente" || echo "OK: /opt/promelec no existe"
docker ps -a --format '{{.Names}}' | grep -E '^promelec' && echo "YA HAY contenedores promelec: detente" || echo "OK: sin contenedores promelec"
ss -tlnp | grep -E ':(8443|8088)\b' && echo "PUERTO OCUPADO: detente" || echo "OK: 8443 y 8088 libres"
```

Las tres líneas deben decir **OK**.

### Paso 3 · Carpetas y secretos **[VPS]**

```bash
mkdir -p /opt/promelec/nginx /opt/promelec/ssl && cd /opt/promelec
IP=TU_IP
cat > .env <<EOF
SERVER_HOST=$IP
JWT_KEY=$(openssl rand -base64 48 | tr -d '\n')
ADMIN_PASSWORD=Adm-$(openssl rand -hex 8)-Bb7
EOF
chmod 600 .env
cut -d= -f1 .env
```

Cambia `TU_IP` por la IP del VPS. El último comando muestra solo los **nombres** de las variables, no sus valores.

### Paso 4 · Copiar la configuración **[PC]**

En **otra** ventana de PowerShell, desde la carpeta `PromElec`:

```powershell
scp -i "C:\Users\Mariana\.ssh\promelec_vps" deploy/docker-compose.yml root@TU_IP:/opt/promelec/
scp -i "C:\Users\Mariana\.ssh\promelec_vps" deploy/nginx/promelec.conf root@TU_IP:/opt/promelec/nginx/
```

### Paso 5 · Certificado HTTPS autofirmado **[VPS]**

```bash
cd /opt/promelec
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout ssl/promelec.key -out ssl/promelec.crt \
  -subj "/CN=$IP" -addext "subjectAltName=IP:$IP"
ls -l ssl
```

### Paso 6 · Levantar PromElec **[VPS]**

```bash
cd /opt/promelec
docker compose pull
docker compose up -d
docker compose ps
```

Tarda unos 30 a 60 segundos. Al final, `promelec-api-1` y `promelec-api-2` deben decir **(healthy)** y `promelec-nginx` debe decir **Up**.

---

## 2. Verificación

**En el VPS:**

```bash
# Configuración de Nginx
docker exec promelec-nginx nginx -t

# HTTP redirige a HTTPS (301 a :8443)
curl -sI http://$IP:8088/api/Product | head -3

# La API responde por HTTPS (-k acepta el certificado autofirmado)
curl -sk https://$IP:8443/api/Product/2; echo

# Balanceo: X-Upstream alterna entre las dos instancias
for i in 1 2 3 4 5 6; do curl -sk -o /dev/null -D - https://$IP:8443/api/Product/1 | grep -i x-upstream; done

# Cabeceras de seguridad
curl -skI https://$IP:8443/ | grep -iE 'strict-transport|x-frame|x-content|referrer|permissions|content-security|server'

# Tolerancia a fallas: con una instancia apagada todo sigue respondiendo 200
docker stop promelec-api-2
for i in 1 2 3 4; do curl -sk -o /dev/null -D - https://$IP:8443/api/Product/1 | grep -iE '^HTTP|x-upstream'; done
docker start promelec-api-2

# Recursos usados (con sus límites)
docker stats --no-stream promelec-api-1 promelec-api-2 promelec-nginx
```

**Desde tu PC:**

- Navegador: **`https://TU_IP:8443/`**. Aparece una advertencia por el certificado autofirmado: *Configuración avanzada → Continuar*. Debe verse el catálogo.
- PowerShell: `curl.exe -k https://TU_IP:8443/api/Product/2` y `curl.exe -k -i https://TU_IP:8443/api/Order` (el segundo debe responder 401).
- Contraseña del Admin **[VPS]**: `grep ADMIN_PASSWORD /opt/promelec/.env`.

**Evidencias sugeridas (capturas):** `docker compose ps`, `nginx -t`, la alternancia de `X-Upstream`, la prueba con una instancia apagada, `docker stats` y el navegador con `https://TU_IP:8443/`.

---

## 3. Resultados de la simulación previa (25/11/2026)

Antes de tocar el VPS se ejecutaron **los mismos archivos y comandos** en local, con `127.0.0.1` como IP:

| Prueba | Resultado |
|---|---|
| Arranque | Instancia 1 sana → instancia 2 sana → Nginx; `nginx -t` correcto |
| `http://…:8088` | 301 a `https://…:8443` |
| Acceso directo a las APIs | Imposible: no publican puertos |
| 8 peticiones seguidas | Alternan entre las dos instancias (`X-Upstream`) |
| Cabeceras | HSTS, `X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy` y CSP, cada una una sola vez; `Server: nginx` sin versión |
| Escritura en SQLite desde las dos instancias | Registro y 3 pedidos repartidos entre ambas; totales calculados por el servidor |
| Pedido ajeno / borrar un producto con pedidos | 403 / 409 |
| URL generada por la API (`Location`) | `https://127.0.0.1:8443/api/Product/11`: incluye el puerto público |
| Formulario de Contacto abierto en una instancia y enviado a la otra | Aceptado: las llaves se comparten |
| Instancia 2 apagada | Todas las peticiones 200 |
| Memoria | API-1 77 MB, API-2 41 MB, Nginx 14 MB (≈130 MB en total) |
| `docker compose restart` | Los datos persisten |

---

## 4. Seguridad del despliegue

| Medida | Dónde |
|---|---|
| Solo los puertos 22, 80, 443, 8088 y 8443 están abiertos a internet | Firewall de Hostinger |
| Las instancias de la API no publican puertos; solo Nginx las alcanza | `deploy/docker-compose.yml` (sin `ports` en `api1` y `api2`) |
| HTTP siempre redirige a HTTPS; TLS 1.2 y 1.3 | `deploy/nginx/promelec.conf` |
| Cabeceras de seguridad centralizadas en Nginx, sin versión del servidor | `add_header`, `proxy_hide_header`, `server_tokens off` |
| Peticiones de más de 1 MB rechazadas | `client_max_body_size 1m` |
| Secretos aleatorios en `.env` con permisos 600 | Paso 3 |
| La API conoce la IP real del cliente (límite de intentos por IP) | `X-Forwarded-For` + `ASPNETCORE_FORWARDEDHEADERS_ENABLED` |
| Tolerancia a fallas | `max_fails`, `fail_timeout`, `proxy_next_upstream`, `restart: unless-stopped` |
| Aislamiento de recursos | `mem_limit` y `cpus` por contenedor |

---

## 5. Operación

```bash
cd /opt/promelec
docker compose logs -f api1                 # ver el log de una instancia
docker compose pull && docker compose up -d # actualizar a una nueva imagen
```

**Quitar todo** (el servidor queda como estaba antes):

```bash
cd /opt/promelec && docker compose down -v
rm -rf /opt/promelec
```

Después borra las reglas 8443 y 8088 en el firewall de hPanel.

---

## 6. Solución de problemas

| Síntoma | Causa probable | Solución |
|---|---|---|
| El navegador no carga `https://IP:8443` (tiempo de espera) | Faltan las reglas del firewall o no se sincronizaron | Paso 1 y botón **Sincronizar** |
| `400 Bad Request - Invalid Hostname` | `SERVER_HOST` en `.env` no coincide con la IP que usas | Corrige `.env` y ejecuta `docker compose up -d` |
| `502 Bad Gateway` | Las instancias no están sanas | `docker compose ps` y `docker compose logs api1` |
| `promelec-api-*` se reinicia una y otra vez | Falta una variable en `.env` o la imagen no se descargó | `docker compose logs api1`; revisa el paso 0 |
| `docker compose pull` falla con `not found` | La imagen 1.1 no se subió a Docker Hub | Paso 0 |

---

## 7. Mejoras posibles

- Con un dominio propio, publicar PromElec a través del Traefik existente en el puerto 443, con un certificado válido de Let's Encrypt.
- Con más memoria (o un servidor dedicado), volver a SQL Server: basta con cambiar `Database__Provider` y la cadena de conexión.
- Respaldos automáticos del volumen `promelec-data`.
- Iniciar sesión SSH solo con llave y desactivar el acceso con contraseña.
