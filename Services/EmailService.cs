using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using WorkshopZagreb.Models;
using WorkshopZagreb.Pages;

namespace WorkshopZagreb.Services;

public interface IEmailService
{
    Task SendConfirmationAsync(string toEmail, string unsubscribeToken);
    Task<EmailBatchResult> SendWorkshopAnnouncementAsync(Workshop workshop, WorkshopOccurrence? occurrence, IList<Subscriber> subscribers, string? subject = null, string kicker = "Nova radionica");
    Task SendInquiryAsync(InquiryInput input);
}

public record EmailBatchResult(int Sent, int Failed, bool SmtpConfigured);

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _log;

    public EmailService(IConfiguration config, ILogger<EmailService> log)
    {
        _config = config;
        _log = log;
    }

    public async Task SendConfirmationAsync(string toEmail, string unsubscribeToken)
    {
        var unsub = UnsubscribeUrl(unsubscribeToken);
        var html = $"""
            <div style="font-family:Inter,Arial,sans-serif;max-width:540px;margin:0 auto;color:#1a1a1a;padding:32px 0;">
              <p style="font-size:0.72rem;font-weight:600;letter-spacing:0.12em;text-transform:uppercase;color:#c8a96e;margin-bottom:8px;">Workshop Zagreb</p>
              <h1 style="font-family:Georgia,'Playfair Display',serif;font-size:1.7rem;line-height:1.25;margin:0 0 20px;">
                Hvala na prijavi!
              </h1>
              <p style="line-height:1.75;margin-bottom:16px;">
                Dobrodošao/la u naš newsletter. Obavijestit ćemo te čim dodamo novu radionicu —
                jednom ili dvaput mjesečno, bez spama.
              </p>
              <p style="line-height:1.75;margin-bottom:32px;">
                Do tada nas možeš pronaći na
                <a href="https://www.instagram.com/workshop.zagreb/" style="color:#c8a96e;">Instagramu</a>.
              </p>
              <hr style="border:none;border-top:1px solid #e5e0d8;margin:32px 0;" />
              <p style="font-size:0.72rem;color:#999;line-height:1.6;">
                Workshop Zagreb, Zagreb<br/>
                <a href="{unsub}" style="color:#999;">Odjavi se s newslettera</a>
              </p>
            </div>
            """;

        await SendOneAsync(toEmail, "Dobrodošli u Workshop Zagreb newsletter!", html);
    }

    public async Task<EmailBatchResult> SendWorkshopAnnouncementAsync(Workshop workshop, WorkshopOccurrence? occurrence, IList<Subscriber> subscribers, string? subject = null, string kicker = "Nova radionica")
    {
        if (!subscribers.Any()) return new EmailBatchResult(0, 0, true);

        var smtp = _config.GetSection("Email:Smtp");
        var host = smtp["Host"];
        var from = smtp["From"];
        var password = smtp["Password"];

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(from) || string.IsNullOrEmpty(password))
        {
            _log.LogWarning("Email:Smtp not fully configured (missing Host/From/Password) — skipping announcement batch");
            return new EmailBatchResult(0, subscribers.Count, false);
        }

        var priceRow = !string.IsNullOrEmpty(workshop.Price)
            ? $"<tr><td style='padding:5px 0;color:#888;font-size:0.85rem;width:100px;'>Cijena</td><td style='font-weight:500;'>{workshop.Price}</td></tr>"
            : "";
        var maxPax  = workshop.MaxParticipants.HasValue
            ? $"<tr><td style='padding:5px 0;color:#888;font-size:0.85rem;width:100px;'>Mjesta</td><td style='font-weight:500;'>max {workshop.MaxParticipants}</td></tr>"
            : "";
        var spotsRow = (!workshop.IsReservable && workshop.SpotsLeft.HasValue && workshop.SpotsLeft.Value < 5)
            ? $"<tr><td style='padding:5px 0;'></td><td style='font-weight:600;font-size:0.85rem;color:{(workshop.SpotsLeft.Value <= 0 ? "#DC2626" : "#C1683A")};'>{(workshop.SpotsLeft.Value <= 0 ? "Rasprodano" : $"Samo još {workshop.SpotsLeft.Value} mjesta")}</td></tr>"
            : "";

        var hostSocial = "";
        if (!string.IsNullOrEmpty(workshop.HostInstagram))
            hostSocial += $"""<a href="{workshop.HostInstagram}" style="color:#c8a96e;text-decoration:none;font-size:0.8rem;margin-left:10px;">Instagram</a>""";
        if (!string.IsNullOrEmpty(workshop.HostWebsite))
            hostSocial += $"""<a href="{workshop.HostWebsite}" style="color:#c8a96e;text-decoration:none;font-size:0.8rem;margin-left:10px;">Web</a>""";
        var hostRow = !string.IsNullOrEmpty(workshop.HostName)
            ? $"<tr><td style='padding:5px 0;color:#888;font-size:0.85rem;'>Voditelj</td><td style='font-weight:500;'>{workshop.HostName}{hostSocial}</td></tr>"
            : "";

        var bannerHtml = "";
        if (!string.IsNullOrEmpty(workshop.BannerUrl))
        {
            var logoOverlay = !string.IsNullOrEmpty(workshop.LogoUrl)
                ? $"""<img src="{workshop.LogoUrl}" alt="{workshop.Name} logo" style="display:block;width:88px;height:88px;object-fit:contain;background:#fff;border-radius:50%;box-shadow:0 2px 8px rgba(0,0,0,0.15);margin:-44px 24px 0 auto;position:relative;" />"""
                : "";
            bannerHtml = $"""
                <div style="margin-bottom:{(string.IsNullOrEmpty(logoOverlay) ? "24" : "44")}px;">
                  <img src="{workshop.BannerUrl}" alt="{workshop.Name}" style="display:block;width:100%;max-height:220px;object-fit:cover;" />
                  {logoOverlay}
                </div>
                """;
        }

        string dateRows;
        string actionBtn;
        if (occurrence != null)
        {
            var date    = occurrence.Date.ToString("dd. MM. yyyy");
            var time    = occurrence.StartTime.ToString(@"hh\:mm");
            var endTime = occurrence.EndTime.HasValue ? $" – {occurrence.EndTime.Value:hh\\:mm}" : "";
            dateRows = $"""
                <tr><td style="padding:5px 0;color:#888;font-size:0.85rem;width:100px;">Datum</td><td style="font-weight:500;">{date}</td></tr>
                <tr><td style="padding:5px 0;color:#888;font-size:0.85rem;">Vrijeme</td><td style="font-weight:500;">{time}{endTime}</td></tr>
                {priceRow}
                {maxPax}
                {spotsRow}
                {hostRow}
                """;
            actionBtn = !string.IsNullOrEmpty(workshop.TicketUrl)
                ? $"""<p style="margin:28px 0 8px;"><a href="{workshop.TicketUrl}" style="background:#c8a96e;color:#fff;padding:12px 32px;text-decoration:none;display:inline-block;font-size:0.9rem;font-weight:600;">Kupi ulaznicu</a></p>"""
                : "";
        }
        else
        {
            dateRows = $"""
                {priceRow}
                {maxPax}
                {spotsRow}
                {hostRow}
                """;
            string bookHref;
            if (workshop.BookingType == "email")
            {
                bookHref = $"mailto:{workshop.BookingValue}";
            }
            else
            {
                var val = workshop.BookingValue;
                if (string.IsNullOrWhiteSpace(val))
                    bookHref = $"{SiteBase()}/suradnja#upit";
                else if (val.StartsWith("http://") || val.StartsWith("https://"))
                    bookHref = val;
                else
                    bookHref = $"{SiteBase()}{(val.StartsWith("/") ? "" : "/")}{val}";
            }
            actionBtn = $"""<p style="margin:28px 0 8px;"><a href="{bookHref}" style="background:#c8a96e;color:#fff;padding:12px 32px;text-decoration:none;display:inline-block;font-size:0.9rem;font-weight:600;">Rezerviraj</a></p>""";
        }

        var calendarUrl = $"{SiteBase()}/#calendar";
        subject ??= $"Nova radionica: {workshop.Name} — Workshop Zagreb";

        using var smtpClient = new SmtpClient();
        try
        {
            await smtpClient.ConnectAsync(host, int.Parse(smtp["Port"] ?? "587"), SecureSocketOptions.StartTls);
            await smtpClient.AuthenticateAsync(smtp["Username"], password);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to connect/authenticate SMTP for announcement batch");
            return new EmailBatchResult(0, subscribers.Count, false);
        }

        int sent = 0, failed = 0;
        foreach (var sub in subscribers)
        {
            var unsub = UnsubscribeUrl(sub.Token);
            var html = $"""
                <div style="font-family:Inter,Arial,sans-serif;max-width:540px;margin:0 auto;color:#1a1a1a;padding:32px 0;">
                  {bannerHtml}
                  <p style="font-size:0.72rem;font-weight:600;letter-spacing:0.12em;text-transform:uppercase;color:#c8a96e;margin-bottom:8px;">{kicker}</p>
                  <h1 style="font-family:Georgia,'Playfair Display',serif;font-size:1.9rem;line-height:1.2;margin:0 0 24px;">{workshop.Name}</h1>

                  <table style="width:100%;border-collapse:collapse;margin-bottom:28px;">
                    {dateRows}
                  </table>

                  <p style="line-height:1.75;margin-bottom:28px;">{workshop.Description}</p>

                  {actionBtn}
                  <p style="margin-top:16px;">
                    <a href="{calendarUrl}" style="color:#c8a96e;font-size:0.9rem;">Pogledaj kalendar radionica →</a>
                  </p>

                  <hr style="border:none;border-top:1px solid #e5e0d8;margin:40px 0;" />
                  <p style="font-size:0.72rem;color:#999;line-height:1.6;">
                    Workshop Zagreb, Zagreb<br/>
                    <a href="{unsub}" style="color:#999;">Odjavi se s newslettera</a>
                  </p>
                </div>
                """;

            try
            {
                var msg = new MimeMessage();
                msg.From.Add(MailboxAddress.Parse(from));
                msg.To.Add(MailboxAddress.Parse(sub.Email));
                msg.Subject = subject;
                msg.Body = new TextPart("html") { Text = html };
                await smtpClient.SendAsync(msg);
                sent++;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to send announcement to {To}", sub.Email);
                failed++;
            }
        }

        await smtpClient.DisconnectAsync(true);
        return new EmailBatchResult(sent, failed, true);
    }

    public async Task SendInquiryAsync(InquiryInput i)
    {
        string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
        string Row(string label, string? value) =>
            string.IsNullOrWhiteSpace(value) ? "" :
            $"<tr><td style='padding:6px 0;color:#888;font-size:0.85rem;width:140px;vertical-align:top;'>{label}</td><td style='font-weight:500;padding:6px 0;'>{H(value)}</td></tr>";

        var details = i.Type switch
        {
            "radionica" => Row("Tema radionice", i.WorkshopTopic)
                        + Row("O sebi", i.WorkshopBio)
                        + Row("Preferirani termini", i.WorkshopSchedule)
                        + Row("Broj polaznika", i.WorkshopParticipants),
            "event"     => Row("Vrsta eventa", i.EventKind)
                        + Row("Željeni datum", i.EventDate)
                        + Row("Broj gostiju", i.EventGuests)
                        + Row("Napomena", i.EventNotes),
            "marketing" => Row("Brend / tvrtka", i.BrandName)
                        + Row("Vrsta suradnje", i.BrandPlacements)
                        + Row("Poruka", i.BrandMessage),
            _           => Row("Poruka", i.OtherMessage),
        };

        var typeLabel = i.Type switch
        {
            "radionica" => "Voditi radionicu",
            "event"     => "Privatni event",
            "marketing" => "Brand / marketing",
            _           => "Ostalo",
        };

        var adminEmail = _config["Email:Smtp:Username"] ?? "info@workshopzagreb.hr";
        var subject    = $"[Workshop Zagreb] Novi upit: {typeLabel}";
        var html = $"""
            <div style="font-family:Inter,Arial,sans-serif;max-width:560px;margin:0 auto;color:#1a1a1a;padding:32px 0;">
              <p style="font-size:0.72rem;font-weight:600;letter-spacing:0.12em;text-transform:uppercase;color:#c8a96e;margin-bottom:8px;">Novi upit — Workshop Zagreb</p>
              <h2 style="font-family:Georgia,serif;font-size:1.6rem;margin:0 0 28px;">{typeLabel}</h2>
              <table style="width:100%;border-collapse:collapse;border-top:1px solid #e5e0d8;">
                {Row("Ime", i.Name)}
                {Row("E-mail", i.Email)}
                {details}
              </table>
            </div>
            """;

        await SendOneAsync(adminEmail, subject, html, replyTo: i.Email);
    }

    private async Task SendOneAsync(string toEmail, string subject, string html, string? replyTo = null)
    {
        var smtp = _config.GetSection("Email:Smtp");
        var host = smtp["Host"];
        var from = smtp["From"];
        var password = smtp["Password"];

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(from) || string.IsNullOrEmpty(password))
        {
            _log.LogWarning("Email:Smtp not fully configured (missing Host/From/Password) — skipping send to {To}", toEmail);
            return;
        }

        try
        {
            var msg = new MimeMessage();
            msg.From.Add(MailboxAddress.Parse(from));
            msg.To.Add(MailboxAddress.Parse(toEmail));
            if (replyTo != null) msg.ReplyTo.Add(MailboxAddress.Parse(replyTo));
            msg.Subject = subject;
            msg.Body = new TextPart("html") { Text = html };

            using var client = new SmtpClient();
            await client.ConnectAsync(host, int.Parse(smtp["Port"] ?? "587"), SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtp["Username"], password);
            await client.SendAsync(msg);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to send email to {To}", toEmail);
        }
    }

    private string SiteBase() => _config["Email:SiteBaseUrl"]?.TrimEnd('/') ?? "https://workshopzagreb.hr";
    private string UnsubscribeUrl(string token) => $"{SiteBase()}/newsletter/unsubscribe?token={token}";
}
