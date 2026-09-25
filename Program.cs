using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using PlataformaCreditos.Data;
using PlataformaCreditos.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();

// Cache distribuido + sesion (Redis-backed) con fallback en memoria.
var redisHost = builder.Configuration["Redis:Host"];
if (!string.IsNullOrWhiteSpace(redisHost))
{
    var redisPort = int.TryParse(builder.Configuration["Redis:Port"], out var port) ? port : 6379;
    var redisSsl = bool.TryParse(builder.Configuration["Redis:Ssl"], out var useSsl) && useSsl;

    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.InstanceName = "PlataformaCreditos:";
        options.ConfigurationOptions = new ConfigurationOptions
        {
            Password = builder.Configuration["Redis:Password"],
            Ssl = redisSsl,
            AbortOnConnectFail = false,
            ConnectTimeout = 5000,
            SyncTimeout = 5000
        };
        options.ConfigurationOptions.EndPoints.Add(redisHost, redisPort);
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".PlataformaCreditos.Session";
    options.Cookie.HttpOnly = true;
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ISesionSolicitudService, SesionSolicitudService>();
builder.Services.AddScoped<ICacheSolicitudesService, CacheSolicitudesService>();

var app = builder.Build();

if (string.IsNullOrWhiteSpace(redisHost))
{
    app.Logger.LogWarning("Redis no configurado (Redis:Host vacio). Cache y sesion usaran memoria local.");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await SeedData.InitializeAsync(scope.ServiceProvider);
        logger.LogInformation("Seeding de datos iniciales aplicado correctamente.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Ocurrio un error al aplicar el seeding de datos iniciales.");
        throw;
    }
}

app.Run();
