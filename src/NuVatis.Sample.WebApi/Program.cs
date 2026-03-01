using NuVatis.Sample.Core.Mappers;

var builder = WebApplication.CreateBuilder(args);

// 컨트롤러 추가
builder.Services.AddControllers();

// Mapper 등록 (Source Generator가 자동 구현)
builder.Services.AddScoped<IUserMapper, IUserMapper>();
builder.Services.AddScoped<IProductMapper, IProductMapper>();
builder.Services.AddScoped<IOrderMapper, IOrderMapper>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
