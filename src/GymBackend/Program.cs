using GymBackend.Data;
using GymBackend.Extensions;
using GymBackend.Options;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddGymOptions();
builder.Services.AddGymPersistence(builder.Configuration);
builder.Services.AddGymAuthentication();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddGymCors();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("Frontend");
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthEndpoints();

if (app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value.Initialize)
{
    await DevelopmentSeeder.SeedAsync(app.Services, app.Environment);
}
app.Run();
