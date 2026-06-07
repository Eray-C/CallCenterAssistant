using CallCenterAssistant.Services;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IAsteriskService, AsteriskService>();
builder.Services.AddValidatorsFromAssemblyContaining<CallCenterAssistant.Validators.ChatRequestValidator>();

var app = builder.Build();

// Configure the HTTP request pipeline.
// Değerlendirme ve test kolaylığı için Swagger'ı tüm ortamlarda (Docker dahil) aktif ediyoruz.
app.UseSwagger();
app.UseSwaggerUI();

// Docker ortamında HTTP isteklerinin HTTPS'e zorlanarak hata vermemesi için yönlendirmeyi Production dışında tutuyoruz.
if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.EndsWith(".html") || ctx.File.Name.EndsWith(".js") || ctx.File.Name.EndsWith(".css"))
        {
            ctx.Context.Response.Headers.ContentType = ctx.Context.Response.Headers.ContentType + "; charset=utf-8";
        }
    }
});

app.UseAuthorization();

app.MapControllers();

app.Run();
