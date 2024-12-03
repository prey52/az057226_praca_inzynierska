using Backend.Database;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

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

//JWT
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
		ValidIssuer = builder.Configuration["Jwt:Issuer"],
		ValidAudience = builder.Configuration["Jwt:Audience"],
		IssuerSigningKey = new SymmetricSecurityKey(
			System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
	};
});

//CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        builder => builder.WithOrigins("https://localhost:7072")
                          .AllowAnyMethod()
                          .AllowAnyHeader());
});

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


app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
