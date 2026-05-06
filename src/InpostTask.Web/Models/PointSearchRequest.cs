using System.ComponentModel.DataAnnotations;

namespace InpostTask.Web.Models;

public sealed class PointSearchRequest
{
    [Display(Name = "Country code (e.g. PL, FR, ES)")]
    [MaxLength(3)]
    public string? CountryCode { get; set; }

    [Display(Name = "City")]
    [MaxLength(80)]
    public string? City { get; set; }

    [Display(Name = "Required functions (comma separated)")]
    [MaxLength(200)]
    public string? RequiredFunctionsCsv { get; set; }

    [Display(Name = "Reference latitude (optional)")]
    [Range(-90, 90)]
    public double? ReferenceLatitude { get; set; }

    [Display(Name = "Reference longitude (optional)")]
    [Range(-180, 180)]
    public double? ReferenceLongitude { get; set; }

    [Display(Name = "Preferred point type")]
    [MaxLength(30)]
    public string? PreferredType { get; set; }

    [Display(Name = "Preferred location type (Outdoor/Indoor)")]
    [MaxLength(30)]
    public string? PreferredLocationType { get; set; }

    [Display(Name = "Only 24/7 points")]
    public bool Require247 { get; set; }

    [Display(Name = "Must support payment")]
    public bool RequirePayment { get; set; }
}
