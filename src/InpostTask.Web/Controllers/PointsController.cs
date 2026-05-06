using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
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
    public IActionResult Search(PointSearchRequest request)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", request);
        }

        return RedirectToAction(nameof(Results), BuildRouteValues(request, 1));
    }

    [HttpGet]
    public async Task<IActionResult> Results(PointSearchRequest request, int page = 1, CancellationToken cancellationToken = default)
    {
        var results = await pointSearchService.SearchAsync(
            request,
            page,
            PointSearchService.DefaultPageSize,
            cancellationToken);
        return View("Results", results);
    }

    private static RouteValueDictionary BuildRouteValues(PointSearchRequest request, int page)
    {
        var values = new RouteValueDictionary
        {
            ["page"] = page,
            ["CountryCode"] = request.CountryCode,
            ["City"] = request.City,
            ["RequiredFunctionsCsv"] = request.RequiredFunctionsCsv,
            ["ReferenceLatitude"] = request.ReferenceLatitude,
            ["ReferenceLongitude"] = request.ReferenceLongitude,
            ["PreferredType"] = request.PreferredType,
            ["PreferredLocationType"] = request.PreferredLocationType,
            ["Require247"] = request.Require247,
            ["RequirePayment"] = request.RequirePayment
        };

        return values;
    }
}
