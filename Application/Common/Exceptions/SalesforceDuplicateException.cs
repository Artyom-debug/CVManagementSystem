namespace Application.Common.Exceptions;

public sealed class SalesforceDuplicateException : Exception
{
    public SalesforceDuplicateException()
        : base("A matching Account or Contact already exists in Salesforce. Review the existing records before submitting again.")
    {
    }
}
