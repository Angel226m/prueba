#!/usr/bin/env python3
# ============================================================
# hash_gen.py — Generador de hashes CEPLAN
# Compatible con SecurityHelper.cs v1.3.0
# PBKDF2-SHA256 con 150,000 iteraciones y pepper de aplicación
# ============================================================

import hashlib
import os
import base64
import sys

# Configuración que debe coincidir con SecurityHelper.cs
ITERATIONS = 150_000
SALT_BYTES = 32
HASH_BYTES = 32
APPLICATION_PEPPER = "CEPLAN_2024_SECURE_PEPPER_V1"

def generate_hash(password: str, use_pepper: bool = True) -> tuple[str, str]:
    """
    Genera hash PBKDF2-SHA256 compatible con SecurityHelper.cs

    Args:
        password: Contraseña en texto plano
        use_pepper: Si usar pepper (True para nuevo formato, False para legacy)

    Returns:
        Tupla con (hash_base64, salt_base64)
    """
    # Generar salt criptográficamente seguro
    salt = os.urandom(SALT_BYTES)

    # Aplicar pepper si es necesario (formato nuevo)
    password_bytes = password.encode('utf-8')
    if use_pepper:
        password_with_pepper = password + APPLICATION_PEPPER
        password_bytes = password_with_pepper.encode('utf-8')

    # Generar hash PBKDF2-SHA256
    hash_bytes = hashlib.pbkdf2_hmac(
        'sha256',
        password_bytes,
        salt,
        ITERATIONS,
        dklen=HASH_BYTES
    )

    # Convertir a base64
    hash_base64 = base64.b64encode(hash_bytes).decode('utf-8')
    salt_base64 = base64.b64encode(salt).decode('utf-8')

    return hash_base64, salt_base64

def main():
    """Función principal del generador de hashes"""

    # Obtener contraseña desde argumentos o input interactivo
    if len(sys.argv) > 1:
        password = sys.argv[1]
    else:
        password = input("Ingrese la contraseña: ")

    if not password:
        print("Error: Contraseña no puede estar vacía")
        sys.exit(1)

    # Generar hash nuevo (con pepper)
    print("=== HASH NUEVO (con pepper) ===")
    hash_nuevo, salt_nuevo = generate_hash(password, use_pepper=True)
    print(f"HASH={hash_nuevo}")
    print(f"SALT={salt_nuevo}")

    print("\n=== HASH LEGACY (sin pepper) ===")
    hash_legacy, salt_legacy = generate_hash(password, use_pepper=False)
    print(f"HASH={hash_legacy}")
    print(f"SALT={salt_legacy}")

    print(f"\n=== CONFIGURACIÓN APLICADA ===")
    print(f"Iteraciones: {ITERATIONS:,}")
    print(f"Salt bytes: {SALT_BYTES}")
    print(f"Hash bytes: {HASH_BYTES}")
    print(f"Pepper: {'SÍ' if True else 'NO'} (formato nuevo)")

    # Generar SQL para inserción directa
    print(f"\n=== SQL DE INSERCIÓN (NUEVO) ===")
    print(f"INSERT INTO Usuarios (NombreUsuario, Email, HashContrasena, Salt, ...)")
    print(f"VALUES ('[usuario]', '[email]', '{hash_nuevo}', '{salt_nuevo}', ...);")

if __name__ == "__main__":
    main()
