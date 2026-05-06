using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using InpostTask.Web.Models;
using InpostTask.Web.Services;
using System.Globalization;

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
        NormalizeCoordinatesFromRequest(request, preferFormValues: true);
        if (!ModelState.IsValid)
        {
            return View("Index", request);
        }

        return RedirectToAction(nameof(Results), BuildRouteValues(request, 1));
    }

    [HttpGet]
    public async Task<IActionResult> Results(PointSearchRequest request, int page = 1, CancellationToken cancellationToken = default)
    {
        NormalizeCoordinatesFromRequest(request, preferFormValues: false);
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

    private void NormalizeCoordinatesFromRequest(PointSearchRequest request, bool preferFormValues)
    {
        var latRaw = ReadRawValue("ReferenceLatitude", preferFormValues);
        var lngRaw = ReadRawValue("ReferenceLongitude", preferFormValues);

        var hasLat = TryParseCoordinate(latRaw, out var lat);
        var hasLng = TryParseCoordinate(lngRaw, out var lng);

        // Handle culture differences (comma/dot) and avoid blocking search on parse quirks.
        if (hasLat)
        {
            request.ReferenceLatitude = lat;
            ModelState.Remove(nameof(PointSearchRequest.ReferenceLatitude));
        }

        if (hasLng)
        {
            request.ReferenceLongitude = lng;
            ModelState.Remove(nameof(PointSearchRequest.ReferenceLongitude));
        }
    }

    private string? ReadRawValue(string key, bool preferFormValues)
    {
        if (preferFormValues && Request.HasFormContentType)
        {
            var formValue = Request.Form[key].ToString();
            if (!string.IsNullOrWhiteSpace(formValue))
            {
                return formValue;
            }
        }

        var queryValue = Request.Query[key].ToString();
        if (!string.IsNullOrWhiteSpace(queryValue))
        {
            return queryValue;
        }

        return null;
    }

    private static bool TryParseCoordinate(string? raw, out double value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Trim();
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
        {
            return true;
        }

        normalized = normalized.Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
