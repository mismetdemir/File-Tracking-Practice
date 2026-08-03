using Microsoft.EntityFrameworkCore;
using FileTrackingPractice.Data;
using FileTrackingPractice.Config;
using FileTrackingPractice.Services;
using FileTrackingPractice.BackgroundServices;

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
