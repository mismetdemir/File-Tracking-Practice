using Microsoft.EntityFrameworkCore;
using FileTrackingPractice.Data;
using FileTrackingPractice.Config;
using FileTrackingPractice.Services;
using FileTrackingPractice.BackgroundServices;
using FileTrackingPractice.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(
    builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<FileScanSettings>(
    builder.Configuration.GetSection("FileScan"));

builder.Services.AddScoped<IFileScannerService, FileScannerService>();
builder.Services.AddHostedService<AutoFileScanService>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
