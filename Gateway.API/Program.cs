using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
builder.Services.AddOcelot(builder.Configuration);

// Explicit allow-list of headers + methods. Pairing AllowAnyHeader/Method with
// AllowCredentials() previously broadened the attack surface for any compromised origin.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDevClient", policy =>
        policy.WithOrigins(
                "http://localhost:4200",
                "http://127.0.0.1:4200",
                "http://localhost:61229",
                "http://localhost:55276",
                "http://127.0.0.1:55276",
                "http://localhost:62536",
                "http://127.0.0.1:62536")
              .WithHeaders("Authorization", "Content-Type", "Accept", "X-Requested-With")
              .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS")
              .AllowCredentials());
});

var app = builder.Build();

app.UseCors("AngularDevClient");

await app.UseOcelot();
app.Run();
