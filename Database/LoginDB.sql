-- ============================================================
-- Database/LoginDB.sql — Base de datos CEPLAN LoginApp
-- Crea: LoginDB, tablas Usuarios/LogsAcceso/Mensajes,
--       índices de rendimiento y 6 stored procedures.
-- OWASP: A02 (hash+salt), A09 (auditoría), datos parametrizados
-- Ejecutar: sqlcmd -S localhost -U sa -P YourPass123! -i LoginDB.sql
-- ============================================================

USE master;
GO

-- Crear BD si no existe
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'LoginDB')
BEGIN
    CREATE DATABASE LoginDB;
    PRINT 'Base de datos LoginDB creada.';
END
GO

USE LoginDB;
GO

-- ============================================================
-- Tablas (drop en orden correcto por FK)
-- ============================================================
IF OBJECT_ID('dbo.Mensajes',   'U') IS NOT NULL DROP TABLE dbo.Mensajes;
IF OBJECT_ID('dbo.LogsAcceso', 'U') IS NOT NULL DROP TABLE dbo.LogsAcceso;
IF OBJECT_ID('dbo.Usuarios',   'U') IS NOT NULL DROP TABLE dbo.Usuarios;
GO

-- ── Usuarios ──────────────────────────────────────────────────
CREATE TABLE dbo.Usuarios (
    Id                INT            IDENTITY(1,1) PRIMARY KEY,
    NombreUsuario     NVARCHAR(50)   NOT NULL,
    Email             NVARCHAR(100)  NOT NULL,
    NombreCompleto    NVARCHAR(150)  NOT NULL DEFAULT '',
    PasswordHash      NVARCHAR(512)  NOT NULL,   -- PBKDF2-SHA256 base64
    PasswordSalt      NVARCHAR(256)  NOT NULL,   -- Salt aleatorio base64
    IntentosFallidos  INT            NOT NULL DEFAULT 0,
    BloqueadoHasta    DATETIME       NULL,        -- NULL = no bloqueado
    FechaCreacion     DATETIME       NOT NULL DEFAULT GETDATE(),
    UltimoAcceso      DATETIME       NULL,
    Activo            BIT            NOT NULL DEFAULT 1,
    CONSTRAINT UQ_NombreUsuario UNIQUE (NombreUsuario),
    CONSTRAINT UQ_Email         UNIQUE (Email)
);
GO

-- ── Auditoría de accesos (OWASP A09) ─────────────────────────
CREATE TABLE dbo.LogsAcceso (
    Id             INT           IDENTITY(1,1) PRIMARY KEY,
    NombreUsuario  NVARCHAR(50)  NOT NULL,
    Accion         NVARCHAR(50)  NOT NULL,  -- LOGIN_OK, LOGIN_FAIL, LOGOUT, etc.
    DireccionIP    NVARCHAR(45)  NULL,
    UserAgent      NVARCHAR(512) NULL,
    Fecha          DATETIME      NOT NULL DEFAULT GETDATE()
);
GO

-- ── Mensajes internos ─────────────────────────────────────────
CREATE TABLE dbo.Mensajes (
    Id             INT            IDENTITY(1,1) PRIMARY KEY,
    RemitenteId    INT            NOT NULL,
    DestinatarioId INT            NOT NULL,
    Asunto         NVARCHAR(200)  NOT NULL,
    Cuerpo         NVARCHAR(4000) NOT NULL,
    FechaEnvio     DATETIME       NOT NULL DEFAULT GETDATE(),
    Leido          BIT            NOT NULL DEFAULT 0,
    LeidoFecha     DATETIME       NULL,
    CONSTRAINT FK_Msj_Remitente    FOREIGN KEY (RemitenteId)    REFERENCES dbo.Usuarios(Id),
    CONSTRAINT FK_Msj_Destinatario FOREIGN KEY (DestinatarioId) REFERENCES dbo.Usuarios(Id)
);
GO

-- ============================================================
-- Índices de rendimiento
-- ============================================================
CREATE INDEX IX_Usuarios_Email        ON dbo.Usuarios(Email);
CREATE INDEX IX_LogsAcceso_Fecha      ON dbo.LogsAcceso(Fecha);
CREATE INDEX IX_LogsAcceso_Usuario    ON dbo.LogsAcceso(NombreUsuario);
CREATE INDEX IX_Mensajes_Destinatario ON dbo.Mensajes(DestinatarioId, Leido);
CREATE INDEX IX_Mensajes_Remitente    ON dbo.Mensajes(RemitenteId);
GO

-- ============================================================
-- Stored Procedures — Mensajería interna
-- ============================================================

-- SP1: Bandeja de entrada
IF OBJECT_ID('dbo.usp_MsjBandejaEntrada','P') IS NOT NULL DROP PROCEDURE dbo.usp_MsjBandejaEntrada;
GO
CREATE PROCEDURE dbo.usp_MsjBandejaEntrada @DestinatarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.Id, m.RemitenteId,
           ur.NombreUsuario  AS RemitenteUsuario,
           ISNULL(NULLIF(ur.NombreCompleto,''), ur.NombreUsuario) AS RemitenteNombre,
           ur.Email          AS RemitenteEmail,
           m.DestinatarioId,
           ud.NombreUsuario  AS DestinatarioUsuario,
           ISNULL(NULLIF(ud.NombreCompleto,''), ud.NombreUsuario) AS DestinatarioNombre,
           ud.Email          AS DestinatarioEmail,
           m.Asunto, LEFT(m.Cuerpo, 120) AS CuerpoPreview,
           m.FechaEnvio, m.Leido, m.LeidoFecha
    FROM dbo.Mensajes m
    INNER JOIN dbo.Usuarios ur ON ur.Id = m.RemitenteId
    INNER JOIN dbo.Usuarios ud ON ud.Id = m.DestinatarioId
    WHERE m.DestinatarioId = @DestinatarioId
    ORDER BY m.FechaEnvio DESC;
