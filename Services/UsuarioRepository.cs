// ============================================================
// Services/UsuarioRepository.cs — Repositorio de usuarios
// Todas las queries son parametrizadas — OWASP A03
// Incluye: auth, bloqueo, logs de auditoría (A09), stats
// ============================================================
using Microsoft.Data.SqlClient;
using System.Data;
using LoginAppCore.Models;

namespace LoginAppCore.Services;

public class UsuarioRepository
{
    private readonly string _connStr;
    private readonly int    _maxIntentos;
    private readonly int    _lockoutMin;

    public UsuarioRepository(IConfiguration config)
    {
        _connStr     = config.GetConnectionString("LoginDB")!;
        _maxIntentos = config.GetValue<int>("AppSettings:MaxIntentosFallidos", 4);
        _lockoutMin  = config.GetValue<int>("AppSettings:LockoutDurationMinutes", 20);
    }

    // ── Buscar por usuario o email ────────────────────────────
    public Usuario? ObtenerPorCredencial(string credencial)
    {
        const string sql = @"
            SELECT Id, NombreUsuario, Email, PasswordHash, PasswordSalt,
                   IntentosFallidos, BloqueadoHasta, FechaCreacion, UltimoAcceso, Activo
            FROM   dbo.Usuarios
            WHERE  (NombreUsuario = @Cred OR Email = @Cred)";

        using var cn  = new SqlConnection(_connStr);
        using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@Cred", SqlDbType.NVarChar, 100).Value = credencial;
        cn.Open();
        using var dr = cmd.ExecuteReader(CommandBehavior.SingleRow);
        return dr.Read() ? MapUsuario(dr) : null;
    }

    // ── Registrar intento fallido + bloqueo automático ────────
    public void RegistrarIntentoFallido(int usuarioId)
    {
        const string sql = @"
            UPDATE dbo.Usuarios
            SET    IntentosFallidos = IntentosFallidos + 1,
                   BloqueadoHasta  = CASE
                       WHEN IntentosFallidos + 1 > @Max
                       THEN DATEADD(MINUTE, @Lock, GETUTCDATE())
                       ELSE BloqueadoHasta
                   END
            WHERE  Id = @Id";

        using var cn  = new SqlConnection(_connStr);
        using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@Id",   SqlDbType.Int).Value = usuarioId;
        cmd.Parameters.Add("@Max",  SqlDbType.Int).Value = _maxIntentos;
        cmd.Parameters.Add("@Lock", SqlDbType.Int).Value = _lockoutMin;
        cn.Open();
        cmd.ExecuteNonQuery();
    }

    // ── Resetear contadores al acceso exitoso ─────────────────
    public void RegistrarAccesoExitoso(int usuarioId)
    {
        const string sql = @"
            UPDATE dbo.Usuarios
            SET    IntentosFallidos = 0,
                   BloqueadoHasta  = NULL,
                   UltimoAcceso    = GETUTCDATE()
            WHERE  Id = @Id";

        using var cn  = new SqlConnection(_connStr);
        using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@Id", SqlDbType.Int).Value = usuarioId;
        cn.Open();
        cmd.ExecuteNonQuery();
    }

    // ── Auditoría de accesos (OWASP A09) ─────────────────────
    public void RegistrarLog(string nombreUsuario, string accion, string? ip, string? ua)
    {
        const string sql = @"
            INSERT INTO dbo.LogsAcceso (NombreUsuario, Accion, DireccionIP, UserAgent)
            VALUES (@Usuario, @Accion, @IP, @UA)";
        try
        {
            using var cn  = new SqlConnection(_connStr);
            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.Add("@Usuario", SqlDbType.NVarChar, 50).Value  = nombreUsuario;
            cmd.Parameters.Add("@Accion",  SqlDbType.NVarChar, 50).Value  = accion;
            cmd.Parameters.Add("@IP",      SqlDbType.NVarChar, 45).Value  = (object?)ip ?? DBNull.Value;
            cmd.Parameters.Add("@UA",      SqlDbType.NVarChar, 512).Value = (object?)ua ?? DBNull.Value;
            cn.Open();
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            // La auditoría nunca debe tumbar la aplicación
            Console.Error.WriteLine($"[LOG_ERROR] {ex.Message}");
        }
    }

