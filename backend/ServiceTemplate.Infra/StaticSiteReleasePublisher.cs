using System.Net;
using System.Text;
using ServiceTemplate.Ports.Output;

namespace ServiceTemplate.Infra;

/// <summary>
/// Drives the static landing page pipeline (Database -> Static Generator -> HTML -> Shared Volume
/// -> Nginx): renders one pregenerated, static HTML file per release into a shared directory that
/// Nginx serves directly. The only JS on the page is a small inline tracking beacon (page-view ping,
/// dwell-time-aware CTA link, invisible honeypot link) — no framework, no runtime rendering.
/// </summary>
public class StaticSiteReleasePublisher(string outputDirectory) : IReleasePublisher
{
    public Task PublishAsync(Release release)
    {
        Directory.CreateDirectory(outputDirectory);
        var html = Render(release);
        return File.WriteAllTextAsync(PathFor(release.Slug), html);
    }

    public Task UnpublishAsync(string slug)
    {
        var path = PathFor(slug);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    private string PathFor(string slug) => Path.Combine(outputDirectory, $"{slug}.html");

    private static string Render(Release release)
    {
        var destinationButtons = new StringBuilder();
        foreach (var link in release.Links)
        {
            destinationButtons.Append(
                $"""<a class="cta" data-destination="{Html(link.Platform.ToLowerInvariant())}" data-url="{Html(link.Url)}" href="{Html(link.Url)}">{Html(release.CtaText)}</a>""");
        }

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="UTF-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <title>{{Html(release.Title)}}</title>
              <meta property="og:title" content="{{Html(release.Title)}}" />
              <meta property="og:description" content="{{Html(release.Description)}}" />
              <meta property="og:image" content="{{Html(release.CoverImageUrl)}}" />
              <style>
                body { margin: 0; font-family: system-ui, sans-serif; color: #fff; background: #000 url('{{Html(release.BackgroundImageUrl)}}') center/cover no-repeat; }
                .wrap { min-height: 100vh; display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 2rem; text-align: center; background: rgba(0,0,0,0.45); }
                img.cover { width: 220px; height: 220px; object-fit: cover; border-radius: 12px; box-shadow: 0 10px 40px rgba(0,0,0,0.5); }
                h1 { margin: 1.5rem 0 0.25rem; font-size: 1.5rem; }
                p.headline { margin: 0 0 1.5rem; opacity: 0.85; }
                .cta { display: inline-block; padding: 0.9rem 2.5rem; background: #1DB954; color: #fff; text-decoration: none; border-radius: 999px; font-weight: 600; }
                .trap { position: absolute; left: -9999px; top: -9999px; }
              </style>
            </head>
            <body>
              <div class="wrap">
                <img class="cover" src="{{Html(release.CoverImageUrl)}}" alt="{{Html(release.Title)}}" />
                <h1>{{Html(release.Title)}}</h1>
                <p class="headline">{{Html(release.Headline)}}</p>
                {{destinationButtons}}
              </div>
              <a class="trap" href="/trap/{{Html(release.Slug)}}" tabindex="-1" aria-hidden="true">.</a>
              <script>
                (function () {
                  var slug = {{JsString(release.Slug)}};
                  var viewedAt = Date.now();
                  fetch('/pv/' + slug, { method: 'GET', keepalive: true }).catch(function () {});

                  document.querySelectorAll('.cta').forEach(function (btn) {
                    btn.addEventListener('click', function (e) {
                      e.preventDefault();
                      var dwell = Date.now() - viewedAt;
                      var destination = btn.getAttribute('data-destination');
                      window.location.href = '/out/' + slug + '/' + destination + '?dwell=' + dwell;
                    });
                  });
                })();
              </script>
            </body>
            </html>
            """;
    }

    private static string Html(string value) => WebUtility.HtmlEncode(value);

    private static string JsString(string value) => System.Text.Json.JsonSerializer.Serialize(value);
}
