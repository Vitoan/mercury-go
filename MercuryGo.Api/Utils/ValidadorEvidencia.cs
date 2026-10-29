namespace MercuryGo.Api.Utils;

using System;

public static class ValidadorEvidencia
{
    public static bool EsEvidenciaValida(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return true;

        var texto = valor.Trim();

        // Evitar ataques de inyección o esquemas ejecutables
        if (texto.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
            texto.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase) ||
            texto.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
            texto.Contains("<script", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // 1. Data URI Base64 de imagen
        if (texto.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            var coma = texto.IndexOf(',');
            if (coma <= 0) return false;

            var encabezado = texto[..coma].ToLowerInvariant();
            if (!encabezado.Contains(";base64")) return false;

            var tipo = encabezado.Replace("data:image/", string.Empty).Replace(";base64", string.Empty).Trim();
            if (tipo is not ("png" or "jpeg" or "jpg" or "webp")) return false;

            var base64Part = texto[(coma + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(base64Part)) return false;

            Span<byte> buffer = stackalloc byte[base64Part.Length <= 256 ? base64Part.Length : 0];
            return Convert.TryFromBase64String(base64Part, buffer.Length > 0 ? buffer : new byte[base64Part.Length], out _);
        }

        // 2. Ruta relativa del servidor (ej: /uploads/comprobantes/... o uploads/...)
        if (texto.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) ||
            texto.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return !texto.Contains(".."); // Prevenir path traversal
        }

        // 3. URL HTTP / HTTPS absoluta
        if (Uri.TryCreate(texto, UriKind.Absolute, out var uri))
        {
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }

        return false;
    }
}
