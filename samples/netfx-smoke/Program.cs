using System;
using System.Collections.Generic;
using RideBuilder.Affiliate;
using RideBuilder.Affiliate.Capture;

namespace RideBuilder.Affiliate.NetFxSmoke
{
    // Touches the public surface a .NET Framework retailer actually uses. Constructing the client is the
    // load-bearing line: its ctor exposes HttpClient, which is what breaks net4x consumers when the
    // package does not carry the System.Net.Http framework reference.
    internal static class Program
    {
        private const string ClickId = "1e8e6c0a-1111-4111-8111-111111111111";

        private static int Main()
        {
            Console.WriteLine(System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);

            var failures = new List<string>();

            void Check(string name, bool ok)
            {
                Console.WriteLine((ok ? "  ok   " : "  FAIL ") + name);
                if (!ok)
                {
                    failures.Add(name);
                }
            }

            Check("FromUrl", ClickCapture.FromUrl("/p?ref=ridebuilder&click_id=" + ClickId) == ClickId);

            var cookie = "ridebuilder_attribution=" + Uri.EscapeDataString(
                "{\"click_id\":\"" + ClickId + "\",\"ref\":\"ridebuilder\"}");
            Check("FromCookieHeader", ClickCapture.FromCookieHeader(cookie) == ClickId);

            var headers = new Dictionary<string, string?> { { "X-RideBuilder-Click-Id", ClickId } };
            Check("FromHeaders", ClickCapture.FromHeaders(headers) == ClickId);

            Check("SdkInfo", SdkInfo.Type == "dotnet_sdk" && !string.IsNullOrEmpty(SdkInfo.Version));

            using (var rb = new RideBuilderClient(new RideBuilderClientOptions { ApiKey = "sk_smoke" }))
            {
                var guarded = false;
                try
                {
                    rb.ReportCheckoutAsync(new CheckoutInput("O1", 199.999m, "USD", ClickId))
                      .GetAwaiter().GetResult();
                }
                catch (RideBuilderException)
                {
                    guarded = true;
                }

                Check("client ctor + input guard", guarded);
            }

            Console.WriteLine(failures.Count == 0
                ? "netfx smoke passed"
                : "netfx smoke FAILED: " + string.Join(", ", failures.ToArray()));
            return failures.Count == 0 ? 0 : 1;
        }
    }
}
