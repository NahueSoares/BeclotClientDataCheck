using BeclotClientDataCheck.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.Configure<AndreaniOptions>(builder.Configuration.GetSection("Andreani"));
builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBigCommerce", policy =>
    {
        policy.WithOrigins("https://cosmetica-v4.mybigcommerce.com",
                "https://talawork.com"
                )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddLogging();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors("AllowBigCommerce");
app.UseAuthorization();
app.MapControllers();

app.Run();