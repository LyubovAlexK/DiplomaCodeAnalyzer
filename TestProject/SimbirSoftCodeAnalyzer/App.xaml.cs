using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Core.Data;
using Core.Services;
using AI.Extractors;
using AI.Services;
using Analyzers.Services;

namespace SimbirSoftCodeAnalyzer
{
    public partial class App : Application
    {
        private ServiceProvider _serviceProvider = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // Запуск главного окна
            _serviceProvider.GetRequiredService<MainWindow>().Show();
        }

        private static void ConfigureServices(ServiceCollection services)
        {
            // База данных
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(@"Server=DESKTOP-3HK6G3K\SQLEXPRESS;Database=AnalyseSystem;Trusted_Connection=True;TrustServerCertificate=True;"));

            // Core сервисы
            services.AddScoped<DictionaryService>();
            services.AddScoped<UserService>();
            services.AddScoped<TokenService>();
            services.AddScoped<ProjectService>();

            // AI сервисы
            services.AddScoped<SpecificationService>();
            services.AddScoped<ExtractorFactory>();

            // Analyzers сервисы
            services.AddScoped<RoslynSyntaxAnalyzer>();
            services.AddScoped<ProjectValidator>();
            services.AddScoped<RoslynResultService>();
            services.AddScoped<ArchitectureAnalyzer>();
            services.AddScoped<AISemanticJudge>();
            services.AddScoped<ReferenceService>();
            services.AddScoped<SessionService>();

            // Главное окно (Singleton — одно на всё приложение)
            services.AddSingleton<MainWindow>();
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
