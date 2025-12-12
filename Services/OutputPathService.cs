using System.IO;

namespace CodeGenApp.Services;

public interface IOutputPathService
{
    string RootDir { get; }
    string DynamoDir { get; }
    string LambdaDir { get; }
}

public class OutputPathService : IOutputPathService
{
    public string RootDir { get; }
    public string DynamoDir { get; }
    public string LambdaDir { get; }

    public OutputPathService()
    {
        RootDir = EnsureRootOutputDirectory();
        DynamoDir = Path.Combine(RootDir, "dynamodb");
        LambdaDir = Path.Combine(RootDir, "lambda");
    }

    private string EnsureRootOutputDirectory()
    {
        // 🔒 Inside the Mac Catalyst sandbox
        // You confirmed this is the actual container path:
        // /Users/salmansaeed/Library/Containers/com.albagold.codegenapp/Data/Library
        var containerPath =
            "/Users/salmansaeed/Library/Containers/com.albagold.codegenapp/Data/Library";

        var rootDir = Path.Combine(containerPath, "codegen");

        Directory.CreateDirectory(rootDir);
        Directory.CreateDirectory(Path.Combine(rootDir, "dynamodb"));
        Directory.CreateDirectory(Path.Combine(rootDir, "lambda"));

        return rootDir;
    }
}
