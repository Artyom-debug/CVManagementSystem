namespace Application.Common.Exceptions;

public sealed class SupportTicketDeliveryException : Exception
{
    public SupportTicketDeliveryException(string message) : base(message) { }

    public SupportTicketDeliveryException(string message, Exception innerException) : base(message, innerException) { }
}
