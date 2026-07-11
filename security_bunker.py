"""
Sandbox de verificacion antifraude para Open Gateway / CAMARA.

Este archivo no esta conectado al flujo productivo de AppServicios. Sirve como
modulo aislado para probar reputacion telefonica, SIM Swap y Device Location
cuando Telefonica entregue las URLs reales del sandbox.
"""

from __future__ import annotations

import html
import json
import os
import re
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import requests
from dotenv import load_dotenv

from auth_gateway import OpenGatewayAuthError, conversor_auth_gateway


load_dotenv()

PHONE_E164_PATTERN = re.compile(r"^\+[1-9]\d{1,14}$")


class SecurityBunkerError(RuntimeError):
    """Error controlado en el sandbox de seguridad."""


@dataclass
class RegistroProtegidoRequest:
    nombre: str
    telefono: str

    def validar(self) -> None:
        if not self.nombre.strip():
            raise SecurityBunkerError("El nombre es obligatorio.")

        if not PHONE_E164_PATTERN.match(self.telefono.strip()):
            raise SecurityBunkerError("El telefono debe estar en formato E.164, por ejemplo +5492611234567.")


@dataclass
class ReporteAntifraude:
    nombre: str
    telefono: str
    status: str = "PENDIENTE"
    h1_tipo: str = "Wireless"
    h1_score: int = 0
    h1_msg: str = "Linea limpia."
    h2_operador: str = "Desconocido"
    h2_riesgo: float = 0.0
    h3_ciudad: str = "Mendoza"
    h3_lat: float | None = None
    h3_lon: float | None = None
    h4_msg: str = "No verificado"
    h5_msg: str = "No verificado"
    tx_id: str = "N/A"
    puntos_fraude: int = 0
    justificacion: str = (
        "El perfil supero las verificaciones configuradas en el sandbox."
    )


def ejecutar_bunker_antifraude(nombre: str, telefono: str) -> dict[str, Any]:
    request = RegistroProtegidoRequest(nombre=nombre, telefono=telefono)
    request.validar()

    reporte = ReporteAntifraude(nombre=request.nombre, telefono=request.telefono)

    _evaluar_ipqualityscore(reporte)
    _evaluar_maxmind(reporte)
    _resolver_ciudad(reporte)

    if reporte.puntos_fraude < 80:
        _evaluar_sim_swap(reporte)

    if reporte.puntos_fraude < 80 and reporte.h3_lat is not None and reporte.h3_lon is not None:
        _evaluar_device_location(reporte)

    _calcular_veredicto(reporte)
    ruta_reporte = generar_reporte_html_unificado(reporte.telefono, asdict(reporte))
    _persistir_auditoria_si_esta_configurada(reporte, ruta_reporte)

    return {
        "success": reporte.status == "APROBADO",
        "status": reporte.status,
        "message": "PROCESO_FINALIZADO",
        "reportPath": str(ruta_reporte),
        "transactionId": reporte.tx_id,
    }


def generar_reporte_html_unificado(telefono: str, datos: dict[str, Any]) -> Path:
    telefono_limpio = re.sub(r"[^0-9]", "", telefono)
    carpeta_reportes = Path(os.getenv("SECURITY_REPORTS_DIR", "reportes_antifraude"))
    ruta_carpeta = carpeta_reportes / telefono_limpio
    ruta_carpeta.mkdir(parents=True, exist_ok=True)

    status = str(datos["status"])
    color_header = "#28a745"
    color_texto_header = "#fff"

    if status == "EN_REVISION":
        color_header = "#ffc107"
        color_texto_header = "#000"
    elif status != "APROBADO":
        color_header = "#dc3545"

    generado_en = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S UTC")
    valores = {key: html.escape(str(value)) for key, value in datos.items()}
    telefono_seguro = html.escape(telefono)

    contenido = f"""
    <html>
      <body style="font-family: Arial, sans-serif; background-color: #f8f9fa; padding: 20px; color: #333;">
        <div style="max-width: 760px; margin: 0 auto; background: #fff; padding: 30px; border-radius: 8px; border: 1px solid #dee2e6;">
          <div style="background-color: {color_header}; color: {color_texto_header}; padding: 15px; font-size: 18px; font-weight: bold; text-align: center; border-radius: 4px;">
            INFORME UNIFICADO DE SEGURIDAD Y TELECOMUNICACIONES
          </div>
          <h2>Auditoria de Perfil: {valores["nombre"]}</h2>
          <p><b>Linea telefonica evaluada:</b> {telefono_seguro}<br><b>Fecha y hora:</b> {generado_en}</p>

          <h3>Herramienta 1: IPQualityScore</h3>
          <p><b>Tipo de linea:</b> {valores["h1_tipo"]}<br>
             <b>Indice de riesgo:</b> {valores["h1_score"]}/100<br>
             <b>Resultado:</b> {valores["h1_msg"]}</p>

          <h3>Herramienta 2: MaxMind minFraud</h3>
          <p><b>Operador comercial de red:</b> {valores["h2_operador"]}<br>
             <b>Puntaje de riesgo financiero:</b> {valores["h2_riesgo"]}%</p>

          <h3>Herramienta 3: Resolucion geoespacial</h3>
          <p><b>Ciudad de referencia:</b> {valores["h3_ciudad"]}<br>
             <b>Coordenadas:</b> Lat: {valores["h3_lat"]}, Lon: {valores["h3_lon"]}</p>

          <h3>Herramienta 4: GSMA SIM Swap</h3>
          <p><b>Analisis de red:</b> {valores["h4_msg"]}</p>

          <h3>Herramienta 5: GSMA Device Location</h3>
          <p><b>Veredicto de infraestructura celular:</b> {valores["h5_msg"]}<br>
             <b>ID de transaccion del operador:</b> <code>{valores["tx_id"]}</code></p>

          <div style="background: #e2e3e5; padding: 20px; border-radius: 6px; margin-top: 20px; border-left: 6px solid #6c757d;">
            <h3 style="margin:0;">VEREDICTO DEL SISTEMA:</h3>
            <h2 style="margin:5px 0 0 0; color:{color_header}; font-weight:bold;">{valores["status"]}</h2>
            <p style="margin:10px 0 0 0; font-size:14px;">{valores["justificacion"]}</p>
          </div>
        </div>
      </body>
    </html>
    """

    ruta_archivo = ruta_carpeta / "reporte.html"
    ruta_archivo.write_text(contenido, encoding="utf-8")
    return ruta_archivo


