using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlataformaIncidencias.Models;

namespace PlataformaIncidencias.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Aplicar migraciones pendientes
        await context.Database.MigrateAsync();

        // 1. Roles
        string[] roles = ["Supervisor", "Operador"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // 2. Usuario Supervisor
        const string supervisorEmail = "supervisor@bicis.com";
        var supervisor = await userManager.FindByEmailAsync(supervisorEmail);
        if (supervisor == null)
        {
            supervisor = new IdentityUser
            {
                UserName = supervisorEmail,
                Email = supervisorEmail,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(supervisor, "Password123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(supervisor, "Supervisor");
            }
        }

        // 3. Incidencias de prueba
        if (!await context.Incidencias.AnyAsync())
        {
            var incidencias = new List<Incidencia>
            {
                new Incidencia
                {
                    Estacion = "Estación Central",
                    Descripcion = "Cadena suelta en bicicleta #101",
                    Prioridad = "Alta",
                    Estado = "Abierta",
                    FechaRegistro = DateTime.UtcNow.AddMinutes(-50)
                },
                new Incidencia
                {
                    Estacion = "Estación San Isidro",
                    Descripcion = "Freno delantero desgastado en bicicleta #204",
                    Prioridad = "Media",
                    Estado = "Abierta",
                    FechaRegistro = DateTime.UtcNow.AddMinutes(-40)
                },
                new Incidencia
                {
                    Estacion = "Estación Miraflores",
                    Descripcion = "Pinchazo de neumático en bicicleta #305",
                    Prioridad = "Alta",
                    Estado = "Abierta",
                    FechaRegistro = DateTime.UtcNow.AddMinutes(-30)
                },
                new Incidencia
                {
                    Estacion = "Estación Larcomar",
                    Descripcion = "Sillín dañado en bicicleta #412",
                    Prioridad = "Baja",
                    Estado = "Abierta",
                    FechaRegistro = DateTime.UtcNow.AddMinutes(-20)
                },
                new Incidencia
                {
                    Estacion = "Estación Javier Prado",
                    Descripcion = "Anclaje #5 trabado con bicicleta #519",
                    Prioridad = "Alta",
                    Estado = "Abierta",
                    FechaRegistro = DateTime.UtcNow.AddMinutes(-10)
                },
                new Incidencia
                {
                    Estacion = "Estación Kennedy",
                    Descripcion = "Pedal flojo en bicicleta #602",
                    Prioridad = "Media",
                    Estado = "Abierta",
                    FechaRegistro = DateTime.UtcNow.AddMinutes(-5)
                },
                new Incidencia
                {
                    Estacion = "Estación San Borja",
                    Descripcion = "Timbre roto en bicicleta #701",
                    Prioridad = "Baja",
                    Estado = "Cerrada",
                    FechaRegistro = DateTime.UtcNow.AddHours(-2)
                },
                new Incidencia
                {
                    Estacion = "Estación Centro Cívico",
                    Descripcion = "Cambio de marcha atascado en bicicleta #803",
                    Prioridad = "Media",
                    Estado = "Cerrada",
                    FechaRegistro = DateTime.UtcNow.AddHours(-3)
                }
            };

            await context.Incidencias.AddRangeAsync(incidencias);
            await context.SaveChangesAsync();
        }
    }
}
