using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace LeadRelay.UnitTests;

internal sealed class TestWebHostEnvironment(string environmentName) : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "LeadRelay.UnitTests";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = "";
    public string EnvironmentName { get; set; } = environmentName;
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