def _evaluar_ipqualityscore(reporte: ReporteAntifraude) -> None:
    api_key = os.getenv("IQS_API_KEY")
    base_url = os.getenv("IQS_PHONE_VALIDATION_URL")

    if not api_key or not base_url:
        reporte.h1_msg = "Omitido: IPQualityScore no esta configurado."
        return

    try:
        response = requests.get(
            f"{base_url.rstrip('/')}/{api_key}/{reporte.telefono}",
            timeout=5.0,
        )
        response.raise_for_status()
        data = response.json()
    except requests.exceptions.RequestException:
        reporte.h1_msg = "No se pudo comprobar reputacion web por timeout o red."
        return

    if not data.get("success", False):
        reporte.h1_msg = "IPQualityScore no devolvio una verificacion exitosa."
        return

    reporte.h1_tipo = data.get("line_type", reporte.h1_tipo)
    reporte.h1_score = int(data.get("fraud_score", 0))
    reporte.h3_ciudad = data.get("city", reporte.h3_ciudad)

    if reporte.h1_tipo.lower() == "voip":
        reporte.puntos_fraude += 50
        reporte.h1_msg = "Alerta: uso de numero virtual VoIP."

    if reporte.h1_score > 75:
        reporte.puntos_fraude += 40
        reporte.h1_msg = "Alerta: linea con alto puntaje de riesgo."


def _evaluar_maxmind(reporte: ReporteAntifraude) -> None:
    account_id = os.getenv("MAXMIND_ACCOUNT_ID")
    license_key = os.getenv("MAXMIND_LICENSE_KEY")
    url = os.getenv("MAXMIND_MINFRAUD_URL")

    if not account_id or not license_key or not url:
        return

    try:
        response = requests.post(
            url,
            json={"device": {"phone_number": reporte.telefono}},
            auth=(account_id, license_key),
            timeout=5.0,
        )
        response.raise_for_status()
        data = response.json()
    except requests.exceptions.RequestException:
        return

    reporte.h2_operador = data.get("phone_number", {}).get(
        "network_operator", reporte.h2_operador
    )
    reporte.h2_riesgo = float(data.get("risk_score", 0.0))

    if reporte.h2_riesgo > 60:
        reporte.puntos_fraude += 30


def _resolver_ciudad(reporte: ReporteAntifraude) -> None:
    geocode_url = os.getenv("NOMINATIM_SEARCH_URL", "https://nominatim.openstreetmap.org/search")

    try:
        response = requests.get(
            geocode_url,
            params={"q": f"{reporte.h3_ciudad}, AR", "format": "json", "limit": 1},
            headers={"User-Agent": "appservicios-security-sandbox"},
            timeout=4.0,
        )
        response.raise_for_status()
        data = response.json()
    except requests.exceptions.RequestException:
        return

    if data:
        reporte.h3_lat = float(data[0]["lat"])
        reporte.h3_lon = float(data[0]["lon"])


