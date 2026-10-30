namespace MercuryGo.Api.Common;

public static class TextoUtil
{
    public static string? Normalizar(string? texto)
    {
        var limpio = (texto ?? string.Empty).Trim();
        return limpio.Length > 0 ? limpio : null;
    }
}
