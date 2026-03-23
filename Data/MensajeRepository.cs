// ============================================================
// Data/IMensajeRepository.cs — Interfaz repositorio mensajes
// Data/MensajeRepository.cs  — Implementación con SQL Server
// Stored Procedures parametrizados — OWASP A03
// ============================================================
using Microsoft.Data.SqlClient;
using System.Data;
using LoginAppCore.Models;

namespace LoginAppCore.Data;

// ── Interfaz ──────────────────────────────────────────────────
public interface IMensajeRepository
{
    Task<IEnumerable<Mensaje>>    ObtenerBandejaEntradaAsync(int destinatarioId);
    Task<IEnumerable<Mensaje>>    ObtenerEnviadosAsync(int remitenteId);
    Task<Mensaje?>                ObtenerDetalleAsync(int mensajeId, int usuarioActualId);
    Task<int>                     CrearAsync(int remitenteId, int destinatarioId, string asunto, string cuerpo);
    Task<int>                     ContarNoLeidosAsync(int destinatarioId);
    Task<IEnumerable<UsuarioItem>> ListarUsuariosAsync(int excluirId);
}

// ── Implementación ────────────────────────────────────────────
public class MensajeRepository : IMensajeRepository
{
    private readonly string _connStr;

    public MensajeRepository(IConfiguration config)
        => _connStr = config.GetConnectionString("LoginDB")!;

    public async Task<IEnumerable<Mensaje>> ObtenerBandejaEntradaAsync(int destinatarioId)
    {
        var list = new List<Mensaje>();
        await using var cn  = new SqlConnection(_connStr);
        await using var cmd = new SqlCommand("dbo.usp_MsjBandejaEntrada", cn)
            { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.Add("@DestinatarioId", SqlDbType.Int).Value = destinatarioId;
        await cn.OpenAsync();
        await using var dr = await cmd.ExecuteReaderAsync();
        while (await dr.ReadAsync()) list.Add(MapBandeja(dr));
        return list;
    }

    public async Task<IEnumerable<Mensaje>> ObtenerEnviadosAsync(int remitenteId)
    {
        var list = new List<Mensaje>();
        await using var cn  = new SqlConnection(_connStr);
        await using var cmd = new SqlCommand("dbo.usp_MsjEnviados", cn)
            { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.Add("@RemitenteId", SqlDbType.Int).Value = remitenteId;
        await cn.OpenAsync();
        await using var dr = await cmd.ExecuteReaderAsync();
        while (await dr.ReadAsync()) list.Add(MapBandeja(dr));
        return list;
    }

    public async Task<Mensaje?> ObtenerDetalleAsync(int mensajeId, int usuarioActualId)
    {
        await using var cn  = new SqlConnection(_connStr);
        await using var cmd = new SqlCommand("dbo.usp_MsjDetalle", cn)
            { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.Add("@MensajeId",      SqlDbType.Int).Value = mensajeId;
        cmd.Parameters.Add("@UsuarioActualId", SqlDbType.Int).Value = usuarioActualId;
        await cn.OpenAsync();
        await using var dr = await cmd.ExecuteReaderAsync();
        if (await dr.ReadAsync()) return MapDetalle(dr);
        return null;
    }

    public async Task<int> CrearAsync(int remitenteId, int destinatarioId, string asunto, string cuerpo)
    {
        await using var cn  = new SqlConnection(_connStr);
        await using var cmd = new SqlCommand("dbo.usp_MsjCrear", cn)
            { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.Add("@RemitenteId",    SqlDbType.Int).Value            = remitenteId;
        cmd.Parameters.Add("@DestinatarioId", SqlDbType.Int).Value            = destinatarioId;
        cmd.Parameters.Add("@Asunto",         SqlDbType.NVarChar, 200).Value  = asunto;
        cmd.Parameters.Add("@Cuerpo",         SqlDbType.NVarChar, 4000).Value = cuerpo;
        await cn.OpenAsync();
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<int> ContarNoLeidosAsync(int destinatarioId)
    {
        await using var cn  = new SqlConnection(_connStr);
        await using var cmd = new SqlCommand("dbo.usp_MsjContarNoLeidos", cn)
            { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.Add("@DestinatarioId", SqlDbType.Int).Value = destinatarioId;
        await cn.OpenAsync();
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<IEnumerable<UsuarioItem>> ListarUsuariosAsync(int excluirId)
    {
        var list = new List<UsuarioItem>();
        await using var cn  = new SqlConnection(_connStr);
        await using var cmd = new SqlCommand("dbo.usp_UsuariosActivos", cn)
            { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.Add("@ExcluirId", SqlDbType.Int).Value = excluirId;
        await cn.OpenAsync();
        await using var dr = await cmd.ExecuteReaderAsync();
        while (await dr.ReadAsync())
            list.Add(new UsuarioItem
            {
                Id             = dr.GetInt32(0),
                NombreUsuario  = dr.GetString(1),
                NombreCompleto = dr.GetString(2),
                Email          = dr.GetString(3)
            });
        return list;
    }

    private static Mensaje MapBandeja(SqlDataReader dr) => new()
    {
        Id                  = dr.GetInt32(0),
        RemitenteId         = dr.GetInt32(1),
        RemitenteUsuario    = dr.GetString(2),
        RemitenteNombre     = dr.GetString(3),
        RemitenteEmail      = dr.GetString(4),
        DestinatarioId      = dr.GetInt32(5),
        DestinatarioUsuario = dr.GetString(6),
        DestinatarioNombre  = dr.GetString(7),
        DestinatarioEmail   = dr.GetString(8),
        Asunto              = dr.GetString(9),
        CuerpoPreview       = dr.GetString(10),
        FechaEnvio          = dr.GetDateTime(11),
        Leido               = dr.GetBoolean(12),
        LeidoFecha          = dr.IsDBNull(13) ? null : dr.GetDateTime(13)
    };

    private static Mensaje MapDetalle(SqlDataReader dr) => new()
    {
        Id                  = dr.GetInt32(0),
        RemitenteId         = dr.GetInt32(1),
        RemitenteUsuario    = dr.GetString(2),
        RemitenteNombre     = dr.GetString(3),
        RemitenteEmail      = dr.GetString(4),
        DestinatarioId      = dr.GetInt32(5),
        DestinatarioUsuario = dr.GetString(6),
        DestinatarioNombre  = dr.GetString(7),
        DestinatarioEmail   = dr.GetString(8),
        Asunto              = dr.GetString(9),
        Cuerpo              = dr.GetString(10),
        CuerpoPreview       = dr.GetString(11),
        FechaEnvio          = dr.GetDateTime(12),
        Leido               = dr.GetBoolean(13),
        LeidoFecha          = dr.IsDBNull(14) ? null : dr.GetDateTime(14)
    };
}
