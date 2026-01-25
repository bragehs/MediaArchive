using MediaArchive.API.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins",
        policy =>
        {
            policy.WithOrigins("https://media-archive-alpha.vercel.app", 
                               "http://localhost:3000")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 21))));

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => "MediaArchive API is running! Try /WeatherForecast or /swagger");

app.UseHttpsRedirection();

app.UseCors("AllowSpecificOrigins");

app.UseAuthorization();

app.MapControllers();

using var scope = app.Services.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

var retryCount = 5;
while (retryCount > 0)
{
    try
    {
        context.Database.Migrate();
        break;
    }
    catch (MySqlConnector.MySqlException ex) when (ex.Message.Contains("Unable to connect"))
    {
        retryCount--;
        if (retryCount == 0) throw;
        Console.WriteLine($"Database not ready, retrying in 5 seconds... ({retryCount} retries left)");
        Thread.Sleep(5000);
    }
}

app.Run();