namespace MercuryGo.Api.Tests;

using Xunit;
using MercuryGo.Api.Utils;

public sealed class ValidadorEvidenciaTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("/uploads/comprobantes/foto123.jpg", true)]
    [InlineData("uploads/firmas/firma.png", true)]
    [InlineData("https://storage.mercurygo.com/remitos/remito.pdf", true)]
    [InlineData("http://mercurygo.runasp.net/uploads/remito.jpg", true)]
    [InlineData("data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=", true)]
    [InlineData("data:image/jpeg;base64,/9j/4AAQSkZJRg==", true)]
    public void ValidaEvidenciasPermitidas(string? entrada, bool esperado)
    {
        var resultado = ValidadorEvidencia.EsEvidenciaValida(entrada);
        Assert.Equal(esperado, resultado);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("/uploads/../secret.env")]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==")]
    [InlineData("ftp://servidor-inseguro.com/archivo.exe")]
    [InlineData("data:image/invalid;base64,12345")]
    public void RechazaEvidenciasMaliciosasOInseguras(string entrada)
    {
        var resultado = ValidadorEvidencia.EsEvidenciaValida(entrada);
        Assert.False(resultado);
    }
}
