var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<InpostTask.Web.Services.InpostApiClient>(client =>
{
    client.BaseAddress = new Uri("https://api-global-points.easypack24.net/");
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddScoped<InpostTask.Web.Services.IInpostApiClient>(sp =>
    sp.GetRequiredService<InpostTask.Web.Services.InpostApiClient>());
builder.Services.AddScoped<InpostTask.Web.Services.PointSearchService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Points}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
