using Microsoft.AspNetCore.Builder;

namespace RideBuilder.Affiliate.AspNetCore;

/// <summary>Wires the click_id capture middleware into the request pipeline.</summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Add the RideBuilder capture middleware. Place it early in the pipeline so the click_id is available
    /// on the request before your handlers run.
    /// </summary>
    public static IApplicationBuilder UseRideBuilderCapture(this IApplicationBuilder app)
        => app.UseMiddleware<RideBuilderCaptureMiddleware>();
}
