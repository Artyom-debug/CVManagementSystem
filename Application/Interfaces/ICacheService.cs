using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken token);

    Task SetAsync<T>(string key, T value, TimeSpan expiration,  CancellationToken token, IEnumerable<string>? dependencies = null);

    Task RemoveAsync(string key, CancellationToken token);

    Task RemoveDependenciesAsync(string dependency, CancellationToken token);
}
