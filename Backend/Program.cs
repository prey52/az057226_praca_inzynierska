using Backend.Classes.Database;
using Backend.Classes;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//DBcontext
builder.Services.AddDbContext<CardsDBContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Cards")));

//Identity
builder.Services.AddIdentity<DBUser, IdentityRole>()
    .AddEntityFrameworkStores<CardsDBContext>()
    .AddDefaultTokenProviders();

//JWT (JSON Web Tokens)
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"], //
        ValidAudience = builder.Configuration["Jwt:Issuer"], //
        IssuerSigningKey = new SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };

    //debug
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"Token failed validation: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine("Token validated successfully.");
            return Task.CompletedTask;
        }
    };
});
//debug
builder.Logging.ClearProviders();
builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Debug);


//CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        builder => builder.WithOrigins("https://localhost:7072")
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials());
});

builder.Services.AddSignalR();
builder.Services.AddSingleton<LobbyManager>();
builder.Services.AddSingleton<GameManager>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//enable CORS
app.UseCors("AllowFrontend");

//DEBUG info
app.UseDeveloperExceptionPage(); // Only in Development!
app.Use(async (context, next) =>
{
    var token = context.Request.Headers["Authorization"].ToString();
    Console.WriteLine($"Authorization Header: {token}");
    await next.Invoke();
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<LobbyHub>("/lobbyhub");
app.MapHub<GameHub>("/gamehub");

app.MapControllers();

app.Run();
