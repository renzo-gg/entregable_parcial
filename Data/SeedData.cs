using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();

        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // 1 usuario con rol Analista
        const string analistaRoleName = "Analista";
        if (!await roleManager.RoleExistsAsync(analistaRoleName))
        {
            await roleManager.CreateAsync(new IdentityRole(analistaRoleName));
        }

        var analista = await CreateUserIfMissingAsync(userManager, "analista@platacreditos.com", "Analista1!");
        if (!await userManager.IsInRoleAsync(analista, analistaRoleName))
        {
            await userManager.AddToRoleAsync(analista, analistaRoleName);
        }

        // 2 clientes vinculados a usuarios Identity
        var cliente1User = await CreateUserIfMissingAsync(userManager, "cliente1@platacreditos.com", "Cliente1!");
        var cliente2User = await CreateUserIfMissingAsync(userManager, "cliente2@platacreditos.com", "Cliente2!");

        if (await db.Clientes.AnyAsync())
        {
            return;
        }

        var cliente1 = new Cliente
        {
            UsuarioId = cliente1User.Id,
            IngresosMensuales = 5000m,
            Activo = true
        };

        var cliente2 = new Cliente
        {
            UsuarioId = cliente2User.Id,
            IngresosMensuales = 8000m,
            Activo = true
        };

        db.Clientes.AddRange(cliente1, cliente2);
        await db.SaveChangesAsync();

        // 2 solicitudes: una pendiente y una aprobada
        db.SolicitudesCreditos.AddRange(
            new SolicitudCredito
            {
                ClienteId = cliente1.Id,
                MontoSolicitado = 10000m,
                FechaSolicitud = DateTime.UtcNow,
                Estado = EstadoSolicitud.Pendiente
            },
            new SolicitudCredito
            {
                ClienteId = cliente2.Id,
                MontoSolicitado = 20000m,
                FechaSolicitud = DateTime.UtcNow.AddDays(-5),
                Estado = EstadoSolicitud.Aprobado
            });

        await db.SaveChangesAsync();
    }

    private static async Task<IdentityUser> CreateUserIfMissingAsync(UserManager<IdentityUser> userManager, string email, string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null)
        {
            return user;
        }

        user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errores = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"No se pudo crear el usuario {email}: {errores}");
        }

        return user;
    }
}