END
GO

-- SP2: Mensajes enviados
IF OBJECT_ID('dbo.usp_MsjEnviados','P') IS NOT NULL DROP PROCEDURE dbo.usp_MsjEnviados;
GO
CREATE PROCEDURE dbo.usp_MsjEnviados @RemitenteId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.Id, m.RemitenteId,
           ur.NombreUsuario AS RemitenteUsuario,
           ISNULL(NULLIF(ur.NombreCompleto,''), ur.NombreUsuario) AS RemitenteNombre,
           ur.Email         AS RemitenteEmail,
           m.DestinatarioId,
           ud.NombreUsuario AS DestinatarioUsuario,
           ISNULL(NULLIF(ud.NombreCompleto,''), ud.NombreUsuario) AS DestinatarioNombre,
           ud.Email         AS DestinatarioEmail,
           m.Asunto, LEFT(m.Cuerpo, 120) AS CuerpoPreview,
           m.FechaEnvio, m.Leido, m.LeidoFecha
    FROM dbo.Mensajes m
    INNER JOIN dbo.Usuarios ur ON ur.Id = m.RemitenteId
    INNER JOIN dbo.Usuarios ud ON ud.Id = m.DestinatarioId
    WHERE m.RemitenteId = @RemitenteId
    ORDER BY m.FechaEnvio DESC;
END
GO

-- SP3: Detalle (marca como leído si es destinatario)
IF OBJECT_ID('dbo.usp_MsjDetalle','P') IS NOT NULL DROP PROCEDURE dbo.usp_MsjDetalle;
GO
CREATE PROCEDURE dbo.usp_MsjDetalle @MensajeId INT, @UsuarioActualId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Mensajes
    SET Leido = 1, LeidoFecha = GETDATE()
    WHERE Id = @MensajeId AND DestinatarioId = @UsuarioActualId AND Leido = 0;

    SELECT m.Id, m.RemitenteId,
           ur.NombreUsuario AS RemitenteUsuario,
           ISNULL(NULLIF(ur.NombreCompleto,''), ur.NombreUsuario) AS RemitenteNombre,
           ur.Email         AS RemitenteEmail,
           m.DestinatarioId,
           ud.NombreUsuario AS DestinatarioUsuario,
           ISNULL(NULLIF(ud.NombreCompleto,''), ud.NombreUsuario) AS DestinatarioNombre,
           ud.Email         AS DestinatarioEmail,
           m.Asunto, m.Cuerpo, m.Cuerpo AS CuerpoPreview,
           m.FechaEnvio, m.Leido, m.LeidoFecha
    FROM dbo.Mensajes m
    INNER JOIN dbo.Usuarios ur ON ur.Id = m.RemitenteId
    INNER JOIN dbo.Usuarios ud ON ud.Id = m.DestinatarioId
    WHERE m.Id = @MensajeId
      AND (m.RemitenteId = @UsuarioActualId OR m.DestinatarioId = @UsuarioActualId);
END
GO

-- SP4: Crear mensaje
IF OBJECT_ID('dbo.usp_MsjCrear','P') IS NOT NULL DROP PROCEDURE dbo.usp_MsjCrear;
GO
CREATE PROCEDURE dbo.usp_MsjCrear
    @RemitenteId INT, @DestinatarioId INT,
    @Asunto NVARCHAR(200), @Cuerpo NVARCHAR(4000)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Mensajes (RemitenteId, DestinatarioId, Asunto, Cuerpo)
    VALUES (@RemitenteId, @DestinatarioId, @Asunto, @Cuerpo);
    SELECT SCOPE_IDENTITY() AS NuevoId;
END
GO

-- SP5: Contar no leídos
IF OBJECT_ID('dbo.usp_MsjContarNoLeidos','P') IS NOT NULL DROP PROCEDURE dbo.usp_MsjContarNoLeidos;
GO
CREATE PROCEDURE dbo.usp_MsjContarNoLeidos @DestinatarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(1) FROM dbo.Mensajes
    WHERE DestinatarioId = @DestinatarioId AND Leido = 0;
END
GO

-- SP6: Listar usuarios activos (para select de destinatario)
IF OBJECT_ID('dbo.usp_UsuariosActivos','P') IS NOT NULL DROP PROCEDURE dbo.usp_UsuariosActivos;
GO
CREATE PROCEDURE dbo.usp_UsuariosActivos @ExcluirId INT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, NombreUsuario,
           ISNULL(NULLIF(NombreCompleto,''), NombreUsuario) AS NombreCompleto,
           Email
    FROM dbo.Usuarios
    WHERE Activo = 1 AND Id <> @ExcluirId
    ORDER BY NombreUsuario;
END
GO

-- ============================================================
-- Fin del script
-- ============================================================
PRINT '============================================================';
PRINT 'LoginDB creada correctamente.';
PRINT 'El usuario Angel se crea automaticamente al iniciar la app.';
PRINT 'O ejecuta Database/SeedAngel.sql con los valores generados.';
PRINT 'Accede a http://localhost:8080/Account/Setup para verificar.';
PRINT '============================================================';
GO