def _evaluar_sim_swap(reporte: ReporteAntifraude) -> None:
    url = os.getenv("OPEN_GATEWAY_SIM_SWAP_URL")

    if not url:
        reporte.h4_msg = "Omitido: SIM Swap no esta configurado."
        return

    try:
        headers = conversor_auth_gateway.obtener_header_autorizacion()
        headers["Content-Type"] = "application/json"
        response = requests.post(
            url,
            json={"phoneNumber": reporte.telefono, "maxAge": 24},
            headers=headers,
            timeout=5.0,
        )
        response.raise_for_status()
        data = response.json()
    except (requests.exceptions.RequestException, OpenGatewayAuthError):
        reporte.h4_msg = "No se pudo comprobar SIM Swap."
        return

    if data.get("swapped") is True:
        reporte.puntos_fraude += 90
        reporte.h4_msg = "Alerta critica: SIM Swap reciente detectado."
    else:
        reporte.h4_msg = "Seguro: no se detecta SIM Swap reciente."


def _evaluar_device_location(reporte: ReporteAntifraude) -> None:
    url = os.getenv("OPEN_GATEWAY_DEVICE_LOCATION_URL")

    if not url:
        reporte.h5_msg = "Omitido: Device Location no esta configurado."
        return

    payload = {
        "device": {"phoneNumber": reporte.telefono},
        "area": {
            "areaType": "CIRCLE",
            "center": {"latitude": reporte.h3_lat, "longitude": reporte.h3_lon},
            "radius": int(os.getenv("OPEN_GATEWAY_LOCATION_RADIUS_METERS", "50000")),
        },
    }

    try:
        headers = conversor_auth_gateway.obtener_header_autorizacion()
        headers["Content-Type"] = "application/json"
        response = requests.post(url, json=payload, headers=headers, timeout=8.0)
    except (requests.exceptions.RequestException, OpenGatewayAuthError):
        reporte.h5_msg = "Servicio de celdas temporalmente inaccesible."
        return

    if response.status_code != 200:
        reporte.h5_msg = (
            f"Linea fuera de cobertura o apagada en el analisis. HTTP {response.status_code}."
        )
        return

    data = response.json()
    reporte.tx_id = data.get("transactionId", "N/A")

    if data.get("verificationResult") == "TRUE":
        reporte.h5_msg = "Conexion legitima: las antenas confirman la zona."
    else:
        reporte.puntos_fraude += 70
        reporte.h5_msg = "Alerta: la SIM opera fuera de la zona esperada."


def _calcular_veredicto(reporte: ReporteAntifraude) -> None:
    if reporte.puntos_fraude >= 80:
        reporte.status = "RECHAZADO_BLOQUEADO"
        reporte.justificacion = (
            "El motor detecto patrones de alto riesgo. Requiere bloqueo o revision "
            "segun politica interna antes de habilitar mensajeria o pagos."
        )
        return

    mensajes_revision = (reporte.h4_msg + " " + reporte.h5_msg).lower()
    if (
        "omitido" in mensajes_revision
        or "fuera de cobertura" in mensajes_revision
        or "no se pudo" in mensajes_revision
        or "inaccesible" in mensajes_revision
    ):
        reporte.status = "EN_REVISION"
        reporte.justificacion = (
            "No se pudo completar toda la validacion automatica. El perfil queda "
            "pendiente para revision manual."
        )
        return

    reporte.status = "APROBADO"


def _persistir_auditoria_si_esta_configurada(
    reporte: ReporteAntifraude,
    ruta_reporte: Path,
) -> None:
    database_url = os.getenv("DATABASE_URL")
    if not database_url:
        return

    try:
        import psycopg2
    except ImportError:
        return

    query = """
    INSERT INTO usuarios_verificados
        (nombre, telefono, compania_carrier, tipo_linea, estado_cuenta,
         telecom_transaction_id, reporte_path, creado_en)
    VALUES (%s, %s, %s, %s, %s, %s, %s, now())
    ON CONFLICT (telefono) DO UPDATE SET
        compania_carrier = EXCLUDED.compania_carrier,
        tipo_linea = EXCLUDED.tipo_linea,
        estado_cuenta = EXCLUDED.estado_cuenta,
        telecom_transaction_id = EXCLUDED.telecom_transaction_id,
        reporte_path = EXCLUDED.reporte_path;
    """

    connection = psycopg2.connect(database_url)
    try:
        with connection:
            with connection.cursor() as cursor:
                cursor.execute(
                    query,
                    (
                        reporte.nombre,
                        reporte.telefono,
                        reporte.h2_operador,
                        reporte.h1_tipo,
                        reporte.status,
                        reporte.tx_id,
                        str(ruta_reporte),
                    ),
                )
    finally:
        connection.close()


if __name__ == "__main__":
    telefono_prueba = os.getenv("SECURITY_TEST_PHONE")
    nombre_prueba = os.getenv("SECURITY_TEST_NAME", "Usuario Sandbox")

    if not telefono_prueba:
        raise SecurityBunkerError("Configura SECURITY_TEST_PHONE para ejecutar el sandbox.")

    print(json.dumps(ejecutar_bunker_antifraude(nombre_prueba, telefono_prueba), indent=2))
