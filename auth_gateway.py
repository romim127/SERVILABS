"""
Autenticacion OAuth2 para Telefonica Open Gateway / CAMARA.

Este modulo obtiene un Bearer Token con Client Credentials y lo mantiene cacheado
en memoria. Solo solicita un token nuevo cuando el actual vence o esta cerca de
vencer, evitando llamadas innecesarias al servidor de autenticacion.
"""

from __future__ import annotations

import os
import time

import requests
from dotenv import load_dotenv


load_dotenv()


class OpenGatewayAuthError(RuntimeError):
    """Error controlado durante la autenticacion con Open Gateway."""


class GestorTokenOpenGateway:
    def __init__(self) -> None:
        self.auth_url = os.getenv("OPEN_GATEWAY_AUTH_URL")
        self.client_id = os.getenv("OPEN_GATEWAY_CLIENT_ID")
        self.client_secret = os.getenv("OPEN_GATEWAY_CLIENT_SECRET")
        self.scope = os.getenv("OPEN_GATEWAY_SCOPE")
        self.audience = os.getenv("OPEN_GATEWAY_AUDIENCE")
        self.timeout_seconds = float(os.getenv("OPEN_GATEWAY_AUTH_TIMEOUT_SECONDS", "5.0"))
        self.margen_seguridad_seconds = int(
            os.getenv("OPEN_GATEWAY_TOKEN_MARGIN_SECONDS", "60")
        )

        self.token_actual: str | None = None
        self.timestamp_expiracion = 0.0

    def obtener_token_valido(self) -> str:
        """
        Devuelve un token vigente.

        Si el token en memoria expiro o esta cerca de expirar, solicita uno nuevo
        mediante OAuth2 Client Credentials.
        """

        tiempo_actual = time.time()

        if (
            self.token_actual
            and tiempo_actual < self.timestamp_expiracion - self.margen_seguridad_seconds
        ):
            return self.token_actual

        self._validar_configuracion()

        payload = {
            "grant_type": "client_credentials",
            "client_id": self.client_id,
            "client_secret": self.client_secret,
        }

        if self.scope:
            payload["scope"] = self.scope

        if self.audience:
            payload["audience"] = self.audience

        headers = {
            "Accept": "application/json",
            "Content-Type": "application/x-www-form-urlencoded",
        }

        try:
            respuesta = requests.post(
                self.auth_url,
                data=payload,
                headers=headers,
                timeout=self.timeout_seconds,
            )
            respuesta.raise_for_status()
        except requests.exceptions.HTTPError as error:
            detalle = error.response.text if error.response is not None else ""
            raise OpenGatewayAuthError(
                f"Error de autenticacion OAuth2. HTTP {respuesta.status_code}. {detalle}"
            ) from error
        except requests.exceptions.RequestException as error:
            raise OpenGatewayAuthError(
                f"Fallo de red al conectar con Open Gateway: {error}"
            ) from error

        datos_token = respuesta.json()
        access_token = datos_token.get("access_token")

        if not access_token:
            raise OpenGatewayAuthError("Open Gateway no devolvio access_token.")

        segundos_vida = int(datos_token.get("expires_in", 3600))
        if segundos_vida <= self.margen_seguridad_seconds:
            raise OpenGatewayAuthError("El expires_in recibido es demasiado corto.")

        self.token_actual = access_token
        self.timestamp_expiracion = time.time() + segundos_vida

        return self.token_actual

    def obtener_header_autorizacion(self) -> dict[str, str]:
        """Devuelve el header Authorization listo para consumir APIs CAMARA."""

        return {"Authorization": f"Bearer {self.obtener_token_valido()}"}

    def limpiar_cache(self) -> None:
        """Limpia el token en memoria para forzar una nueva autenticacion."""

        self.token_actual = None
        self.timestamp_expiracion = 0.0

    def _validar_configuracion(self) -> None:
        faltantes = [
            nombre
            for nombre, valor in {
                "OPEN_GATEWAY_AUTH_URL": self.auth_url,
                "OPEN_GATEWAY_CLIENT_ID": self.client_id,
                "OPEN_GATEWAY_CLIENT_SECRET": self.client_secret,
            }.items()
            if not valor
        ]

        if faltantes:
            raise OpenGatewayAuthError(
                "Faltan variables de entorno para Open Gateway: "
                + ", ".join(faltantes)
            )


# Instancia global unica para usar en todo el proyecto.
conversor_auth_gateway = GestorTokenOpenGateway()
