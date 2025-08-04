using AICodePortal.Backend.Services.Interfaces;
using AICodePortal.Backend.Services.Implementations;
using AICodePortal.Backend.Data.Context;
using AICodePortal.Backend.Core.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AICodePortal.Backend.Infrastructure.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Register basic services
            services.AddScoped<IRepositoryService, RepositoryService>();
            services.AddScoped<IProjectService, ProjectService>();
            services.AddScoped<IChatService, ChatService>();

            // ✅ SIMPLE FIX: Register Claude as the main IAIService for now
            services.AddScoped<IAIService, ClaudeAIService>();

            // Register HttpClient
            services.AddHttpClient();

            // ✅ SIMPLE CONFIG: Use Claude configuration directly
            services.Configure<AIProviderConfiguration>(configuration.GetSection("AIProviders"));
            services.Configure<RepositoryConfiguration>(configuration.GetSection("Repository"));

            return services;
        }

        public static IServiceCollection AddDatabaseServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AIPortalDbContext>(options =>
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                options.UseSqlServer(connectionString);
                options.EnableSensitiveDataLogging(false);
                options.EnableServiceProviderCaching();
            });

            return services;
        }

        public static IServiceCollection AddAuthenticationServices(this IServiceCollection services, IConfiguration configuration)
        {
            return services;
        }
    }
}