    // ── Setup inicial ─────────────────────────────────────────
    public bool ExistenUsuarios()
    {
        using var cn  = new SqlConnection(_connStr);
        using var cmd = new SqlCommand("SELECT COUNT(1) FROM dbo.Usuarios", cn);
        cn.Open();
        return (int)cmd.ExecuteScalar()! > 0;
    }

    public void CrearUsuario(string nombreUsuario, string email, string password, string nombreCompleto = "")
    {
        var (hash, salt) = SecurityHelper.HashPassword(password);
        const string sql = @"
            INSERT INTO dbo.Usuarios (NombreUsuario, Email, NombreCompleto, PasswordHash, PasswordSalt)
            VALUES (@Usuario, @Email, @NombreCompleto, @Hash, @Salt)";

        using var cn  = new SqlConnection(_connStr);
        using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@Usuario",       SqlDbType.NVarChar, 50).Value  = nombreUsuario;
        cmd.Parameters.Add("@Email",         SqlDbType.NVarChar, 100).Value = email;
        cmd.Parameters.Add("@NombreCompleto",SqlDbType.NVarChar, 150).Value = nombreCompleto;
        cmd.Parameters.Add("@Hash",          SqlDbType.NVarChar, 512).Value = hash;
        cmd.Parameters.Add("@Salt",          SqlDbType.NVarChar, 256).Value = salt;
        cn.Open();
        cmd.ExecuteNonQuery();
    }

    // ── Seed automático al iniciar la aplicación ──────────────
    public void SeedSiVacio()
    {
        if (ExistenUsuarios()) return;
        try
        {
            CrearUsuario("Angel", "angel@ceplan.gob.pe", "Angel2206!", "Angel");
            Console.WriteLine("[SEED] Usuario 'Angel' creado exitosamente.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SEED_ERROR] {ex.Message}");
        }
    }

    // ── Estadísticas del dashboard ────────────────────────────
    public EstadisticasHoy ObtenerEstadisticasHoy()
    {
        const string sql = @"
            SELECT
                (SELECT COUNT(1) FROM dbo.LogsAcceso
                 WHERE Accion = 'LOGIN_OK'
                   AND CAST(Fecha AS DATE) = CAST(GETDATE() AS DATE)),
                (SELECT COUNT(1) FROM dbo.LogsAcceso
                 WHERE Accion = 'LOGIN_FAIL'
                   AND CAST(Fecha AS DATE) = CAST(GETDATE() AS DATE)),
                (SELECT COUNT(1) FROM dbo.Usuarios WHERE Activo = 1)";

        using var cn  = new SqlConnection(_connStr);
        using var cmd = new SqlCommand(sql, cn);
        cn.Open();
        using var dr = cmd.ExecuteReader(CommandBehavior.SingleRow);
        if (!dr.Read()) return new EstadisticasHoy();
        return new EstadisticasHoy
        {
            AccesosOk        = dr.GetInt32(0),
            IntentosFallidos = dr.GetInt32(1),
            TotalUsuarios    = dr.GetInt32(2)
        };
    }

    private static Usuario MapUsuario(SqlDataReader dr) => new()
    {
        Id               = dr.GetInt32(0),
        NombreUsuario    = dr.GetString(1),
        Email            = dr.GetString(2),
        PasswordHash     = dr.GetString(3),
        PasswordSalt     = dr.GetString(4),
        IntentosFallidos = dr.GetInt32(5),
        BloqueadoHasta   = dr.IsDBNull(6)  ? null : dr.GetDateTime(6),
        FechaCreacion    = dr.GetDateTime(7),
        UltimoAcceso     = dr.IsDBNull(8)  ? null : dr.GetDateTime(8),
        Activo           = dr.GetBoolean(9)
    };
}
