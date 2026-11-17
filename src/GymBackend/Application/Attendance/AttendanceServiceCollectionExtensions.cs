using GymBackend.Application.Attendance;
using GymBackend.Application.Qr;
using Microsoft.Extensions.DependencyInjection;

namespace GymBackend.Extensions;

public static class AttendanceServiceCollectionExtensions
{
    public static IServiceCollection AddAttendanceServices(this IServiceCollection services)
    {
        services.AddScoped<IQrTokenService, QrTokenService>();
        services.AddScoped<IQrService, QrService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        return services;
    }
}
