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
/// inline tracking beacon (page-view ping, dwell-time-aware CTA click ping, invisible honeypot link)
/// plus the CTA redirect itself: a native-app deep-link attempt (Spotify URI scheme / Android intent,
/// precomputed per link at render time by BuildDeepLinks) with a timed fallback to the web URL — no
/// framework, no runtime rendering. The honeypot link is emitted as the very first element inside
/// &lt;body&gt;, ahead of any real content, so a naive crawler that just follows the first link it finds
/// trips it immediately instead of reaching the destination links.
/// </summary>
public class StaticSiteReleasePublisher(IAmazonS3 s3Client, string bucketName, ISlugGenerator slugGenerator) : IReleasePublisher
{
    public async Task PublishAsync(Release release)
    {
        var html = Render(release, ContentNameFor(release));
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

    /// <summary>
    /// The Facebook Pixel/Conversions API "content_name" for this release: an artist+title slug
    /// (e.g. "sibelclan-upside-down-world"), independent of the page's own routing <see cref="Release.Slug"/>
    /// (which is derived from the title alone) so ad-platform attribution can group events by
    /// artist+release without changing published URLs.
    /// </summary>
    private string ContentNameFor(Release release) => slugGenerator.Generate($"{release.ArtistName} {release.Title}");

    private const string SpotifyLogoSvg = """<svg viewBox="0 0 496 512" width="24" height="24"><path fill="#1ED760" d="M248 8C111.1 8 0 119.1 0 256s111.1 248 248 248 248-111.1 248-248S384.9 8 248 8zm100.7 364.9c-4.2 0-6.8-1.3-10.7-3.6-62.4-37.6-135-39.2-206.7-24.5-3.9 1-9 2.6-11.9 2.6-9.7 0-15.8-7.7-15.8-15.8 0-10.3 6.1-15.2 13.6-16.8 81.9-18.1 165.6-16.5 237 26.2 6.1 3.9 9.7 7.4 9.7 16.5s-7.1 15.4-15.2 15.4zm26.9-65.6c-5.2 0-8.7-2.3-12.3-4.2-62.5-37-155.7-51.9-238.6-29.4-4.8 1.3-7.4 2.6-11.9 2.6-10.7 0-19.4-8.7-19.4-19.4s5.2-17.8 15.5-20.7c27.8-7.8 56.2-13.6 97.8-13.6 64.9 0 127.6 16.1 177 45.5 8.1 4.8 11.3 11 11.3 19.7-.1 10.8-8.5 19.5-19.4 19.5zm31-76.2c-5.2 0-8.4-1.3-12.9-3.9-71.2-42.5-198.5-52.7-280.9-29.7-3.6 1-8.1 2.6-12.9 2.6-13.2 0-23.3-10.3-23.3-23.6 0-13.6 8.4-21.3 17.4-23.9 35.2-10.3 74.6-15.2 117.5-15.2 73 0 149.5 15.2 205.4 47.8 7.8 4.5 12.9 10.7 12.9 22.6 0 13.6-11 23.3-23.2 23.3z"/></svg>""";

    private const string PlaySvg = """<svg class="play" viewBox="0 0 24 24" width="24" height="24"><path fill="currentColor" d="M8 5v14l11-7z"/></svg>""";

    /// <summary>
    /// Play-button overlay drawn on top of the cover art, inline as SVG so it never adds a second
    /// HTTP request (unlike the hand-composited Canva overlay this replaces, which baked the button
    /// into the uploaded image itself).
    /// </summary>
    private const string PlayOverlaySvg = """<svg class="play-overlay" viewBox="0 0 100 100" aria-hidden="true"><circle cx="50" cy="50" r="46" fill="rgba(255,255,255,.15)" stroke="#fff" stroke-width="3"/><path fill="#fff" d="M42 32 72 50 42 68Z"/></svg>""";

    private static string IconFor(string platform) => platform.Equals("spotify", StringComparison.OrdinalIgnoreCase)
        ? SpotifyLogoSvg
        : "";

    private static string Render(Release release, string contentName)
    {
        var destinationButtons = new StringBuilder();
        foreach (var link in release.Links)
        {
            var (appUri, androidIntent) = BuildDeepLinks(link);
            var deepLinkAttrs = new StringBuilder();
            if (appUri != null)
                deepLinkAttrs.Append($" data-app-uri=\"{Html(appUri)}\"");
            if (androidIntent != null)
                deepLinkAttrs.Append($" data-android-intent=\"{Html(androidIntent)}\"");

            destinationButtons.Append(
                $"""
                <a class="cta" data-destination="{Html(link.Platform.ToLowerInvariant())}" data-url="{Html(link.Url)}"{deepLinkAttrs} href="{Html(link.Url)}"><div>{IconFor(link.Platform)}<span class="lbl">{Html(release.CtaText)}</span></div><span class="play-lbl">Play{PlaySvg}</span></a>
                """);
        }

        var primaryLink = release.Links.Count > 0 ? release.Links[0] : null;
        var coverAttrs = new StringBuilder();
        var coverTag = "div";
        var coverOverlay = "";
        if (primaryLink != null)
        {
            var (coverAppUri, coverAndroidIntent) = BuildDeepLinks(primaryLink);
            coverTag = "a";
            coverAttrs.Append($" href=\"{Html(primaryLink.Url)}\" class=\"cta\" data-destination=\"{Html(primaryLink.Platform.ToLowerInvariant())}\" data-url=\"{Html(primaryLink.Url)}\"");
            if (coverAppUri != null)
                coverAttrs.Append($" data-app-uri=\"{Html(coverAppUri)}\"");
            if (coverAndroidIntent != null)
                coverAttrs.Append($" data-android-intent=\"{Html(coverAndroidIntent)}\"");
            coverOverlay = PlayOverlaySvg;
        }

        var hasDescription = !string.IsNullOrWhiteSpace(release.Headline);
        var bodyClass = hasDescription ? "" : " class=\"no-description\"";

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="UTF-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0" />
              <meta name="robots" content="noindex" />
              <link rel="icon" href="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 1 1'%3E%3Crect width='1' height='1' fill='%231DB954'/%3E%3C/svg%3E" />
              <title>{{Html(release.ArtistName)}} - {{Html(release.Title)}}</title>
              <meta name="description" content="{{Html(release.Description)}}" />
              <meta property="og:type" content="website" />
              <meta property="og:title" content="{{Html(release.ArtistName)}} - {{Html(release.Title)}}" />
              <meta property="og:description" content="{{Html(release.Description)}}" />
              <meta property="og:image" content="{{Html(release.CoverImageUrl)}}" />
              <link rel="preload" href="{{Html(release.CoverImageUrl)}}" as="image" fetchpriority="high" />
              <style>
                html { height: 100%; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Arial, sans-serif; }
                body { margin: 0; padding: 0; background: #0d0d0d; overflow: hidden; overflow-y: scroll; position: absolute; width: 100%; min-height: 100%; display: flex; justify-content: center; align-items: flex-start; text-align: center; }
                body, a { color: #fff; text-decoration: none; }
                main { background: none; width: 90%; max-width: 420px; margin: 32px 0 80px; position: relative; z-index: 1; }
                #bg { z-index: -1; opacity: .85; position: absolute; top: 0; left: 0; width: 100%; height: 100%; background: url('{{Html(release.CoverImageUrl)}}') 50% no-repeat; background-size: cover; overflow: hidden; }
                #bg::before { content: ""; position: absolute; inset: 0; background: inherit; background-size: cover; transform: scale(1.6); }
                #bg::after { content: ""; position: absolute; inset: 0; -webkit-backdrop-filter: blur(60px) saturate(1.4); backdrop-filter: blur(60px) saturate(1.4); background: linear-gradient(180deg, rgba(13,13,13,.35) 0%, rgba(13,13,13,.85) 100%); }
                #top { padding: 8px 4px 18px; margin-bottom: 8px; }
                #a { display: inline-block; position: relative; width: 320px; height: 320px; border-radius: 14px; background: url('{{Html(release.CoverImageUrl)}}') 50% no-repeat; background-size: cover; box-shadow: 0 10px 30px rgba(0,0,0,.45); margin: 0 auto; cursor: pointer; }
                .play-overlay { position: absolute; top: 50%; left: 50%; width: 30%; height: 30%; transform: translate(-50%, -50%); filter: drop-shadow(0 4px 14px rgba(0,0,0,.35)); pointer-events: none; }
                #t { margin: 18px 0 4px; font-size: 25px; font-weight: 700; letter-spacing: -.01em; }
                #t + #d { margin-top: 12px; }
                #d { font-size: 14px; line-height: 1.45; color: rgba(255,255,255,.72); margin: 0 6px 4px; white-space: pre-line; }
                #b { position: relative; padding: 0; }
                #b a { line-height: 24px; font-size: 16px; font-weight: 600; cursor: pointer; background: rgba(48,48,48,.72); backdrop-filter: blur(20px); -webkit-backdrop-filter: blur(20px); border: 1px solid rgba(255,255,255,.15); border-radius: 14px; margin-bottom: 10px; display: flex; align-items: center; justify-content: space-between; min-height: 56px; padding: 8px 16px 8px 18px; text-align: left; gap: 14px; transition: transform .12s ease, background .12s ease; }
                #b a:hover { background: rgba(64,64,64,.78); transform: translateY(-1px); }
                #b a > div { display: flex; align-items: center; gap: 14px; flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
                #b a > div svg { flex-shrink: 0; background: rgba(0,0,0,.55); border-radius: 10px; padding: 10px; width: 24px; height: 24px; box-sizing: content-box; }
                #b .play-lbl { border: none; padding: 0; font-size: 0; white-space: nowrap; margin-left: 8px; color: rgba(255,255,255,.5); flex-shrink: 0; display: inline-flex; align-items: center; }
                #b .play-lbl svg { width: 24px; height: 24px; color: rgba(255,255,255,.6); }
                #b a:hover .play-lbl svg { color: #fff; }
                svg.play { margin: 0; }
                #b svg, #b .play-lbl, #bg { vertical-align: middle; }
                body.no-title #a { margin-bottom: 24px; }
                body.no-title.no-description #a { margin-bottom: 10px; }
              </style>
            </head>
            <body{{bodyClass}}>
              <a href="/trap/{{Html(release.Slug)}}" tabindex="-1" aria-hidden="true" style="position:absolute;left:-9999px;top:-9999px;">.</a>
              <div id="bg"></div>
              <main>
                <div id="top">
                  <{{coverTag}} id="a" aria-label="Artwork"{{coverAttrs}}>{{coverOverlay}}</{{coverTag}}>
                  <div id="t">{{Html(release.ArtistName)}} - {{Html(release.Title)}}</div>
                  {{(hasDescription ? $"<div id=\"d\">{Html(release.Headline)}</div>" : "")}}
                </div>
                <div id="b">
                  {{destinationButtons}}
                </div>
              </main>
              <script>
                !function(f,b,e,v,n,t,s) {
                  if(f.fbq)return;
                  n=f.fbq=function(){n.callMethod?
                    n.callMethod.apply(n,arguments):n.queue.push(arguments)};
                  if(!f._fbq)f._fbq=n;
                  n.push=n;
                  n.loaded=!0;
                  n.version='2.0';
                  n.queue=[];
                  t=b.createElement(e);
                  t.async=!0;
                  t.src=v;
                  s=b.getElementsByTagName(e)[0];
                  s.parentNode.insertBefore(t,s);
                }(window, document, 'script', 'https://connect.facebook.net/en_US/fbevents.js');

                fbq('init', {{JsString(release.FacebookPixelId)}});

                (function () {
                  var slug = {{JsString(release.Slug)}};
                  var contentName = {{JsString(contentName)}};
                  var viewedAt = Date.now();
                  var lastClick = 0;
                  var ua = navigator.userAgent || '';
                  var isAndroid = /Android/i.test(ua);
                  var isIOS = /iPhone|iPad|iPod/i.test(ua);

                  function newEventId(prefix) {
                    if (window.crypto && typeof window.crypto.randomUUID === 'function') {
                      return prefix + '_' + window.crypto.randomUUID();
                    }
                    return prefix + '_' + Date.now() + '_' + Math.random().toString(36).slice(2);
                  }

                  // _fbp is set by the Meta Pixel; _fbc is set by Meta when present, or derived here
                  // from a ?fbclid= ad-click param using Meta's own "fb.1.<ms>.<fbclid>" convention.
                  // Both are read fresh per beacon (not cached at page load) since the Pixel may not
                  // have written _fbp yet by the time the initial PageView beacon fires.
                  function fbCookie(name) {
                    var match = document.cookie.match(new RegExp('(?:^|; )' + name + '=([^;]*)'));
                    return match ? decodeURIComponent(match[1]) : null;
                  }

                  function fbTrackingParams() {
                    var params = [];
                    var fbp = fbCookie('_fbp');
                    var fbc = fbCookie('_fbc');
                    if (!fbc) {
                      var fbclid = new URLSearchParams(location.search).get('fbclid');
                      if (fbclid) fbc = 'fb.1.' + Date.now() + '.' + fbclid;
                    }
                    if (fbp) params.push('fbp=' + encodeURIComponent(fbp));
                    if (fbc) params.push('fbc=' + encodeURIComponent(fbc));
                    return params;
                  }

                  // Shared with the server-side CAPI PageView call as event_id so Meta dedupes the
                  // browser Pixel event against the server-side one instead of counting both.
                  var pageViewEventId = newEventId('pv');
                  fbq('track', 'PageView', { content_name: contentName }, { eventID: pageViewEventId });

                  var pvParams = fbTrackingParams();
                  pvParams.push('eid=' + encodeURIComponent(pageViewEventId));
                  fetch('/pv/' + slug + '?' + pvParams.join('&'), { method: 'GET', keepalive: true }).catch(function () {});

                  document.querySelectorAll('.cta').forEach(function (btn) {
                    btn.addEventListener('click', function (e) {
                      e.preventDefault();
                      var now = Date.now();
                      if (now - lastClick < 500) return;
                      lastClick = now;

                      var dwell = now - viewedAt;
                      var destination = btn.getAttribute('data-destination');
                      var webUrl = btn.getAttribute('data-url');
                      var appUri = btn.getAttribute('data-app-uri');
                      var androidIntent = btn.getAttribute('data-android-intent');

                      // SpotifyClick has no browser Pixel event, so there's nothing to dedupe here —
                      // the id is still generated and sent for traceability/idempotency on Meta's side.
                      var clickEventId = newEventId('click');
                      var clickParams = fbTrackingParams();
                      clickParams.push('eid=' + encodeURIComponent(clickEventId));
                      var outUrl = '/out/' + slug + '/' + destination + '?dwell=' + dwell +
                        clickParams.map(function (p) { return '&' + p; }).join('');
                      fetch(outUrl, { method: 'GET', keepalive: true }).catch(function () {});

                      if (isAndroid && androidIntent) {
                        location.href = androidIntent;
                        setTimeout(function () { location.href = webUrl; }, 1500);
                      } else if (isIOS && appUri) {
                        location.href = appUri;
                        setTimeout(function () { location.href = webUrl; }, 1500);
                      } else {
                        location.href = webUrl;
                      }
                    });
                  });
                })();
              </script>
            </body>
            </html>
            """;
    }

    /// <summary>
    /// Derives a native-app URI scheme and an Android intent URL from a Spotify web URL
    /// (e.g. https://open.spotify.com/track/{id} -> spotify:track:{id}:play), so the landing page
    /// can attempt to open the Spotify app directly before falling back to the web URL. The
    /// trailing ":play" is a Spotify URI convention that triggers autoplay of the linked item
    /// once the app opens, instead of just navigating to it. Only Spotify is a supported
    /// destination in the MVP (see docs/CONTRACT.md); any other platform, or a URL that doesn't
    /// match the expected shape, gets no deep link and just uses the web URL.
    /// </summary>
    private static (string? AppUri, string? AndroidIntent) BuildDeepLinks(DestinationLink link)
    {
        if (!string.Equals(link.Platform, "spotify", StringComparison.OrdinalIgnoreCase))
            return (null, null);

        if (!Uri.TryCreate(link.Url, UriKind.Absolute, out var uri))
            return (null, null);

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            return (null, null);

        var type = segments[0];
        var id = segments[1];
        return (
            $"spotify:{type}:{id}:play",
            $"intent://open.spotify.com/{type}/{id}:play#Intent;scheme=https;package=com.spotify.music;end");
    }

    private static string Html(string value) => WebUtility.HtmlEncode(value);

    private static string JsString(string value) => System.Text.Json.JsonSerializer.Serialize(value);
}
