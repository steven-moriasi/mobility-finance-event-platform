using System.Xml.Linq;

namespace MobilityFinance.Architecture.Tests;

public sealed class RepositoryStructureTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void ServicesDoNotReferenceOtherServices()
    {
        string servicesRoot = Path.Combine(RepositoryRoot, "src", "Services");

        foreach (string projectFile in Directory.EnumerateFiles(
                     servicesRoot,
                     "*.csproj",
                     SearchOption.AllDirectories))
        {
            XDocument project = XDocument.Load(projectFile);
            IEnumerable<string> references = project
                .Descendants("ProjectReference")
                .Select(reference => reference.Attribute("Include")?.Value ?? string.Empty);

            Assert.DoesNotContain(
                references,
                reference => reference.Contains(
                    $"{Path.DirectorySeparatorChar}Services{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase)
                    || reference.Contains("/Services/", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void WebApplicationDoesNotReferenceServiceImplementations()
    {
        string projectFile = Path.Combine(
            RepositoryRoot,
            "src",
            "Web",
            "MobilityFinance.Web",
            "MobilityFinance.Web.csproj");
        string contents = File.ReadAllText(projectFile);

        Assert.DoesNotContain("/Services/", contents, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\\Services\\", contents, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EverySourceProjectIsInTheSolution()
    {
        string solution = File.ReadAllText(Path.Combine(RepositoryRoot, "MobilityFinance.slnx"));
        IEnumerable<string> projects = Directory
            .EnumerateFiles(
                Path.Combine(RepositoryRoot, "src"),
                "*.csproj",
                SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(RepositoryRoot, path).Replace('\\', '/'));

        foreach (string project in projects)
        {
            Assert.Contains(project, solution, StringComparison.Ordinal);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MobilityFinance.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
