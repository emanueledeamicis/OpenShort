using OpenShort.Core.Entities;

namespace OpenShort.Core.Interfaces;

public interface IDomainService
{
    Task<IEnumerable<Domain>> GetAllAsync();
    Task<Domain?> GetByIdAsync(long id);
    Task<Domain?> GetByHostAsync(string host);
    Task<Domain?> CreateAsync(Domain domain);
    Task<bool> UpdateAsync(Domain domain);
    Task<bool> DeleteAsync(long id);
    Task<bool> ExistsAsync(long id);
    Task<int> GetLinkCountByDomainIdAsync(long domainId);
}
