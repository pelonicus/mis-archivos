from selenium import webdriver
from selenium.webdriver.firefox.options import Options
from selenium.webdriver.common.by import By
from datetime import datetime
import hashlib
import os
import time
import unicodedata
import subprocess

URL = "https://elperuano.pe/"
CARPETA = "/root/scripts/publicaciones"
ARCHIVO_CLAVES = "/root/scripts/claves"

DESTINATARIOS = [
    "grubio@valmersys.com",
    "cerquinigo@valmersys.com",
    "vsalvador@valmersys.com"
]

# ---------- FUNCIONES ----------

def normalizar(texto):
    texto = texto.lower()
    texto = unicodedata.normalize("NFD", texto)
    texto = "".join(c for c in texto if unicodedata.category(c) != "Mn")
    return texto

def hash_publicacion(texto):
    return hashlib.sha256(texto.encode("utf-8")).hexdigest()

def cargar_claves():
    with open(ARCHIVO_CLAVES, "r", encoding="utf-8") as f:
        return [normalizar(l.strip()) for l in f if l.strip()]

def enviar_correo(contenido_html):

    fecha = datetime.now().strftime("%d %b %Y")
    asunto = "El Peruano - Publicaciones (" + fecha + ")"

    cuerpo = f"""From: auto-mail@valmersys.com
To: {DESTINATARIOS[0]}
Cc: {", ".join(DESTINATARIOS[1:])}
Subject: {asunto}
MIME-Version: 1.0
Content-Type: text/html; charset=UTF-8

{contenido_html}
"""

    proceso = subprocess.Popen(
        ["/usr/sbin/sendmail", "-oi", "-t"],
        stdin=subprocess.PIPE
    )

    proceso.communicate(cuerpo.encode("utf-8"))

def extraer_articulo(texto):

    lineas = [l.strip() for l in texto.splitlines() if l.strip()]

    for i, l in enumerate(lineas):

        t = l.strip()

        # --- ARTICULO 1 ---
        if t.startswith("Artículo 1"):

            # Caso normal (ya contiene la acción)
            if "designar" in t.lower():
                return t

            # Caso "Artículo 1.- Designación"
            # Buscar hacia adelante la línea real
            for j in range(i + 1, len(lineas)):

                siguiente = lineas[j].strip().lower()

                if "designar" in siguiente:
                    return t + "\n" + lineas[j]

            return t

        # --- ARTICULO UNICO ---
        if t.startswith("Artículo Único") or t.startswith("Articulo Unico"):

            if "designar" in t.lower():
                return t

            for j in range(i + 1, len(lineas)):

                siguiente = lineas[j].strip().lower()

                if "designar" in siguiente:
                    return t + " " + lineas[j]

            return t

    return "Artículo no encontrado"

def obtener_texto_publicacion(driver, link):

    driver.get(link)
    time.sleep(6)

    try:

        iframe = driver.find_elements(By.TAG_NAME, "iframe")[0]

        driver.switch_to.frame(iframe)

        time.sleep(2)

        texto = driver.find_element(By.TAG_NAME, "body").text

        driver.switch_to.default_content()

        return texto

    except:
        return ""

# ---------- PREPARACIÓN ----------

os.makedirs(CARPETA, exist_ok=True)
claves = cargar_claves()

print("Iniciando Firefox headless...")

options = Options()
options.add_argument("--headless")

driver = webdriver.Firefox(options=options)

publicaciones_match = []

# ---------- SCRAPING ----------

try:

    print("Abriendo El Peruano...")
    driver.get(URL)
    time.sleep(6)

    links = driver.find_elements(By.TAG_NAME, "a")

    publicaciones = []

    # Guardar links primero (evita stale element)
    for link in links:

        titulo = link.text.strip()

        if not titulo:
            continue

        texto_norm = normalizar(titulo)

        if any(clave in texto_norm for clave in claves):

            url_publicacion = link.get_attribute("href")

            publicaciones.append((titulo, url_publicacion))

    # Procesar publicaciones
    for titulo, url_publicacion in publicaciones:

        texto_norm = normalizar(titulo)

        texto_completo = f"""Título : {titulo}
Link   : {url_publicacion}
"""

        articulo = ""

        if "designan" in texto_norm:

            print("Analizando designación:", titulo)

            texto_publicacion = obtener_texto_publicacion(driver, url_publicacion)

            if texto_publicacion:

                articulo = extraer_articulo(texto_publicacion)

        if articulo:

            texto_completo += f"{articulo}\n"

        h = hash_publicacion(texto_completo)

        ruta = os.path.join(CARPETA, h + ".txt")

        if not os.path.exists(ruta):

            with open(ruta, "w", encoding="utf-8") as f:
                f.write(texto_completo)

            publicaciones_match.append(texto_completo)

finally:

    driver.quit()

# ---------- SALIDA Y CORREO ----------

# ---------- SALIDA Y CORREO ----------

if publicaciones_match:

    html_items = ""

    for pub in publicaciones_match:

        lineas = pub.splitlines()

        titulo = ""
        link = ""
        articulo = ""

        for l in lineas:
            if l.startswith("Título"):
                titulo = l.replace("Título :", "").strip()
            elif l.startswith("Link"):
                link = l.replace("Link   :", "").strip()
            else:
                articulo = l.strip()

        html_items += f"""
        <div style="background:#ffffff;border-radius:10px;padding:15px;margin-bottom:15px;
                    box-shadow:0 2px 6px rgba(0,0,0,0.1);">
            
            <div style="font-size:14px;color:#666;margin-bottom:5px;">
                {titulo}
            </div>

            <a href="{link}" style="text-decoration:none;">
                <div style="font-size:16px;font-weight:bold;color:#0b57d0;">
                    {articulo}
                </div>
            </a>

        </div>
        """

    html_final = f"""
    <html>
    <head>
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    </head>

    <body style="margin:0;padding:0;background:#f4f6f8;font-family:Arial,sans-serif;">

    <div style="max-width:600px;margin:auto;padding:20px;">

        <h2 style="text-align:center;color:#333;">
            📄 El Peruano - Publicaciones
        </h2>

        {html_items}

        <div style="text-align:center;color:#999;font-size:12px;margin-top:20px;">
            Generado automáticamente
        </div>

    </div>

    </body>
    </html>
    """

    print("Enviando correo HTML...")
    enviar_correo(html_final)

else:

    print("No hay publicaciones nuevas que coincidan con las claves.")
