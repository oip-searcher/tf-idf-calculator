using TfIdfCalculator.Options;
using TfIdfCalculator.Services;
using TfIdfCalculator.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TfIdfOptions>(
    builder.Configuration.GetSection("TfIdf"));

builder.Services.AddSingleton<TfIdfService>();
builder.Services.AddHostedService<TfIdfWorker>();

var app = builder.Build();

app.Run();