using Microsoft.AspNetCore.Mvc;
using InpostTask.Web.Models;
using InpostTask.Web.Services;

namespace InpostTask.Web.Controllers;

public sealed class PointsController(PointSearchService pointSearchService) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View(new PointSearchRequest { CountryCode = "PL" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Search(PointSearchRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", request);
        }

        var results = await pointSearchService.SearchAsync(request, cancellationToken);
        return View("Results", results);
    }
}
