-- ============================================================
-- Database/SeedAngel.sql — Seed de usuario inicial Angel
-- NOTA: Este script requiere los valores de hash y salt
--       generados por la aplicación al iniciarse por primera vez.
-- 
-- OPCIÓN RECOMENDADA: Inicia la app — se crea el usuario
--   automáticamente si la BD está vacía (Program.cs startup seed).
--
-- OPCIÓN MANUAL (si ya hay usuarios y quieres agregar Angel):
--   1. Genera el hash con la app o SecurityHelper
--   2. Reemplaza <HASH_BASE64> y <SALT_BASE64> abajo
-- ============================================================

USE LoginDB;
GO

-- Verificar si ya existe el usuario Angel
IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios WHERE NombreUsuario = 'Angel')
BEGIN
    -- Reemplaza los valores de <HASH_BASE64> y <SALT_BASE64>
    -- con los generados por SecurityHelper.HashPassword("Angel2206")
    INSERT INTO dbo.Usuarios (NombreUsuario, Email, NombreCompleto, PasswordHash, PasswordSalt, Activo)
    VALUES (
        'Angel',
        'angel@ceplan.gob.pe',
        'Angel',
        '2d+AGo2XPeDO+VjkhFdlpMqY5e9u6h14XEyQYOyTk4k=',   -- Hash PBKDF2-SHA256 + pepper "Angel2206!" (SEGURO)
        'woDA9CJ3H4EVICkwmLjtaGOfOIGyaA/u9Q2gdknUW40=',   -- Salt base64 aleatorio (SEGURO)
        1
    );
    PRINT 'Usuario Angel insertado correctamente.';
END
ELSE
BEGIN
    PRINT 'El usuario Angel ya existe. No se realizaron cambios.';
END
GO
