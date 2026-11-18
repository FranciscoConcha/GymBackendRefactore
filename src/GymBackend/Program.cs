using GymBackend.Data;
using GymBackend.Extensions;
using GymBackend.Options;
using GymBackend.Security;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(o => { o.IncludeScopes = true; o.UseUtcTimestamp = true; });
    builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
    builder.Services.AddHsts(options =>
    {
        options.MaxAge = TimeSpan.FromDays(365);
        options.IncludeSubDomains = true;
    });
}
builder.Services.AddControllers();
builder.Services.AddGymErrors();
builder.Services.AddGymObservability();
builder.Services.AddGymOptions();
builder.Services.AddGymTime();
builder.Services.AddGymPersistence(builder.Configuration);
builder.Services.AddGymAuthentication();
builder.Services.AddGymRateLimiting();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddGymCors();
builder.Services.AddReservationServices();
builder.Services.AddAuthServices();
builder.Services.AddAttendanceServices();

var app = builder.Build();
app.UseGymObservability();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseRouting();
app.UseCors("Frontend");
app.UseRateLimiter();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthEndpoints();

if (app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value.Initialize)
{
    await DevelopmentSeeder.SeedAsync(app.Services, app.Environment);
}
app.Run();
