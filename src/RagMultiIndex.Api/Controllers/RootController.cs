using Microsoft.AspNetCore.Mvc;

namespace RagMultiIndex.Api.Controllers;

[ApiController]
public class RootController : ControllerBase
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        // Serve index.html from wwwroot (copied from templates)
        return new PhysicalFileResult(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html"), "text/html");
    }
}
