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

def enviar_correo(contenido):

    fecha = datetime.now().strftime("%d %b %Y")
    asunto = "El Peruano - Publicaciones que hacen match (" + fecha + ")"

    cuerpo = f"""From: auto-mail@valmersys.com
To: {DESTINATARIOS[0]}
Cc: {", ".join(DESTINATARIOS[1:])}
Subject: {asunto}

{contenido}
"""

    proceso = subprocess.Popen(
        ["/usr/sbin/sendmail", "-oi", "-t"],
        stdin=subprocess.PIPE
    )

    proceso.communicate(cuerpo.encode("utf-8"))

def extraer_articulo(texto):

    lineas = texto.splitlines()

    for l in lineas:

        t = l.strip()

        if t.startswith("Artículo 1"):
            return t

        if t.startswith("Artículo Único") or t.startswith("Articulo Unico"):
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

if publicaciones_match:

    salida = "\n" + ("-" * 80 + "\n").join(publicaciones_match)

    print(salida)

    enviar_correo(salida)

else:

    print("No hay publicaciones nuevas que coincidan con las claves.")