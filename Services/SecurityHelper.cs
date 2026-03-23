// ============================================================
// Services/SecurityHelper.cs — Utilidades de seguridad CEPLAN
// PBKDF2-SHA256, validación de inputs, OWASP A02 A03 A07
// Implementa controles de seguridad críticos para autenticación
// ============================================================
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace LoginAppCore.Services;

/// <summary>
/// Clase estática de utilidades de seguridad para CEPLAN
/// Proporciona funciones criptográficas seguras y validaciones de entrada
/// Implementa mejores prácticas de OWASP para prevenir vulnerabilidades comunes
/// </summary>
public static class SecurityHelper
{
    // ── Constantes PBKDF2 ─────────────────────────────────────
    /// <summary>Tamaño del salt en bytes (256 bits para máxima seguridad)</summary>
    private const int SaltBytes = 32;

    /// <summary>Tamaño del hash resultante en bytes (256 bits)</summary>
    private const int HashBytes = 32;

    /// <summary>
    /// Número de iteraciones PBKDF2 (150,000 iteraciones)
    /// Aumentado para resistir mejor ataques de fuerza bruta con hardware moderno (2024)
    /// NIST recomienda mínimo 100k, usamos 150k para mayor seguridad
    /// </summary>
    private const int Iterations = 150_000;

    /// <summary>
    /// Pepper de aplicación para añadir una capa extra de seguridad
    /// En producción esto debería obtenerse de una variable de entorno segura
    /// </summary>
    private const string ApplicationPepper = "CEPLAN_2024_SECURE_PEPPER_V1";

