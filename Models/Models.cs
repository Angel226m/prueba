// ============================================================
// Models/Models.cs — Modelos y ViewModels CEPLAN
// Contiene: Usuario, LoginViewModel, SetupViewModel,
//           EstadisticasHoy, PerfilViewModel
// Implementa validaciones según OWASP A04 y A07
// ============================================================
using System.ComponentModel.DataAnnotations;

namespace LoginAppCore.Models;

// ── Entidad de base de datos ──────────────────────────────────
/// <summary>
/// Entidad principal de usuario del sistema CEPLAN
/// Incluye funcionalidad de bloqueo automático para prevenir ataques de fuerza bruta (OWASP A04)
/// </summary>
public class Usuario
{
    /// <summary>Identificador único del usuario en la base de datos</summary>
    public int Id { get; set; }

    /// <summary>Nombre de usuario único para autenticación</summary>
    public string NombreUsuario { get; set; } = string.Empty;

    /// <summary>Correo electrónico del usuario (también puede usarse para login)</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Nombre completo del usuario para mostrar en la interfaz</summary>
    public string NombreCompleto { get; set; } = string.Empty;

    /// <summary>Hash de la contraseña usando algoritmos seguros (bcrypt/scrypt)</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Salt único para el hash de contraseña</summary>
    public string PasswordSalt { get; set; } = string.Empty;

    /// <summary>Contador de intentos de login fallidos (reinicia en login exitoso)</summary>
    public int IntentosFallidos { get; set; }

    /// <summary>Fecha hasta la cual la cuenta está bloqueada (UTC)</summary>
    public DateTime? BloqueadoHasta { get; set; }

    /// <summary>Fecha de creación de la cuenta (UTC)</summary>
    public DateTime FechaCreacion { get; set; }

    /// <summary>Último acceso exitoso del usuario (UTC)</summary>
    public DateTime? UltimoAcceso { get; set; }

    /// <summary>Indica si la cuenta está activa (permite login)</summary>
    public bool Activo { get; set; }

    /// <summary>
    /// Propiedad calculada que verifica si la cuenta está actualmente bloqueada
    /// Compara BloqueadoHasta con la hora actual UTC para determinar el estado
    /// </summary>
    public bool EstaBloqueado =>
        BloqueadoHasta.HasValue && BloqueadoHasta.Value > DateTime.UtcNow;
}

// ── ViewModel Login ───────────────────────────────────────────
/// <summary>
/// ViewModel para el formulario de inicio de sesión
/// Incluye validaciones del lado del cliente y servidor
/// </summary>
public class LoginViewModel
{
    /// <summary>Credencial de acceso: puede ser nombre de usuario o email</summary>
    [Required(ErrorMessage = "Ingresa tu usuario o correo.")]
    public string Credencial { get; set; } = string.Empty;

    /// <summary>Contraseña del usuario</summary>
    [Required(ErrorMessage = "Ingresa tu contraseña.")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Mantener sesión iniciada (cookie persistente)</summary>
    public bool Recordar { get; set; }
}

// ── ViewModel Setup (primer usuario admin) ────────────────────
/// <summary>
/// ViewModel para la configuración inicial del sistema
/// Permite crear el primer usuario administrador
/// </summary>
public class SetupViewModel
{
    /// <summary>Nombre de usuario único para el primer administrador</summary>
    [Required] public string NombreUsuario { get; set; } = string.Empty;

    /// <summary>Email del administrador</summary>
    [Required] public string Email { get; set; } = string.Empty;

    /// <summary>Contraseña inicial (debe cumplir políticas de seguridad)</summary>
    [Required] public string Password { get; set; } = string.Empty;
}

// ── Estadísticas del dashboard ────────────────────────────────
/// <summary>
/// Métricas de seguridad y uso del sistema para el dashboard
/// Ayuda a detectar patrones anómalos de acceso (OWASP A09)
/// </summary>
public class EstadisticasHoy
{
    /// <summary>Número de accesos exitosos en las últimas 24 horas</summary>
    public int AccesosOk { get; set; }

    /// <summary>Número de intentos de login fallidos en las últimas 24 horas</summary>
    public int IntentosFallidos { get; set; }

    /// <summary>Total de usuarios registrados en el sistema</summary>
    public int TotalUsuarios { get; set; }
}
