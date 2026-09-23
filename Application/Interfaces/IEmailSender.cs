using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces;

public interface IEmailSender
{
    Task SendConfirmationEmailAsync(string toEmail, string confirmationToken, CancellationToken cancellationToken);
}
