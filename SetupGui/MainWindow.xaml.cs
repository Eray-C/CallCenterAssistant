using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace SetupGui
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _statusTimer;
        private readonly SolidColorBrush _greenBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A6E3A1"));
        private readonly SolidColorBrush _redBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F38BA8"));
        private readonly SolidColorBrush _orangeBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9E2AF"));
        private bool _isDockerReady = false;
        private bool _isOllamaReady = false;

        public MainWindow()
        {
            InitializeComponent();
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _statusTimer.Tick += StatusTimer_Tick;
            _statusTimer.Start();
            
            // İlk açılışta hemen durumları denetle
            _ = CheckStatusesAsync();
        }

        private async void StatusTimer_Tick(object? sender, EventArgs e)
        {
            await CheckStatusesAsync();
        }

        private async Task CheckStatusesAsync()
        {
            // Docker Kontrolü
            _isDockerReady = await CheckCommandAsync("docker", "--version");
            Dispatcher.Invoke(() =>
            {
                DockerStatusDot.Fill = _isDockerReady ? _greenBrush : _redBrush;
                DockerStatusText.Text = _isDockerReady ? "Çalışıyor / Hazır" : "Bağlantı Yok / Kapalı";
            });

            // Ollama Kontrolü
            _isOllamaReady = await CheckCommandAsync("ollama", "--version");
            Dispatcher.Invoke(() =>
            {
                OllamaStatusDot.Fill = _isOllamaReady ? _greenBrush : _redBrush;
                OllamaStatusText.Text = _isOllamaReady ? "Çalışıyor / Hazır" : "Bağlantı Yok / Kapalı";
            });
        }

        private void AppendLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText($"\n[{DateTime.Now:HH:mm:ss}] {message}");
                LogTextBox.ScrollToEnd();
            });
        }

        private async Task<bool> CheckCommandAsync(string cmd, string args)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = cmd,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var process = Process.Start(startInfo);
                if (process == null) return false;
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            InstallButton.IsEnabled = false;
            UninstallButton.IsEnabled = false;
            OpenChatbotButton.IsEnabled = false;
            OpenSwaggerButton.IsEnabled = false;

            try
            {
                LogTextBox.Text = "[KURULUM SÜRECİ BAŞLATILDI]";
                InstallProgressBar.Value = 0;

                // 1. Docker Kontrolü
                ProgressLabel.Text = "Docker kontrol ediliyor...";
                InstallProgressBar.Value = 10;
                AppendLog("Docker durumu denetleniyor...");
                await CheckStatusesAsync();
                if (!_isDockerReady)
                {
                    AppendLog("HATA: Docker Desktop yüklü değil veya çalışmıyor!");
                    AppendLog("Lütfen Docker Desktop uygulamasını başlatın veya kurup tekrar deneyin.");
                    MessageBox.Show("Docker Desktop çalışır durumda olmalıdır. Lütfen Docker'ı açıp tekrar deneyin.", "Docker Gerekli", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                AppendLog("Docker çalışıyor, kuruluma devam ediliyor.");

                // 2. Ollama Kontrolü ve Gerekirse Kurulumu
                ProgressLabel.Text = "Ollama kontrol ediliyor...";
                InstallProgressBar.Value = 20;
                AppendLog("Ollama AI Engine denetleniyor...");
                if (!_isOllamaReady)
                {
                    AppendLog("Ollama kurulu veya çalışır durumda bulunamadı!");
                    var result = MessageBox.Show("Ollama sisteminizde bulunamadı. Otomatik olarak indirilip kurulmasını onaylıyor musunuz?", "Ollama Kurulumu", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        AppendLog("Ollama Setup indiriliyor (https://ollama.com/download/OllamaSetup.exe)...");
                        ProgressLabel.Text = "Ollama indiriliyor...";
                        var tempPath = Path.Combine(Path.GetTempPath(), "OllamaSetup.exe");
                        
                        using (var client = new HttpClient())
                        {
                            var response = await client.GetAsync("https://ollama.com/download/OllamaSetup.exe");
                            using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                await response.Content.CopyToAsync(fileStream);
                            }
                        }
                        AppendLog("Ollama Setup başarıyla indirildi. Yükleyici çalıştırılıyor, lütfen ekrandaki yönergeleri tamamlayın...");
                        ProgressLabel.Text = "Ollama yükleniyor...";
                        
                        var setupProcess = Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
                        if (setupProcess != null)
                        {
                            await setupProcess.WaitForExitAsync();
                        }
                        
                        AppendLog("Ollama yüklemesi tamamlandı. Başlatılması bekleniyor...");
                        await Task.Delay(5000); // Servisin kendine gelmesi için
                        await CheckStatusesAsync();
                    }
                    else
                    {
                        AppendLog("Ollama kurulumu iptal edildi. Yapay zeka özellikleri olmadan devam ediliyor.");
                    }
                }

                // Eğer Ollama hazırsa, çevre değişkenini (0.0.0.0) set edip servisi yeniden başlatıyoruz
                if (_isOllamaReady)
                {
                    await ConfigureAndStartOllamaAsync();
                    await CheckStatusesAsync();
                }

                // 3. Ollama Llama3 Modelini Çekme
                if (_isOllamaReady)
                {
                    ProgressLabel.Text = "Ollama model çekimi...";
                    InstallProgressBar.Value = 40;
                    AppendLog("Llama3 modeli yerel Ollama motoruna çekiliyor (Bu işlem indirme hızınıza bağlı sürebilir)...");
                    await RunProcessAsync("ollama", "pull llama3", GetSolutionRoot());
                    AppendLog("Llama3 modeli indirme/kontrol aşaması bitti.");

                    AppendLog("İlk kullanım gecikmesini önlemek için Llama3 modeli belleğe yükleniyor (Isıtılıyor)...");
                    ProgressLabel.Text = "Llama3 modeli ısıtılıyor...";
                    try
                    {
                        using var client = new HttpClient();
                        client.Timeout = TimeSpan.FromMinutes(3);
                        var response = await client.PostAsync("http://localhost:11434/api/generate", 
                            new StringContent("{\"model\": \"llama3\"}", System.Text.Encoding.UTF8, "application/json"));
                        if (response.IsSuccessStatusCode)
                        {
                            AppendLog("Llama3 modeli başarıyla belleğe önceden yüklendi.");
                        }
                        else
                        {
                            AppendLog($"Model yükleme uyarısı: {response.ReasonPhrase}");
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"Model yüklenirken uyarı/hata oluştu: {ex.Message}");
                    }
                }

                // 4. Docker Compose ile Build + Başlat (Asterisk + CallCenterApp)
                ProgressLabel.Text = "Eski konteynerler temizleniyor...";
                InstallProgressBar.Value = 55;
                var rootDir = GetSolutionRoot();
                await RunProcessAsync("docker", "rm -f callcenter-app asterisk", rootDir);

                ProgressLabel.Text = "Docker Compose derleniyor ve başlatılıyor...";
                InstallProgressBar.Value = 60;
                AppendLog("Docker Compose ile Asterisk ve CallCenterAssistant servisleri derleniyor...");
                AppendLog("(İlk kurulumda ses modelleri indirildiğinden 3-5 dakika sürebilir)");
                var buildSuccess = await RunProcessAsync("docker", "compose up -d --build", rootDir);
                if (!buildSuccess)
                {
                    AppendLog("HATA: docker compose up sırasında hata oluştu!");
                    MessageBox.Show("Docker Compose çalıştırılamadı. Lütfen internet bağlantınızı ve Docker uygulamasını kontrol edin.", "Derleme Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                AppendLog("Tüm servisler başarıyla ayağa kaldırıldı.");

                // 5. Servislerin hazır olmasını bekle
                ProgressLabel.Text = "Servisler başlatılıyor, bekleniyor...";
                InstallProgressBar.Value = 90;
                AppendLog("Asterisk ve API'nin tam hazır olması için 10 saniye bekleniyor...");
                await Task.Delay(10000);

                // 6. Çalışan konteynerleri logla
                AppendLog("--- Çalışan Docker Konteynerleri ---");
                await RunProcessAsync("docker", "compose ps", rootDir);

                InstallProgressBar.Value = 100;
                ProgressLabel.Text = "Kurulum Tamamlandı!";
                AppendLog("\n✅ Tüm servisler başarıyla Docker üzerinde çalışıyor!");
                AppendLog("📞 Asterisk PBX  → SIP: localhost:5060 | WS: localhost:8088 | AMI: localhost:5038");
                AppendLog("🤖 Chatbot API   → http://localhost:8080");
                AppendLog("📖 Swagger UI    → http://localhost:8080/swagger/index.html");
                OpenChatbotButton.IsEnabled = true;
                OpenSwaggerButton.IsEnabled = true;
                MessageBox.Show("Kurulum başarıyla tamamlandı!\n\nAsterisk PBX ve CallCenterAssistant API çalışıyor.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppendLog($"HATA OLUŞTU: {ex.Message}");
                MessageBox.Show($"Kurulum sırasında beklenmeyen bir hata oluştu: {ex.Message}", "Beklenmedik Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                InstallButton.IsEnabled = true;
                UninstallButton.IsEnabled = true;
            }
        }

        private async void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            var confirmResult = MessageBox.Show("Uygulamayı ve Docker konteyner/imajlarını silmek istediğinize emin misiniz?", "Sistem Kaldırma Onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirmResult != MessageBoxResult.Yes) return;

            InstallButton.IsEnabled = false;
            UninstallButton.IsEnabled = false;
            OpenChatbotButton.IsEnabled = false;
            OpenSwaggerButton.IsEnabled = false;

            try
            {
                LogTextBox.Text = "[SİSTEM KALDIRILIYOR]";
                InstallProgressBar.Value = 0;
                var rootDir = GetSolutionRoot();

                ProgressLabel.Text = "Konteyner durduruluyor...";
                InstallProgressBar.Value = 30;
                AppendLog("Docker Compose servisler durduruluyor ve imajlar siliniyor...");
                var downSuccess = await RunProcessAsync("docker", "compose down --rmi all --volumes", rootDir);

                InstallProgressBar.Value = 100;
                ProgressLabel.Text = "Sistem Temizlendi!";
                if (downSuccess)
                {
                    AppendLog("✅ Tüm Docker Compose konteynerleri, imajları ve volume'ları silindi.");
                    MessageBox.Show("Tüm servisler (Asterisk + API) başarıyla durduruldu ve temizlendi.", "Kaldırma Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    AppendLog("Uyarı: Bazı kaynaklar silinememiş olabilir. 'docker compose ps' ile kontrol edin.");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"HATA: {ex.Message}");
            }
            finally
            {
                InstallButton.IsEnabled = true;
                UninstallButton.IsEnabled = true;
            }
        }

        private void OpenChatbotButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("http://localhost:8080/index.html") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppendLog($"Tarayıcı açılamadı: {ex.Message}");
            }
        }

        private void OpenSwaggerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("http://localhost:8080/swagger/index.html") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppendLog($"Tarayıcı açılamadı: {ex.Message}");
            }
        }

        private string GetSolutionRoot()
        {
            var dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "docker-compose.yml")))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            return Directory.GetCurrentDirectory();
        }

        private async Task<bool> RunProcessAsync(string cmd, string args, string workingDir)
        {
            var tcs = new TaskCompletionSource<bool>();

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = cmd,
                    Arguments = args,
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

                process.Exited += (sender, args) =>
                {
                    tcs.SetResult(process.ExitCode == 0);
                    process.Dispose();
                };

                process.Start();

                // Çıktıları arka planda oku ve log ekranına bas
                _ = Task.Run(async () =>
                {
                    using var reader = process.StandardOutput;
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        AppendLog(line);
                    }
                });

                _ = Task.Run(async () =>
                {
                    using var reader = process.StandardError;
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        // Hataları da logla
                        AppendLog($"[Hata/Bilgi] {line}");
                    }
                });
            }
            catch (Exception ex)
            {
                AppendLog($"Çalıştırma hatası: {ex.Message}");
                tcs.SetResult(false);
            }

            return await tcs.Task;
        }

        private async Task ConfigureAndStartOllamaAsync()
        {
            AppendLog("Ollama çevre değişkenleri (OLLAMA_HOST=0.0.0.0) yapılandırılıyor...");
            try
            {
                Environment.SetEnvironmentVariable("OLLAMA_HOST", "0.0.0.0", EnvironmentVariableTarget.User);
                AppendLog("OLLAMA_HOST çevre değişkeni '0.0.0.0' olarak kalıcı kaydedildi.");
            }
            catch (Exception ex)
            {
                AppendLog($"Çevre değişkeni kalıcı olarak kaydedilemedi: {ex.Message}");
            }

            AppendLog("Dış bağlantı ayarlarının aktif olması için mevcut Ollama süreçleri yeniden başlatılıyor...");
            try
            {
                var processes = System.Diagnostics.Process.GetProcesses();
                foreach (var p in processes)
                {
                    try
                    {
                        if (p.ProcessName.Contains("ollama", StringComparison.OrdinalIgnoreCase))
                        {
                            AppendLog($"Mevcut Ollama süreci sonlandırılıyor: {p.ProcessName} (PID: {p.Id})");
                            p.Kill();
                            await p.WaitForExitAsync();
                        }
                    }
                    catch {}
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Ollama süreçleri denetlenirken hata: {ex.Message}");
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = "serve",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.EnvironmentVariables["OLLAMA_HOST"] = "0.0.0.0";
                Process.Start(startInfo);
                AppendLog("Ollama servisi arka planda 0.0.0.0 (dışarıya açık) olarak başlatıldı.");
                await Task.Delay(3000); // 3 saniye açılmasını bekle
            }
            catch (Exception ex)
            {
                AppendLog($"Ollama otomatik başlatılamadı: {ex.Message}. Lütfen Ollama uygulamasını manuel olarak tekrar açın.");
            }
        }
    }
}
