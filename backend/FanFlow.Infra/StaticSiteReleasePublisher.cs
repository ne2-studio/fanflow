using System.Net;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using FanFlow.Ports.Output;

namespace FanFlow.Infra;

/// <summary>
/// Drives the static landing page pipeline (Database -> Static Generator -> HTML -> MinIO -> Nginx):
/// renders one pregenerated, static HTML file per release into an S3-compatible object store (MinIO)
/// that a thin Nginx container proxies to the public internet. The only JS on the page is a small
/// inline tracking beacon (page-view ping, dwell-time-aware CTA link, invisible honeypot link) — no
/// framework, no runtime rendering.
/// </summary>
public class StaticSiteReleasePublisher(IAmazonS3 s3Client, string bucketName) : IReleasePublisher
{
    public async Task PublishAsync(Release release)
    {
        var html = Render(release);
        await s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = KeyFor(release.Slug),
            ContentBody = html,
            ContentType = "text/html"
        });
    }

    public async Task UnpublishAsync(string slug)
    {
        await s3Client.DeleteObjectAsync(bucketName, KeyFor(slug));
    }

    private static string KeyFor(string slug) => $"{slug}.html";

    /// <summary>
    /// Creates the publishing bucket if missing and grants anonymous read access, since these
    /// objects are public landing pages served straight through Nginx without request signing.
    /// Idempotent — safe to call on every startup.
    /// </summary>
    public static async Task EnsureBucketAsync(IAmazonS3 s3Client, string bucketName)
    {
        try
        {
            await s3Client.PutBucketAsync(bucketName);
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
        {
        }

        await s3Client.PutBucketPolicyAsync(bucketName, PublicReadPolicy(bucketName));
    }

    private static string PublicReadPolicy(string bucketName) => $$"""
        {
          "Version": "2012-10-17",
          "Statement": [
            {
              "Effect": "Allow",
              "Principal": "*",
              "Action": ["s3:GetObject"],
              "Resource": ["arn:aws:s3:::{{bucketName}}/*"]
            }
          ]
        }
        """;

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
              <title>{{Html(release.ArtistName)}} - {{Html(release.Title)}}</title>
              <meta property="og:title" content="{{Html(release.ArtistName)}} - {{Html(release.Title)}}" />
              <meta property="og:description" content="{{Html(release.Description)}}" />
              <meta property="og:image" content="{{Html(release.CoverImageUrl)}}" />
              <style>
                body { margin: 0; font-family: system-ui, sans-serif; color: #fff; background: #000; overflow-x: hidden; }
                .backdrop {
                  position: fixed; inset: -40px;
                  background: url('{{Html(release.CoverImageUrl)}}') center/cover no-repeat;
                  filter: blur(40px) brightness(0.55);
                  transform: scale(1.15);
                  z-index: -1;
                }
                .wrap { position: relative; min-height: 100vh; display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 2rem; text-align: center; background: rgba(0,0,0,0.45); }
                img.cover { width: 220px; height: 220px; object-fit: cover; border-radius: 12px; box-shadow: 0 10px 40px rgba(0,0,0,0.5); }
                h1 { margin: 0.25rem 0 0.25rem; font-size: 1.5rem; }
                p.artist { margin: 1.5rem 0 0; font-size: 0.9rem; text-transform: uppercase; letter-spacing: 0.08em; opacity: 0.75; }
                p.headline { margin: 0 0 1.5rem; opacity: 0.85; }
                .cta { display: inline-block; padding: 0.9rem 2.5rem; background: #1DB954; color: #fff; text-decoration: none; border-radius: 999px; font-weight: 600; }
                .trap { position: absolute; left: -9999px; top: -9999px; }
              </style>
            </head>
            <body>
              <div class="backdrop"></div>
              <img height="1" width="1" style="display:none" alt="" src="https://www.facebook.com/tr?id={{Html(release.FacebookPixelId)}}&amp;ev=PageView&amp;noscript=1" />
              <div class="wrap">
                <img class="cover" src="{{Html(release.CoverImageUrl)}}" alt="{{Html(release.ArtistName)}} - {{Html(release.Title)}}" />
                <p class="artist">{{Html(release.ArtistName)}}</p>
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