    // ── Utilidades de fecha y hora ──────────────────────────────────────────
    /// <summary>
    /// Obtiene la fecha y hora actual de Perú (UTC-5) de manera robusta
    /// Maneja errores de zona horaria con fallback automático
    /// </summary>
    /// <returns>DateTime con la hora actual de Perú</returns>
    public static DateTime GetPeruDateTime()
    {
        try
        {
            // Intentar obtener la zona horaria de Perú (UTC-5)
            var peruTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, peruTimeZone);
        }
        catch
        {
            try
            {
                // Fallback: intentar con ID alternativo de Linux
                var peruTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, peruTimeZone);
            }
            catch
            {
                // Último fallback: UTC-5 manual
                return DateTime.UtcNow.AddHours(-5);
            }
        }
    }

    // ── Regex compilados para performance ──────────────────────────────────
    /// <summary>
    /// Regex para validación de contraseñas fuertes (actualizada 2024)
    /// Requiere: minúscula, mayúscula, número, carácter especial, mínimo 8 caracteres
    /// Excluye patrones comunes débiles
    /// </summary>
    private static readonly Regex _passRegex = new(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*()_\-+=\[\]{};':""\\|,.<>/?]).{8,128}$",
        RegexOptions.Compiled);

    /// <summary>
    /// Regex para detectar patrones débiles en contraseñas
    /// Previene secuencias comunes y patrones predecibles
    /// </summary>
    private static readonly Regex _weakPatterns = new(
        @"(123|abc|qwe|admin|pass|user|test|1234|aaaa|0000)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Regex para detectar caracteres peligrosos que podrían causar XSS o inyecciones
    /// Previene OWASP A03: Injection
    /// </summary>
    private static readonly Regex _dangerousChars = new(
        @"[<>""';&\\]", RegexOptions.Compiled);

    // ── Funciones de hashing de contraseñas ────────────────────────────────
    /// <summary>
    /// Genera un hash seguro de contraseña usando PBKDF2-SHA256
    /// Utiliza 150,000 iteraciones para resistir ataques de fuerza bruta
    /// Cumple con OWASP A02: Cryptographic Failures y NIST guidelines 2024
    /// </summary>
    /// <param name="password">Contraseña en texto plano</param>
    /// <returns>Tupla con (hash base64, salt base64)</returns>
    /// <exception cref="ArgumentException">Si la contraseña es nula, vacía o demasiado débil</exception>
    public static (string hash, string salt) HashPassword(string password)
    {
        // Validaciones de entrada más estrictas
        ArgumentException.ThrowIfNullOrEmpty(password);

        if (password.Length < 8 || password.Length > 128)
            throw new ArgumentException("La contraseña debe tener entre 8 y 128 caracteres");

        if (!IsStrongPassword(password))
            throw new ArgumentException("La contraseña no cumple con las políticas de seguridad");

        // Generar salt criptográficamente seguro
        byte[] saltBytes = RandomNumberGenerator.GetBytes(SaltBytes);

        // Usar un pepper adicional para mayor seguridad (opcional)
        var passwordWithPepper = password + GetPepper();

        // Generar hash PBKDF2-SHA256
        byte[] hashBytes = Pbkdf2(passwordWithPepper, saltBytes);

        // Crear copias para retornar antes de limpiar
        var hashBase64 = Convert.ToBase64String(hashBytes);
        var saltBase64 = Convert.ToBase64String(saltBytes);

        // Limpiar variables sensibles de memoria
        Array.Clear(hashBytes);
        Array.Clear(saltBytes);
        passwordWithPepper = null;

        return (hashBase64, saltBase64);
    }

    /// <summary>
    /// Verifica una contraseña contra su hash almacenado
    /// Soporta tanto el sistema antiguo (sin pepper) como el nuevo (con pepper) para compatibilidad
    /// Utiliza comparación en tiempo constante para prevenir ataques de temporización
    /// Implementa protección contra timing attacks (OWASP A02)
    /// </summary>
    /// <param name="password">Contraseña a verificar</param>
    /// <param name="storedHash">Hash almacenado en base64</param>
    /// <param name="storedSalt">Salt almacenado en base64</param>
    /// <returns>true si la contraseña coincide, false en caso contrario</returns>
    public static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        // Validación de parámetros de entrada
        if (string.IsNullOrEmpty(password) ||
            string.IsNullOrEmpty(storedHash) ||
            string.IsNullOrEmpty(storedSalt))
        {
            // Ejecutar operación dummy para prevenir timing attacks
            _ = DummyHash();
            return false;
        }

        try
        {
            var saltBytes = Convert.FromBase64String(storedSalt);
            var storedBytes = Convert.FromBase64String(storedHash);

            // COMPATIBILIDAD: Intentar primero con el sistema NUEVO (con pepper)
            var passwordWithPepper = password + GetPepper();
            var computedBytesNew = Pbkdf2(passwordWithPepper, saltBytes);

            if (CryptographicOperations.FixedTimeEquals(storedBytes, computedBytesNew))
            {
                // Hash nuevo válido
                passwordWithPepper = null;
                Array.Clear(saltBytes);
                Array.Clear(computedBytesNew);
                return true;
            }

            // COMPATIBILIDAD: Si falla, intentar con el sistema ANTIGUO (sin pepper)
            var computedBytesOld = Pbkdf2Legacy(password, saltBytes);
            var result = CryptographicOperations.FixedTimeEquals(storedBytes, computedBytesOld);

            // Limpiar variables sensibles
            passwordWithPepper = null;
            Array.Clear(saltBytes);
            Array.Clear(computedBytesNew);
            Array.Clear(computedBytesOld);

            return result;
        }
        catch
        {
            // En caso de error de formato en base64, retornar false sin exponer información
            // Ejecutar operación dummy para mantener tiempo constante
            _ = DummyHash();
            return false;
        }
    }

    /// <summary>
    /// Verifica si un hash necesita ser migrado al nuevo sistema
    /// </summary>
    /// <param name="password">Contraseña en texto plano</param>
    /// <param name="storedHash">Hash almacenado en base64</param>
    /// <param name="storedSalt">Salt almacenado en base64</param>
    /// <returns>true si el hash es del sistema antiguo y necesita migración</returns>
    public static bool NeedsMigration(string password, string storedHash, string storedSalt)
    {
        try
        {
            var saltBytes = Convert.FromBase64String(storedSalt);
            var storedBytes = Convert.FromBase64String(storedHash);

            // Verificar si es hash nuevo (con pepper)
            var passwordWithPepper = password + GetPepper();
            var computedBytesNew = Pbkdf2(passwordWithPepper, saltBytes);

            if (CryptographicOperations.FixedTimeEquals(storedBytes, computedBytesNew))
            {
                // Es hash nuevo, no necesita migración
                Array.Clear(saltBytes);
                Array.Clear(computedBytesNew);
                return false;
            }

            // Verificar si es hash antiguo (sin pepper)
            var computedBytesOld = Pbkdf2Legacy(password, saltBytes);
            var isOldHash = CryptographicOperations.FixedTimeEquals(storedBytes, computedBytesOld);

            Array.Clear(saltBytes);
            Array.Clear(computedBytesNew);
            Array.Clear(computedBytesOld);

            return isOldHash; // true si es hash antiguo
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Función interna para generar hash PBKDF2-SHA256
    /// Utiliza la implementación estándar de .NET para máxima seguridad
    /// </summary>
    /// <param name="password">Contraseña en texto plano</param>
    /// <param name="salt">Salt en bytes</param>
    /// <returns>Hash resultante en bytes</returns>
    private static byte[] Pbkdf2(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt,
            Iterations, HashAlgorithmName.SHA256, HashBytes);

    /// <summary>
    /// Función legacy para verificar hashes antiguos (sin pepper)
    /// Utilizada para mantener compatibilidad con usuarios existentes
    /// </summary>
    /// <param name="password">Contraseña en texto plano</param>
    /// <param name="salt">Salt en bytes</param>
    /// <returns>Hash resultante en bytes</returns>
    private static byte[] Pbkdf2Legacy(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt,
            Iterations, HashAlgorithmName.SHA256, HashBytes);

    /// <summary>
    /// Obtiene el pepper de aplicación para añadir seguridad adicional
    /// En producción esto debería obtenerse de variables de entorno o Key Vault
    /// </summary>
    /// <returns>Pepper de aplicación</returns>
    private static string GetPepper()
    {
        // En producción, obtener de Environment.GetEnvironmentVariable("CEPLAN_PEPPER")
        return ApplicationPepper;
    }

    /// <summary>
    /// Ejecuta una operación de hash dummy para prevenir timing attacks
    /// Mantiene tiempo de ejecución constante cuando la verificación falla
    /// </summary>
    /// <returns>Hash dummy (se descarta)</returns>
    private static byte[] DummyHash()
    {
        var dummySalt = new byte[SaltBytes];
        return Pbkdf2("dummy", dummySalt);
    }

    // ── Validación de inputs (OWASP A03) ─────────────────────────────────
    /// <summary>
    /// Valida y sanitiza entrada de usuario para prevenir inyecciones
    /// Implementa protección contra OWASP A03: Injection
    /// </summary>
    /// <param name="input">Entrada del usuario a validar</param>
    /// <param name="sanitized">Salida sanitizada si la validación es exitosa</param>
    /// <returns>true si la entrada es válida, false en caso contrario</returns>
    public static bool IsValidInput(string? input, out string sanitized)
    {
        sanitized = string.Empty;

        // Verificar longitud y contenido básico
        if (string.IsNullOrWhiteSpace(input) || input.Length > 128) return false;

        // Verificar caracteres peligrosos que podrían causar inyecciones
        if (_dangerousChars.IsMatch(input)) return false;

        sanitized = input.Trim();
        return true;
    }

    /// <summary>
    /// Valida que una contraseña cumpla con las políticas de seguridad avanzadas
    /// Requiere: minúscula, mayúscula, número, carácter especial, mínimo 8 caracteres
    /// Rechaza patrones comunes débiles y secuencias predecibles
    /// Cumple con OWASP A07: Identification and Authentication Failures
    /// </summary>
    /// <param name="password">Contraseña a validar</param>
    /// <returns>true si la contraseña es fuerte y segura, false en caso contrario</returns>
    public static bool IsStrongPassword(string? password)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        // Verificar longitud (8-128 caracteres)
        if (password.Length < 8 || password.Length > 128)
            return false;

        // Verificar que cumple requisitos básicos (mayús, minús, número, especial)
        if (!_passRegex.IsMatch(password))
            return false;

        // Rechazar patrones comunes débiles
        if (_weakPatterns.IsMatch(password))
            return false;

        // Verificar que no sea solo repetición de caracteres
        if (IsRepeatingPattern(password))
            return false;

        // Verificar que no sea secuencia obvia
        if (IsSequentialPattern(password))
            return false;

        return true;
    }

    /// <summary>
    /// Detecta patrones de repetición en contraseñas (aaaa, 1111, etc.)
    /// </summary>
    private static bool IsRepeatingPattern(string password)
    {
        if (password.Length < 4) return false;

        var firstChar = password[0];
        var repeats = 1;

        for (int i = 1; i < password.Length; i++)
        {
            if (password[i] == firstChar)
            {
                repeats++;
                if (repeats >= 4) return true; // 4 o más repeticiones consecutivas
            }
            else
            {
                repeats = 1;
                firstChar = password[i];
            }
        }
        return false;
    }

    /// <summary>
    /// Detecta secuencias obvias (1234, abcd, etc.)
    /// </summary>
    private static bool IsSequentialPattern(string password)
    {
        if (password.Length < 4) return false;

        for (int i = 0; i <= password.Length - 4; i++)
        {
            bool isSequential = true;
            for (int j = i + 1; j < i + 4; j++)
            {
                if (password[j] != password[j - 1] + 1)
                {
                    isSequential = false;
                    break;
                }
            }
            if (isSequential) return true;
        }
        return false;
    }

    /// <summary>
    /// Valida formato de correo electrónico usando la clase MailAddress de .NET
    /// Proporciona validación robusta sin vulnerabilidades de regex
    /// </summary>
    /// <param name="email">Dirección de email a validar</param>
    /// <returns>true si el email es válido, false en caso contrario</returns>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 100) return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email.Trim();
        }
        catch
        {
            // En caso de formato inválido, retornar false
            return false;
        }
    }

    /// <summary>
    /// Codifica HTML para prevenir ataques XSS (Cross-Site Scripting)
    /// Implementa protección contra OWASP A03: Cross-Site Scripting
    /// </summary>
    /// <param name="value">Valor a codificar</param>
    /// <returns>Valor codificado para uso seguro en HTML</returns>
    public static string HtmlEncode(string? value) =>
        System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
}
