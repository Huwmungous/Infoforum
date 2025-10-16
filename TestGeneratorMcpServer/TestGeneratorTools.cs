using System.Text;
using System.Text.Json;

namespace TestGeneratorMcpServer;

public static class TestGeneratorTools
{
    public static Task<object> GenerateUnitTests(JsonElement args)
    {
        var className = args.GetProperty("className").GetString()!;
        var methods = args.GetProperty("methods").EnumerateArray().Select(m => new {
            Name = m.GetProperty("name").GetString()!,
            ReturnType = m.TryGetProperty("returnType", out var r) ? r.GetString() : "void"
        }).ToArray();
        var framework = args.TryGetProperty("framework", out var f) ? f.GetString() : "xUnit";
        
        var sb = new StringBuilder();
        sb.AppendLine("using Xunit;");
        sb.AppendLine();
        sb.AppendLine($"namespace {className}Tests;");
        sb.AppendLine();
        sb.AppendLine($"public class {className}Tests");
        sb.AppendLine("{");
        
        foreach (var method in methods)
        {
            sb.AppendLine($"    [Fact]");
            sb.AppendLine($"    public void {method.Name}_ShouldReturnExpectedResult()");
            sb.AppendLine("    {");
            sb.AppendLine("        // Arrange");
            sb.AppendLine($"        var sut = new {className}();");
            sb.AppendLine();
            sb.AppendLine("        // Act");
            sb.AppendLine($"        var result = sut.{method.Name}();");
            sb.AppendLine();
            sb.AppendLine("        // Assert");
            sb.AppendLine("        Assert.NotNull(result);");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        
        sb.AppendLine("}");
        
        return Task.FromResult<object>(new { success = true, className, testCount = methods.Length, code = sb.ToString() });
    }

    public static Task<object> GenerateIntegrationTests(JsonElement args)
    {
        var className = args.GetProperty("className").GetString()!;
        var databaseTests = args.TryGetProperty("databaseTests", out var dt) && dt.GetBoolean();
        
        var sb = new StringBuilder();
        sb.AppendLine("using Xunit;");
        if (databaseTests)
        {
            sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        }
        sb.AppendLine();
        sb.AppendLine($"namespace {className}IntegrationTests;");
        sb.AppendLine();
        sb.AppendLine($"public class {className}IntegrationTests : IDisposable");
        sb.AppendLine("{");
        
        if (databaseTests)
        {
            sb.AppendLine("    private readonly DbContext _context;");
            sb.AppendLine();
            sb.AppendLine($"    public {className}IntegrationTests()");
            sb.AppendLine("    {");
            sb.AppendLine("        // Setup test database");
            sb.AppendLine("        var options = new DbContextOptionsBuilder<YourDbContext>()");
            sb.AppendLine("            .UseInMemoryDatabase(databaseName: \"TestDb\")");
            sb.AppendLine("            .Options;");
            sb.AppendLine("        _context = new YourDbContext(options);");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        
        sb.AppendLine("    [Fact]");
        sb.AppendLine("    public async Task IntegrationTest_ShouldWork()");
        sb.AppendLine("    {");
        sb.AppendLine("        // Arrange");
        sb.AppendLine($"        var sut = new {className}();");
        sb.AppendLine();
        sb.AppendLine("        // Act");
        sb.AppendLine("        var result = await sut.ExecuteAsync();");
        sb.AppendLine();
        sb.AppendLine("        // Assert");
        sb.AppendLine("        Assert.NotNull(result);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public void Dispose()");
        sb.AppendLine("    {");
        if (databaseTests)
        {
            sb.AppendLine("        _context?.Dispose();");
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return Task.FromResult<object>(new { success = true, className, code = sb.ToString() });
    }

    public static Task<object> CreateMockData(JsonElement args)
    {
        var entityName = args.GetProperty("entityName").GetString()!;
        var count = args.TryGetProperty("count", out var c) ? c.GetInt32() : 10;
        var properties = args.GetProperty("properties").EnumerateArray().Select(p => new {
            Name = p.GetProperty("name").GetString()!,
            Type = p.GetProperty("type").GetString()!
        }).ToArray();
        
        var sb = new StringBuilder();
        sb.AppendLine($"public static List<{entityName}> Generate{entityName}MockData(int count = {count})");
        sb.AppendLine("{");
        sb.AppendLine($"    var data = new List<{entityName}>();");
        sb.AppendLine("    for (int i = 0; i < count; i++)");
        sb.AppendLine("    {");
        sb.AppendLine($"        data.Add(new {entityName}");
        sb.AppendLine("        {");
        
        foreach (var prop in properties)
        {
            var mockValue = prop.Type.ToLower() switch
            {
                "string" => "$\"Test{prop.Name}{i}\"",
                "int" or "integer" => "i",
                "decimal" or "double" => "i * 1.5m",
                "datetime" => "DateTime.Now.AddDays(i)",
                "bool" or "boolean" => "i % 2 == 0",
                _ => "default"
            };
            sb.AppendLine($"            {prop.Name} = {mockValue},");
        }
        
        sb.AppendLine("        });");
        sb.AppendLine("    }");
        sb.AppendLine("    return data;");
        sb.AppendLine("}");
        
        return Task.FromResult<object>(new { success = true, entityName, count, code = sb.ToString() });
    }

    public static Task<object> GenerateRepositoryTests(JsonElement args)
    {
        var repositoryName = args.GetProperty("repositoryName").GetString()!;
        var entityName = args.GetProperty("entityName").GetString()!;
        
        var sb = new StringBuilder();
        sb.AppendLine("using Xunit;");
        sb.AppendLine("using Moq;");
        sb.AppendLine();
        sb.AppendLine($"namespace {repositoryName}Tests;");
        sb.AppendLine();
        sb.AppendLine($"public class {repositoryName}Tests");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly Mock<IDbConnection> _mockConnection;");
        sb.AppendLine($"    private readonly {repositoryName} _repository;");
        sb.AppendLine();
        sb.AppendLine($"    public {repositoryName}Tests()");
        sb.AppendLine("    {");
        sb.AppendLine("        _mockConnection = new Mock<IDbConnection>();");
        sb.AppendLine($"        _repository = new {repositoryName}(_mockConnection.Object);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    [Fact]");
        sb.AppendLine("    public async Task GetAllAsync_ShouldReturnAllEntities()");
        sb.AppendLine("    {");
        sb.AppendLine("        // Arrange");
        sb.AppendLine($"        var expected = new List<{entityName}> {{ new {entityName}() }};");
        sb.AppendLine();
        sb.AppendLine("        // Act");
        sb.AppendLine("        var result = await _repository.GetAllAsync();");
        sb.AppendLine();
        sb.AppendLine("        // Assert");
        sb.AppendLine("        Assert.NotNull(result);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return Task.FromResult<object>(new { success = true, repositoryName, entityName, code = sb.ToString() });
    }

    public static Task<object> GenerateTestProject(JsonElement args)
    {
        var projectName = args.GetProperty("projectName").GetString()!;
        var framework = args.TryGetProperty("framework", out var f) ? f.GetString() : "xUnit";
        
        var csproj = new StringBuilder();
        csproj.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        csproj.AppendLine("  <PropertyGroup>");
        csproj.AppendLine("    <TargetFramework>net9.0</TargetFramework>");
        csproj.AppendLine("    <IsPackable>false</IsPackable>");
        csproj.AppendLine("  </PropertyGroup>");
        csproj.AppendLine("  <ItemGroup>");
        csproj.AppendLine("    <PackageReference Include=\"xunit\" Version=\"2.9.2\" />");
        csproj.AppendLine("    <PackageReference Include=\"xunit.runner.visualstudio\" Version=\"2.8.2\" />");
        csproj.AppendLine("    <PackageReference Include=\"Moq\" Version=\"4.20.72\" />");
        csproj.AppendLine("    <PackageReference Include=\"FluentAssertions\" Version=\"6.12.2\" />");
        csproj.AppendLine("  </ItemGroup>");
        csproj.AppendLine("</Project>");
        
        return Task.FromResult<object>(new { success = true, projectName, framework, csproj = csproj.ToString() });
    }

    public static Task<object> GenerateMockSetup(JsonElement args)
    {
        var interfaceName = args.GetProperty("interfaceName").GetString()!;
        var methods = args.GetProperty("methods").EnumerateArray().Select(m => m.GetString()!).ToArray();
        
        var sb = new StringBuilder();
        sb.AppendLine($"var mock{interfaceName.TrimStart('I')} = new Mock<{interfaceName}>();");
        sb.AppendLine();
        
        foreach (var method in methods)
        {
            sb.AppendLine($"mock{interfaceName.TrimStart('I')}.Setup(x => x.{method}())");
            sb.AppendLine("    .ReturnsAsync(expectedResult);");
            sb.AppendLine();
        }
        
        return Task.FromResult<object>(new { success = true, interfaceName, code = sb.ToString() });
    }
}
