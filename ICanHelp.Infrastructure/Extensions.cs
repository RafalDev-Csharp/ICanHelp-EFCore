using ICanHelp.Application.Abstractions;
using ICanHelp.Infrastructure.DAL;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Infrastructure
{
    public static class Extensions
    {
        public static IServiceCollection AddICanHelpSettings(this IServiceCollection services, IConfiguration configuration)
        {
            var section = configuration.GetSection(AppOptions.SectionName);
            services.Configure<AppOptions>(section);
            //services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));

            services.AddDbContext<ICanHelpDbContext>(option =>
                option.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            var infrastructureAssembly = typeof(AppOptions).Assembly;

            services.Scan(s => s.FromAssemblies(infrastructureAssembly)
                .AddClasses(c => c.AssignableTo(typeof(IQueryHandler<,>)), false)
                .AsImplementedInterfaces()
                .WithScopedLifetime());

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(swagger =>
            {
                var appTitle = section.GetSection("name").Value;
                swagger.EnableAnnotations();
                swagger.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = appTitle,
                    Version = "v1"
                });
            });

            return services;
        }



        public static WebApplication UseICanHelpSettings(this WebApplication app)
        {
            app.UseSwagger();
            //app.UseSwaggerUI();
            app.UseReDoc(reDoc =>
            {
                var appTitle = new AppOptions().SettingName ?? "ICanHelp API";
                reDoc.RoutePrefix = "docs";
                reDoc.DocumentTitle = appTitle;
                reDoc.SpecUrl("/swagger/v1/swagger.json");
            });
            app.UseAuthentication();
            app.UseAuthorization();
            //app.UseAntiforgery();

            return app;
        }
    }
}
