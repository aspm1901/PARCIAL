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
        if (await context.Incidencias.CountAsync() < 20)
        {
            context.Incidencias.RemoveRange(context.Incidencias);
            await context.SaveChangesAsync();

            var incidencias = new List<Incidencia>
            {
                new Incidencia { Id = 1, Estacion = "Estación Central", Descripcion = "Cadena suelta en bicicleta #101", Prioridad = "Alta", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-50) },
                new Incidencia { Id = 2, Estacion = "Estación San Isidro", Descripcion = "Freno delantero desgastado en bicicleta #204", Prioridad = "Media", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-45) },
                new Incidencia { Id = 3, Estacion = "Estación Miraflores", Descripcion = "Pinchazo de neumático en bicicleta #305", Prioridad = "Alta", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-40) },
                new Incidencia { Id = 4, Estacion = "Estación Larcomar", Descripcion = "Sillín dañado en bicicleta #412", Prioridad = "Baja", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-35) },
                new Incidencia { Id = 5, Estacion = "Estación Javier Prado", Descripcion = "Anclaje #5 trabado con bicicleta #519", Prioridad = "Alta", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-30) },
                new Incidencia { Id = 6, Estacion = "Estación Kennedy", Descripcion = "Pedal flojo en bicicleta #602", Prioridad = "Media", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-25) },
                new Incidencia { Id = 7, Estacion = "Estación Barranco", Descripcion = "Luz LED delantera no enciende en bicicleta #108", Prioridad = "Baja", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-20) },
                new Incidencia { Id = 8, Estacion = "Estación Surco", Descripcion = "Manubrio desalineado en bicicleta #315", Prioridad = "Media", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-18) },
                new Incidencia { Id = 9, Estacion = "Estación San Borja", Descripcion = "Cambio de marcha atascado en 3ra posición bicicleta #522", Prioridad = "Alta", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-15) },
                new Incidencia { Id = 10, Estacion = "Estación Centro Cívico", Descripcion = "Freno trasero sin tensión en bicicleta #209", Prioridad = "Alta", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-12) },
                new Incidencia { Id = 11, Estacion = "Estación Los Olivos", Descripcion = "Panel solar de la estación con suciedad y baja carga", Prioridad = "Baja", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-10) },
                new Incidencia { Id = 12, Estacion = "Estación Magdalena", Descripcion = "Sensor de anclaje #12 no detecta devolución bicicleta #407", Prioridad = "Media", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-9) },
                new Incidencia { Id = 13, Estacion = "Estación Pueblo Libre", Descripcion = "Neumático trasero bajo de aire en bicicleta #710", Prioridad = "Baja", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-8) },
                new Incidencia { Id = 14, Estacion = "Estación Jesús María", Descripcion = "Cadena oxidada con fricción en bicicleta #815", Prioridad = "Media", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-7) },
                new Incidencia { Id = 15, Estacion = "Estación Lince", Descripcion = "Cesta delantera doblada por impacto en bicicleta #120", Prioridad = "Baja", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-6) },
                new Incidencia { Id = 16, Estacion = "Estación Salaverry", Descripcion = "Pantalla digital de bloqueo no responde en estación", Prioridad = "Alta", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-5) },
                new Incidencia { Id = 17, Estacion = "Estación Angamos", Descripcion = "Rayos de rueda delantera torcidos en bicicleta #633", Prioridad = "Media", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-4) },
                new Incidencia { Id = 18, Estacion = "Estación Benavides", Descripcion = "Candado de seguridad trabado en rueda trasera bicicleta #904", Prioridad = "Alta", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-3) },
                new Incidencia { Id = 19, Estacion = "Estación Chorrillos", Descripcion = "Cables de freno desgastados y deshilachados en bicicleta #218", Prioridad = "Alta", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-2) },
                new Incidencia { Id = 20, Estacion = "Estación Pardo", Descripcion = "Bicicleta #540 con holgura excesiva en el eje central", Prioridad = "Media", Estado = "Abierta", FechaRegistro = DateTime.UtcNow.AddMinutes(-1) }
            };

            await context.Incidencias.AddRangeAsync(incidencias);
            await context.SaveChangesAsync();
        }
    }
}
