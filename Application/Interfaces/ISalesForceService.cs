using Application.Dtos;

namespace Application.Interfaces;

public interface ISalesForceService
{
    Task CreateAccountWithContactAsync(SalesForceDto data, CancellationToken cancellationToken);
}
