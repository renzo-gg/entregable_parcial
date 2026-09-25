using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<SolicitudCredito> SolicitudesCreditos => Set<SolicitudCredito>();

    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Cliente>(entity =>
        {
            entity.ToTable(t => t.HasCheckConstraint("CK_Cliente_IngresosMensuales", "\"IngresosMensuales\" > 0"));

            entity.HasIndex(c => c.UsuarioId).IsUnique();
        });

        builder.Entity<SolicitudCredito>(entity =>
        {
            entity.ToTable(t => t.HasCheckConstraint("CK_SolicitudCredito_MontoSolicitado", "\"MontoSolicitado\" > 0"));

            // Un cliente solo puede tener UNA solicitud en estado Pendiente.
            entity.HasIndex(s => s.ClienteId)
                .IsUnique()
                .HasDatabaseName("IX_SolicitudesCreditos_ClienteId_Pendiente_Unico")
                .HasFilter("\"Estado\" = 0");

            entity.HasOne(s => s.Cliente)
                .WithMany(c => c.Solicitudes)
                .HasForeignKey(s => s.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Notificacion>(entity =>
        {
            // Idempotencia en redeliveries: el MessageId del mensaje es unico.
            entity.HasIndex(n => n.MessageId).IsUnique();

            entity.HasIndex(n => n.UsuarioId);
        });
    }
}