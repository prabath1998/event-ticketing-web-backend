// Services/Email/DiscountEmailTemplate.cs
namespace EventTicketing.Services.Email;

public static class DiscountEmailTemplate
{
    public static string BuildHtml(
        string orgName,
        string logoUrl,           
        string eventTitle,
        DateTime startUtc,
        DateTime endUtc,
        string? city,
        string discountLabel,    
        string promoCode,         
        string validityText,     
        string ctaUrl,            
        string? heroUrl = null,  
        string? supportEmail = null,
        string? unsubscribeUrl = null)
    {
        string Safe(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        string headerLogo = string.IsNullOrWhiteSpace(logoUrl)
            ? $"<div style=\"font-weight:700;font-size:20px;color:#111\">{Safe(orgName)}</div>"
            : $"<img src=\"{Safe(logoUrl)}\" width=\"120\" alt=\"{Safe(orgName)}\" style=\"display:block;border:0;outline:none;text-decoration:none;max-width:120px;height:auto\"/>";

        string cityLine = string.IsNullOrWhiteSpace(city) ? "" :
            $@"<tr><td style=""padding-top:6px;font:500 14px/1.6 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial;color:#4b5563"">
                   <span style=""display:inline-block;padding:4px 10px;border-radius:999px;background:#eef2ff;color:#3730a3"">📍 {Safe(city)}</span>
               </td></tr>";

        string hero = string.IsNullOrWhiteSpace(heroUrl) ? "" :
            $@"<tr>
                 <td style=""padding:0 0 16px 0"">
                   <img src=""{Safe(heroUrl)}"" width=""600"" alt="""" style=""width:100%;max-width:600px;border-radius:14px;display:block;border:0""/>
                 </td>
               </tr>";
        
        return $@"
<!doctype html>
<html lang=""en"">
<head>
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0""/>
  <meta http-equiv=""Content-Type"" content=""text/html; charset=UTF-8""/>
  <title>{Safe(eventTitle)} — Discount</title>
  <style>
    @media (prefers-color-scheme: dark) {{
      .bg-body {{ background:#0b1020 !important; }}
      .card {{ background:#111827 !important; border-color:#1f2937 !important; }}
      .text {{ color:#e5e7eb !important; }}
      .muted {{ color:#9ca3af !important; }}
      .pill {{ background:#1f2937 !important; color:#e5e7eb !important; }}
    }}
    @media only screen and (max-width: 600px) {{
      .container {{ width:100% !important; }}
      .h1 {{ font-size:22px !important; }}
    }}
  </style>
</head>
<body class=""bg-body"" style=""margin:0;padding:0;background:#f3f4f6;"">
  <!-- Preheader (inbox preview) -->
  <div style=""display:none;max-height:0;overflow:hidden;opacity:0;visibility:hidden;mso-hide:all"">
    New discount {Safe(discountLabel)} for {Safe(eventTitle)} — use code {Safe(promoCode)}. {Safe(validityText)}.
  </div>

  <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background:#f3f4f6"" class=""bg-body"">
    <tr><td align=""center"" style=""padding:28px 14px"">

      <!-- container -->
      <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""600"" class=""container"" style=""width:600px;max-width:100%"">
        <!-- header -->
        <tr>
          <td align=""left"" style=""padding:0 4px 16px 4px"">
            {headerLogo}
          </td>
        </tr>

        <!-- card -->
        <tr>
          <td style=""background:#ffffff;border:1px solid #e5e7eb;border-radius:16px;padding:0"" class=""card"">
            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"">
              <tr><td style=""padding:22px 22px 0 22px"">
                <table role=""presentation"" width=""100%"">
                  <tr>
                    <td>
                      <div style=""font:700 12px/1.2 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial;color:#6b7280;text-transform:uppercase;letter-spacing:.08em"">
                        Limited Offer
                      </div>
                      <div class=""h1"" style=""margin-top:6px;font:800 26px/1.35 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial;color:#111827"">
                        {Safe(eventTitle)}
                      </div>
                      <div style=""margin-top:8px"">
                        <span style=""display:inline-block;padding:6px 10px;border-radius:999px;background:linear-gradient(135deg,#4f46e5,#06b6d4);color:#fff;font:700 12px/1 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial;letter-spacing:.04em"">
                          {Safe(discountLabel)}
                        </span>
                      </div>
                    </td>
                  </tr>
                  {cityLine}
                </table>
              </td></tr>

              {hero}

              <tr><td style=""padding:0 22px 6px 22px"">
                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"">
                  <tr>
                    <td style=""padding:0 0 8px 0;font:600 14px/1.6 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial;color:#374151"">
                      Event schedule
                    </td>
                  </tr>
                  <tr>
                    <td class=""muted"" style=""font:500 14px/1.6 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial;color:#6b7280"">
                      {startUtc:ddd, MMM d, yyyy h:mm tt} — {endUtc:ddd, MMM d, yyyy h:mm tt} (UTC)
                    </td>
                  </tr>
                </table>
              </td></tr>

              <tr><td style=""padding:10px 22px 0 22px"">
                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" width=""100%"">
                  <tr>
                    <td valign=""middle"" style=""padding:0 0 12px 0"">
                      <span class=""pill"" style=""display:inline-block;border:1px dashed #d1d5db;color:#111827;background:#f9fafb;border-radius:12px;padding:10px 14px;font:700 16px/1 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial;letter-spacing:.08em"">
                        Promo code: <span style=""font-family:ui-monospace,SFMono-Regular,Menlo,Monaco,Consolas,'Liberation Mono','Courier New',monospace"">{Safe(promoCode)}</span>
                      </span>
                    </td>
                  </tr>
                  <tr>
                    <td>
                      <!-- CTA -->
                      <a href=""{Safe(ctaUrl)}""
                         style=""display:inline-block;background:#111827;color:#fff;text-decoration:none;border-radius:12px;padding:12px 18px;font:600 15px/1 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial"">
                        Apply Promo & View Tickets →
                      </a>
                    </td>
                  </tr>
                </table>
              </td></tr>

              <tr><td style=""padding:16px 22px 6px 22px"">
                <div class=""muted"" style=""font:500 13px/1.6 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial;color:#6b7280"">
                  {Safe(validityText)}. Limited redemptions may apply. Subject to availability and terms.
                </div>
              </td></tr>

              <tr><td style=""padding:18px 22px 24px 22px"">
                <hr style=""border:0;border-top:1px solid #e5e7eb;margin:0 0 12px 0""/>
                <div class=""muted"" style=""font:500 12px/1.6 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial;color:#9ca3af"">
                  Need help? {(!string.IsNullOrWhiteSpace(supportEmail) ? $@"<a href=""mailto:{Safe(supportEmail)}"" style=""color:#6366f1;text-decoration:none"">{Safe(supportEmail)}</a>" : "Reply to this email")}.
                  {(string.IsNullOrWhiteSpace(unsubscribeUrl) ? "" : $@" • <a href=""{Safe(unsubscribeUrl)}"" style=""color:#9ca3af;text-decoration:underline"">Unsubscribe</a>")}
                </div>
              </td></tr>

            </table>
          </td>
        </tr>

        <!-- footer -->
        <tr><td align=""center"" style=""padding:18px 6px 6px 6px"">
          <div class=""muted"" style=""font:500 12px/1.6 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial;color:#9ca3af"">
            © {DateTime.UtcNow:yyyy} {Safe(orgName)}. All rights reserved.
          </div>
        </td></tr>
      </table>
      <!-- /container -->

    </td></tr>
  </table>
</body>
</html>";
    }
}
