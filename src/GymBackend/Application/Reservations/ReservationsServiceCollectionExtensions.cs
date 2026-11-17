using GymBackend.Application.Reservations;
using GymBackend.Application.Schedules;

namespace GymBackend.Extensions;

public static class ReservationsServiceCollectionExtensions
{
    public static IServiceCollection AddReservationServices(this IServiceCollection services)
    {
        services.AddScoped<IReservationService, ReservationService>();
        services.AddScoped<IScheduleService, ScheduleService>();
        return services;
    }
}
