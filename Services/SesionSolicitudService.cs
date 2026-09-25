using System.Text.Json;
using PlataformaCreditos.ViewModels;

namespace PlataformaCreditos.Services;

public class SesionSolicitudService(IHttpContextAccessor httpContextAccessor) : ISesionSolicitudService
{
    private const string ClaveUltimaSolicitud = "UltimaSolicitud";

    public void GuardarUltimaSolicitud(int solicitudId, decimal monto)
    {
        var sesion = httpContextAccessor.HttpContext?.Session;
        if (sesion is null)
        {
            return;
        }

        sesion.SetString(ClaveUltimaSolicitud,
            JsonSerializer.Serialize(new UltimaSolicitudSessionInfo(solicitudId, monto)));
    }

    public UltimaSolicitudSessionInfo? ObtenerUltimaSolicitud()
    {
        var valor = httpContextAccessor.HttpContext?.Session.GetString(ClaveUltimaSolicitud);
        return valor is null
            ? null
            : JsonSerializer.Deserialize<UltimaSolicitudSessionInfo>(valor);
    }

    public void LimpiarUltimaSolicitud() => httpContextAccessor.HttpContext?.Session.Remove(ClaveUltimaSolicitud);
}