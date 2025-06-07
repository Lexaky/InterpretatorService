using InterpretatorService.Interfaces;
using InterpretatorService.Services;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.EntityFrameworkCore;
using InterpretatorService.Data;
using System;

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
                c.CustomSchemaIds(type => type.FullName); // Избежать конфликтов схем
                Console.WriteLine("SwaggerGen configured successfully");
            });

            var app = builder.Build();
            var lifetime = app.Lifetime;

            // Конфигурация middleware
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "InterpretatorService API v1"));
            }

            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine("Произведён выход из приложения");
            };

            //app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }

        public class FileUploadOperationFilter : IOperationFilter
        {
            public void Apply(OpenApiOperation operation, OperationFilterContext context)
            {
                Console.WriteLine($"Checking operation filter for method: {context.MethodInfo.Name} in {context.MethodInfo.DeclaringType.FullName}");

                // Проверяем метод Upload
                if (context.MethodInfo.Name.Equals("Upload", StringComparison.OrdinalIgnoreCase) &&
                    context.MethodInfo.DeclaringType.FullName.Contains("InterpretatorService.Controllers.CodeController"))
                {
                    Console.WriteLine("Applying filter to Upload method");
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
                                        ["CodeFile"] = new OpenApiSchema
                                        {
                                            Type = "string",
                                            Format = "binary",
                                            Description = "C# code file (.cs)"
                                        },
                                        ["ImageFile"] = new OpenApiSchema
                                        {
                                            Type = "string",
                                            Format = "binary",
                                            Description = "Image file (.jpeg or .jpg, optional)"
                                        },
                                        ["AlgorithmName"] = new OpenApiSchema
                                        {
                                            Type = "string",
                                            Description = "Name of the algorithm"
                                        }
                                    },
                                    Required = new HashSet<string> { "CodeFile", "AlgorithmName" }
                                }
                            }
                        },
                        Description = "Upload C# code file, optional image, and algorithm name"
                    };
                }
                // Проверяем метод UpdateAlgorithmPicture
                else if (context.MethodInfo.Name.Equals("UpdateAlgorithmPicture", StringComparison.OrdinalIgnoreCase) &&
                         context.MethodInfo.DeclaringType.FullName.Contains("InterpretatorService.Controllers.CodeController"))
                {
                    Console.WriteLine("Applying filter to UpdateAlgorithmPicture method");
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
                                        ["imageFile"] = new OpenApiSchema
                                        {
                                            Type = "string",
                                            Format = "binary",
                                            Description = "Image file (.jpeg or .jpg)"
                                        },
                                        ["AlgoId"] = new OpenApiSchema
                                        {
                                            Type = "integer",
                                            Description = "Id of the algorithm"
                                        }
                                    },
                                    Required = new HashSet<string> { "imageFile" }
                                }
                            }
                        },
                        Description = "Update algorithm picture"
                    };
                }
            }
        }
    }
}