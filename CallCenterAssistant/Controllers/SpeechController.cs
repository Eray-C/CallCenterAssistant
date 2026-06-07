using CallCenterAssistant.Models.Request;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Whisper.net;
using Whisper.net.Ggml;

namespace CallCenterAssistant.Controllers
{
    [ApiController]
    [Route("api/speech")]
    public class SpeechController : ControllerBase
    {
        private readonly string _modelPath;
        private readonly IValidator<TtsRequest> _ttsValidator;

        public SpeechController(IConfiguration configuration, IValidator<TtsRequest> ttsValidator)
        {
            _modelPath = configuration["Whisper:ModelPath"] ?? "ggml-base.bin";
            _ttsValidator = ttsValidator;
        }

        [HttpPost("transcribe")]
        public async Task<IActionResult> Transcribe(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Lütfen geçerli bir ses dosyası yükleyin.");
            }

            // Model dosyasının varlığını kontrol et, yoksa indir
            if (!System.IO.File.Exists(_modelPath))
            {
                try
                {
                    using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType.Base);
                    using var fileStream = System.IO.File.OpenWrite(_modelPath);
                    await modelStream.CopyToAsync(fileStream);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Model dosyası indirilemedi veya oluşturulamadı: {ex.Message}");
                }
            }

            try
            {
                // Yüklenen dosya verilerini oku
                byte[] fileBytes;
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    fileBytes = ms.ToArray();
                }

                // 16kHz mono WAV formatına otomatik dönüştür
                byte[] resampledBytes;
                try
                {
                    resampledBytes = CallCenterAssistant.Services.WavResampler.ResampleTo16KhzMono(fileBytes);
                }
                catch (Exception ex)
                {
                    return BadRequest($"Ses dosyası çözümlenemedi veya dönüştürülemedi: {ex.Message} Not: Ses dosyasının standart PCM WAV formatında olduğundan emin olun.");
                }

                using var whisperFactory = WhisperFactory.FromPath(_modelPath);
                using var processor = whisperFactory.CreateBuilder()
                    .WithLanguage("tr")
                    .Build();

                using var waveStream = new MemoryStream(resampledBytes);
                
                var transcription = new StringBuilder();
                await foreach (var result in processor.ProcessAsync(waveStream))
                {
                    transcription.Append(result.Text).Append(' ');
                }

                return Ok(new
                {
                    text = transcription.ToString().Trim(),
                    language = "tr"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Transkripsiyon sırasında bir hata oluştu: {ex.Message}.");
            }
        }

        [HttpPost("synthesize")]
        public async Task<IActionResult> Synthesize([FromBody] TtsRequest request)
        {
            // FluentValidation ile istek doğrulama
            var validationResult = await _ttsValidator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });
            }

            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var piperDir = Path.Combine(Directory.GetCurrentDirectory(), "piper");
            var piperExeName = isWindows ? "piper.exe" : "piper";
            var piperExe = Path.Combine(piperDir, piperExeName);
            var modelPath = Path.Combine(piperDir, "tr_TR-fahrettin-medium.onnx");

            if (!System.IO.File.Exists(piperExe) || !System.IO.File.Exists(modelPath))
            {
                return StatusCode(500, $"Piper motoru ({piperExeName}) veya ses modeli bulunamadı. Lütfen kurulum adımlarını kontrol edin.");
            }

            var tempOutputFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.wav");

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = piperExe,
                    Arguments = $"-m \"{modelPath}\" -f \"{tempOutputFile}\"",
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = piperDir
                };

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();

                    using (var writer = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false)))
                    {
                        await writer.WriteAsync(request.Text);
                    }

                    await process.WaitForExitAsync();

                    var error = await process.StandardError.ReadToEndAsync();
                    if (process.ExitCode != 0)
                    {
                        return StatusCode(500, $"Piper hatası: {error}");
                    }
                }

                if (!System.IO.File.Exists(tempOutputFile))
                {
                    return StatusCode(500, "Ses dosyası üretilemedi.");
                }

                var bytes = await System.IO.File.ReadAllBytesAsync(tempOutputFile);
                return File(bytes, "audio/wav", "synthesized.wav");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Metin sese dönüştürülürken hata oluştu: {ex.Message}");
            }
            finally
            {
                if (System.IO.File.Exists(tempOutputFile))
                {
                    try
                    {
                        System.IO.File.Delete(tempOutputFile);
                    }
                    catch { }
                }
            }
        }
    }
}
