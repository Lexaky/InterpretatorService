using InterpretatorService.Interfaces;
using InterpretatorService.Services;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.EntityFrameworkCore;
using InterpretatorService.Data;

namespace InterpretatorService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Регистрация сервисов
            builder.Services.AddControllers();
            builder.Services.AddScoped<IInterpreterService, InterpreterService>();
            builder.Services.AddDbContext<TestsDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("TestsDb")));

            // Настройка Swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "InterpretatorService API", Version = "v1" });
                c.OperationFilter<FileUploadOperationFilter>();
            });

            var app = builder.Build();

            // Конфигурация middleware
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "InterpretatorService API v1"));
            }
            //app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();
            app.MapGet("/hello", () => "Hello World!");
            app.Run();
        }

        public class FileUploadOperationFilter : IOperationFilter
        {
            public void Apply(OpenApiOperation operation, OperationFilterContext context)
            {
                // Отладочный вывод
                Console.WriteLine($"Applying FileUploadOperationFilter to {context.MethodInfo.Name}");

                // Проверяем, является ли метод UploadCodeFile
                if (context.MethodInfo.Name == "UploadCodeFile")
                {
                    operation.Parameters.Clear();
                    operation.RequestBody = new OpenApiRequestBody
                    {
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["multipart/form-data"] = new OpenApiMediaType
                            {
                                Schema = new OpenApiSchema
                                {
                                    Type = "object",
                                    Properties = new Dictionary<string, OpenApiSchema>
                                    {
                                        ["codeFile"] = new OpenApiSchema
                                        {
                                            Type = "string",
                                            Format = "binary",
                                            Description = "C# code file (.cs)"
                                        },
                                        ["metaFile"] = new OpenApiSchema
                                        {
                                            Type = "string",
                                            Format = "binary",
                                            Description = "Metadata file (.txt)"
                                        }
                                    },
                                    Required = new HashSet<string> { "codeFile", "metaFile" }
                                }
                            }
                        },
                        Description = "Upload code and metadata files"
                    };
                }
            }
        }
    }
}