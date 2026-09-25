using PlataformaCreditos.ViewModels;

namespace PlataformaCreditos.Services;

public interface ISesionSolicitudService
{
    void GuardarUltimaSolicitud(int solicitudId, decimal monto);

    UltimaSolicitudSessionInfo? ObtenerUltimaSolicitud();

    void LimpiarUltimaSolicitud();
}