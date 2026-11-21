using System.Net;
using System.Net.Sockets;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Dixon.LabelVerification.API.Services;

public sealed class PasswordResetEmailSender(IConfiguration configuration)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(configuration["Smtp:Host"])
        && int.TryParse(configuration["Smtp:Port"], out var port)
        && port is > 0 and <= 65535
        && !string.IsNullOrWhiteSpace(configuration["Smtp:FromEmail"])
        && (string.IsNullOrWhiteSpace(configuration["Smtp:UserName"])
            == string.IsNullOrWhiteSpace(configuration["Smtp:Password"]));

    public async Task SendPasswordResetAsync(
        string recipient,
        string resetUrl,
        CancellationToken cancellationToken)
    {
        var host = configuration["Smtp:Host"];
        var senderAddress = configuration["Smtp:FromEmail"];
        if (!IsConfigured || host is null || senderAddress is null
            || !int.TryParse(configuration["Smtp:Port"], out var port))
            throw new InvalidOperationException("SMTP settings are incomplete.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(configuration["Smtp:FromName"] ?? "LabelVerify", senderAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = "Reset your LabelVerify password";
        var safeResetUrl = WebUtility.HtmlEncode(resetUrl);
        message.Body = new BodyBuilder
        {
            TextBody = $"Use this one-time link to reset your LabelVerify password. It expires in 15 minutes.\n\n{resetUrl}\n\nIf you did not request a reset, you can ignore this email.",
            HtmlBody = $"<p>Use this one-time link to reset your LabelVerify password. It expires in 15 minutes.</p><p><a href=\"{safeResetUrl}\">Reset password</a></p><p>If you did not request a reset, you can ignore this email.</p>"
        }.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            var useSsl = bool.TryParse(configuration["Smtp:UseSsl"], out var configuredUseSsl) && configuredUseSsl;
            await client.ConnectAsync(
                host,
                port,
                useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls,
                cancellationToken);

            var userName = configuration["Smtp:UserName"];
            var password = configuration["Smtp:Password"];
            if (!string.IsNullOrWhiteSpace(userName) && !string.IsNullOrWhiteSpace(password))
                await client.AuthenticateAsync(userName, password, cancellationToken);

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception exception) when (
            exception is CommandException
                or ProtocolException
                or SocketException
                or IOException
                or System.Security.Authentication.AuthenticationException
                or MailKit.Security.AuthenticationException)
        {
            throw new EmailDeliveryException("The password reset email could not be delivered.", exception);
        }
    }
}

public sealed class EmailDeliveryException(string message, Exception innerException)
    : Exception(message, innerException)
{
}
