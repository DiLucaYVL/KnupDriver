using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EmuladorKnup360;

namespace KnupService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private EmulatorService? _emulator;
        private FileSystemWatcher? _configWatcher;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Serviço de Driver Knup 360 iniciando...");
            try
            {
                var config = ConfigManager.Load();
                _emulator = new EmulatorService(config);

                _emulator.OnLog += msg => _logger.LogInformation("[Driver] {msg}", msg);
                _emulator.OnConnectionChanged += connected =>
                {
                    _logger.LogInformation("[Driver] Estado da conexão do controle: {status}", connected ? "CONECTADO" : "DESCONECTADO");
                };

                _emulator.Start();

                // Ativa HidHide se disponível
                if (_emulator.IsHidHideAvailable)
                {
                    _emulator.EnableHidHide();
                }

                // Observa alterações no arquivo de configuração em tempo real
                SetupConfigWatcher();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao inicializar o motor do driver.");
            }

            return base.StartAsync(cancellationToken);
        }

        private void SetupConfigWatcher()
        {
            try
            {
                string configPath = ConfigManager.GetConfigPath();
                string? dir = Path.GetDirectoryName(configPath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

                _configWatcher = new FileSystemWatcher(dir, "config.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                _configWatcher.Changed += (s, e) => ReloadConfig();
                _configWatcher.Created += (s, e) => ReloadConfig();
            }
            catch { }
        }

        private void ReloadConfig()
        {
            try
            {
                Thread.Sleep(200); // Aguarda gravação do arquivo
                var newConfig = ConfigManager.Load();
                if (_emulator != null)
                {
                    _emulator.Config = newConfig;
                    _logger.LogInformation("✔ Mapeamento de botões recarregado com sucesso!");
                }
            }
            catch { }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(2000, stoppingToken);
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Serviço de Driver Knup 360 finalizando...");
            try
            {
                _configWatcher?.Dispose();
                _emulator?.Dispose();
            }
            catch { }

            return base.StopAsync(cancellationToken);
        }
    }
}

