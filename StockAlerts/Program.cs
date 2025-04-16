using GrpcLoggerService;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddMemoryCache();

builder.Services.AddGrpc();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.CustomSchemaIds(x => x.FullName));





//builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

//app.MapGrpcService<LoggerService>().EnableGrpcWeb() 
//                                                  .RequireCors("AllowAll");

app.UseEndpoints(endpoints =>
 {
     endpoints.MapGrpcService<LoggerService>().EnableGrpcWeb()
                                       .RequireCors("AllowAll");
 });
 app.MapControllers();

app.Run();
