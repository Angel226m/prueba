#!/bin/bash
# ============================================================
# entrypoint-sql.sh — SQL Server con inicialización optimizada
# Reduce tiempo de arranque y ejecuta scripts automáticamente
# ============================================================

# Inicia SQL Server en segundo plano
echo "Iniciando SQL Server..."
/opt/mssql/bin/sqlservr &

# Esperar que SQL Server esté listo (optimizado: menos tiempo entre reintentos)
echo "Esperando que SQL Server arranque..."
for i in {1..20}; do
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourPass123!" -No -Q "SELECT 1" > /dev/null 2>&1
  if [ $? -eq 0 ]; then
    echo "SQL Server listo. Ejecutando scripts..."

    # Ejecutar script de creación de BD en paralelo para mayor velocidad
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourPass123!" -No -i /scripts/LoginDB.sql

    echo "============================================================"
    echo "LoginDB creada correctamente."
    echo "El usuario Angel se crea automaticamente al iniciar la app."
    echo "O ejecuta Database/SeedAngel.sql con los valores generados."
    echo "Accede a http://localhost:8080/Account/Setup para verificar."
    echo "============================================================"
    break
  fi
  echo "Intento $i/20 — no está listo, esperando 2s..."
  sleep 2
done

# Mantiene el contenedor SQL Server vivo
wait