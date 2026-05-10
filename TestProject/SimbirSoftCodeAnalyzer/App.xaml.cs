using AI.Extractors;
using AI.Services;
using Analyzers.Services;
using Core.Data;
using Core.Models;
using Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimbirSoftCodeAnalyzer.Views;
using System.Windows;

namespace SimbirSoftCodeAnalyzer
{
    public partial class App : Application
    {
        private ServiceProvider _serviceProvider = null!;

        public static User? CurrentUser { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {

            base.OnStartup(e);
            
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            var loginWindow = new LoginWindow();
            loginWindow.Show();
        }

        private static void ConfigureServices(ServiceCollection services)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(@"Server=DESKTOP-3HK6G3K\SQLEXPRESS;Database=AnalyseSystem;Trusted_Connection=True;TrustServerCertificate=True;"));

            services.AddScoped<DictionaryService>();
            services.AddScoped<UserService>();
            services.AddScoped<TokenService>();
            services.AddScoped<ProjectService>();

            services.AddScoped<SpecificationService>();
            services.AddScoped<ExtractorFactory>();

            services.AddScoped<RoslynSyntaxAnalyzer>();
            services.AddScoped<ProjectValidator>();
            services.AddScoped<RoslynResultService>();
            services.AddScoped<ArchitectureAnalyzer>();
            services.AddScoped<AISemanticJudge>();
            services.AddScoped<ReferenceService>();
            services.AddScoped<SessionService>();
        }

        public static T GetService<T>() where T : class
        {
            return ((App)Current)._serviceProvider.GetRequiredService<T>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider.Dispose();
            base.OnExit(e);
        }
    }

}
