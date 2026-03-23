// ============================================================
// Models/Mensaje.cs — Modelo de mensajes internos CEPLAN
// Usado por: MensajesController, MensajeRepository
// Implementa sistema de mensajería interna con validaciones
// ============================================================
using System.ComponentModel.DataAnnotations;

namespace LoginAppCore.Models;

/// <summary>
/// Entidad de mensaje para el sistema de mensajería interna de CEPLAN
/// Permite comunicación entre usuarios del sistema
/// </summary>
public class Mensaje
{
    /// <summary>Identificador único del mensaje</summary>
    public int Id { get; set; }

    /// <summary>ID del usuario remitente</summary>
    public int RemitenteId { get; set; }

    /// <summary>Nombre completo del remitente (desnormalizado para performance)</summary>
    public string RemitenteNombre { get; set; } = string.Empty;

    /// <summary>Nombre de usuario del remitente</summary>
    public string RemitenteUsuario { get; set; } = string.Empty;

    /// <summary>Email del remitente</summary>
    public string RemitenteEmail { get; set; } = string.Empty;

    /// <summary>ID del usuario destinatario</summary>
    public int DestinatarioId { get; set; }

    /// <summary>Nombre completo del destinatario (desnormalizado para performance)</summary>
    public string DestinatarioNombre { get; set; } = string.Empty;

    /// <summary>Nombre de usuario del destinatario</summary>
    public string DestinatarioUsuario { get; set; } = string.Empty;

    /// <summary>Email del destinatario</summary>
    public string DestinatarioEmail { get; set; } = string.Empty;

    /// <summary>Asunto del mensaje (máximo 200 caracteres)</summary>
    public string Asunto { get; set; } = string.Empty;

    /// <summary>Cuerpo completo del mensaje (máximo 4000 caracteres)</summary>
    public string Cuerpo { get; set; } = string.Empty;

    /// <summary>Vista previa del mensaje para listados (primeros 150 caracteres)</summary>
    public string CuerpoPreview { get; set; } = string.Empty;

    /// <summary>Fecha y hora de envío del mensaje (UTC)</summary>
    public DateTime FechaEnvio { get; set; }

    /// <summary>Indica si el destinatario ha leído el mensaje</summary>
    public bool Leido { get; set; }

    /// <summary>Fecha y hora en que se marcó como leído (UTC)</summary>
    public DateTime? LeidoFecha { get; set; }
}

// ── ViewModels de mensajería ──────────────────────────────────
/// <summary>
/// ViewModel para el formulario de creación de mensajes
/// Incluye validaciones del lado del cliente y servidor
/// </summary>
public class CrearMensajeViewModel
{
    /// <summary>ID del usuario destinatario seleccionado</summary>
    [Required(ErrorMessage = "Selecciona un destinatario.")]
    public int DestinatarioId { get; set; }

    /// <summary>Asunto del mensaje</summary>
    [Required(ErrorMessage = "El asunto es obligatorio.")]
    [MaxLength(200)] public string Asunto { get; set; } = string.Empty;

    /// <summary>Cuerpo del mensaje</summary>
    [Required(ErrorMessage = "El mensaje es obligatorio.")]
    [MaxLength(4000)] public string Cuerpo { get; set; } = string.Empty;

    /// <summary>Lista de usuarios disponibles para seleccionar como destinatarios</summary>
    public List<UsuarioItem> Usuarios { get; set; } = new();
}

/// <summary>
/// Información básica de usuario para listas de selección
/// Optimizado para dropdowns y listas de destinatarios
/// </summary>
public class UsuarioItem
{
    /// <summary>ID del usuario</summary>
    public int Id { get; set; }

    /// <summary>Nombre completo para mostrar</summary>
    public string NombreCompleto { get; set; } = string.Empty;

    /// <summary>Nombre de usuario</summary>
    public string NombreUsuario { get; set; } = string.Empty;

    /// <summary>Email del usuario</summary>
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel para la visualización de la bandeja de mensajes
/// Puede representar entrada, enviados u otra vista de mensajes
/// </summary>
public class BandejaViewModel
{
    /// <summary>Lista de mensajes a mostrar en la bandeja</summary>
    public List<Mensaje> Mensajes { get; set; } = new();

    /// <summary>Contador de mensajes no leídos del usuario</summary>
    public int NoLeidos { get; set; }

    /// <summary>Tipo de vista: "entrada", "enviados", etc.</summary>
    public string Vista { get; set; } = "entrada";
}
